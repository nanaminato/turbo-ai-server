namespace Turbo.Auth.Models.Config;

public class JwtSettings
{
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string? SecretKey { get; set; }
    public string? RefreshTokenPepper { get; set; }
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public int AuthenticationStateCacheSeconds { get; set; } = 60;
}
