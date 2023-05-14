using Microsoft.AspNetCore.Components;
using Tirax.KMS.Server;

namespace Tirax.KMS.App.Features.Home;

public partial class Index
{
    [Inject]
    public IKmsServer Server { get; set; } = null!;

    protected override async Task OnParametersSetAsync() {
        var home = await Server.GetHome();
        ViewModel = new(home.Id, Server);
        await base.OnParametersSetAsync();
    }
}