module Tirax.KMS.Domain

open System
open System.Runtime.CompilerServices

type ConceptId = string
type ConceptTag = string
    
[<Struct; IsReadOnly>]
type Concept = {
    id       :ConceptId
    name     :string
    contains :Set<ConceptId>
    note     :string option
    link     :Uri option
    tags     :Set<ConceptTag>
}
with
    static member Empty = { id = String.Empty; name = String.Empty; contains = Set.empty; note = None; link = None; tags = Set.empty }