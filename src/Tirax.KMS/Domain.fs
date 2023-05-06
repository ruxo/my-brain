module Tirax.KMS.Domain

open System
open System.Runtime.CompilerServices

exception RaceCondition
exception Duplication of key:string
exception DatabaseTransactionError of code:string * original:exn

type ConceptId = string

[<Struct; IsReadOnly>]
type ConceptTag =
    { id :ConceptId
      name :string }
    
[<Struct; IsReadOnly>]
type URI =
    URI of string
with
    override my.ToString() = match my with URI uri -> uri
    
    static member inline op_Implicit(uri :Uri) = URI(uri.ToString())
    static member inline op_Implicit(URI uri) = Uri(uri)
    
[<Struct; IsReadOnly>]
type LinkObject =
    { id :ConceptId
      name :string voption
      uri :URI }
    
[<Struct; IsReadOnly>]
type ConceptLink =
    | PureLink of URI
    | Link of id:ConceptId
    
    static member IsPure(link) = match link with PureLink _ -> true | _ -> false
    static member GetLinkId(link) = match link with Link id -> id | _ -> failwith "Link is Purelink, cannot get id!"
    static member GetPureUri(link) = match link with PureLink(URI uri) -> uri | _ -> failwith "Link is not PureLink"
    
    member inline link.GetLinkId() = ConceptLink.GetLinkId(link)
    
[<Struct; IsReadOnly>]
type Concept =
    { id :ConceptId
      name :string
      contains :Set<ConceptId>
      note :string voption
      link :Set<ConceptLink>
      tags :Set<ConceptId> }
    
    override my.ToString() = my.name

module Concept =
    let empty = { id = String.Empty; name = String.Empty
                  contains = Set.empty
                  note = ValueNone; link = Set.empty; tags = Set.empty }

let newObjectId() = Guid.NewGuid().ToString()