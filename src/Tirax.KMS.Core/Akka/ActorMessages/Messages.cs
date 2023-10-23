using Tirax.KMS.Domain;

namespace Tirax.KMS.Akka.ActorMessages;

public static class Librarian
{
    public sealed record GetRoot
    {
        public static readonly GetRoot Default = new();

        public sealed record Response(Concept Root);
    }

    public sealed record GetConcept(ConceptId Id)
    {
        public sealed record Response(Concept? Concept);
    }

    public sealed record GetConcepts(Seq<ConceptId> Ids)
    {
        public sealed record Response(Map<ConceptId,Concept> Concepts, Seq<ConceptId> Invalids);
    }

    public readonly record struct AddConcept(ConceptId Owner, string Name);
}