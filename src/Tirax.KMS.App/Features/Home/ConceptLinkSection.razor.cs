using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReactiveUI;
using Tirax.KMS.App.Extensions;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;
using Seq = LanguageExt.Seq;
using Unit = System.Reactive.Unit;

namespace Tirax.KMS.App.Features.Home;

public partial class ConceptLinkSection
{
    public ConceptLinkSection() {
        this.WhenActivated(_ => {
            ViewModel = new(Server);
        });
    }
    
    [Parameter, EditorRequired]
    public Option<Concept> Concept { get; set; }
    
    [Parameter]
    public bool Disabled { get; set; }
    
    [Parameter, EditorRequired]
    public EventCallback<Concept> OnConceptUpdated { get; set; }

    [Inject]
    public IDialogService DialogService { get; set; } = default!;

    [Inject]
    public IKmsServer Server { get; set; } = default!;

    protected override void OnParametersSet() {
        ViewModel!.Concept = Concept;
        base.OnParametersSet();
    }

    async Task ShowAddLink() {
        var result = await DialogService.ShowDialog<AddLinkDialog, AddLinkDialog.Result>("Add a link");
        if (result.IsNone) return;
        var concept = await ViewModel!.NewLink.Execute(result.Get()).ToTask();
        await OnConceptUpdated.InvokeAsync(concept);
    }

    public sealed class VModel : ReactiveObject
    {
        Option<Concept> concept;
        bool isSaving;

        readonly ObservableAsPropertyHelper<Seq<LinkObject>> links;

        public VModel(IKmsServer server) {
            links = this.WhenAnyValue(my => my.Concept)
                        .Where(c => c.IsSome)
                        .SelectMany(async c => await server.GetLinks(c.Get().Links.ToSeq()))
                        .ToProperty(this, my => my.Links, Seq.empty<LinkObject>());

            NewLink = ReactiveCommand.CreateFromTask(async (AddLinkDialog.Result result) => {
                var current = concept.Get();
                return await server.NewLink(current, result.Name, result.Uri);
            });
        }

        public Option<Concept> Concept {
            get => concept;
            set => this.RaiseAndSetIfChanged(ref concept, value);
        }

        public bool IsSaving {
            get => isSaving;
            set => this.RaiseAndSetIfChanged(ref isSaving, value);
        }
        
        public Seq<LinkObject> Links => links.Value;

        public ReactiveCommand<AddLinkDialog.Result, Concept> NewLink { get; }
    }
}