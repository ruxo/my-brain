﻿using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReactiveUI;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;

namespace Tirax.KMS.App.Features.Topic;

public partial class Topic
{
    public Topic() {
        this.WhenActivated(_ => {
            AppModel.CurrentTopic = TopicId;
            ViewModel = new(AppModel);
        });
    }

    [Parameter]
    public string TopicId { get; set; } = default!;

    [Inject]
    AppModel.ViewModel AppModel { get; set; } = default!;
    
    [Inject]
    public IKmsServer Server { get; set; } = null!;

    Task OnConceptSelected(ConceptId id) => AppModel.Go.Execute(id).ToTask();

    public sealed class VModel : ReactiveObject
    {
        readonly ObservableAsPropertyHelper<List<BreadcrumbItem>> breadcrumbs;

        public VModel(AppModel.ViewModel vm) {
            breadcrumbs = vm.WhenAnyValue(x => x.History)
                            .Select(list => new List<BreadcrumbItem>(list.Map(c => new BreadcrumbItem(c.Name, c.Id, c.Id == vm.CurrentTopic))))
                            .ToProperty(this, my => my.Breadcrumbs);
        }

        public List<BreadcrumbItem> Breadcrumbs => breadcrumbs.Value;
    }
}