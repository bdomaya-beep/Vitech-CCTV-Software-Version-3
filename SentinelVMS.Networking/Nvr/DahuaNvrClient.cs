using System.Net;

namespace SentinelVMS.Networking.Nvr;

/// <summary>
/// Queries Dahua NVR HTTP API for real channel names.
/// Endpoint: http://{host}/cgi-bin/configManager.cgi?action=getConfig&amp;name=ChannelTitle
/// Uses HTTP Digest authentication.
/// </summary>
public sealed class DahuaNvrClient
{
    /// <summary>
    /// Auto-detects channel count and names from the NVR — no prior count needed.
    /// Returns null if the device is unreachable or not a Dahua device.
    /// </summary>
    public async Task<string[]?> AutoDetectChannelNamesAsync(
        string host,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };

            var url = $"http://{host}/cgi-bin/configManager.cgi?action=getConfig&name=ChannelTitle";
            var response = await client.GetStringAsync(url, cancellationToken);
            var names = AutoParseChannelNames(response);
            return names.Length > 0 ? names : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Queries channel names using a known channel count (used by RefreshChannelNames).
    /// </summary>
    public async Task<string[]?> GetChannelNamesAsync(
        string host, string username, string password, int channelCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
                PreAuthenticate = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };

            var url = $"http://{host}/cgi-bin/configManager.cgi?action=getConfig&name=ChannelTitle";
            var response = await client.GetStringAsync(url, cancellationToken);
            return ParseChannelNames(response, channelCount);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses all ChannelTitle entries without needing a pre-specified count.
    /// Handles: table.ChannelTitle[0]=Name  and  table.ChannelTitle[0].Name=Name
    /// </summary>
    private static string[] AutoParseChannelNames(string response)
    {
        var dict = new SortedDictionary<int, string>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!line.StartsWith("table.ChannelTitle[", StringComparison.OrdinalIgnoreCase))
                continue;

            var bracketEnd = line.IndexOf(']');
            if (bracketEnd < 0) continue;

            if (!int.TryParse(line[19..bracketEnd], out int idx) || idx < 0) continue;

            var eqPos = line.IndexOf('=');
            if (eqPos < 0) continue;

            var name = line[(eqPos + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(name) && !dict.ContainsKey(idx))
                dict[idx] = name;
        }

        if (dict.Count == 0) return [];

        var maxIdx = dict.Keys.Max();
        var names = new string[maxIdx + 1];
        foreach (var (idx, name) in dict)
            names[idx] = name;

        return names;
    }

    private static string[] ParseChannelNames(string response, int channelCount)
    {
        var names = new string[channelCount];
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!line.StartsWith("table.ChannelTitle[", StringComparison.OrdinalIgnoreCase))
                continue;

            var bracketEnd = line.IndexOf(']');
            if (bracketEnd < 0) continue;

            if (!int.TryParse(line[19..bracketEnd], out int idx)) continue;
            if (idx < 0 || idx >= channelCount) continue;

            var eqPos = line.IndexOf('=');
            if (eqPos < 0) continue;

            var name = line[(eqPos + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(name))
                names[idx] = name;
        }

        return names;
    }
}
