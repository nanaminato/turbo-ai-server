using System.Security.Claims;

namespace Turbo_Auth.Controllers.MessageSync;

public class IdGetter: IIdGetter
{
    public int GetId(ClaimsPrincipal user)
    {
        var id = -1;
        foreach (var claim in user.Claims)
        {
            if (claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
            {
                id = int.Parse(claim.Value);
            }
        }

        return id;
    }
}