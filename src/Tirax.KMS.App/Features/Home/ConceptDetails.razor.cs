using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReactiveUI;
using ReactiveUI.Blazor;
using Tirax.KMS.Domain;
using Tirax.KMS.Extensions;
using Tirax.KMS.Server;
using Seq = LanguageExt.Seq;
using Unit = System.Reactive.Unit;

namespace Tirax.KMS.App.Features.Home;

public partial class ConceptDetails : ReactiveComponentBase<ConceptDetails.VModel>
{
    public ConceptDetails() {
        this.WhenActivated(disposables => {
            ViewModel = new(Server);
        });
    }
    
    [Inject]
    public IKmsServer Server { get; set; } = default!;

    [Inject]
    public IDialogService DialogService { get; set; } = default!;

    [Parameter]
    public Concept? Concept { get; set; }

    protected override void OnParametersSet() {
        ViewModel!.Concept = Concept;
        base.OnParametersSet();
    }

    async Task ShowDialog() {
        var result = await AddDialog.Show(DialogService);
        if (result.IfSome(out var name))
            await ViewModel!.NewConcept.Execute(name).ToTask();
    }

    public sealed class VModel : ReactiveObject
    {
        readonly IKmsServer server;
        Option<Concept> concept;
        readonly ObservableAsPropertyHelper<string> name;
        bool isSaving;

        readonly ObservableAsPropertyHelper<string> editButtonIcon;
        readonly ObservableAsPropertyHelper<Color> editButtonColor;
        readonly ObservableAsPropertyHelper<Observation<System.Collections.Generic.HashSet<ConceptListItem>>> subConcepts;

        public readonly record struct ConceptListItem(Observation<Concept> Concept, System.Collections.Generic.HashSet<ConceptListItem> SubConcepts);

        public VModel(IKmsServer server) {
            this.server = server;
            name = this.WhenAnyValue(x => x.Concept)
                       .Select(_concept => _concept.Map(c => c.Name).IfNone(string.Empty))
                       .ToProperty(this, x => x.Name);
            subConcepts = this.WhenAnyValue(x => x.Concept)
                              .Select(_concept => _concept.Map(c => c.Contains).IfNone(LanguageExt.HashSet.empty<ConceptId>()))
                              .SelectMany(cids => Observation.From(() => LoadSubConcepts(cids.ToSeq())))
                              .ToProperty(this, x => x.SubConcepts);
            editButtonIcon = this.WhenAnyValue(x => x.isSaving)
                                 .Select(saving => saving ? Icons.Material.Filled.Savings : Icons.Material.Filled.Add)
                                 .ToProperty(this, x => x.EditButtonIcon);
            editButtonColor = this.WhenAnyValue(x => x.isSaving)
                                  .Select(saving => saving ? Color.Dark : Color.Primary)
                                  .ToProperty(this, x => x.EditButtonColor);
            
            NewConcept = ReactiveCommand.CreateFromTask<string>(NewConceptTask);
        }

        public Option<Concept> Concept {
            get => concept;
            set => this.RaiseAndSetIfChanged(ref concept, value);
        }

        public string Name => name.Value;

        public Observation<System.Collections.Generic.HashSet<ConceptListItem>> SubConcepts => subConcepts.Value;
        
        public ReactiveCommand<string,Unit> NewConcept { get; }

        public bool IsSaving {
            get => isSaving;
            set => this.RaiseAndSetIfChanged(ref isSaving, value);
        }

        public string EditButtonIcon => editButtonIcon.Value;
        public Color EditButtonColor => editButtonColor.Value;

        async Task NewConceptTask(string conceptName) {
            IsSaving = true;
            try {
                var newConcept = await server.CreateSubConcept(concept.Get().Id, conceptName);
                Concept = newConcept;
            }
            finally {
                IsSaving = false;
            }
        }
        
        #region Sub-concepts loader

        async Task<System.Collections.Generic.HashSet<ConceptListItem>> LoadSubConcepts(Seq<ConceptId> conceptIds) {
            var concepts = await conceptIds.ParallelMapAsync(UiFetch);
            var result = await concepts.MapAsync(async oc => {
                var subConceptIds = oc.Map(c => c.Contains.ToSeq()).UnwrapWithDefault(Seq.empty<ConceptId>());
                var subs = await subConceptIds.ParallelMapAsync(LoadUiConcept);
                return new ConceptListItem(oc, subs.ToHashSet());
            }).ToArrayAsync();
            return result.ToHashSet();
        }

        async Task<ConceptListItem> LoadUiConcept(ConceptId conceptId) => 
            new(await UiFetch(conceptId), new());

        async Task<Observation<Concept>> UiFetch(ConceptId cid) {
            var r = await server.Fetch(cid);
            return Convert(cid, r);
        }

        static Observation<Concept> Convert(ConceptId cid, Option<Concept> c) =>
            c.Map(Observation.Return)
             .IfNone(Observation.Failed<Concept>(new ApplicationException($"Invalid Concept Id {cid}")));

        #endregion
    }
}