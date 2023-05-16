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

    public Observation<R> Map<R>(Func<T, R> mapper) =>
        this switch{
            Loading                  => Observation<R>.Loading.Default,
            Failed(Error: var error) => new Observation<R>.Failed(error),
            Data(Value  : var value) => new Observation<R>.Data(mapper(value)),

            _ => throw new NotSupportedException()
        };

    public async Task<Observation<R>> MapAsync<R>(Func<T, Task<R>> mapper) =>
        this switch{
            Loading                  => Observation<R>.Loading.Default,
            Failed(Error: var error) => new Observation<R>.Failed(error),
            Data(Value  : var value) => new Observation<R>.Data(await mapper(value)),

            _ => throw new NotSupportedException()
        };

    public T UnwrapWithDefault(T @default) => 
        this is Data(Value: var value)? value : @default;
}

public static class Observation
{
    public static IObservable<Observation<T>> From<T>(Func<Task<T>> loader) {
        var asyncStream = Observable.FromAsync(loader)
                                    .Select(Return)
                                    .Catch<Observation<T>,Exception>(ex => Observable.Return(Failed<T>(ex)));
        return Observable.Return(Observation<T>.Loading.Default).Concat(asyncStream);
    }

    public static Observation<T> Return<T>(T data) => new Observation<T>.Data(data);
    public static Observation<T> Failed<T>(Exception ex) => new Observation<T>.Failed(ex);
}