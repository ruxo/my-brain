using System.Text;

namespace RZ.Database.Neo4J.Query;

public sealed class WhereNode(BooleanTerm expression) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) {
        return sb.Append("WHERE ").Add(expression);
    }
}