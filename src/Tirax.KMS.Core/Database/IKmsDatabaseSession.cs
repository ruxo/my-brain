using Tirax.KMS.Domain;

namespace Tirax.KMS.Database;

public interface IKmsDatabase
{
    IKmsDatabaseSession Session();
}

public interface IKmsDatabaseSession : IAsyncDisposable
{
    Task<LinkObject> CreateLink(ConceptId owner, Option<string> name, URI uri);
    Task<Concept> CreateSubConcept(ConceptId owner, string name);
    
    Task<Option<Concept>> FetchConcept(ConceptId conceptId);
    Task<(Seq<Concept> Result, Seq<ConceptId> Invalids)> FetchConcepts(Seq<ConceptId> conceptIds);
    Task<(Seq<LinkObject> Result, Seq<ConceptId> Invalids)> FetchLinkObjects(Seq<ConceptId> linkIds);
    
    Task<Seq<ConceptTag>> GetTags();
    Task<Concept> GetHome();
    Task<Seq<ConceptId>> FetchOwners(ConceptId conceptId);

    Task<Seq<(Concept Concept, float Score)>> SearchByName(string name);

    Task<Concept> Update(Concept old, Concept @new);
}