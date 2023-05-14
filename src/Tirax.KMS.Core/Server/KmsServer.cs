using System.Collections.Concurrent;
using LanguageExt.Common;
using LanguageExt.UnitsOfMeasure;
using Microsoft.Extensions.Logging;
using Tirax.KMS.Database;
using Tirax.KMS.Domain;
using Tirax.KMS.Extensions;
using Map = LanguageExt.Map;

namespace Tirax.KMS.Server;

public interface IKmsServer : IDisposable
{
    Task<Concept> GetHome();
    Task<Option<Concept>> Fetch(ConceptId id);
    Task<Seq<Concept>> Search(string keyword, CancellationToken cancellationToken);
}

public sealed class KmsServer : IKmsServer
{
    readonly ILogger logger;
    readonly IKmsDatabase db;
    Task<State> snapshot;
    readonly BlockingCollection<TransactionTask> inbox = new();
    readonly ManualResetEventSlim inboxQuit = new();
    bool isDisposed;

    public KmsServer(ILogger<KmsServer> logger, IKmsDatabase db) {
        this.logger = logger;
        this.db = db;
        snapshot = Task.Run(async () => {
            await using var session = db.Session();
            var tags = await session.GetTags();
            var home = await session.GetHome();
            return new State(Some(home.Id),
                             tags.Map(t => (t.Id, t)).ToMap(),
                             Map.empty<ConceptId, Concept>().Add(home.Id, home),
                             Map.empty<ConceptId, LinkObject>(),
                             Map.empty<ConceptId, List<ConceptId>>());
        });

        Task.Factory.StartNew(InboxProcessor, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent);
    }

    public void Dispose() {
        if (isDisposed) return;
        isDisposed = true;
        inbox.CompleteAdding();
        inboxQuit.Wait(5.Seconds());
        inbox.Dispose();
    }

    public async Task<Concept> GetHome() {
        var state = await snapshot;
        return state.Concepts[state.Home.Get()];
    }

    public async Task<Option<Concept>> Fetch(ConceptId id) {
        var state = await snapshot;
        if (state.Concepts.Get(id).IfSome(out var v))
            return Some(v);
        else {
            await using var session = db.Session();
            return await Transact(Operations.Fetch(session, id));
        }
    }

    public Task<Seq<Concept>> Search(string keyword, CancellationToken cancellationToken) {
        throw new NotImplementedException();
    }
    
    #region Transaction methods

    Task<T> Transact<T>(TransactionResult<T> operation) {
        var response = new TaskCompletionSource<T>();

        async Task<ChangeLogs> updater(State state) {
            try {
                var (changes, result) = await operation(state);
                result.Then(response.SetResult, response.SetException);
                return changes;
            }
            catch (Exception e) {
                response.SetException(e);
                return new();
            }
        }
        inbox.Add(updater);

        return response.Task;
    }

    async Task InboxProcessor() {
        logger.LogInformation("KMS Server processor starts");
        try {
            foreach (var updater in inbox.GetConsumingEnumerable()) {
                var state = await snapshot;
                var changes = await updater(state).ConfigureAwait(false);
                snapshot = Task.FromResult(Apply(state, changes));
            }
            inboxQuit.Set();
        }
        catch (Exception e) {
            logger.LogCritical(e, "Unexpected inbox operation failure");
        }
        finally
        {
            logger.LogInformation("KMS Server processor ends");
        }
    }

    static State ApplyChange(State state, ModelChange change) =>
        change switch{
            ModelChange.ConceptChange c    => state with{ Concepts = c.Concept.Apply(state.Concepts, ct => ct.Id) },
            ModelChange.Tag t              => state with{ Tags = t.Value.Apply(state.Tags, tag => tag.Id) },
            ModelChange.OwnerChange o      => state with{ Owners = o.Owner.Apply(state.Owners) },
            ModelChange.LinkObjectChange l => state with{ Links = l.Link.Apply(state.Links, link => link.Id) },
            _                              => throw new NotSupportedException()
        };

    static State Apply(State state, ChangeLogs changes) => changes.Value.Fold(state, ApplyChange);

    #endregion

    readonly record struct State(
        Option<ConceptId> Home,
        Map<ConceptId, ConceptTag> Tags,
        Map<ConceptId, Concept> Concepts,
        Map<ConceptId, LinkObject> Links,
        Map<ConceptId, List<ConceptId>> Owners
    );

    delegate Task<ChangeLogs> TransactionTask(State state);
    delegate Task<(ChangeLogs Changes, Result<T> Result)> TransactionResult<T>(State server);

    static class Operations
    {
        public static TransactionResult<Option<Concept>> Fetch(IKmsDatabaseSession session, ConceptId id) => async state => {
            if (state.Concepts.Find(id).IfSome(out var existing))
                return (new(), new(existing));
            
            var concepts = await session.FetchConcept(id);
            var changes = concepts.Map(c => ModelChange.NewConceptChange(ModelOperationType.Add(c)));
            return (new(changes), new(concepts.HeadOrNone()));
        };

    }
}