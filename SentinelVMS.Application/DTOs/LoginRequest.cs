namespace SentinelVMS.Application.DTOs;

public sealed record LoginRequest(string Username, string Password, bool RememberMe);
