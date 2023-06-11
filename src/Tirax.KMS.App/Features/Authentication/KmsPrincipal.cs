using System.Security.Claims;

namespace Tirax.KMS.App.Features.Authentication;

public readonly record struct KmsPrincipal
{
    public const string AccessTokenClaim = "access_token";
    public const string PermissionsClaim = "permissions";
}