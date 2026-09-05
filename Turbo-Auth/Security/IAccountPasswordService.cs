using Turbo_Auth.Models.Accounts;

namespace Turbo_Auth.Security;

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
