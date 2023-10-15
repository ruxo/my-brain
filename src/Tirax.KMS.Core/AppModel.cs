using System.Reactive.Linq;
using ReactiveUI;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;
using Unit = System.Reactive.Unit;

namespace Tirax.KMS;

public sealed class AppModel
{
    public const int MaxHistory = 10;

    public sealed class ViewModel : ReactiveObject
    {
        ConceptId currentTopic;

        readonly ObservableAsPropertyHelper<Concept> home;
        readonly ObservableAsPropertyHelper<Observation<Concept>> currentConcept;
        readonly ObservableAsPropertyHelper<Seq<Concept>> owners;
        readonly ObservableAsPropertyHelper<Lst<Concept>> history;

        bool currentDrawerIsOpen;
        readonly ObservableAsPropertyHelper<bool> drawerIsOpen;

        public ViewModel(ConceptId initialId, IKmsServer server) {
            currentTopic = initialId;
            
            home = Observable.FromAsync(async () => await server.GetHome()).ToProperty(this, my => my.Home);
            
            async Task<Concept> fetch(string id) => (await server.Fetch(id)).Get();
            currentConcept = this.WhenAnyValue(my => my.CurrentTopic)
                                 .SelectMany(topic => Observation.From(() => fetch(topic)))
                                 .ToProperty(this, my => my.CurrentConcept);

            owners = this.WhenAnyValue(my => my.CurrentTopic)
                         .SelectMany(async cid => await server.GetOwners(cid))
                         .ToProperty(this, my => my.Owners);
            history = this.WhenAnyValue(my => my.CurrentConcept)
                          .Scan(Lst<Concept>.Empty,
                                (last, cid) => cid switch{
                                    Observation<Concept>.Loading
                                        or Observation<Concept>.Failed => last,
                                    Observation<Concept>.Data{ Value: var concept } => AddHistory(last, concept),
                                    _                                               => throw new NotSupportedException()
                                })
                          .ToProperty(this, my => my.History);

            Go = ReactiveCommand.Create<ConceptId>(cid => CurrentTopic = cid);
            GoHome = ReactiveCommand.Create(() => {
                CurrentTopic = Home.Id;
                return Home.Id;
            });
            
            ToggleDrawerOpen = ReactiveCommand.Create(() => currentDrawerIsOpen = !currentDrawerIsOpen);
            drawerIsOpen = ToggleDrawerOpen.ToProperty(this, my => my.DrawerIsOpen);
        }

        public ConceptId CurrentTopic {
            get => currentTopic;
            set => this.RaiseAndSetIfChanged(ref currentTopic, value);
        }

        public Concept Home => home.Value;
        public Observation<Concept> CurrentConcept => currentConcept.Value;
        public Seq<Concept> Owners => owners.Value;
        public Lst<Concept> History => history.Value;
        
        public ReactiveCommand<ConceptId, Unit> Go { get; }
        public ReactiveCommand<Unit, ConceptId> GoHome { get; }

        public bool DrawerIsOpen => drawerIsOpen.Value;
        public ReactiveCommand<Unit, bool> ToggleDrawerOpen { get; }

        static Lst<Concept> AddHistory(Lst<Concept> history, Concept concept) {
            var result = history.Add(concept);
            if (result.Count > MaxHistory)
                result = result.RemoveAt(0);
            return result;
        }
    }
}