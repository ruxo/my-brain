﻿module Tirax.KMS.Domain

open System
open System.Runtime.CompilerServices

type ConceptId = string

type ConceptTag =
    { id   :ConceptId
      name :string }
    
[<Struct; IsReadOnly>]
type Concept = {
    id       :ConceptId
    name     :string
    contains :Set<ConceptId>
    note     :string voption
    link     :Uri voption
    tags     :Set<ConceptId>
}

module Concept =
    let empty = { id = String.Empty; name = String.Empty; contains = Set.empty; note = ValueNone; link = ValueNone; tags = Set.empty }