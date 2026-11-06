using System.ComponentModel.DataAnnotations;

namespace RelaxKon.Auth.Contracts.Auth;

public sealed record TokenResponse(string Token, string AccessToken, string RefreshToken, int Id,
    Guid SessionId, int AccessTokenExpiresIn, DateTime RefreshTokenExpiresAt)
{
    public override string ToString() => $"TokenResponse {{ AccountId = {Id}, SessionId = {SessionId} }}";
}
public sealed record SessionResponse(Guid SessionId, string? DeviceName, string? IpAddress,
    DateTime CreatedAt, DateTime LastUsedAt, DateTime ExpiresAt);
public sealed record CaptchaResponse(byte[] Img, string Code, string ContentType);
public sealed record RoleResponse(int RoleId, string? Name, string? Code = null, string? Description = null, bool IsSystem = false, int MemberCount = 0);

public sealed class RoleReferenceRequest
{
    [Range(1, int.MaxValue)] public int RoleId { get; init; }
    public string? Name { get; init; }
}
public sealed class RoleWriteRequest
{
    [Range(0, int.MaxValue)] public int RoleId { get; init; }
    [StringLength(64)] public string? Code { get; init; }
    [StringLength(256)] public string? Description { get; init; }
    [Required, StringLength(64)] public string Name { get; init; } = "";
}
