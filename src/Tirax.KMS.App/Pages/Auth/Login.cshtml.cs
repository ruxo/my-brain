using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tirax.KMS.App.Pages.Auth;

[AllowAnonymous]
public class Login : PageModel
{
    public async Task OnGet(string? redirectUri) {
        var props = new AuthenticationProperties{
            RedirectUri = redirectUri ?? "/"
        };
        await HttpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, props);
    }
}