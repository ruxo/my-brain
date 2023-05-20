namespace Tirax.KMS.Domain;

public readonly record struct ConceptId(string Value) : IComparable<ConceptId>
#region ConceptId helper methods

{
    public static implicit operator string(ConceptId cid) => cid.Value;
    public static implicit operator ConceptId(string cid) => new(cid);
    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode();
    public bool Equals(ConceptId? other) => Value.Equals(other?.Value, StringComparison.Ordinal);

    public int CompareTo(ConceptId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
}

#endregion

public readonly record struct URI(string Value)
#region URI helper methods
{
    public static implicit operator Uri(URI uri) => new(uri.Value);
    public static implicit operator URI(Uri uri) => new(uri.ToString());
    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode();
    public bool Equals(URI? other) => other?.Value == Value;
}
#endregion

public interface IDomainObject
{
    ConceptId Id { get; }
}

public sealed record Concept(ConceptId Id, string Name) : IDomainObject
{
    public LanguageExt.HashSet<ConceptId> Contains { get; init; }
    public Option<string> Note { get; init; } 
    public LanguageExt.HashSet<ConceptId> Links { get; init; }
    public LanguageExt.HashSet<ConceptId> Tags { get; init; }
}

public readonly record struct ConceptTag(ConceptId Id, string Name) : IDomainObject;

public sealed record LinkObject(ConceptId Id, Option<string> Name, URI Uri) : IDomainObject;