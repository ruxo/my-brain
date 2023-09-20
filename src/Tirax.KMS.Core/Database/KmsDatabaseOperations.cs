using System.Runtime.CompilerServices;
using Neo4j.Driver;
using RZ.Database.Neo4J;
using RZ.Database.Neo4J.Query;
using Tirax.KMS.Domain;
using static RZ.Database.Neo4J.Prelude;
using Seq = LanguageExt.Seq;

namespace Tirax.KMS.Database;

public static class KmsDatabaseOperations
{
    static ReturnBuilder ReturnConcept(MergeBuilder mb) =>
        mb.Return(Direct(Var("concept")),
                  Alias("contains",
                        Select(QueryNode.AnyWithId("concept").LinkTo("CONTAINS", QueryNode.AnyWithId("sub")), Call("elementId", Var("sub")))),
                  Alias("links",
                        Select(QueryNode.AnyWithId("concept").LinkTo("REFERS", QueryNode.AnyWithId("links")), Call("elementId", Var("links")))),
                  Alias("tags", Select(QueryNode.AnyWithId("concept").LinkTo("TAGS", QueryNode.AnyWithId("tags")), Call("elementId", Var("tags")))));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<LinkObject> CreateLink(this IQueryRunner runner, ConceptId owner, string? name, URI uri) => 
        runner.GetSingle(CreateLinkQuery, Materialization.ToLinkObject, new{ oid=owner.Value, name, uri = uri.Value });

    internal static readonly string CreateLinkQuery =
        Cypher.Match(("owner", "Concept"))
              .Where(Call("elementId", Var("owner")) == Param("oid"))
              .Create(N("link", "LinkObject", Props(("name", Param("name")), ("uri", Param("uri")))),
                      P(N("owner"), L("REFERS", N("link"))))
              .Return("link");
                         
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Concept> CreateSubConcept(this IQueryRunner runner, ConceptId owner, string name) => 
        runner.GetSingle(CreateSubConceptQuery, Materialization.ToConcept, new{ oid = owner.Value, name });

    static readonly string CreateSubConceptQuery =
        Cypher.Match(("owner", "Concept"))
              .Where(Call("elementId", Var("owner")) == Param("oid"))
              .Create(N("concept", "Concept", Props(("name", Param("name")))),
                      P(N("owner"), L("CONTAINS", N("concept"))))
              .Return(Direct(Var("concept")), Alias("contains", SelectNone()), Alias("links", SelectNone()), Alias("tags", SelectNone()));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Concept?> FetchConcept(this IQueryRunner runner, ConceptId conceptId) => 
        runner.TryGetSingle(FetchConceptQuery, Materialization.ToConcept, new{ conceptId });

    static readonly string FetchConceptQuery =
        ReturnConcept(Cypher.Match(("owner", "Concept"))
                            .Where(Call("elementId", Var("concept")) == Param("conceptId")));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<(Seq<Concept> Result, Seq<ConceptId> Invalids)> FetchConcepts(this IQueryRunner runner, Seq<ConceptId> conceptIds) => 
        runner.Fetch(FetchConceptsQuery, Materialization.ToConcept, conceptIds);

    static readonly string FetchConceptsQuery =
        ReturnConcept(Cypher.Match(("owner", "Concept"))
                            .Where(Contains(Call("elementId", Var("concept")), Param("ids"))));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<(Seq<LinkObject> Result, Seq<ConceptId> Invalids)> FetchLinkObjects(this IQueryRunner runner, Seq<ConceptId> linkIds) => 
        runner.Fetch(FetchLinkObjectsQuery, Materialization.ToLinkObject, linkIds);

    static readonly string FetchLinkObjectsQuery =
        Cypher.Match(("link", "LinkObject"))
              .Where(Contains(Call("elementId", Var("link")), Param("ids")))
              .Return("link");
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Seq<ConceptTag>> GetTags(this IQueryRunner runner) => 
        runner.GetSequence(GetTagsQuery, Materialization.ToConceptTag);
    
    static readonly string GetTagsQuery = Cypher.Match(("tag", "Tag")).Return("tag");
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Concept> GetHome(this IQueryRunner runner) => 
        runner.GetSingle(GetHomeQuery, Materialization.ToConcept);

