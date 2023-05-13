using System.Reactive.Linq;
using ReactiveUI;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;
using Unit = System.Reactive.Unit;

namespace Tirax.KMS;

public static class AppModel
{
    public const string RootTopic = "brain";
    public const int MaxHistory = 10;

    public sealed class ViewModel : ReactiveObject
    {
        string currentTopic = RootTopic;

        readonly List<string> historyBack = new();
        readonly ObservableAsPropertyHelper<Observation<Concept>> currentConcept;
        readonly ObservableAsPropertyHelper<IReadOnlyCollection<string>> history;

        bool currentDrawerIsOpen;
        readonly ObservableAsPropertyHelper<bool> drawerIsOpen;

        public ViewModel(IKmsServer server) {
            async Task<Concept> fetch(string id) => (await server.Fetch(id)).Get();
            currentConcept = this.WhenAnyValue(my => my.CurrentTopic)
                                 .SelectMany(topic => Observation.From(() => fetch(topic)))
                                 .ToProperty(this, my => my.CurrentConcept, scheduler: RxApp.MainThreadScheduler);
            history = this.WhenAnyValue(my => my.CurrentTopic)
                          .Scan(historyBack,
                                (last, cid) => {
                                    last.Add(cid);
                                    if (last.Count > MaxHistory) last.RemoveAt(0);
                                    return last;
                                })
                          .ToProperty(this, my => my.History);
            
            ToggleDrawerOpen = ReactiveCommand.Create(() => currentDrawerIsOpen = !currentDrawerIsOpen);
            drawerIsOpen = ToggleDrawerOpen.ToProperty(this, my => my.DrawerIsOpen);
        }

        public string CurrentTopic {
            get => currentTopic;
            set => this.RaiseAndSetIfChanged(ref currentTopic, value);
        }

        public Observation<Concept> CurrentConcept => currentConcept.Value;
        public IReadOnlyCollection<string> History => history.Value;

        public bool DrawerIsOpen => drawerIsOpen.Value;
        public ReactiveCommand<Unit, bool> ToggleDrawerOpen { get; }
    }
}