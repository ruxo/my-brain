using System.Text;
using Seq = LanguageExt.Seq;

namespace RZ.Database.Neo4J.Query;

public sealed record MatchNode(QueryNode Head, Seq<LinkNode> Links, string? Id = null) : ICypherNode
{
    public static implicit operator MatchNode(string label) =>
        new(new(){ LabelExpression = (LabelTerm)label }, Seq.empty<LinkNode>());
    
    public static implicit operator MatchNode((string Id, string Label) x) =>
        new(new(){ Id = x.Id, LabelExpression = (LabelTerm)x.Label }, Seq.empty<LinkNode>());
    
    public static implicit operator MatchNode((string Id, string Label, Neo4JProperties Body) x) =>
        new(new(){ Id = x.Id, LabelExpression = (LabelTerm)x.Label, Body = x.Body }, Seq.empty<LinkNode>());
    
    public StringBuilder ToCommandString(StringBuilder sb) {
        sb.Append("MATCH ");
        if (Id is not null) sb.Append(Id).Add('=');
        sb.Add(Head);
        Links.Iter(link => sb.Add(link));
        return sb;
    }
}