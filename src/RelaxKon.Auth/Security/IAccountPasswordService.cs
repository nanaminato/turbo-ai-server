using RelaxKon.Auth.Models.Accounts;

namespace RelaxKon.Auth.Security;

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
