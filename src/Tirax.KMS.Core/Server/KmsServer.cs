using Tirax.KMS.Domain;

namespace Tirax.KMS.Server;

public interface IKmsServer
{
    Task<Option<Concept>> Fetch(string id);
    Task<Seq<Concept>> Search(string keyword, CancellationToken cancellationToken);
}

public sealed class KmsServer : IKmsServer
{
    public Task<Option<Concept>> Fetch(string id) {
        throw new NotImplementedException();
    }

    public Task<Seq<Concept>> Search(string keyword, CancellationToken cancellationToken) {
        throw new NotImplementedException();
    }
}