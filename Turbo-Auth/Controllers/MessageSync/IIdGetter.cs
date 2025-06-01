using System.Security.Claims;

namespace Turbo_Auth.Controllers.MessageSync;

public interface IIdGetter
{
    int GetId(ClaimsPrincipal user);
}