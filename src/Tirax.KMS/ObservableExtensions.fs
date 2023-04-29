[<AutoOpen>]
module RZ.FSharp.Extension.ObservableExtensions

open System
open System.Collections.Generic
open System.Reactive.Linq
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open FSharp.Control.Reactive

type Async<'T> with
    member inline my.toObservable() = Observable.ofAsync my
    
    member my.startImmediately([<Optional>] cancellation_token :CancellationToken voption) =
        Async.StartImmediateAsTask(my, cancellation_token.defaultValue(CancellationToken.None))

type IObservable<'T> with
    member inline my.concat(another) = my |> Observable.concat another
    member inline my.bind another = my |> Observable.bind another
    
    member inline my.bind async' = my |> Observable.flatmapAsync async'
    
    member source.choose f =
        Observable.Create (fun (o : IObserver<_>) ->
            Observable.subscribeSafeWithCallbacks 
                (fun x -> ValueOption.iter o.OnNext (try f x with ex -> o.OnError ex; ValueNone))
                o.OnError
                o.OnCompleted
                source)
        
type Map<'K,'V when 'K :comparison> with
    member my.tryGet key =
        match my.TryGetValue(key) with
        | true, v -> ValueSome v
        | false, _ -> ValueNone
        
type IEnumerator<'T> with
    member my.next() =
        if my.MoveNext()
        then ValueSome my.Current
        else ValueNone
        
type IEnumerable<'T> with
    member my.choose f =
        my.collect(fun x -> match f x with ValueSome v -> Seq.singleton v | ValueNone -> Seq.empty)
        
    member my.tryFirst() =
        use itor = my.GetEnumerator()
        in itor.next()
        
    member my.tryPick (f :'T -> 'a voption) =
        my.choose(f).tryFirst()
        
    member my.join delimiter =
        use itor = my.GetEnumerator()
        let mutable current = itor.next()
        seq {
            while itor.MoveNext() do
                yield current.Value
                yield delimiter
                current <- ValueSome itor.Current
            if current.IsSome then
                yield current.Value
        }
        
[<Extension>]
type EnumerableExtensions =
    [<Extension>]
    static member inline fst(my :struct ('K*'V)) =
        let struct (v, _) = my in v
    
    [<Extension>]
    static member inline snd(my :struct ('K*'V)) =
        let struct (_, v) = my in v
        
    [<Extension>]
    static member toMap(my :struct ('K*'V) seq) =
        my.fold(Map.empty, fun m x -> let struct (k,v) = x in m.Add(k,v))