using System.Diagnostics;
using Akka.Actor;
using RZ.Database.Neo4J;
using TCRB.CoreApp;
using Tirax.KMS.Database;
using Tirax.KMS.Domain;
using static Tirax.KMS.Akka.ActorMessages.Librarian;
using Map = LanguageExt.Map;

namespace Tirax.KMS.Akka;

public sealed class Librarian : ReceiveActor, IWithStash
{
    readonly INeo4JDatabase db;
    State state;
    
    public Librarian(INeo4JDatabase db, Concept root, Seq<ConceptTag> initialTags) {
        this.db = db;
        state = new(root.Id,
                    initialTags.Map(tag => (tag.Id, tag)).ToMap(),
                    Map.empty<ConceptId, Concept>().Add(root.Id, root),
                    Map.empty<ConceptId, LinkObject>(),
                    Map.empty<ConceptId, Seq<ConceptId>>());
        
        Receive<GetRoot>(_ => Sender.Tell(state.Concepts[state.Root]));
        ReceiveAsync<GetConcept>(async req => Sender.Tell(await Fetch(req.Id)));
        ReceiveAsync<GetConcepts>(async req => {
            var outcome = await Fetch(req.Ids);
            Sender.Tell(outcome.Map(r => new GetConcepts.Response(r.Valid, r.Invalid)));
        });
        
        ReceiveAsync<AddConcept>(async req => Sender.Tell(await AddConcept(req.Owner, req.Name)));
    }

    async ValueTask<Outcome<Option<Concept>>> Fetch(ConceptId id) {
        if (state.Concepts.Get(id).IfSome(out var v))
            return Some(v);
        else {
            var outcome = await Outcome.Of(() => db.Read(q => q.FetchConcept(id)));
            var concept = outcome.Result.Match(identity, _ => None);
            if (concept.IfSome(out var c))
                state = state with{ Concepts = state.Concepts.Add(id, c) };
            return outcome;
        }
    }

    async ValueTask<Outcome<FetchResult>> Fetch(Seq<ConceptId> ids) {
        try {
            var (existed, notInCache) = ids.Map(id => (id, concept: state.Concepts.Get(id)))
                                           .Partition(result => result.concept.IsSome, r => r.concept.Get(), r => r.id);
            var (fromDb, invalids) = await db.Read(q => q.FetchConcepts(notInCache.ToSeq()));
            state = fromDb.Fold(state, (s, concept) => s with{ Concepts = s.Concepts.Add(concept.Id, concept) });
            return new FetchResult(Seq(existed).Append(fromDb).Map(c => (c.Id, c)).ToMap(), invalids);
        }
        catch (Exception e) {
            return ErrorFrom.Exception(e);
        }
    }

    readonly record struct FetchResult(Map<ConceptId, Concept> Valid, Seq<ConceptId> Invalid);

    async ValueTask<Outcome<Concept>> AddConcept(ConceptId ownerId, string name) {
        if (state.Concepts.Get(ownerId).IfSome(out var owner)) {
            var outcome = await Fetch(owner.Contains.ToSeq());
            if (outcome.IfFaulted(out var error, out var result)) return error;
            
            var (subConcepts, invalids) = result;
            Debug.Assert(invalids.IsEmpty);
            if (subConcepts.Values.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return ErrorInfo.Of(ErrorCodes.InvalidRequest, "Name is duplicated");

            var newConcept = await db.Write(q => q.CreateSubConcept(ownerId, name));
            var newOwnerConcept = owner with{ Contains = owner.Contains.Add(newConcept.Id) };
            state = state with{ Concepts = state.Concepts.AddOrUpdate(ownerId, newOwnerConcept) };
            return newConcept;
        }
        else
           return ErrorInfo.Of(ErrorCodes.InvalidRequest, "Invalid owner ID");
    }

    readonly record struct State(
        ConceptId Root,
        Map<ConceptId, ConceptTag> Tags,
        Map<ConceptId, Concept> Concepts,
        Map<ConceptId, LinkObject> Links,
        Map<ConceptId, Seq<ConceptId>> Owners
    );
    
    #region Framework declarations
    
    public IStash Stash { get; set; }
    
    #endregion
}