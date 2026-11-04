namespace Turbo.Auth.Security.PasswordReset;

public sealed class PasswordResetOptions
{
    public bool Enabled { get; set; }
    public string PublicBaseUrl { get; set; } = "";
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = "";
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";

    public bool IsValid() => !Enabled || (
        Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo) &&
        IsSmtpValid());

    public bool IsSmtpValid() => !string.IsNullOrWhiteSpace(SmtpHost) && SmtpPort is > 0 and <= 65535 &&
        (SmtpPort != 465 || EnableSsl) &&
        System.Net.Mail.MailAddress.TryCreate(FromAddress, out _);
}
