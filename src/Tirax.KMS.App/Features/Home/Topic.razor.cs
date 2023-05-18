using System.Reactive.Linq;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReactiveUI;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;

namespace Tirax.KMS.App.Features.Home;

public partial class Topic
{
    public Topic() {
        this.WhenActivated(_ => {
            ViewModel = new(AppModel);
        });
    }

    [Parameter]
    public string TopicId { get; set; } = default!;

    [Inject]
    AppModel.ViewModel AppModel { get; set; } = default!;
    
    [Inject]
    public IKmsServer Server { get; set; } = null!;

    protected override void OnParametersSet() {
        AppModel.CurrentTopic = TopicId;
        base.OnParametersSet();
    }

    void OnConceptSelected(ConceptId id) {
        AppModel.CurrentTopic = id;
    }

    public sealed class VModel : ReactiveObject
    {
        readonly ObservableAsPropertyHelper<List<BreadcrumbItem>> breadcrumbs;

        public VModel(AppModel.ViewModel vm) {
            breadcrumbs = vm.WhenAnyValue(x => x.History)
                            .Select(list => new List<BreadcrumbItem>(list.Map(c => new BreadcrumbItem(c.Name, $"/topic/{c.Id}", c.Id == vm.CurrentTopic))))
                            .ToProperty(this, my => my.Breadcrumbs);
        }

        public List<BreadcrumbItem> Breadcrumbs => breadcrumbs.Value;
    }
}