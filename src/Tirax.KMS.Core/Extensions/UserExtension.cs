using System.Security.Claims;

namespace Tirax.KMS.Extensions;

public static class UserExtension
{
    public static string GetName(this ClaimsPrincipal principal) =>
        principal.Identities.Choose(i => Optional(i.Name)).TryFirst()
                 .OrElse(() => principal.Claims.TryFirst(c => c.Type == "name").Map(c => c.Value))
                 .IfNone("(Anonymous)");

    public static string GetShortName(this ClaimsPrincipal principal) =>
        new(principal.GetName()
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Map(s => char.ToUpper(s[0]))
                     .ToArray());
}