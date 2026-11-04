namespace Turbo.Auth.Security.Registration;

public sealed class RegistrationOptions { public bool Enabled { get; set; } = true; }
public sealed class RegistrationEmailChallenge
{
    public string EmailHash { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime NextSendAt { get; set; }
    public int AttemptsLeft { get; set; }
}
