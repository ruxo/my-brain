using System.Text;

namespace RZ.Database.Neo4J.Query;

public sealed class CreateNode(Neo4JNode node, Seq<LinkTarget> targets) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) => 
        sb.Append("CREATE ").Add(node).Add(targets);
}

public sealed class MatchSetNode(Seq<AssignmentTerm> assignments) : ICypherNode {
    public StringBuilder ToCommandString(StringBuilder sb) => 
        sb.Append("SET ").Join(',', assignments, (inner, t) => inner.Add(t.Left).Add('=').Add(t.Right));
}