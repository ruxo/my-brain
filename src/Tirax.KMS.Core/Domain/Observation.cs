using System.Reactive.Linq;

namespace Tirax.KMS.Domain;

public abstract record Observation<T>
{
    public sealed record Loading : Observation<T>
    {
        public static readonly Loading Default = new();
    }
    public sealed record Data(T Value) : Observation<T>;
    public sealed record Failed(Exception Error) : Observation<T>;
    Observation(){}
}

public static class Observation
{
    public static IObservable<Observation<T>> From<T>(Func<Task<T>> loader) {
        var asyncStream = Observable.FromAsync(loader)
                                    .Select(Return)
                                    .Catch<Observation<T>,Exception>(ex => Observable.Return(Failed<T>(ex)));
        return Observable.Return(Observation<T>.Loading.Default).Concat(asyncStream);
    }

    static Observation<T> Return<T>(T data) => new Observation<T>.Data(data);
    static Observation<T> Failed<T>(Exception ex) => new Observation<T>.Failed(ex);
}