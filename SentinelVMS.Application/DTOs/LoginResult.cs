using SentinelVMS.Domain.Models;

namespace SentinelVMS.Application.DTOs;

public sealed record LoginResult(bool Succeeded, string Message, UserSession? Session);
