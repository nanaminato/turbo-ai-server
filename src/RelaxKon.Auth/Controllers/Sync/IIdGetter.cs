using System.Security.Claims;

namespace RelaxKon.Auth.Controllers.Sync;

public interface IIdGetter
{
    int GetId(ClaimsPrincipal user);
}