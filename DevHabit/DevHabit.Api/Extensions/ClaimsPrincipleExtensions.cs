using System.Security.Claims;

namespace DevHabit.Api.Extensions;

public static class ClaimsPrincipleExtensions
{
    public static string? GetIdentityId(this ClaimsPrincipal? claimsPrincipal)
    {
        return claimsPrincipal?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
