using System.Runtime.CompilerServices;
using System.Text;
using Seq = LanguageExt.Seq;

namespace Tirax.KMS.Migration.Core.Query;

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

public record ValueTerm
{
    public sealed record Constant(string Value) : ValueTerm;
    public sealed record Variable(string Name) : ValueTerm;
    public sealed record Property(string NodeId, string Field) : ValueTerm;

    public sealed record FunctionCall(string Name, Seq<ValueTerm> Parameters) : ValueTerm
    {
        public static FunctionCall Of(string name, params ValueTerm[] parameters) => new(name, Seq(parameters));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueTerm(string variable) => new Variable(variable);
    
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

    public sealed record And(BooleanTerm Left, BooleanTerm Right) : BooleanTerm;
    public sealed record Or(BooleanTerm Left, BooleanTerm Right) : BooleanTerm;
    public sealed record Not(BooleanTerm Expr) : BooleanTerm;
    
    BooleanTerm(){}
}

public record ProjectionTerm
{
    public sealed record Direct(ValueTerm Value) : ProjectionTerm;
    public sealed record Alias(string Name, ProjectionTerm Term) : ProjectionTerm;

    public StringBuilder ToCommandString(StringBuilder sb) =>
        this switch{
            Direct d => sb.Add(d.Value),
            Alias a  => a.Term.ToCommandString(sb).Append(" AS ").Append(a.Name),

            _ => throw new NotImplementedException($"Command {this} is not yet implemented!")
        };

    ProjectionTerm() {}
}

public sealed class QueryNode
{
    public Option<string> Id { get; init; }
    public Option<LabelTerm> LabelExpression { get; init; }
    public Neo4JProperties Body { get; init; } = default;
    public Option<BooleanTerm> WhereExpression { get; init; }

    public static readonly QueryNode Any = new();

    public static implicit operator QueryNode(string label) =>
        new(){ LabelExpression = (LabelTerm)label };

    public static MatchNodeBuilder Of(LabelTerm labelExpr, params Neo4JProperty[] properties) => 
        new(new(){ LabelExpression = labelExpr, Body = new(Seq(properties)) }, Seq.empty<LinkNode>());

    public static MatchNodeBuilder Of(LabelTerm labelExpr, string? id = null, params Neo4JProperty[] properties) => 
        new(new(){ Id = Optional(id), LabelExpression = labelExpr, Body = new(Seq(properties)) }, Seq.empty<LinkNode>());
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
public readonly record struct QueryLink(QueryNode? Link = null, LinkDirection Direction = LinkDirection.ToRight, Qualifier? Qualifier = null);
public readonly record struct LinkNode(QueryLink Link, QueryNode Target);

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
    public static implicit operator MatchNode(MatchNodeBuilder builder) => builder.ToMatchNode();
}

public readonly record struct PropertySetStatement(ValueTerm.Property Prop, ValueTerm Value);
public readonly record struct PropertySetNode(Seq<PropertySetStatement> Statements);

public record ResultOrderBy
{
    public sealed record Ascending(ValueTerm Value) : ResultOrderBy;
    public sealed record Descending(ValueTerm Value) : ResultOrderBy;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ResultOrderBy(string variable) => new Ascending(variable);
    
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