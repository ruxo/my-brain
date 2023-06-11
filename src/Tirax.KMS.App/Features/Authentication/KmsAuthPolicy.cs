namespace Tirax.KMS.App.Features.Authentication;

public static class KmsAuthPolicy
{
    public const string Authenticated = "Authenticated";
    public const string Admin = "tirax:kms:admin";
    public const string User = "tirax:kms:user";
}