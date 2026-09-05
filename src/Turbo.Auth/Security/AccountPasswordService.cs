using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Turbo.Auth.Models.Accounts;

namespace Turbo.Auth.Security;

public sealed class AccountPasswordService : IAccountPasswordService
{
    private readonly PasswordHasher<Account> _hasher = new();

    public string Hash(Account account, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(account, password);
    }

    public PasswordVerificationState Verify(Account account, string password)
    {
        if (string.IsNullOrEmpty(account.Password) || string.IsNullOrEmpty(password))
        {
            return PasswordVerificationState.Invalid;
        }

        PasswordVerificationResult result;
        try
        {
            result = _hasher.VerifyHashedPassword(account, account.Password, password);
        }
        catch (FormatException)
        {
            // Legacy records were stored as plaintext and cannot be decoded as a password hash.
            result = PasswordVerificationResult.Failed;
        }
        if (result == PasswordVerificationResult.Success)
        {
            return PasswordVerificationState.Valid;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            return PasswordVerificationState.ValidNeedsUpgrade;
        }

        return MatchesLegacyPlaintext(account.Password, password)
            ? PasswordVerificationState.ValidNeedsUpgrade
            : PasswordVerificationState.Invalid;
    }

    private static bool MatchesLegacyPlaintext(string persistedPassword, string suppliedPassword)
    {
        var persistedBytes = Encoding.UTF8.GetBytes(persistedPassword);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedPassword);

        return persistedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(persistedBytes, suppliedBytes);
    }
}
