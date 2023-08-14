using System.Runtime.CompilerServices;
using Seq = LanguageExt.Seq;

namespace Tirax.KMS.Migration.Core;

public enum LinkDirection
{
    ToRight, ToLeft, BiDirection
}

public readonly record struct Neo4JLink(string LinkType, Neo4JProperties Body)
{
    public static implicit operator Neo4JLink(string linkType) => new(linkType, new(Seq.empty<Neo4JProperty>()));
}

public readonly record struct LinkTarget(Neo4JLink Link, Neo4JNode Target);

public readonly record struct NodeId(string Value)
{
    public static implicit operator NodeId(string id) => new(id);
    public static implicit operator string(NodeId id) => id.Value;
}

public readonly record struct Neo4JNode(string? NodeType = null, Neo4JProperties Body = default, NodeId? Id = null)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Neo4JNode Of(string? nodeType = null, Neo4JProperties body = default, NodeId? id = null) =>
        new(nodeType, body, id);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Neo4JNode Of(string? nodeType = null, params Neo4JProperty[] properties) =>
        new(nodeType, new(Seq(properties)));
}

public readonly record struct Neo4JProperty(string Name, string Value)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Neo4JProperty(in (string name, string value) property) => new(property.name, property.value);
}

public readonly record struct Neo4JProperties(Seq<Neo4JProperty> Properties)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Neo4JProperties(in Seq<Neo4JProperty> properties) => new(properties);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Neo4JProperties(in Seq<(string name, string value)> properties) => new(properties.Map(i => (Neo4JProperty)i));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Neo4JProperties(in (string name, string value) property) => new(Seq1((Neo4JProperty)property));
}

public readonly record struct NodeFields(Seq<string> Fields)
{
    public static implicit operator NodeFields(Seq<string> fields) => new(fields);
    public static implicit operator NodeFields(string field) => new(Seq1(field));
}