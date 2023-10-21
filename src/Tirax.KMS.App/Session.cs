using MudBlazor;
using ReactiveUI;

namespace Tirax.KMS.App;

public sealed class Session : ReactiveObject
{
    readonly ObservableAsPropertyHelper<bool> drawerIsOpenProp;

    public bool DrawerIsOpen => drawerIsOpenProp.Value;

    public List<BreadcrumbItem> Breadcrumbs { get; } = new();
    
    public ReactiveCommand<bool,bool> ToggleDrawer { get; }

    public Session() {
        ToggleDrawer = ReactiveCommand.Create<bool,bool>(identity);

        drawerIsOpenProp = ToggleDrawer.ToProperty(this, v => v.DrawerIsOpen, scheduler: RxApp.MainThreadScheduler);
    }
}