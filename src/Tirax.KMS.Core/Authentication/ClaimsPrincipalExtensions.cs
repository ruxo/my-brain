using System.Security.Claims;

namespace Tirax.KMS.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static string UserName(this ClaimsPrincipal principal) =>
        principal.FindFirst("name")?.Value ?? "(guest?)";
}