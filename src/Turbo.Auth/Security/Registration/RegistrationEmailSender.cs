using Microsoft.Extensions.Options;
using Turbo.Auth.Localization;
using Turbo.Auth.Security.PasswordReset;
namespace Turbo.Auth.Security.Registration;

public interface IRegistrationEmailSender { Task SendAsync(string recipient, string code, CancellationToken token); }
public sealed class RegistrationEmailSender(IOptions<PasswordResetOptions> options) : IRegistrationEmailSender
{
    public Task SendAsync(string recipient, string code, CancellationToken token) => PasswordResetEmailSender.SendMessageAsync(
        options.Value, recipient, ApiMessages.Get("registration_email_subject"),
        ApiMessages.Get("registration_email_body") + "\n\n" + code, token);
}
