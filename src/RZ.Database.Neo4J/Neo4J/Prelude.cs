using System.Runtime.CompilerServices;
using RZ.Database.Neo4J.Query;

namespace RZ.Database.Neo4J;

public static class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Neo4JProperties Props(params Neo4JProperty[] props) =>
        new(props.ToSeq());
    
    #region Neo4JPath helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryNode N(string? id = null, string? type = null, Neo4JProperties body = default) =>
        QueryNode.From(type, body, id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LinkNode L(QueryLink link, QueryNode target) =>
        new(link, target);

    public static QueryPathNode P(QueryNode head, LinkNode link) => new(head, Seq1(link));
    public static QueryPathNode P(QueryNode head, Seq<LinkNode> links) => new(head, links);

    #endregion
    
    #region Projection
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProjectionTerm Direct(ValueTerm value) => new ProjectionTerm.Direct(value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProjectionTerm Alias(string name, ProjectionTerm projection) => new ProjectionTerm.Alias(name, projection);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProjectionTerm Select(QueryPathNode path, ValueTerm projection) => new ProjectionTerm.Select(path, projection);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProjectionTerm SelectNone() => new ProjectionTerm.SelectNone();
    
    #endregion
    
    #region ValueTerm

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTerm Var(string variable) => new ValueTerm.Variable(variable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTerm Param(string variable) => new ValueTerm.Parameter(variable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTerm Call(string name, params ValueTerm[] parameters ) => new ValueTerm.FunctionCall(name, parameters.ToSeq());
    
    #endregion
    
    #region BooleanTerm

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BooleanTerm Contains(ValueTerm item, ValueTerm collection) => new BooleanTerm.In(item, collection);

    #endregion
}