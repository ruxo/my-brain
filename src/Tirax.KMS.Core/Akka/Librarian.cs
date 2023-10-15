using Akka.Actor;
using RZ.Database.Neo4J;
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
        
        Receive<GetRoot>(_ => Sender.Tell(new GetRoot.Response(root)));
        ReceiveAsync<GetConcept>(async req => Sender.Tell(new GetConcept.Response(await Fetch(req.Id))));
    }

    async ValueTask<Concept?> Fetch(ConceptId id) {
        if (state.Concepts.Get(id).IfSome(out var v))
            return v;
        else {
            var concept = await db.Read(q => q.FetchConcept(id));
            if (concept is not null) {
                state = state with{ Concepts = state.Concepts.Add(id, concept) };
                return concept;
            }
            return null;
        }
    }
    
    #region Messages

    sealed record Init
    {
        public static readonly Init Default = new();
    }

    #endregion

    readonly record struct State(
        ConceptId? Root,
        Map<ConceptId, ConceptTag> Tags,
        Map<ConceptId, Concept> Concepts,
        Map<ConceptId, LinkObject> Links,
        Map<ConceptId, Seq<ConceptId>> Owners
    );
    
    #region Framework declarations
    
    public IStash Stash { get; set; }
    
    #endregion
}