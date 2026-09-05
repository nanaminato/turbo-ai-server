using System.Security.Claims;

namespace Turbo.Auth.Controllers.Sync;

public interface IIdGetter
{
    int GetId(ClaimsPrincipal user);
}