    static readonly string GetHomeQuery =
        ReturnConcept(Cypher.Match(P(N("t", "Bookmark", Props(("label", "home"))), L("POINT", ("concept", "Concept")))));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Seq<ConceptId>> FetchOwners(this IQueryRunner runner, ConceptId conceptId) => 
        runner.GetSequence(FetchOwnersQuery, rec => new ConceptId(rec["id"].As<string>()), new{ cid = conceptId.Value });

    static readonly string FetchOwnersQuery =
        Cypher.Match(P(("owner", "Concept"), L("CONTAINS", ("concept", "Concept"))))
              .Where(Call("elementId", Var("concept")) == Param("cid"))
              .Return(Alias("id", Direct(Call("elementId", "owner"))));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Seq<(ConceptId Id, float Score)>> SearchByConceptName(this IQueryRunner runner, string name, int maxResult) => 
        runner.GetSequence(SearchByConceptNameQuery, Materialization.ToSearchConceptResult, new{ name, maxResult });

    static readonly string SearchByConceptNameQuery =
        Cypher.Call(Call("db.index.fulltext.queryNodes", "conceptNameIndex", Param("name")), Direct(Var("node")), Direct(Var("score")))
              .Return(Alias("conceptId", Direct(Call("elementId", Var("node")))), Direct(Var("score")))
              .Limit(Param("maxResult"));
    
    public static ValueTask<Seq<(ConceptId Id, float Score)>> SearchByLinkName(this IQueryRunner runner, string name, int maxResult) => 
        runner.GetSequence(SearchByLinkNameQuery, Materialization.ToSearchConceptResult, new{ name, maxResult });

    static readonly string SearchByLinkNameQuery =
        Cypher.Call(Call("db.index.fulltext.queryNodes", "linkObjectNameIndex", Param("name")), Direct(Var("node")), Direct(Var("score")))
              .Match(P(("concept", "Concept"), L("REFERS", N("node"))))
              .Return(Alias("conceptId", Direct(Call("elementId", Var("node")))), Direct(Var("score")))
              .Limit(Param("maxResult"));
    
    public static async ValueTask<Concept> Update(this IQueryRunner runner, Concept old, Concept @new){
        if (old.Name != @new.Name)
            await runner.Write(UpdateQuery, new{ cid = @new.Id.Value, name = @new.Name });
        return @new;
    }

    static readonly string UpdateQuery =
        Cypher.Match(("concept", nameof(Concept)))
              .Where(Call("elementId", Var("concept")) == Param("cid"))
              .Set((Field("concept", "name"), Param("name")));
    
    public static async ValueTask RecordUpTime(this IQueryRunner runner, DateTime startTime){
        var uptime = DateTime.UtcNow - startTime;
        await runner.Write(RecordUpTimeQuery, new{ startTime, uptime });
    }

    internal static readonly string RecordUpTimeQuery =
        Cypher.Merge(N("t", NodeLabels.Bookmark, Props(("label", "uptime"))),
                     PropSet((Field("t", "startTime"), Param("startTime")), (Field("t", "uptime"), Param("uptime"))),
                     PropSet((Field("t", "startTime"), Param("startTime")), (Field("t", "uptime"), Param("uptime"))));

    static async ValueTask<T?> TryGetSingle<T>(this IQueryRunner runner, string query, Func<IRecord, T> mapper, object? @params = null) {
        var cursor = await runner.Read(query, @params);
        return await cursor.FetchAsync() ? mapper(cursor.Current) : default;
    }

    static async ValueTask<T> GetSingle<T>(this IQueryRunner runner, string query, Func<IRecord, T> mapper, object? @params = null) => 
        await runner.TryGetSingle(query, mapper, @params) ?? throw new InvalidOperationException("Impossible case");
    
    static async ValueTask<Seq<T>> GetSequence<T>(this IQueryRunner runner, string q, Func<IRecord, T> mapper, object? @params = null) {
        var cursor = await runner.Read(q, @params);
        return Seq(await cursor.ToArrayAsync()).Map(mapper);
    }
    
    static async ValueTask<(Seq<T> Result, Seq<ConceptId> Invalids)> Fetch<T>(this IQueryRunner runner, string q, Func<IRecord, T> mapper, Seq<ConceptId> ids) where T : IDomainObject {
        if (ids.IsEmpty)
            return (Seq.empty<T>(), ids);
        var cursor = await runner.Read(q, new{ ids = ids.Map(lid => lid.Value).ToArray() });
        var result = Seq(await cursor.ToArrayAsync()).Map(mapper);
        var invalids = ids.Except(result.Map(x => x.Id)).ToSeq();
        return (result, invalids);
    }
}