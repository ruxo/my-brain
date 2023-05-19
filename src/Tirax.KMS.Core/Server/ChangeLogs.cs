using Tirax.KMS.Domain;
// ReSharper disable InconsistentNaming

namespace Tirax.KMS.Server;

public abstract record ModelOperationType<T>
{
    public sealed record Add(T Value) : ModelOperationType<T>;
    public sealed record Update(T Old, T New) : ModelOperationType<T>;
    public sealed record Delete(T Value) : ModelOperationType<T>;
}

public static class ModelOperationType
{
    public static ModelOperationType<T> Add<T>(T value) => new ModelOperationType<T>.Add(value);
    public static ModelOperationType<T> Update<T>(T old, T @new) => new ModelOperationType<T>.Update(old, @new);
    public static ModelOperationType<T> Delete<T>(T value) => new ModelOperationType<T>.Delete(value);
}

public static class ModelOperationTypeExtensions
{
    public static LanguageExt.HashSet<T> Apply<T>(this ModelOperationType<T> operation, LanguageExt.HashSet<T> s) =>
        operation switch{
            ModelOperationType<T>.Add x    => s.Add(x.Value),
            ModelOperationType<T>.Update x => s.Contains(x.New) ? s : s.Add(x.New),
            ModelOperationType<T>.Delete x => s.Remove(x.Value),
            _                              => throw new NotSupportedException()
        };

    public static Map<K, T> Apply<K, T>(this ModelOperationType<T> operation, Map<K, T> m, Func<T, K> keyIdentifier) =>
        operation switch{
            ModelOperationType<T>.Add x    => m.Add(keyIdentifier(x.Value), x.Value),
            ModelOperationType<T>.Update x => m.AddOrUpdate(keyIdentifier(x.New), _ => x.New, x.New),
            ModelOperationType<T>.Delete x => m.Remove(keyIdentifier(x.Value)),
            _                              => throw new NotSupportedException()
        };

    public static Map<K, T> Apply<K, T>(this ModelOperationType<(K,T)> operation, Map<K, T> m) =>
        operation switch{
            ModelOperationType<(K,T)>.Add x    => x.Value.Map(m.Add),
            ModelOperationType<(K,T)>.Update x => m.AddOrUpdate(x.New.Item1, _ => x.New.Item2, x.New.Item2),
            ModelOperationType<(K,T)>.Delete x => m.Remove(x.Value.Item1),
            _                                  => throw new NotSupportedException()
        };

}

public abstract record ModelChange
{
    public sealed record Tag(ModelOperationType<ConceptTag> Value) : ModelChange;
    public sealed record ConceptChange(ModelOperationType<Concept> Concept) : ModelChange;
    public sealed record OwnerChange(ModelOperationType<(ConceptId ConceptId, Seq<ConceptId> Owners)> Owner) : ModelChange;
    public sealed record LinkObjectChange(ModelOperationType<LinkObject> Link) : ModelChange;
    
    public static ModelChange NewTag(ModelOperationType<ConceptTag> Value) => new Tag(Value);
    public static ModelChange NewConceptChange(ModelOperationType<Concept> Concept) => new ConceptChange(Concept);
    public static ModelChange NewOwnerChange(ModelOperationType<(ConceptId ConceptId, Seq<ConceptId> Owners)> Owner) => new OwnerChange(Owner);
    public static ModelChange NewLinkObjectChange(ModelOperationType<LinkObject> Link) => new LinkObjectChange(Link);
}

public readonly record struct ChangeLogs(Seq<ModelChange> Value);