using System.Runtime.CompilerServices;
using System.Text;
using Seq = LanguageExt.Seq;

namespace RZ.Database.Neo4J.Query;

public readonly struct MergeBuilder(Seq<ICypherNode> nodes)
{
    Seq<ICypherNode> Nodes => nodes;

    public static readonly MergeBuilder Empty = new(Seq.empty<ICypherNode>());
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MergeBuilder Call(ValueTerm functionCall, params ProjectionTerm[] yield) =>
        new(nodes.Add(new CallNode(functionCall, yield.ToSeq())));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MergeBuilder Match(MatchNode match) =>
        new(nodes.Add(match));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MergeBuilder Merge(QueryNode node, PropertySetNode? onCreate = null, PropertySetNode? onMatch = null) => 
        new(nodes.Add(new MergeNode(node, onCreate, onMatch)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReturnBuilder Return(ProjectionNode projection) =>
        new(nodes.Add(projection));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReturnBuilder Return(params ProjectionTerm[] terms) =>
        new(nodes.Add(new ProjectionNode(terms.ToSeq())));

    public MergeBuilder Create(params QueryPathNode[] path) =>
        AddNode(new CreateNode(path.ToSeq()));

    public MergeBuilder Set(params AssignmentTerm[] assignments) =>
        AddNode(new MatchSetNode(assignments.ToSeq()));

    public MergeBuilder Where(BooleanTerm boolExpression) =>
        AddNode(new WhereNode(boolExpression));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    MergeBuilder AddNode(ICypherNode node) =>
        new(nodes.Add(node));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(MergeBuilder builder) =>
        new StringBuilder(256).Add(builder.Nodes).Add(Cypher.CommandTerminationDelimiter).ToString();
}

public readonly record struct ReturnBuilder(Seq<ICypherNode> Commands)
{
    Option<OrderByNode> orderBy { get; init; }
    ValueTerm? limit { get; init; }

    public ReturnBuilder OrderBy(params ResultOrderBy[] orders) => this with{ orderBy = new OrderByNode(Seq(orders)) };
    public ReturnBuilder Limit(ValueTerm value) => this with{ limit = value };

    public static implicit operator string(ReturnBuilder builder) {
        var sb = new StringBuilder(256);
        sb.Add(builder.Commands);
        builder.orderBy.Iter(x => x.ToCommandString(sb.NewLine()));
        if (builder.limit is not null)
            sb.NewLine().Append("LIMIT ").Add(builder.limit);
        return sb.Add(Cypher.CommandTerminationDelimiter).ToString();
    }
}