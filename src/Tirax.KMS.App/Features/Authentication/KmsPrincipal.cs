using System.Security.Claims;

namespace Tirax.KMS.App.Features.Authentication;

public readonly record struct KmsPrincipal
{
    readonly ClaimsPrincipal basePrincipal;
    
    public KmsPrincipal(ClaimsPrincipal basePrincipal) {
        this.basePrincipal = basePrincipal;
    }

    public static readonly KmsPrincipal Guest = new(new());
    public const string AccessTokenClaim = "access_token";
    public const string PermissionsClaim = "permissions";

    public bool IsGuest => !IsAuthenticated;
    public bool IsAuthenticated => basePrincipal.Identity?.IsAuthenticated ?? false;
}