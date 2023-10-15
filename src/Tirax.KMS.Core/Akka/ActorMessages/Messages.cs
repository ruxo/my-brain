using Tirax.KMS.Domain;

namespace Tirax.KMS.Akka.ActorMessages;

public static class Librarian
{
    public sealed record GetRoot
    {
        public static readonly GetRoot Default = new();

        public sealed record Response(Concept Root);
    }
}