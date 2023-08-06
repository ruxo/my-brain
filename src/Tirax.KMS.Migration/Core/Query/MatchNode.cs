using System.Text;

namespace Tirax.KMS.Migration.Core.Query;

public sealed record MatchNode(QueryNode Head, Seq<LinkNode> Links, string? Id = null) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) {
        sb.Append("MATCH ");
        if (Id is not null) sb.Append(Id).Add('=');
        sb.Add(Head);
        Links.Iter(link => sb.Add(link));
        return sb.AppendLine();
    }
}