using System.Text;

namespace Tirax.KMS.Migration.Core.Query;

public sealed record ProjectionNode(Seq<ProjectionTerm> Terms) : ICypherNode
{
    public static implicit operator ProjectionNode(string variable) => new(Seq1((ProjectionTerm)new ProjectionTerm.Direct(new ValueTerm.Variable(variable))));
    
    public StringBuilder ToCommandString(StringBuilder sb) => 
        sb.Append("RETURN ").Join(',', Terms, (inner, term) => term.ToCommandString(inner)).AppendLine();
}