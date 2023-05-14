using Tirax.KMS.Domain;

namespace Tirax.KMS.Database;

public interface IKmsDatabase
{
    IKmsDatabaseSession Session();
}

public interface IKmsDatabaseSession : IAsyncDisposable
{
    Task<Seq<Concept>> FetchConcept(ConceptId conceptId);
    Task<Seq<ConceptTag>> GetTags();
    Task<Concept> GetHome();
}