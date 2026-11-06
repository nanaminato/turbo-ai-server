using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using RelaxKon.Auth.Models.Accounts;

namespace RelaxKon.Auth.Security.PasswordReset;

public sealed record ResetIdentity(int AccountId, string Email, string SecurityStamp, string PasswordFingerprint);

public sealed class ResetTokenService
{
    private readonly ITimeLimitedDataProtector _protector;
    public ResetTokenService(IDataProtectionProvider protection) =>
        _protector = protection.CreateProtector("Turbo.Auth.PasswordReset.v1").ToTimeLimitedDataProtector();

    public string Create(Account account) => _protector.Protect(JsonSerializer.Serialize(new ResetIdentity(
        account.AccountId, account.Email!, account.SecurityStamp, Fingerprint(account.Password))), TimeSpan.FromMinutes(30));

    public ResetIdentity? Read(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return null;
        try { return JsonSerializer.Deserialize<ResetIdentity>(_protector.Unprotect(token)); }
        catch (Exception error) when (error is CryptographicException or JsonException or FormatException) { return null; }
    }

    public static string Fingerprint(string? password) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password ?? "")));
    public static bool Matches(ResetIdentity identity, Account account) => identity.AccountId == account.AccountId &&
        identity.Email == account.Email && identity.SecurityStamp == account.SecurityStamp &&
        identity.PasswordFingerprint == Fingerprint(account.Password);
}
