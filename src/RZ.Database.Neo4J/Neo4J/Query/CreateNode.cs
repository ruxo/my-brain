using System.Text;

namespace RZ.Database.Neo4J.Query;

public sealed class CreateNode(Seq<QueryPathNode> paths) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) => 
        sb.Append("CREATE ")
          .Join(',', paths, (inner, p) => inner.Add(p));
}

public sealed class MatchSetNode(Seq<AssignmentTerm> assignments) : ICypherNode {
    public StringBuilder ToCommandString(StringBuilder sb) => 
        sb.Append("SET ").Join(',', assignments, (inner, t) => inner.Add(t.Left).Add('=').Add(t.Right));
}