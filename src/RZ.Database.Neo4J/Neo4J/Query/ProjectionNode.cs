using System.Runtime.CompilerServices;
using System.Text;

namespace RZ.Database.Neo4J.Query;

public static class Projection
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProjectionTerm Direct(ValueTerm value) => new ProjectionTerm.Direct(value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProjectionTerm Alias(string name, ProjectionTerm projection) => new ProjectionTerm.Alias(name, projection);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProjectionTerm Select(QueryPathNode path, ValueTerm projection) => new ProjectionTerm.Select(path, projection);
}

public sealed record ProjectionNode(Seq<ProjectionTerm> Terms) : ICypherNode
{
    public static implicit operator ProjectionNode(string variable) => new(Seq1((ProjectionTerm)new ProjectionTerm.Direct(new ValueTerm.Variable(variable))));
    
    public StringBuilder ToCommandString(StringBuilder sb) => 
        sb.Append("RETURN ").Join(',', Terms, (inner, term) => term.ToCommandString(inner));
}