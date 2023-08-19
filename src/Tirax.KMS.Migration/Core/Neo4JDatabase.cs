using System.Runtime.CompilerServices;
using System.Text;
using Neo4j.Driver;
using Tirax.KMS.Database;
using Tirax.KMS.Migration.Core.Query;

namespace Tirax.KMS.Migration.Core;

public sealed class Neo4JDatabase : INeo4JDatabase, IAsyncDisposable, IDisposable
{
    readonly IDriver db;
    readonly IAsyncSession session;
    
    public Neo4JDatabase(GenericDbConnection connection) {
        var auth = from user in connection.User
                   from pass in connection.Password
                   select AuthTokens.Basic(user, pass);
        db = auth.IfSome(out var a)? GraphDatabase.Driver(connection.Host, a) : GraphDatabase.Driver(connection.Host);
        session = db.AsyncSession();
    }

    public async ValueTask CreateUniqueConstraint(string nodeType, string field) {
        await Execute($"CREATE CONSTRAINT IF NOT EXISTS FOR (x:{nodeType}) REQUIRE (x.{field}) IS UNIQUE");
    }

    public async ValueTask DeleteUniqueConstraint(string nodeType, string field) {
        await Execute($"DROP CONSTRAINT ON (x:{nodeType}) ASSERT x.{field} IS UNIQUE");
    }

    public async ValueTask CreateIndex(string nodeType, NodeFields fields) {
        var query = From("CREATE INDEX IF NOT EXISTS FOR (x:").Add(nodeType).Add(") ON (").Add(fields).Add(")");
        await Execute(query.ToString());
    }

    public async ValueTask DeleteIndex(string nodeType, NodeFields fields) {
        var query = From("DROP INDEX ON :").Add(nodeType).Add('(').Add(fields).Add(')');
        await Execute(query.ToString());
    }

    public async ValueTask CreateFullTextIndex(string indexName, string nodeType, NodeFields fields) {
        var query = From("CREATE FULLTEXT INDEX ").Add(indexName).Add(" IF NOT EXISTS FOR (x:").Add(nodeType).Add(") ON EACH [").Add(fields).Add(']');
        await Execute(query.ToString());
    }

    public async ValueTask DeleteFullTextIndex(string indexName) {
        await Execute($"DROP INDEX {indexName}");
    }

    public async ValueTask CreateNode(Neo4JNode node, params LinkTarget[] targets) {
        var n = new CreateNode(node, targets.ToSeq());
        var query = n.ToCommandString(new (128)).ToString();
        await Execute(query);
    }

    public async ValueTask DeleteNodes(params Neo4JNode[] nodes) {
        var sb = new StringBuilder();
        nodes.Iter(node => sb.AddDeleteExpression(node).NewLine());
        await Execute(sb.ToString());
    }

    public async ValueTask<IResultCursor> Query(string query, object? parameters = null) => 
        await (parameters is null ? session.RunAsync(query) : session.RunAsync(query, parameters));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<IResultSummary> Execute(string query, object? parameters = null) => 
        await (await Query(query, parameters)).ConsumeAsync();

    public async ValueTask DisposeAsync() {
        await session.DisposeAsync();
        await db.DisposeAsync();
    }

    public void Dispose() {
        session.Dispose();
        db.Dispose();
    }

    static StringBuilder From(string initial) => new StringBuilder(64).Add(initial);
}