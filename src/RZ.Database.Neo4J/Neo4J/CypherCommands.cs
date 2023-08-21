using System.Runtime.CompilerServices;
using System.Text;
using Neo4j.Driver;
using RZ.Database.Neo4J.Query;

namespace RZ.Database.Neo4J;

public static class CypherCommands
{
    public static ValueTask<IResultSummary> CreateUniqueConstraint(this IQueryRunner runner, string nodeType, string field) => 
        runner.Write($"CREATE CONSTRAINT IF NOT EXISTS FOR (x:{nodeType}) REQUIRE (x.{field}) IS UNIQUE");
    
    public static ValueTask<IResultSummary> DeleteUniqueConstraint(this IQueryRunner runner, string nodeType, string field) => 
        runner.Write($"DROP CONSTRAINT ON (x:{nodeType}) ASSERT x.{field} IS UNIQUE");

    public static ValueTask<IResultSummary> CreateIndex(this IQueryRunner runner, string nodeType, NodeFields fields) {
        var query = From("CREATE INDEX IF NOT EXISTS FOR (x:").Add(nodeType).Add(") ON (").Add(fields).Add(")");
        return runner.Write(query.ToString());
    }

    public static ValueTask<IResultSummary> DeleteIndex(this IQueryRunner runner, string nodeType, NodeFields fields) {
        var query = From("DROP INDEX ON :").Add(nodeType).Add('(').Add(fields).Add(')');
        return runner.Write(query.ToString());
    }

    public static ValueTask<IResultSummary> CreateFullTextIndex(this IQueryRunner runner, string indexName, string nodeType, NodeFields fields) {
        var query = From("CREATE FULLTEXT INDEX ").Add(indexName).Add(" IF NOT EXISTS FOR (x:").Add(nodeType).Add(") ON EACH [").Add(fields).Add(']');
        return runner.Write(query.ToString());
    }

    public static ValueTask<IResultSummary> DeleteFullTextIndex(this IQueryRunner runner, string indexName) => 
        runner.Write($"DROP INDEX {indexName}");

    public static async ValueTask<IResultSummary> CreateNode(this IQueryRunner runner, Neo4JNode node, params LinkTarget[] targets) {
        var n = new CreateNode(node, targets.ToSeq());
        var query = n.ToCommandString(new (128)).ToString();
        return await runner.Write(query);
    }

    public static async ValueTask<IResultSummary> DeleteNodes(this IQueryRunner runner, params Neo4JNode[] nodes) {
        var sb = new StringBuilder();
        nodes.Iter(node => sb.AddDeleteExpression(node).NewLine());
        return await runner.Write(sb.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static StringBuilder From(string initial) => new StringBuilder(64).Add(initial);
}