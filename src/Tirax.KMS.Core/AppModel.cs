using System.Reactive.Linq;
using ReactiveUI;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;
using Unit = System.Reactive.Unit;

namespace Tirax.KMS;

public static class AppModel
{
    public const int MaxHistory = 10;

    public sealed class ViewModel : ReactiveObject
    {
        ConceptId currentTopic;

        readonly ObservableAsPropertyHelper<Observation<Concept>> currentConcept;
        readonly ObservableAsPropertyHelper<Lst<Concept>> history;

        bool currentDrawerIsOpen;
        readonly ObservableAsPropertyHelper<bool> drawerIsOpen;

        public ViewModel(ConceptId home, IKmsServer server) {
            currentTopic = home;
            
            async Task<Concept> fetch(string id) => (await server.Fetch(id)).Get();
            currentConcept = this.WhenAnyValue(my => my.CurrentTopic)
                                 .SelectMany(topic => Observation.From(() => fetch(topic)))
                                 .ToProperty(this, my => my.CurrentConcept);

            history = this.WhenAnyValue(my => my.CurrentConcept)
                          .Scan(Lst<Concept>.Empty,
                                (last, cid) => cid switch{
                                    Observation<Concept>.Loading
                                        or Observation<Concept>.Failed => last,
                                    Observation<Concept>.Data{ Value: var concept } => AddHistory(last, concept),
                                    _                                               => throw new NotSupportedException()
                                })
                          .ToProperty(this, my => my.History);

            GoHome = ReactiveCommand.Create(() => CurrentTopic = home);
            
            ToggleDrawerOpen = ReactiveCommand.Create(() => currentDrawerIsOpen = !currentDrawerIsOpen);
            drawerIsOpen = ToggleDrawerOpen.ToProperty(this, my => my.DrawerIsOpen);
        }

        public ConceptId CurrentTopic {
            get => currentTopic;
            set => this.RaiseAndSetIfChanged(ref currentTopic, value);
        }

        public Observation<Concept> CurrentConcept => currentConcept.Value;
        public Lst<Concept> History => history.Value;
        
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