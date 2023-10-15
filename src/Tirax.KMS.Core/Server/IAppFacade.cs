using Akka.Actor;

namespace Tirax.KMS.Server;

public interface IAppFacade
{
    IActorRef PublicLibrarian { get; }
}