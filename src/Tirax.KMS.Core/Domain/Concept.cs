namespace Tirax.KMS.Domain;

public readonly record struct URI(string Value)
{
    public static implicit operator Uri(URI uri) => new(uri.Value);
    public static implicit operator URI(Uri uri) => new(uri.ToString());
    public override string ToString() => Value;
}

public abstract record ConceptLink
{
    public sealed record PureLink(URI Value) : ConceptLink;
    public sealed record Link(string Value) : ConceptLink;

    ConceptLink(){}
    
    public bool IsPure => this is PureLink;
    
    public string GetLinkId() => this is Link link ? link.Value : throw new InvalidOperationException();
    public URI GetPureUri() => this is PureLink link ? link.Value : throw new InvalidOperationException();
}

public sealed record Concept
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public LanguageExt.HashSet<string> Contains { get; init; }
    public Option<string> Note { get; init; } 
    public LanguageExt.HashSet<ConceptLink> Links { get; init; }
    public LanguageExt.HashSet<string> Tags { get; init; }
}