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
    ValueTask<Concept> GetHome();
    ValueTask<Option<Concept>> Fetch(ConceptId id);
    ValueTask<Seq<Concept>> Search(string keyword, CancellationToken cancellationToken);

    ValueTask<Concept> CreateSubConcept(ConceptId owner, string name);

    ValueTask<Concept> NewLink(Concept owner, Option<string> name, URI uri);
    ValueTask<Seq<LinkObject>> GetLinks(Seq<ConceptId> linkIds);
    ValueTask<Seq<Concept>> GetOwners(ConceptId id);

    ValueTask<Concept> Update(Concept concept);
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
                             Map.empty<ConceptId, Seq<ConceptId>>());
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

    public async ValueTask<Concept> GetHome() {
        var state = await snapshot;
        return state.Concepts[state.Home.Get()];
    }

    public async ValueTask<Option<Concept>> Fetch(ConceptId id) {
        var state = await snapshot;
        if (state.Concepts.Get(id).IfSome(out var v))
            return Some(v);
        else {
            await using var session = db.Session();
            return await Transact(Operations.Fetch(session, id));
        }
    }

    public ValueTask<Seq<Concept>> Search(string keyword, CancellationToken cancellationToken) {
        throw new NotImplementedException();
    }

    public async ValueTask<Concept> CreateSubConcept(ConceptId owner, string name) {
        await using var session = db.Session();
        return await Transact(Operations.CreateSubConcept(session, owner, name));
    }

    public async ValueTask<Concept> NewLink(Concept owner, Option<string> name, URI uri) {
        await using var session = db.Session();
        var (concept, _) = await Transact(Operations.CreateLink(session, owner, name, uri));
        return concept;
    }

    public ValueTask<Seq<LinkObject>> GetLinks(Seq<ConceptId> linkIds) => 
        Fetch(linkIds, state => state.Links, Operations.FetchLinkObject);

    public async ValueTask<Seq<Concept>> GetOwners(ConceptId id) {
        var state = await snapshot;
        var owners = state.Owners.Find(id).IfSome(out var result) ? result.ToSeq() : await findOwners();
        return await FetchUnsafe(owners);

        async Task<Seq<ConceptId>> findOwners() {
            await using var session = db.Session();
            return await Transact(Operations.FindOwners(session, id));
        }
    }

    public async ValueTask<Concept> Update(Concept concept) {
        var state = await snapshot;
        var current = state.Concepts[concept.Id];
        await using var session = db.Session();
        return await Transact(Operations.Update(session, current, concept));
    }

    ValueTask<Seq<Concept>> FetchUnsafe(Seq<ConceptId> ids) => 
        Fetch(ids, state => state.Concepts, Operations.Fetch);

    async ValueTask<Seq<T>> Fetch<T>(Seq<ConceptId> ids,
                                     Func<State, Map<ConceptId, T>> getMap,
                                     Func<IKmsDatabaseSession, Seq<ConceptId>, TransactionResult<Seq<T>>> fetch) {
        var state = await snapshot;
        var map = getMap(state);
        var (existing, needFetching) = ids.Partition(map.Find);
        if (needFetching.IsEmpty) return existing;

        await using var session = db.Session();
        var fetched = await Transact(fetch(session, needFetching.ToSeq()));
        return existing.Append(fetched);
    }

    #region Transaction methods

    Task<T> Transact<T>(TransactionResult<T> operation) {
        var response = new TaskCompletionSource<T>();

        async Task<State> updater(State state) {
            try {
                var (newState, result) = await operation(state);
                result.Then(response.SetResult, response.SetException);
                return newState;
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
                var newState = await updater(state).ConfigureAwait(false);
                snapshot = Task.FromResult(newState);
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

    #endregion

    readonly record struct State(
        Option<ConceptId> Home,
        Map<ConceptId, ConceptTag> Tags,
        Map<ConceptId, Concept> Concepts,
        Map<ConceptId, LinkObject> Links,
        Map<ConceptId, Seq<ConceptId>> Owners
    );

    delegate Task<State> TransactionTask(State state);
    delegate Task<(State NewState, Result<T> Result)> TransactionResult<T>(State server);

    static class Operations
    {
        public static TransactionResult<Option<Concept>> Fetch(IKmsDatabaseSession session, ConceptId id) => async state => {
            if (state.Concepts.Find(id).IfSome(out var existing))
                return (state, new(existing));
            
            var concept = await session.FetchConcept(id);
            var newState = state with{
                Concepts = concept.Map(c => state.Concepts.Add(c.Id, c)).IfNone(state.Concepts)
            };
            return (newState, new(concept));
        };

        public static TransactionResult<Seq<Concept>> Fetch(IKmsDatabaseSession session, Seq<ConceptId> ids) =>
            Fetch(ids, state => state.Concepts, (state, map) => state with{ Concepts = map }, session.FetchConcepts);

        public static TransactionResult<Seq<LinkObject>> FetchLinkObject(IKmsDatabaseSession session, Seq<ConceptId> ids) => 
            Fetch(ids, state => state.Links, (state, map) => state with{ Links = map }, session.FetchLinkObjects);

        public static TransactionResult<Seq<ConceptId>> FindOwners(IKmsDatabaseSession session, ConceptId cid) => async state => {
            if (state.Owners.Find(cid).IfSome(out var existing))
                return (state, new(existing));
            var ownerIds = await session.FetchOwners(cid);
            var newState = state with{ Owners = state.Owners.Add(cid, ownerIds) };
            return (newState, new(ownerIds));
        };

        public static TransactionResult<Concept> CreateSubConcept(IKmsDatabaseSession session, ConceptId owner, string name) => async state => {
            var newConcept = await session.CreateSubConcept(owner, name);
            var ownerConcept = state.Concepts[owner];
            var updatedOwner = ownerConcept with{ Contains = ownerConcept.Contains.Add(newConcept.Id) };
            var newState = state with{
                Concepts = state.Concepts.Add(newConcept.Id, newConcept).AddOrUpdate(owner, updatedOwner),
                Owners = state.Owners.Add(newConcept.Id, Seq1(owner))
            };
            return (newState, new(newConcept));
        };

        public static TransactionResult<(Concept, LinkObject)> CreateLink(IKmsDatabaseSession session, Concept owner, Option<string> name, URI uri) =>
            async state => {
                var link = await session.CreateLink(owner.Id, name, uri);
                var updated = owner with{ Links = owner.Links.Add(link.Id) };
                var newState = state with{
                    Concepts = state.Concepts.AddOrUpdate(updated.Id, updated),
                    Links = state.Links.Add(link.Id, link)
                };
                return (newState, new((updated, link)));
            };

        public static TransactionResult<Concept> Update(IKmsDatabaseSession session, Concept old, Concept @new) => async state => {
            var concept = await session.Update(old, @new);
            var newState = state with{ Concepts = state.Concepts.AddOrUpdate(concept.Id, concept) };
            return (newState, new(concept));
        };

        static TransactionResult<Seq<T>> Fetch<T>(Seq<ConceptId> ids,
                                                  Func<State, Map<ConceptId, T>> getMap,
                                                  Func<State, Map<ConceptId, T>, State> setMap,
                                                  Func<Seq<ConceptId>, Task<(Seq<T>, Seq<ConceptId>)>> fetch) where T : IDomainObject =>
            async state => {
                var map = getMap(state);
                var (existings, missings) = ids.Partition(map.Find);

                var (missingConcepts, invalids) = await fetch(missings);
                var newState = setMap(state, map.AddRange(missingConcepts.Map(c => (c.Id, c))));
                var result = invalids.IsEmpty
                                 ? new(existings.Append(missingConcepts))
                                 : new Result<Seq<T>>(new InvalidOperationException($"Invalid IDS: {invalids}"));
                return (newState, result);
            };
    }
}