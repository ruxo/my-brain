namespace Tirax.KMS.Extensions;

public static class SeqExtensions
{
    public static async Task<Seq<R>> ParallelMapAsync<T, R>(this Seq<T> source, Func<T, Task<R>> mapper) {
        var tasks = source.Map(mapper);
        var result = await Task.WhenAll(tasks);
        return result.ToSeq();
    }

    public static (Seq<R> True, Seq<T> False) Partition<T, R>(this Seq<T> seq, Func<T, Option<R>> predicate) {
        var result = seq.Map(x => (Key: x, Value: predicate(x)));
        return (result.Choose(x => x.Value), result.Filter(x => x.Value.IsNone).Map(x => x.Key));
    }
}