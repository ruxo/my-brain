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
    
[<Struct; IsReadOnly>]
type ModelChange =
    | Tag     of tag:ModelOperationType<ConceptTag>
    | ConceptChange of concept:ModelOperationType<Concept>
    
type ChangeLogs = ModelChange seq