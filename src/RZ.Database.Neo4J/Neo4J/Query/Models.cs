using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Seq = LanguageExt.Seq;
using static RZ.Database.Neo4J.Prelude;

namespace RZ.Database.Neo4J.Query;

// reference: https://neo4j.com/docs/cypher-manual/current/patterns/reference/#label-expressions
public record LabelTerm
{
    public sealed record Identifier(string Name) : LabelTerm;
    public sealed record And(LabelTerm Left, LabelTerm Right) : LabelTerm;
    public sealed record Or(LabelTerm Left, LabelTerm Right) : LabelTerm;
    public sealed record Not(LabelTerm Expr) : LabelTerm;
    public sealed record Wildcard : LabelTerm;
    public sealed record Group(LabelTerm Expr) : LabelTerm;

    public static implicit operator LabelTerm(string identifier) =>
        new Identifier(identifier);
    
    LabelTerm(){}
}

#pragma warning disable CS0660, CS0661
public abstract class ValueTerm
#pragma warning restore CS0660, CS0661
{
    public sealed class Constant(object? value) : ValueTerm
    {
        public object? Value => value;
    }
    public sealed class Variable(string name) : ValueTerm
    {
        public string Name => name;
    }

    public sealed class Property(string nodeId, string field) : ValueTerm
    {
        public string NodeId => nodeId;
        public string Field => field;
    }

    public sealed class Parameter(string name) : ValueTerm
    {
        public string Name => name;
    }

    public sealed class FunctionCall(string name, Seq<ValueTerm> parameters) : ValueTerm
    {
        public static FunctionCall Of(string name, params ValueTerm[] parameters) => new(name, Seq(parameters));
        
        public string Name => name;
        public Seq<ValueTerm> Parameters => parameters;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueTerm(string? value) => new Constant(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueTerm(int value) => new Constant(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueTerm(bool value) => new Constant(value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueTerm(in (string nodeId, string field) x) => new Property(x.nodeId, x.field);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BooleanTerm operator ==(ValueTerm lhs, ValueTerm rhs) =>
        new BooleanTerm.Eq(lhs, rhs);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BooleanTerm operator !=(ValueTerm lhs, ValueTerm rhs) =>
        new BooleanTerm.Neq(lhs, rhs);
    
    ValueTerm() {}
}

public record BooleanTerm
{
    public sealed record StartsWith(ValueTerm.Property Prop, ValueTerm.Constant Value) : BooleanTerm;
    public sealed record Eq(ValueTerm Left, ValueTerm Right) : BooleanTerm;
    public sealed record Lt(ValueTerm Left, ValueTerm Right) : BooleanTerm;
    public sealed record Gt(ValueTerm Left, ValueTerm Right) : BooleanTerm;
    public sealed record Lte(ValueTerm Left, ValueTerm Right) : BooleanTerm;
    public sealed record Gte(ValueTerm Left, ValueTerm Right) : BooleanTerm;
    public sealed record Neq(ValueTerm Left, ValueTerm Right) : BooleanTerm;

    public sealed record In(ValueTerm Left, ValueTerm Right) : BooleanTerm;

    public sealed record And(BooleanTerm Left, BooleanTerm Right) : BooleanTerm;
    public sealed record Or(BooleanTerm Left, BooleanTerm Right) : BooleanTerm;
    public sealed record Not(BooleanTerm Expr) : BooleanTerm;
    
    BooleanTerm(){}
}

public readonly record struct AssignmentTerm(ValueTerm Left, ValueTerm Right)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssignmentTerm(in (ValueTerm Left, ValueTerm Right) x) => new(x.Left, x.Right);
}

public record ProjectionTerm
{
    public sealed record Direct(ValueTerm Value) : ProjectionTerm;
    public sealed record Alias(string Name, ProjectionTerm Term) : ProjectionTerm;
    public sealed record Select(QueryPathNode Pattern, ValueTerm Projection) : ProjectionTerm;
    public sealed record SelectNone : ProjectionTerm;

    public StringBuilder ToCommandString(StringBuilder sb) =>
        this switch{
            Direct d => sb.Add(d.Value),
            Alias a  => a.Term.ToCommandString(sb).Append(" AS ").Append(a.Name),
            Select s => sb.Append('[').Add(s.Pattern).Append('|').Add(s.Projection).Append(']'),
            SelectNone _ => sb.Append("[]"),

            _ => throw new NotImplementedException($"Command {this} is not yet implemented!")
        };

    ProjectionTerm() {}
}

/// <summary>
/// Neo4J node for query
/// </summary>
public sealed class QueryNode
{
    public Option<string> Id { get; init; }
    public Option<LabelTerm> LabelExpression { get; init; }
    public Neo4JProperties Body { get; init; }
    public Option<BooleanTerm> WhereExpression { get; init; }

    public static readonly QueryNode Any = new();

    public static MatchNodeBuilder AnyWithId(string id) =>
        new(new() { Id = id }, Seq.empty<LinkNode>());

    public static implicit operator QueryNode(string label) =>
        new(){ LabelExpression = (LabelTerm)label };
    
    public static implicit operator QueryNode((string Id, string Label) x) =>
        new(){ Id = x.Id, LabelExpression = (LabelTerm)x.Label };
    
    public static implicit operator QueryNode((string Id, string Label, Neo4JProperties Body) x) =>
        new(){ Id = x.Id, LabelExpression = (LabelTerm)x.Label, Body = x.Body };

    public static MatchNodeBuilder Of(LabelTerm labelExpr, params Neo4JProperty[] properties) => 
        new(new(){ LabelExpression = labelExpr, Body = new(Seq(properties)) }, Seq.empty<LinkNode>());

    public static MatchNodeBuilder Of(string id, LabelTerm labelExpr, params Neo4JProperty[] properties) => 
        new(new(){ Id = Optional(id), LabelExpression = labelExpr, Body = new(Seq(properties)) }, Seq.empty<LinkNode>());

    public static QueryNode From(string? type = null, Neo4JProperties body = default, string? id = null) =>
        new(){ Id = Optional(id), LabelExpression = type is null ? None : Some((LabelTerm)type), Body = body };
}

public readonly record struct Qualifier(int LowerBound = 1, int? UpperBound = null)
{
    /// <summary>
    /// Represent <c>*</c> qualifier, which is equivalent to <c>{,}</c> and <c>{0,}</c>.
    /// </summary>
    public static readonly Qualifier Any = new(0);
    
    /// <summary>
    /// Represent <c>+</c> qualifier, which is equivalent to <c>{1,}</c>.
    /// </summary>
    public static readonly Qualifier AtLeastOnce = new();
    
}

public readonly record struct QueryLink(QueryNode? Link = null, LinkDirection Direction = LinkDirection.ToRight, Qualifier? Qualifier = null)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator QueryLink(string name) =>
        new(name);
}

public readonly record struct LinkNode(QueryLink Link, QueryNode Target);

public readonly record struct QueryPathNode(QueryNode Head, Seq<LinkNode> Links)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator QueryPathNode(QueryNode head) =>
        new(head, Seq.empty<LinkNode>());
}

public readonly struct MatchNodeBuilder(QueryNode head, Seq<LinkNode> links)
{
    public MatchNodeBuilder LinkTo(string linkType, QueryNode target, Qualifier? qualifier = null) =>
        new(head, links.Add(new(new(linkType, Qualifier: qualifier), target)));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MatchNode ToPath(string id) =>
        new(head, links, id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MatchNode ToMatchNode() => new(head, links);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryPathNode ToQueryPathNode() => new(head, links);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryNode ToQueryNode() {
        Debug.Assert(links.IsEmpty);
        return head;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator MatchNode(MatchNodeBuilder builder) => builder.ToMatchNode();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator QueryPathNode(MatchNodeBuilder matchNode) =>
        matchNode.ToQueryPathNode();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator QueryNode(MatchNodeBuilder matchNode) => 
        matchNode.ToQueryNode();
}

public readonly record struct PropertySetStatement(ValueTerm.Property Prop, ValueTerm Value);
public readonly record struct PropertySetNode(Seq<PropertySetStatement> Statements);

public record ResultOrderBy
{
    public sealed record Ascending(ValueTerm Value) : ResultOrderBy;
    public sealed record Descending(ValueTerm Value) : ResultOrderBy;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ResultOrderBy(string variable) => new Ascending(Var(variable));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ResultOrderBy Desc(ValueTerm term) => new Descending(term);

    public StringBuilder ToCommandString(StringBuilder sb) =>
        this switch{
            Ascending order  => sb.Add(order.Value),
            Descending order => sb.Add(order.Value).Append(" DESC"),

            _ => throw new NotImplementedException($"OrderBy {this} is not yet implemented!")
        };

    ResultOrderBy() {}
}