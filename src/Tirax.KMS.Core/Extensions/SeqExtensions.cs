namespace Tirax.KMS.Extensions;

public static class SeqExtensions
{
    public static async Task<Seq<R>> ParallelMapAsync<T, R>(this Seq<T> source, Func<T, Task<R>> mapper) {
        var tasks = source.Map(mapper);
        var result = await Task.WhenAll(tasks);
        return result.ToSeq();
    }
}