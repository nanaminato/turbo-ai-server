using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using Turbo.Auth.Localization;

namespace Turbo.Auth.Security.PasswordReset;

public interface IPasswordResetEmailSender
{
    Task SendAsync(string recipient, string link, CancellationToken cancellationToken);
}

public sealed class PasswordResetEmailSender(IOptions<PasswordResetOptions> options) : IPasswordResetEmailSender
{
    public static SecureSocketOptions SocketOptions(PasswordResetOptions settings) => !settings.EnableSsl
        ? SecureSocketOptions.None
        : settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    public async Task SendAsync(string recipient, string link, CancellationToken cancellationToken)
    {
        await SendMessageAsync(options.Value, recipient, ApiMessages.Get("reset_email_subject"),
            ApiMessages.Get("reset_email_body") + "\n\n" + link, cancellationToken);
    }
    public static async Task SendMessageAsync(PasswordResetOptions settings, string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        using var client = new SmtpClient { Timeout = 30000 };
        // Certificate validation remains enabled. Never silently downgrade required STARTTLS.
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SocketOptions(settings), cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
            await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
