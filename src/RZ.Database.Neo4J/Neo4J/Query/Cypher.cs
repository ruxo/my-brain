using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace RZ.Database.Neo4J.Query;

public interface ICypherNode
{
    StringBuilder ToCommandString(StringBuilder sb);
}

public static class Cypher
{
    public const char CommandTerminationDelimiter = ';';
    
    /// <summary>
    /// Create or update the node
    /// </summary>
    /// <param name="node"></param>
    /// <param name="onCreate"></param>
    /// <param name="onMatch"></param>
    /// <returns></returns>
    public static MergeBuilder Merge(Neo4JNode node, PropertySetNode? onCreate = null, PropertySetNode? onMatch = null) {
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MergeBuilder Match(MatchNode match) => new(Seq1<ICypherNode>(match));

    public readonly struct MergeBuilder(Seq<ICypherNode> nodes)
    {
        Seq<ICypherNode> Nodes => nodes;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReturnBuilder Returns(ProjectionNode projection) =>
            new(nodes.Add(projection));

        public MergeBuilder Create(Neo4JNode node, params LinkTarget[] targets) =>
            AddNode(new CreateNode(node, Seq(targets)));

        public MergeBuilder Set(params AssignmentTerm[] assignments) =>
            AddNode(new MatchSetNode(assignments.ToSeq()));

        public MergeBuilder Where(BooleanTerm boolExpression) =>
            AddNode(new WhereNode(boolExpression));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        MergeBuilder AddNode(ICypherNode node) =>
            new(nodes.Add(node));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator string(MergeBuilder builder) => 
            new StringBuilder(256).Add(builder.Nodes).Add(CommandTerminationDelimiter).ToString();
    }

    public readonly record struct ReturnBuilder(Seq<ICypherNode> Commands)
    {
        Option<OrderByNode> orderBy { get; init; }
        Option<int> limit { get; init; }

        public ReturnBuilder OrderBy(params ResultOrderBy[] orders) => this with { orderBy = new OrderByNode(Seq(orders)) };
        public ReturnBuilder Limit(int n) => this with{ limit = n };

        public static implicit operator string(ReturnBuilder builder) {
            var sb = new StringBuilder(256);
            sb.Add(builder.Commands);
            builder.orderBy.Iter(x => x.ToCommandString(sb.NewLine()));
            builder.limit.Iter(i => sb.NewLine().Append("LIMIT ").Append(i));
            return sb.Add(CommandTerminationDelimiter).ToString();
        }
    }
}

static class CypherStringBuilderExtension
{
    const char PropertyDelimiter = '.';
    const char NodeTypeDelimiter = ':';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder NewLine(this StringBuilder sb) => sb.Append('\n');
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Add(this StringBuilder sb, char c) => sb.Append(c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Add(this StringBuilder sb, string s) => string.IsNullOrEmpty(s)? sb : sb.Append(s);

    public delegate StringBuilder Transformer<in T>(StringBuilder sb, T value);

    public static StringBuilder Join<T>(this StringBuilder sb, Seq<T> seq, Transformer<T> mapper, Transformer<T> joiner) =>
        seq.HeadOrNone().Map(head => seq.Tail.Fold(mapper(sb,head), (inner, i) => mapper(joiner(inner,i), i))).IfNone(sb);

    public static StringBuilder Join<T>(this StringBuilder sb, char delimiter, Seq<T> seq, Transformer<T> mapper) =>
        seq.HeadOrNone().Map(head => seq.Tail.Fold(mapper(sb,head), (inner, i) => mapper(inner.Add(delimiter),i))).IfNone(sb);

    public static StringBuilder Add(this StringBuilder sb, NodeFields nodeFields, string nodeName = "x") => 
        sb.Join(',', nodeFields.Fields, (inner, field) => inner.Add(nodeName).Add(PropertyDelimiter).Add(field));

    public static StringBuilder Add(this StringBuilder sb, Neo4JProperty property, char delimiter = PropertyDelimiter) =>
        sb.Add(property.Name).Add(delimiter).AddValue(property.Value);

    public static StringBuilder Add(this StringBuilder sb, Neo4JProperties properties) =>
        properties.Properties.HeadOrNone()
                  .Map(head => {
                       sb.Add('{').Add(head, NodeTypeDelimiter);
                       properties.Properties.Tail.Iter(item => sb.Add(',').Add(item, NodeTypeDelimiter));
                       return sb.Add('}');
                   })
                  .IfNone(sb);

    public static StringBuilder Add(this StringBuilder sb, Neo4JNode node) {
        sb.Add('(').Add(node.Id ?? string.Empty);
        if (node.NodeType is not null) sb.Add(NodeTypeDelimiter).Add(node.NodeType);
        return sb.Add(node.Body).Add(')');
    }

    public static StringBuilder Add(this StringBuilder sb, Neo4JLink link) => 
        sb.Add('[').Add(NodeTypeDelimiter).Add(link.LinkType).Add(link.Body).Add(']');

    public static StringBuilder Add(this StringBuilder sb, Seq<LinkTarget> targets) => 
        targets.Fold(sb, (inner, target) => inner.Add('-').Add(target.Link).Add("->").Add(target.Target));

    public static StringBuilder AddDeleteExpression(this StringBuilder sb, Neo4JNode node) {
        var validNode = node.Id is null ? node with{ Id = "x" } : node;
        return sb.Add("MATCH ").Add(validNode).Add(" DETACH DELETE ").Add(node.Id!.Value).Add(Cypher.CommandTerminationDelimiter);
    }

    public static StringBuilder AddValue(this StringBuilder sb, object? v) =>
        v switch{
            null     => sb.Append("null"),
            string s => sb.AddQuotedString(s),
            bool b   => sb.Append(b),
            int n    => sb.Append(n),
            double r => sb.Append(r),

            _ => throw new ArgumentOutOfRangeException($"Value of type {v.GetType()} is not supported")
        };

    public static StringBuilder AddQuotedString(this StringBuilder sb, string s) {
        sb.Add('\'');
        foreach(var c in s)
            if (c == '\'')
                sb.Append("\\'");
            else
                sb.Add(c);
        return sb.Add('\'');
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Add(this StringBuilder sb, Seq<ICypherNode> nodes) => 
        sb.Join(nodes, (inner, n) => n.ToCommandString(inner), (inner, _) => inner.NewLine());

    public static StringBuilder Add(this StringBuilder sb, QueryNode node, string enclosing = "()") {
        Debug.Assert(enclosing.Length == 2);
        sb.Add(enclosing[0]);
        node.Id.Iter(id => sb.Append(id));
        node.LabelExpression.Iter(term => sb.Add(':').Add(term));
        sb.Add(node.Body);
        node.WhereExpression.Iter(term => sb.Append(" WHERE ").Add(term));
        sb.Add(enclosing[1]);
        return sb;
    }

    public static StringBuilder Add(this StringBuilder sb, LinkNode node) {
        sb.Append(node.Link.Direction == LinkDirection.ToLeft ? "<-" : "-");
        if (node.Link.Link is not null) sb.Add(node.Link.Link, "[]");
        sb.Append(node.Link.Direction == LinkDirection.ToRight ? "->" : "-");
        if (node.Link.Qualifier is not null) sb.Add(node.Link.Qualifier.Value);
        sb.Add(node.Target);
        return sb;
    }

    public static StringBuilder Add(this StringBuilder sb, QueryPathNode path) {
        sb.Add(path.Head);
        if (path.Links.Any()) path.Links.Iter(p => sb.Add(p));
        return sb;
    }

    public static StringBuilder Add(this StringBuilder sb, LabelTerm term) =>
        term switch{
            LabelTerm.Identifier i => sb.Append(i.Name),
            LabelTerm.Wildcard => sb.Add('%'),
            LabelTerm.Group g => sb.Add('(').Add(g.Expr).Add(')'),
            LabelTerm.And op => sb.Add(op.Left).Add('&').Add(op.Right),
            LabelTerm.Or op => sb.Add(op.Left).Add('|').Add(op.Right),
            LabelTerm.Not op => sb.Add('!').Add(op.Expr),
            
            _ => throw new NotImplementedException($"Term {term} is not yet implemented!")
        };

    public static StringBuilder Add(this StringBuilder sb, BooleanTerm term) =>
        term switch{
            BooleanTerm.StartsWith op => sb.Add(op.Prop).Append(" STARTS WITH ").Add(op.Value),
            BooleanTerm.Eq op => sb.Add(op.Left).Add('=').Add(op.Right),
            BooleanTerm.Lt op => sb.Add(op.Left).Add('<').Add(op.Right),
            BooleanTerm.Gt op => sb.Add(op.Left).Add('>').Add(op.Right),
            BooleanTerm.Lte op => sb.Add(op.Left).Append("<=").Add(op.Right),
            BooleanTerm.Gte op => sb.Add(op.Left).Append(">=").Add(op.Right),
            BooleanTerm.Neq op => sb.Add(op.Left).Append("<>").Add(op.Right),
            BooleanTerm.In op => sb.Add(op.Left).Append(" IN ").Add(op.Right),
            
            BooleanTerm.And op => sb.Add(op.Left).Append(" AND ").Add(op.Right),
            BooleanTerm.Or op => sb.Add(op.Left).Append(" OR ").Add(op.Right),
            BooleanTerm.Not op => sb.Append("NOT").Add(op.Expr),
            
            _ => throw new NotImplementedException($"Term {term} is not yet implemented!")
        };

    public static StringBuilder Add(this StringBuilder sb, ValueTerm term) =>
        term switch{
            ValueTerm.Constant c => sb.AddValue(c.Value),
            ValueTerm.Variable v => sb.Append(v.Name),
            ValueTerm.Property p => sb.Append(p.NodeId).Add('.').Append(p.Field),
            ValueTerm.FunctionCall f => sb.Append(f.Name).Add('(').Join(',', f.Parameters, (inner,v) => inner.Add(v)).Add(')'),
            
            _ => throw new NotImplementedException($"Term {term} is not yet implemented!")
        };

    public static StringBuilder Add(this StringBuilder sb, Qualifier qualifier) {
        if (qualifier == Qualifier.Any) return sb.Add('*');
        if (qualifier == Qualifier.AtLeastOnce) return sb.Add('+');
        if (qualifier is{ LowerBound: 0, UpperBound: null }) return sb;
        sb.Add('{');
        if (qualifier.LowerBound > 0) sb.Append(qualifier.LowerBound);
        if (qualifier.LowerBound != qualifier.UpperBound) {
            sb.Add(',');
            if (qualifier.UpperBound is not null) sb.Append(qualifier.UpperBound.Value);
        }
        return sb.Add('}');
    }
}