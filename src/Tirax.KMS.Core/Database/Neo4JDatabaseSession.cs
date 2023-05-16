using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Tirax.KMS.Domain;
using Tirax.KMS.Extensions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

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
        var contains = record["contains"].As<IEnumerable<object>>().ToSeq().Map(o => new ConceptId(o.As<string>()));
        var tags = record["tags"].As<IEnumerable<object>>().ToSeq().Map(o => new ConceptId(o.As<string>()));
        
        Debug.Assert(node.Labels.Single() == NodeLabels.Concept);
        return new(node.ElementId, node["name"].As<string>()){
            Contains = toHashSet(contains),
            Tags = toHashSet(tags)
        };
    }
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

    public Task<Concept> CreateSubConcept(ConceptId owner, string name) =>
        session.ExecuteWriteAsync(async tx => {
            const string q = """
MATCH (owner:Concept)
WHERE elementId(owner) = $oid
CREATE (concept:Concept { name: $name }), (owner)-[:CONTAINS]->(concept)
RETURN concept, [] AS contains, [] AS tags
""";
            var newConcept = await Query(tx, q, Materialization.ToConcept, new{oid=owner.Value, name});
            return newConcept.First();
        });

    public Task<Seq<Concept>> FetchConcept(ConceptId conceptId) {
        const string q = """
MATCH (concept:Concept)
WHERE elementId(concept) = $cid
RETURN concept, [(concept)-[:CONTAINS]->(sub)|elementId(sub)] AS contains, [(concept)-[:TAG]->(tag)|elementId(tag)] AS tags
""";
        return Query(q, Materialization.ToConcept, new{ cid = conceptId.Value });
    }

    public Task<Seq<ConceptTag>> GetTags() =>
        Query("MATCH (tag:Tag) RETURN tag", Materialization.ToConceptTag);
    
    public async Task<Concept> GetHome() {
        const string q = """
MATCH (t:Bookmark { label: 'home' })-[:POINT]->(concept:Concept)
RETURN concept, [(concept)-[:CONTAINS]->(sub)|elementId(sub)] AS contains, [(concept)-[:TAG]->(tag)|elementId(tag)] AS tags
""";
        var result = await Query(q, Materialization.ToConcept);
        return result.Single();
    }

    Task<Seq<T>> Query<T>(string query, Func<IRecord, T> mapper, object? parameters = null) =>
        Query(session, query, mapper, parameters);

    async Task<Seq<T>> Query<T>(IAsyncQueryRunner runner, string query, Func<IRecord,T> mapper, object? parameters = null) {
        var records = await (parameters is null ? runner.RunAsync(query) : runner.RunAsync(query, parameters));
        var result = await records.Map(mapper).ToArrayAsync();
        logger.LogDebug("Query {Query}, got {Count} result", query, result.Length);
        return Seq(result);
    }
}