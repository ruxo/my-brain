namespace Tirax.KMS

open System.Runtime.CompilerServices
open RZ.FSharp.Extension
open Domain

[<Struct; IsReadOnly>]
type ModelOperationType<'T> =
    | Add of add:'T
    | Update of update_old:'T * update_new:'T
    | Delete of delete:'T

[<Extension>]
type ModelOperationTypeExtensions =
    [<Extension>]
    static member inline apply<'T when 'T: comparison> (operation :ModelOperationType<'T>, s :Set<'T>) =
        match operation with
        | Add x         -> s.Add(x)
        | Update (_, x) -> if not (s.Contains x) then s.Add(x) else s
        | Delete x      -> s.Remove(x)
        
    [<Extension>]
    static member inline apply(operation :ModelOperationType<'T>, m :Map<'K,'T>, keyIdentifier :'T -> 'K) =
        match operation with
        | Add x         -> m.Add(keyIdentifier x, x)
        | Update (_, x) -> m.Change(keyIdentifier x, constant (Some x))
        | Delete x      -> m.Remove(keyIdentifier x)
        
    [<Extension>]
    static member inline applyKeyValue(operation :ModelOperationType<struct ('K*'T)>, m :Map<'K,'T>) =
        match operation with
        | Add struct (k,v)         -> m.Add(k, v)
        | Update (_, struct (k,v)) -> m.Change(k, constant (Some v))
        | Delete struct (k,_)      -> m.Remove(k)
    
[<Struct; IsReadOnly>]
type ModelChange =
    | Tag           of tag:ModelOperationType<ConceptTag>
    | ConceptChange of concept:ModelOperationType<Concept>
    | OwnerChange   of owner:ModelOperationType<struct (ConceptId * ConceptId list)>
    
type ChangeLogs = ModelChange seq