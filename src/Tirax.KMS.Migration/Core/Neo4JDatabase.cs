using System.Runtime.CompilerServices;
using System.Text;
using Neo4j.Driver;
using Tirax.KMS.Database;
using Seq = LanguageExt.Seq;

namespace Tirax.KMS.Migration.Core;

#region Neo4J native objects

public readonly record struct Neo4JLink(string LinkType, Neo4JProperties Body)
{
    public static implicit operator Neo4JLink(string linkType) => new(linkType, new(Seq.empty<Neo4JProperty>()));
}

public readonly record struct LinkTarget(Neo4JLink Link, Neo4JNode Target);

public readonly record struct Neo4JNode(string NodeType, Neo4JProperties Body);

public readonly record struct Neo4JProperty(string Name, string Value)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Neo4JProperty((string name, string value) property) => new(property.name, property.value);
}

public readonly record struct Neo4JProperties(Seq<Neo4JProperty> Properties)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Neo4JProperties(Seq<Neo4JProperty> properties) => new(properties);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Neo4JProperties(Seq<(string name, string value)> properties) => new(properties.Map(i => (Neo4JProperty)i));
}

public readonly record struct NodeFields(Seq<string> Fields)
{
    public static implicit operator NodeFields(Seq<string> fields) => new(fields);
    public static implicit operator NodeFields(string field) => new(Seq1(field));
}

static class StringBuilderExtension
{
    const char PropertyDelimiter = '.';
    const char NodeTypeDelimiter = ':';
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Add(this StringBuilder sb, char c) => sb.Append(c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Add(this StringBuilder sb, string s) => sb.Append(s);

    public static StringBuilder Join<T>(this StringBuilder sb, char delimiter, in Seq<T> seq, Func<StringBuilder,T,StringBuilder> mapper) =>
        seq.Tail.Fold(mapper(sb,seq.Head), (inner, i) => mapper(inner.Add(delimiter),i));

    public static StringBuilder Add(this StringBuilder sb, NodeFields nodeFields, string nodeName = "x") => 
        sb.Join(',', nodeFields.Fields, (inner, field) => inner.Add(nodeName).Add(PropertyDelimiter).Add(field));

    public static StringBuilder Add(this StringBuilder sb, Neo4JProperty property) => 
        sb.Add(property.Name).Add(PropertyDelimiter).Add('\'').Add(property.Value.Replace("'", "\\'")).Add('\'');

    public static StringBuilder Add(this StringBuilder sb, Neo4JProperties properties) =>
        properties.Properties.HeadOrNone()
                  .Map(head => {
                       sb.Add('{').Add(head);
                       properties.Properties.Tail.Iter(item => sb.Add(',').Add(item));
                       return sb.Add('}');
                   })
                  .IfNone(sb);

    public static StringBuilder Add(this StringBuilder sb, Neo4JNode node, string name = "") => 
        sb.Add('(').Add(name).Add(NodeTypeDelimiter).Add(node.NodeType).Add(' ').Add(node.Body).Add(')');

    public static StringBuilder Add(this StringBuilder sb, Neo4JLink link) => 
        sb.Add("[").Add(link.LinkType).Add(' ').Add(link.Body).Add(']');

    public static StringBuilder Add(this StringBuilder sb, Seq<LinkTarget> targets) => 
        targets.Fold(sb, (inner, target) => inner.Add('-').Add(target.Link).Add("->").Add(target.Target));

    public static StringBuilder AddDeleteExpression(this StringBuilder sb, Neo4JNode node) =>
        sb.Add("MATCH ").Add(node, "x").Add(" DETACH DELETE x;");
}

#endregion

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
        nodes.Iter(node => sb.AddDeleteExpression(node).AppendLine());
        await session.RunAsync(sb.ToString());
    }

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