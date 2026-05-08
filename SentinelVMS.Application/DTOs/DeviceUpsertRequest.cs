namespace SentinelVMS.Application.DTOs;

public sealed record DeviceUpsertRequest(
    string Name,
    string Host,
    int Port,
    string Username,
    string Password,
    string Manufacturer,
    string Model,
    int ChannelCount = 1,
    bool IsNvr = false);
