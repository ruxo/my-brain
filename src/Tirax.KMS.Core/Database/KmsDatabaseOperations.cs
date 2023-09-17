using RZ.Database.Neo4J;
using RZ.Database.Neo4J.Query;
using Tirax.KMS.Domain;
using static RZ.Database.Neo4J.Query.ValueTerm;

namespace Tirax.KMS.Database;

public static class KmsDatabaseOperations
{
    public static ValueTask<LinkObject> CreateLink(this IQueryRunner runner, ConceptId owner, Option<string> name, URI uri){
        throw new NotImplementedException();
    }
    
    public static ValueTask<Concept> CreateSubConcept(this IQueryRunner runner, ConceptId owner, string name){
        throw new NotImplementedException();
    }

    static readonly string FetchConceptQuery =
        Cypher.Match(QueryNode.Of("Concept", "concept"))
              .Where(new BooleanTerm.Eq(FunctionCall.Of("elementId", Var("concept")), Param("conceptId")))
              .Returns(new(Seq(Projection.Direct("concept"),
                               Projection.Select(QueryNode.AnyWithId("concept").LinkTo("CONTAINS", QueryNode.AnyWithId("sub")), Call("elementId", Var("sub"))),
                               Projection.Select(QueryNode.AnyWithId("concept").LinkTo("REFERS", QueryNode.AnyWithId("links")), Call("elementId", Var("links"))),
                               Projection.Select(QueryNode.AnyWithId("concept").LinkTo("TAGS", QueryNode.AnyWithId("tags")), Call("elementId", Var("tags")))
                               )));
    public static async ValueTask<Concept?> FetchConcept(this IQueryRunner runner, ConceptId conceptId) {
        var cursor = await runner.Read(FetchConceptQuery, new { conceptId });
        return await cursor.FetchAsync() ? cursor.Current.ToConcept() : null;
    }
    
    public static ValueTask<(Seq<Concept> Result, Seq<ConceptId> Invalids)> FetchConcepts(this IQueryRunner runner, Seq<ConceptId> conceptIds){
        throw new NotImplementedException();
    }
    
    public static ValueTask<(Seq<LinkObject> Result, Seq<ConceptId> Invalids)> FetchLinkObjects(this IQueryRunner runner, Seq<ConceptId> linkIds){
        throw new NotImplementedException();
    }

    static readonly string GetTagsQuery = Cypher.Match(QueryNode.Of("Tag", "tag")).Returns("tag");
    public static async ValueTask<Seq<ConceptTag>> GetTags(this IQueryRunner runner) {
        var cursor = await runner.Read(GetTagsQuery);
        var data = await cursor.ToArrayAsync();
        return data.ToSeq().Map(Materialization.ToConceptTag);
    }
    
    public static ValueTask<Concept> GetHome(this IQueryRunner runner){
        throw new NotImplementedException();
    }
    
    public static ValueTask<Seq<ConceptId>> FetchOwners(this IQueryRunner runner, ConceptId conceptId){
        throw new NotImplementedException();
    }
    
    public static ValueTask<Seq<(ConceptId Id, float Score)>> SearchByConceptName(this IQueryRunner runner, string name, int maxResult){
        throw new NotImplementedException();
    }
    
    public static ValueTask<Seq<(ConceptId Id, float Score)>> SearchByLinkName(this IQueryRunner runner, string name, int maxResult){
        throw new NotImplementedException();
    }
    
    public static ValueTask<Concept> Update(this IQueryRunner runner, Concept old, Concept @new){
        throw new NotImplementedException();
    }
    
    public static ValueTask RecordUpTime(this IQueryRunner runner, DateTime startTime){
        throw new NotImplementedException();
    }
    
}