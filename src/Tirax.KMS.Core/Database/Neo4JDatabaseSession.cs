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
    
    public static ConceptTag ToConceptTag(this INode node) {
        Debug.Assert(node.Labels.Single() == NodeLabels.Tag);
        return new(node.ElementId, node["name"].As<string>());
    }

    public static Concept ToConcept(this INode node) {
        Debug.Assert(node.Labels.Single() == NodeLabels.Concept);
        return new(node.ElementId, node["name"].As<string>());
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

    public Task<Seq<Concept>> FetchConcept(ConceptId conceptId) =>
        Query("MATCH (v:Concept) WHERE elementId(v) = $cid RETURN v", Materialization.ToConcept, new {cid = conceptId.Value});
    
    public Task<Seq<ConceptTag>> GetTags() =>
        Query("MATCH (v:Tag) RETURN v", Materialization.ToConceptTag);
    
    public async Task<Concept> GetHome() {
        var result = await Query("MATCH (t:Bookmark { label: 'home' })-[:POINT]->(c:Concept) RETURN c", Materialization.ToConcept);
        return result.Single();
    }

    async Task<Seq<T>> Query<T>(string query, Func<INode,T> mapper, object? parameters = null) {
        var records = await (parameters is null ? session.RunAsync(query) : session.RunAsync(query, parameters));
        var result = await records.Map(rec => mapper(rec[0].As<INode>())).ToArrayAsync();
        logger.LogDebug("Query {Query}, got {Count} result", query, result.Length);
        return Seq(result);
    }
}