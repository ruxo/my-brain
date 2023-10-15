using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tirax.KMS.App.Pages.Auth;

[AllowAnonymous]
public class Login : PageModel
{
    public Task OnGet(string? redirectUri) {
        var props = new AuthenticationProperties{
            RedirectUri = redirectUri ?? "/"
        };
        return HttpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, props);
    }
}