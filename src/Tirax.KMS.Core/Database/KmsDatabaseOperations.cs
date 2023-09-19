using RZ.Database.Neo4J;
using RZ.Database.Neo4J.Query;
using Tirax.KMS.Domain;
using static RZ.Database.Neo4J.Prelude;
using static RZ.Database.Neo4J.Query.ValueTerm;

namespace Tirax.KMS.Database;

public static class KmsDatabaseOperations
{
    internal static readonly string CreateLinkQuery =
        Cypher.Match(("owner", "Concept"))
              .Where(Call("elementId", Var("owner")) == Param("oid"))
              .Create(N("link", "LinkObject", Props(("name", Param("name")), ("uri", Param("uri")))),
                      P(N("owner"), L("REFERS", N("link"))))
              .Return("link");
    public static async ValueTask<LinkObject> CreateLink(this IQueryRunner runner, ConceptId owner, string? name, URI uri) {
        var cursor = await runner.Read(CreateLinkQuery, new{ oid=owner.Value, name, uri = uri.Value });
        return await cursor.FetchAsync() ? cursor.Current.ToLinkObject() : throw new InvalidOperationException("Impossible case");
    }

    internal static readonly string CreateSubConceptQuery =
        Cypher.Match(("owner", "Concept"))
              .Where(Call("elementId", Var("owner")) == Param("oid"))
              .Create(N("concept", "Concept", Props(("name", Param("name")))),
                      P(N("owner"), L("CONTAINS", N("concept"))))
              .Return(Direct(Var("concept")), Alias("contains", SelectNone()), Alias("links", SelectNone()), Alias("tags", SelectNone()));
                         
    public static async ValueTask<Concept> CreateSubConcept(this IQueryRunner runner, ConceptId owner, string name){
        var cursor = await runner.Read(CreateSubConceptQuery, new{oid=owner.Value, name});
        return await cursor.FetchAsync() ? cursor.Current.ToConcept() : throw new InvalidOperationException("Impossible case");
    }

    static Cypher.ReturnBuilder ReturnConcept(Cypher.MergeBuilder mb) =>
        mb.Return(Direct(Var("concept")),
                  Alias("contains",
                        Select(QueryNode.AnyWithId("concept").LinkTo("CONTAINS", QueryNode.AnyWithId("sub")), Call("elementId", Var("sub")))),
                  Alias("links",
                        Select(QueryNode.AnyWithId("concept").LinkTo("REFERS", QueryNode.AnyWithId("links")), Call("elementId", Var("links")))),
                  Alias("tags", Select(QueryNode.AnyWithId("concept").LinkTo("TAGS", QueryNode.AnyWithId("tags")), Call("elementId", Var("tags")))));

    static readonly string FetchConceptQuery =
        ReturnConcept(Cypher.Match(("owner", "Concept"))
                            .Where(new BooleanTerm.Eq(FunctionCall.Of("elementId", Var("concept")), Param("conceptId"))));
    
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

    static readonly string GetTagsQuery = Cypher.Match(QueryNode.Of("tag", "Tag")).Return("tag");
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