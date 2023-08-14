using System.Runtime.CompilerServices;
using System.Text;
using Neo4j.Driver;
using Tirax.KMS.Database;

namespace Tirax.KMS.Migration.Core;

static class StringBuilderExtension
{
    const char PropertyDelimiter = '.';
    const char NodeTypeDelimiter = ':';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder NewLine(this StringBuilder sb) => sb.Append('\n');
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Add(this StringBuilder sb, char c) => sb.Append(c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Add(this StringBuilder sb, string s) => string.IsNullOrEmpty(s)? sb : sb.Append(s);

    public delegate StringBuilder Transformer<in T>(StringBuilder sb, T value);

    public static StringBuilder Join<T>(this StringBuilder sb, Seq<T> seq, Transformer<T> mapper, Transformer<T> joiner) =>
        seq.HeadOrNone().Map(head => seq.Tail.Fold(mapper(sb,head), (inner, i) => mapper(joiner(inner,i), i))).IfNone(sb);

    public static StringBuilder Join<T>(this StringBuilder sb, char delimiter, Seq<T> seq, Transformer<T> mapper) =>
        seq.HeadOrNone().Map(head => seq.Tail.Fold(mapper(sb,head), (inner, i) => mapper(inner.Add(delimiter),i))).IfNone(sb);

    public static StringBuilder Add(this StringBuilder sb, NodeFields nodeFields, string nodeName = "x") => 
        sb.Join(',', nodeFields.Fields, (inner, field) => inner.Add(nodeName).Add(PropertyDelimiter).Add(field));

    public static StringBuilder Add(this StringBuilder sb, Neo4JProperty property, char delimiter = PropertyDelimiter) =>
        sb.Add(property.Name).Add(delimiter).AddQuotedString(property.Value);

    public static StringBuilder Add(this StringBuilder sb, Neo4JProperties properties) =>
        properties.Properties.HeadOrNone()
                  .Map(head => {
                       sb.Add('{').Add(head, NodeTypeDelimiter);
                       properties.Properties.Tail.Iter(item => sb.Add(',').Add(item, NodeTypeDelimiter));
                       return sb.Add('}');
                   })
                  .IfNone(sb);

    public static StringBuilder Add(this StringBuilder sb, Neo4JNode node, string name = "") {
        sb.Add('(').Add(name).Add(NodeTypeDelimiter);
        if (node.NodeType is not null) sb.Add(node.NodeType);
        return sb.Add(node.Body).Add(')');
    }

    public static StringBuilder Add(this StringBuilder sb, Neo4JLink link) => 
        sb.Add("[").Add(link.LinkType).Add(' ').Add(link.Body).Add(']');

    public static StringBuilder Add(this StringBuilder sb, Seq<LinkTarget> targets) => 
        targets.Fold(sb, (inner, target) => inner.Add('-').Add(target.Link).Add("->").Add(target.Target));

    public static StringBuilder AddDeleteExpression(this StringBuilder sb, Neo4JNode node) =>
        sb.Add("MATCH ").Add(node, "x").Add(" DETACH DELETE x;");

    public static StringBuilder AddQuotedString(this StringBuilder sb, string s) {
        sb.Add('\'');
        foreach(var c in s)
            if (c == '\'')
                sb.Append("\\'");
            else
                sb.Add(c);
        return sb.Add('\'');
    }
}

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
        await session.RunAsync($"CREATE CONSTRAINT IF NOT EXISTS FOR (x:{nodeType}) REQUIRE (x.{field}) IS UNIQUE");
    }

    public async ValueTask DeleteUniqueConstraint(string nodeType, string field) {
        await session.RunAsync($"DROP CONSTRAINT ON (x:{nodeType}) ASSERT x.{field} IS UNIQUE");
    }

    public async ValueTask CreateIndex(string nodeType, NodeFields fields) {
        var query = From("CREATE INDEX IF NOT EXISTS FOR (x:").Add(nodeType).Add(") ON (").Add(fields).Add(")");
        await session.RunAsync(query.ToString());
    }

    public async ValueTask DeleteIndex(string nodeType, NodeFields fields) {
        var query = From("DROP INDEX ON :").Add(nodeType).Add('(').Add(fields).Add(')');
        await session.RunAsync(query.ToString());
    }

    public async ValueTask CreateFullTextIndex(string indexName, string nodeType, NodeFields fields) {
        var query = From("CREATE FULLTEXT INDEX ").Add(indexName).Add(" IF NOT EXISTS FOR (x:").Add(nodeType).Add(") ON EACH [").Add(fields).Add(']');
        await session.RunAsync(query.ToString());
    }

    public async ValueTask DeleteFullTextIndex(string indexName) {
        await session.RunAsync($"DROP INDEX {indexName}");
    }

    public async ValueTask CreateNode(Neo4JNode node, params LinkTarget[] targets) {
        var query = new StringBuilder().Add("CREATE ").Add(node).Add(targets.ToSeq()).ToString();
        await session.RunAsync(query);
    }

    public async ValueTask DeleteNodes(params Neo4JNode[] nodes) {
        var sb = new StringBuilder();
        nodes.Iter(node => sb.AddDeleteExpression(node).NewLine());
        await session.RunAsync(sb.ToString());
    }

    public async ValueTask<IResultCursor> Query(string query, object? parameters = null) => 
        await (parameters is null ? session.RunAsync(query) : session.RunAsync(query, parameters));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask Execute(string query, object? parameters = null) => 
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