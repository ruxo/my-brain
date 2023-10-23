using Tirax.KMS.Akka;

namespace Tirax.KMS.Server;

public interface IAppFacade
{
    LibraryFacade PublicLibrary { get; }
}