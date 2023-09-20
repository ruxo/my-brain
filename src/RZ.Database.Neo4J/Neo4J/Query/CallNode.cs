using System.Text;

namespace RZ.Database.Neo4J.Query;

public sealed record CallNode(ValueTerm FunctionCall, Seq<ProjectionTerm> Yield) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) {
        sb.Add(FunctionCall);
        if (Yield.Any())
            sb.Append(" YIELD ").Join(',', Yield, (inner, yield) => yield.ToCommandString(inner));
        return sb;
    }
}