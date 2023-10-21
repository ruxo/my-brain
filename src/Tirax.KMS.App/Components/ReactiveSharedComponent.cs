using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using ReactiveUI;

namespace Tirax.KMS.App.Components;

public abstract class ReactiveSharedLayoutComponent<T> : LayoutComponentBase, IDisposable
    where T: ReactiveObject
{
    IDisposable changeSubscription = Disposable.Empty;
    
    protected abstract T ViewModel { get; }
    
    protected override void OnInitialized() {
        changeSubscription = ViewModel.Changed.Subscribe(_ => StateHasChanged());
    }

    public void Dispose() {
        changeSubscription.Dispose();
    }
}