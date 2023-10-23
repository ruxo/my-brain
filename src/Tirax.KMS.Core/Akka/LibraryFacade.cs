using Akka.Actor;
using TCRB.CoreApp;
using Tirax.KMS.Domain;
using static Tirax.KMS.Akka.ActorMessages.Librarian;

namespace Tirax.KMS.Akka;

public sealed class LibraryFacade
{
    readonly IActorRef actor;
    
    public LibraryFacade(IActorRef actor) {
        this.actor = actor;
    }
    
    public async ValueTask<Outcome<Concept>> AddConcept(ConceptId owner, string name) => 
        await actor.Ask<Outcome<Concept>>(new AddConcept(owner, name));
    
    public async ValueTask<Outcome<Option<Concept>>> GetConcept(ConceptId id) =>
        await actor.Ask<Outcome<Option<Concept>>>(new GetConcept(id));

    public async ValueTask<Outcome<GetConcepts.Response>> GetConcepts(Seq<ConceptId> ids) =>
        await actor.Ask<Outcome<GetConcepts.Response>>(new GetConcepts(ids));
    
    public async ValueTask<Concept> GetRoot() =>
        await actor.Ask<Concept>(ActorMessages.Librarian.GetRoot.Default);
}