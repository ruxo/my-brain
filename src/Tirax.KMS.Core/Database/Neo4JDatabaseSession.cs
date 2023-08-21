using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using RZ.Database;
using Tirax.KMS.Domain;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Seq = LanguageExt.Seq;

namespace Tirax.KMS.Database;

public sealed class Neo4JDatabase : IKmsDatabase, IAsyncDisposable
{
    readonly ILoggerFactory loggerFactory;
    readonly IDriver connector;
    
    #region ctors & dtor

    public Neo4JDatabase(ILoggerFactory loggerFactory, GenericDbConnection connection) {
        this.loggerFactory = loggerFactory;
        var auth = from user in connection.User
                   from pass in connection.Password
                   select AuthTokens.Basic(user, pass);
        connector = auth.IfSome(out var a)? GraphDatabase.Driver(connection.Host, a) : GraphDatabase.Driver(connection.Host);
    }

    public ValueTask DisposeAsync() => connector.DisposeAsync();
    
    #endregion
    
    public IKmsDatabaseSession Session() => new Neo4JDatabaseSession(loggerFactory.CreateLogger<Neo4JDatabaseSession>(), connector.AsyncSession());
}

static class Materialization
{
    public static class NodeLabels
    {
        public const string Tag = "Tag";
        public const string LinkObject = "LinkObject";
        public const string Concept = "Concept";
    }
    
    public static ConceptTag ToConceptTag(this IRecord record) {
        var node = record["tag"].As<INode>();
        Debug.Assert(node.Labels.Single() == NodeLabels.Tag);
        return new(node.ElementId, node["name"].As<string>());
    }

    public static Concept ToConcept(this IRecord record) {
        var node = record["concept"].As<INode>();
        var contains = record.ReadConceptIdList("contains");
        var links = record.ReadConceptIdList("links");
        var tags = record.ReadConceptIdList("tags");
        
        Debug.Assert(node.Labels.Single() == NodeLabels.Concept);
        return new(node.ElementId, node["name"].As<string>()){
            Contains = toHashSet(contains),
            Links = toHashSet(links),
            Tags = toHashSet(tags)
        };
    }

    public static LinkObject ToLinkObject(this IRecord record) {
        var node = record["link"].As<INode>();
        return new(node.ElementId, Optional(node["name"].As<string>()), new(node["uri"].As<string>()));
    }

    public static (ConceptId, float) ToSearchConceptResult(this IRecord record) {
        var score = record["score"].As<float>();
        var conceptId = record["conceptId"].As<string>();
        return (conceptId, score);
    }

    static Seq<ConceptId> ReadConceptIdList(this IRecord record, string fieldName) =>
        record[fieldName].As<IEnumerable<object>>().ToSeq().Map(o => new ConceptId(o.As<string>()));
}

public sealed class Neo4JDatabaseSession : IKmsDatabaseSession
{
    readonly ILogger logger;
    readonly Option<IDisposable> loggerDisposal;
    readonly IAsyncSession session;
    
    #region ctors & dtor

    public Neo4JDatabaseSession(ILogger logger, IAsyncSession session) {
        this.logger = logger;
        this.session = session;
        loggerDisposal = Optional(logger.BeginScope($"Neo4JDatabaseSession:{session.GetHashCode()}"));
    }

    public ValueTask DisposeAsync() {
        loggerDisposal.Do(d => d.Dispose());
        return session.DisposeAsync();
    }

    #endregion

    const string ConceptReturn = " RETURN concept, [(concept)-[:CONTAINS]->(sub)|elementId(sub)] AS contains," +
                                 "[(concept)-[:REFERS]->(link)|elementId(link)] AS links," +
                                 "[(concept)-[:TAG]->(tag)|elementId(tag)] AS tags";

    public async Task<LinkObject> CreateLink(ConceptId ownerId, Option<string> name, URI uri) {
        const string q = """
MATCH (owner:Concept)
WHERE elementId(owner) = $oid
CREATE (link:LinkObject { name: $name, uri: $uri }), (owner)-[:REFERS]->(link)
RETURN link
""";
        var result = await Query(q, Materialization.ToLinkObject, new{ oid = ownerId.Value, name = name.ToNullable(), uri = uri.Value });
        return result.Single();
    }

    public Task<Concept> CreateSubConcept(ConceptId owner, string name) =>
        session.ExecuteWriteAsync(async tx => {
            const string q = """
MATCH (owner:Concept)
WHERE elementId(owner) = $oid
CREATE (concept:Concept { name: $name }), (owner)-[:CONTAINS]->(concept)
RETURN concept, [] AS contains, [] AS links, [] AS tags
""";
            var newConcept = await Query(tx, q, Materialization.ToConcept, new{oid=owner.Value, name});
            return newConcept.First();
        });
    
