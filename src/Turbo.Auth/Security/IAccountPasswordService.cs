using Turbo.Auth.Models.Accounts;

namespace Turbo.Auth.Security;

public interface IAccountPasswordService
{
    string Hash(Account account, string password);

    PasswordVerificationState Verify(Account account, string password);
}

public enum PasswordVerificationState
{
    Invalid,
    Valid,
    ValidNeedsUpgrade
}
