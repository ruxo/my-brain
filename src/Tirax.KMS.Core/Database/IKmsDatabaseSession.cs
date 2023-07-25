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
    
    ValueTask<Seq<ConceptTag>> GetTags();
    ValueTask<Concept> GetHome();
    ValueTask<Seq<ConceptId>> FetchOwners(ConceptId conceptId);

    ValueTask<Seq<(ConceptId Id, float Score)>> SearchByConceptName(string name, int maxResult);
    ValueTask<Seq<(ConceptId Id, float Score)>> SearchByLinkName(string name, int maxResult);

    Task<Concept> Update(Concept old, Concept @new);

    ValueTask RecordUpTime(DateTime startTime);
}