    public async Task<Option<Concept>> FetchConcept(ConceptId conceptId) {
        const string q = "MATCH (concept:Concept) WHERE elementId(concept) = $cid " + ConceptReturn;
        var result = await Query(q, Materialization.ToConcept, new{ cid = conceptId.Value });
        return result.TrySingle();
    }
    
    public Task<(Seq<Concept> Result, Seq<ConceptId> Invalids)> FetchConcepts(Seq<ConceptId> conceptIds) => 
        Fetch("MATCH (concept:Concept) WHERE elementId(concept) IN $ids " + ConceptReturn,
              Materialization.ToConcept,
              conceptIds);

    public Task<(Seq<LinkObject> Result, Seq<ConceptId> Invalids)> FetchLinkObjects(Seq<ConceptId> linkIds) => 
        Fetch("MATCH (link:LinkObject) WHERE elementId(link) IN $ids RETURN link",
              Materialization.ToLinkObject,
              linkIds);

    public ValueTask<Seq<ConceptTag>> GetTags() =>
        Query("MATCH (tag:Tag) RETURN tag", Materialization.ToConceptTag);
    
    public async ValueTask<Concept> GetHome() {
        const string q = "MATCH (t:Bookmark { label: 'home' })-[:POINT]->(concept:Concept) " + ConceptReturn;
        var result = await Query(q, Materialization.ToConcept);
        return result.Single();
    }

    public ValueTask<Seq<ConceptId>> FetchOwners(ConceptId conceptId) {
        const string q = """
MATCH (owner:Concept)-[:CONTAINS]->(concept:Concept)
WHERE elementId(concept) = $cid
RETURN elementId(owner) AS id
""";
        return Query(q, rec => new ConceptId(rec["id"].As<string>()), new{ cid = conceptId.Value });
    }
    
    #region Keyword search

    public ValueTask<Seq<(ConceptId Id, float Score)>> SearchByConceptName(string name, int maxResult) {
        const string q = """
CALL db.index.fulltext.queryNodes("conceptNameIndex", $name) YIELD node, score
WITH node AS concept, score
RETURN elementId(concept) AS conceptId, score
LIMIT $maxResult
""";
        return Query(q, Materialization.ToSearchConceptResult, new{ name, maxResult });
    }
    
    public ValueTask<Seq<(ConceptId Id, float Score)>> SearchByLinkName(string name, int maxResult) {
        const string q = """
CALL db.index.fulltext.queryNodes("linkObjectNameIndex", $name) YIELD node, score
WITH node AS link, score
MATCH (concept:Concept)-[:REFERS]->(link)
RETURN elementId(concept) AS conceptId, score
LIMIT $maxResult
""";
        return Query(q, Materialization.ToSearchConceptResult, new{ name, maxResult });
    }

    #endregion
    
    public async Task<Concept> Update(Concept old, Concept @new) {
        Debug.Assert(old.Id == @new.Id);
        
        var sb = new StringBuilder(128);
        if (old.Name != @new.Name)
            sb.AppendLine("SET concept.name = $name");

        if (sb.Length > 0) {
            sb.Insert(0, "MATCH (concept:Concept) WHERE elementId(concept) = $cid ");

            await session.RunAsync(sb.ToString(), new{ cid = @new.Id.Value, name = @new.Name });
        }
        return @new;
    }

    public async ValueTask RecordUpTime(DateTime startTime) {
        const string q = """
MERGE (t:Bookmark { label: 'uptime' })
ON CREATE SET t.startTime = $startTime, t.uptime = $uptime
ON MATCH SET t.startTime = $startTime, t.uptime = $uptime
""";
        var uptime = DateTime.UtcNow - startTime;
        await session.RunAsync(q, new{ startTime, uptime });
    }

    async Task<(Seq<T> Result, Seq<ConceptId> Invalids)> Fetch<T>(string q, Func<IRecord, T> mapper, Seq<ConceptId> ids) where T : IDomainObject {
        if (ids.IsEmpty)
            return (Seq.empty<T>(), ids);
        var result = await Query(q, mapper, new{ ids = ids.Map(lid => lid.Value).ToArray() });
        var invalids = ids.Except(result.Map(x => x.Id)).ToSeq();
        return (result, invalids);
    }

    ValueTask<Seq<T>> Query<T>(string query, Func<IRecord, T> mapper, object? parameters = null) =>
        Query(session, query, mapper, parameters);

    async ValueTask<Seq<T>> Query<T>(IAsyncQueryRunner runner, string query, Func<IRecord, T> mapper, object? parameters = null) {
        var records = await (parameters is null ? runner.RunAsync(query) : runner.RunAsync(query, parameters));
        var result = await records.Map(mapper).ToArrayAsync();
        logger.LogDebug("Query {Query}, got {Count} result", query, result.Length);
        return Seq(result);
    }
}