using System.Text;

namespace RZ.Database.Neo4J.Query;

public sealed record MergeNode(QueryNode Node, PropertySetNode? OnCreate = null, PropertySetNode? OnMatch = null) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) {
        sb.Append("MERGE ").Add(Node);
        if (OnCreate is not null)
            sb.NewLine().Append("ON CREATE SET ").Join(',', OnCreate.Value.Statements, (inner, statement) => inner.Add(statement));
        if (OnMatch is not null)
            sb.NewLine().Append("ON MATCH SET ").Join(',', OnMatch.Value.Statements, (inner, statement) => inner.Add(statement));
        return sb;
    }
}