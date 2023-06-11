using System.Reactive.Linq;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReactiveUI;
using Tirax.KMS.App.Extensions;
using Tirax.KMS.Domain;

namespace Tirax.KMS.App.Features.Topic;

public partial class AddLinkDialog : IDialog<AddLinkDialog.Result>
{
    public AddLinkDialog() {
        this.WhenActivated(_ => {
            ViewModel = new();
        });
    }
    
    [CascadingParameter]
    public MudDialogInstance MudDialog { get; set; } = default!;

    void CancelDialog() {
        MudDialog.Cancel();
    }

    void ConfirmSave() {
        if (ViewModel!.IsValid) MudDialog.Close(new Result(ViewModel.Name, new(ViewModel.LinkUrl)));
    }

    public readonly record struct Result(Option<string> Name, URI Uri);
    
    public sealed class VModel : ReactiveObject
    {
        string linkUrl = string.Empty;
        Option<string> overridingName;
        string[] errors = System.Array.Empty<string>();

        readonly ObservableAsPropertyHelper<bool> hasError;
        readonly ObservableAsPropertyHelper<bool> isValid;

        public VModel() {
            hasError = this.WhenAnyValue(my => my.Errors)
                           .Select(es => es.Any())
                           .ToProperty(this, my => my.HasError);
            isValid = this.WhenAnyValue(my => my.LinkUrl)
                          .Select(url => !string.IsNullOrWhiteSpace(url) && IsValidUri(url))
                          .ToProperty(this, my => my.IsValid);
        }

        public string[] Errors {
            get => errors;
            set => this.RaiseAndSetIfChanged(ref errors, value);
        }

        public bool HasError => hasError.Value;

        public bool IsValid => isValid.Value;

        public string Name {
            get => overridingName.IfNone(linkUrl);
            set => this.RaiseAndSetIfChanged(ref overridingName, Some(value));
        }

        public string LinkUrl {
            get => linkUrl;
            set => this.RaiseAndSetIfChanged(ref linkUrl, value);
        }

        static bool IsValidUri(string url) => Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}