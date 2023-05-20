using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using DynamicData.Binding;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReactiveUI;
using ReactiveUI.Blazor;
using Tirax.KMS.Domain;
using Tirax.KMS.Extensions;
using Tirax.KMS.Server;
using Tirax.KMS.App.Extensions;
using Seq = LanguageExt.Seq;
using Unit = System.Reactive.Unit;

namespace Tirax.KMS.App.Features.Home;

public partial class ConceptDetails : ReactiveComponentBase<ConceptDetails.VModel>
{
    public ConceptDetails() {
        this.WhenActivated(_ => {
            ViewModel = new(Server);
        });
    }
    
    [Inject]
    public IKmsServer Server { get; set; } = default!;

    [Inject]
    public IDialogService DialogService { get; set; } = default!;

    [Parameter]
    public Concept? Concept { get; set; }

    [Parameter]
    public EventCallback<ConceptId> OnConceptSelected { get; set; }

    protected override void OnParametersSet() {
        ViewModel!.Concept = Concept;
        base.OnParametersSet();
    }

    void ConceptUpdated(Concept updated) {
        ViewModel!.Concept = updated;
    }

    async Task SelectConcept(VModel.ConceptListItem item) {
        if (item.Concept is Observation<Concept>.Data{ Value: var concept })
            await NotifyConceptSelected(concept.Id);
    }

    async Task ShowDialog() {
        var result = await DialogService.ShowDialog<AddDialog, string>("Add a new concept");
        if (result.IfSome(out var name)) {
            var concept = await ViewModel!.NewConcept.Execute(name).ToTask();
            await NotifyConceptSelected(concept.Id);
        }
    }

    Task NotifyConceptSelected(ConceptId cid) => OnConceptSelected.InvokeAsync(cid);

    public sealed class VModel : ReactiveObject
    {
        readonly IKmsServer server;
        Option<Concept> concept;
        readonly ObservableAsPropertyHelper<string> name;
        bool isSaving;
        bool isNameEditing;
        string editingName = string.Empty;

        readonly ObservableAsPropertyHelper<Observation<System.Collections.Generic.HashSet<ConceptListItem>>> subConcepts;
        readonly ObservableAsPropertyHelper<List<BreadcrumbItem>> ownerBreadcrumbs;

        readonly ObservableAsPropertyHelper<string> addConceptButtonIcon;
        readonly ObservableAsPropertyHelper<Color> addConceptButtonColor;

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
            ownerBreadcrumbs = this.WhenAnyValue(my => my.Concept)
                                   .Where(c => c.IsSome)
                                   .SelectMany(async c => await server.GetOwners(c.Get().Id))
                                   .Select(owners => new List<BreadcrumbItem>(owners.Map(c => new BreadcrumbItem(c.Name, c.Id))))
                                   .ToProperty(this, my => my.OwnerBreadcrumbs, new List<BreadcrumbItem>());
            addConceptButtonIcon = this.WhenAnyValue(x => x.isSaving)
                                 .Select(saving => saving ? Icons.Material.Filled.Savings : Icons.Material.Filled.Add)
                                 .ToProperty(this, x => x.AddConceptButtonIcon);
            addConceptButtonColor = this.WhenAnyValue(x => x.isSaving)
                                  .Select(saving => saving ? Color.Dark : Color.Primary)
                                  .ToProperty(this, x => x.AddConceptButtonColor);

            #region Name Editing
            this.WhenAnyValue(my => my.Concept).Subscribe(_ => IsNameEditing = false);
            this.WhenAnyValue(my => my.Name).Subscribe(n => editingName = n);
            
            BeginEditName = ReactiveCommand.Create(() => IsNameEditing = true);
            CancelEditName = ReactiveCommand.Create(() => IsNameEditing = false);
            SaveEditName = ReactiveCommand.CreateFromTask(UpdateConcept);
            #endregion
            
            NewConcept = ReactiveCommand.CreateFromTask<string,Concept>(NewConceptTask);
        }

        public Option<Concept> Concept {
            get => concept;
            set => this.RaiseAndSetIfChanged(ref concept, value);
        }

        public string Name => name.Value;

        public Observation<System.Collections.Generic.HashSet<ConceptListItem>> SubConcepts => subConcepts.Value;

        public List<BreadcrumbItem> OwnerBreadcrumbs => ownerBreadcrumbs.Value;

        public bool IsSaving {
            get => isSaving;
            set => this.RaiseAndSetIfChanged(ref isSaving, value);
        }
        
        public string AddConceptButtonIcon => addConceptButtonIcon.Value;
        public Color AddConceptButtonColor => addConceptButtonColor.Value;

        #region Name Editing

        public string EditingName {
            get => editingName;
            set => this.RaiseAndSetIfChanged(ref editingName, value);
        }
        
        public bool IsNameEditing {
            get => isNameEditing;
            set => this.RaiseAndSetIfChanged(ref isNameEditing, value);
        }
        
        public ReactiveCommand<Unit,bool> BeginEditName { get; }
        public ReactiveCommand<Unit,bool> CancelEditName { get; }
        public ReactiveCommand<Unit,Unit> SaveEditName { get; }

        async Task UpdateConcept() {
            IsSaving = true;
            try {
                var current = Concept.Get();
                var updated = current with{ Name = editingName };
                Concept = await server.Update(updated);
            }
            finally {
                IsSaving = false;
            }
        }
        
        #endregion

        public ReactiveCommand<string,Concept> NewConcept { get; }
        async Task<Concept> NewConceptTask(string conceptName) {
            IsSaving = true;
            try {
                return await server.CreateSubConcept(concept.Get().Id, conceptName);
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