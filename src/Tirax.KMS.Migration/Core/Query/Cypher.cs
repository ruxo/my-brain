using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Tirax.KMS.Migration.Core.Query;

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
        public ReturnBuilder Returns(ProjectionNode projection) => new(nodes.Add(projection));

        public MergeBuilder Create(Neo4JNode node, params LinkTarget[] targets) => new(nodes.Add(new CreateNode(node, Seq(targets))));

        public static implicit operator string(MergeBuilder builder) {
            var sb = new StringBuilder(256);
            builder.Nodes.Iter(n => n.ToCommandString(sb));
            return sb.ToString();
        }
    }

    public readonly record struct ReturnBuilder(Seq<ICypherNode> Commands)
    {
        Option<OrderByNode> orderBy { get; init; }
        Option<int> limit { get; init; }

        public ReturnBuilder OrderBy(params ResultOrderBy[] orders) => this with { orderBy = new OrderByNode(Seq(orders)) };
        public ReturnBuilder Limit(int n) => this with{ limit = n };

        public static implicit operator string(ReturnBuilder builder) {
            var sb = new StringBuilder(256);
            sb.Join(builder.Commands, (inner, n) => n.ToCommandString(inner), (inner, _) => inner.NewLine());
            builder.orderBy.Iter(x => x.ToCommandString(sb.NewLine()));
            builder.limit.Iter(i => sb.NewLine().Append("LIMIT ").Append(i));
            return sb.Add(CommandTerminationDelimiter).ToString();
        }
    }
}

static class CypherStringBuilderExtension
{
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
            
            BooleanTerm.And op => sb.Add(op.Left).Append(" AND ").Add(op.Right),
            BooleanTerm.Or op => sb.Add(op.Left).Append(" OR ").Add(op.Right),
            BooleanTerm.Not op => sb.Append("NOT").Add(op.Expr),
            
            _ => throw new NotImplementedException($"Term {term} is not yet implemented!")
        };

    public static StringBuilder Add(this StringBuilder sb, ValueTerm term) =>
        term switch{
            ValueTerm.Constant c => sb.AddQuotedString(c.Value),
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