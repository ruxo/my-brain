using Akka.Actor;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;
using static Tirax.KMS.Akka.ActorMessages.Librarian;

namespace Tirax.KMS.Akka;

public sealed class Librarian : ReceiveActor, IWithStash
{
    public Librarian(IKmsServer server, Concept root, Seq<ConceptTag> initialTags) {
        Receive<GetRoot>(_ => Sender.Tell(new GetRoot.Response(root)));
    }
    
    #region Messages

    sealed record Init
    {
        public static readonly Init Default = new();
    }

    #endregion
    
    #region Framework declarations
    
    public IStash Stash { get; set; }
    
    #endregion
}