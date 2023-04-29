module Tirax.KMS.Server

open System
open System.Runtime.CompilerServices
open System.Threading.Tasks
open RZ.FSharp.Extension
open RZ.FSharp.Extension.ValueOption
open Tirax.KMS.Domain
open Tirax.KMS.Stardog
open VDS.RDF
open VDS.RDF.Query

[<Struct; IsReadOnly>]
type ServerState = {
    version :uint64
    
    tags :Set<ConceptTag>
    
    concepts :Map<ConceptId, Concept>
}

[<Struct; IsReadOnly>]
type ModelOperationType<'T> =
    | Add of add:'T
    | Update of update:'T
    | Delete of delete:'T

[<Extension>]
type ModelOperationTypeExtensions =
    [<Extension>]
    static member inline apply<'T when 'T: comparison> (operation :ModelOperationType<'T>, s :Set<'T>) =
        match operation with
        | Add x -> s.Add(x)
        | Update x -> if not (s.Contains x) then s.Add(x) else s
        | Delete x -> s.Remove(x)
        
    [<Extension>]
    static member inline apply(operation :ModelOperationType<'T>, m :Map<'K,'T>, keyIdentifier :'T -> 'K) =
        match operation with
        | Add x -> m.Add(keyIdentifier x, x)
        | Update x -> m.Change(keyIdentifier x, constant (Some x))
        | Delete x -> m.Remove(keyIdentifier x)
    
[<Struct; IsReadOnly>]
type ModelChange =
    | Tag     of tag:ModelOperationType<ConceptTag>
    | Concept of concept:ModelOperationType<Concept>
    
type ChangeLogs = ModelChange seq

module ChangeLogs =
    let apply state changes =
        let new' = changes |> Seq.fold (fun last -> function
                                        | Concept c -> { last with concepts = c.apply(last.concepts, fun ct -> ct.id) }
                                        | Tag t -> { last with tags = t.apply(last.tags) }
                                        ) state
        { new' with version = new'.version + 1UL }
    
type TransactionResult<'T> = ServerState -> Async<struct (ChangeLogs * 'T)>

module Operations =
    [<Literal>]
    let ConceptNamespace = "http://ruxoz.net/rdfs/schema/knowledge/"

    [<Literal>]
    let ConceptDataNamespace = "http://ruxoz.net/rdfs/model/knowledge/"

    [<Literal>]
    let XmlSchemaNamespace = "http://www.w3.org/2001/XMLSchema#"

    [<Literal>]
    let RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#"

    [<Literal>]
    let RdfsNamespace = "http://www.w3.org/2000/01/rdf-schema#"

    let private tryNamespace (namespace' :string) (s :string) =
        if s.StartsWith(namespace')
        then ValueSome(s.Substring(namespace'.Length))
        else ValueNone
        
    let (|Concept|_|)    = tryNamespace ConceptNamespace   >> toOption
    let (|XMLSchema|_|)  = tryNamespace XmlSchemaNamespace >> toOption
    let (|RDFPattern|_|) = tryNamespace RdfNamespace       >> toOption
    let (|RDFS|_|)       = tryNamespace RdfsNamespace      >> toOption

    [<RequireQualifiedAccess; Struct; IsReadOnly>]
    type SparqlDataType =
        | Integer of i:int64
        | Decimal of dm:decimal
        | Float of f:float32
        | Double of fd:float
        | String of s:string
        | Boolean of b:bool
        | DateTime of dt:DateTimeOffset
    with
        member me.AsString() =
            match me with SparqlDataType.String s -> s | _ -> failwith "Not a string"

    [<RequireQualifiedAccess>]
    [<Struct; IsReadOnly>]
    type SparqlValue =
        | Uri of uri:Uri
        | Literal of value:SparqlDataType

    [<Struct; IsReadOnly>]
    type RdfProperties =
        | RdfClass
        | RdfProperty
        | RdfLabel of label:string
        
    [<Struct; IsReadOnly>]
    type ConceptProperties =
        | RDF of rdf_prop:RdfProperties
        | Contains of content:string
        | Note of note:string
        | Link of link:Uri

    [<AbstractClass>]
    type SparqlNode =
        static member getUri (node :INode) = (node :?> UriNode).Uri.ToString()
        
        static member parse datatype value =
            match datatype with
            | "integer" -> parseInt64(value).map(SparqlDataType.Integer)
            | "decimal" -> parseDecimal(value).map(SparqlDataType.Decimal)
            | "float"   -> parseFloat(value).map(SparqlDataType.Float)
            | "double"  -> parseDouble(value).map(SparqlDataType.Double)
            | "string"  -> ValueSome(SparqlDataType.String value)
            | "boolean" -> parseBool(value).map(SparqlDataType.Boolean)
            | "dateTime"-> parseDateTimeOffset(value).map(SparqlDataType.DateTime)
            | _ -> ValueNone
        
        static member getLiteral (node :INode) =
            let n = node :?> LiteralNode
            let v = voption {
                let! datatype = n.DataType.ToString() |> tryNamespace XmlSchemaNamespace
                return! SparqlNode.parse datatype n.Value
            }
            v.unwrap()

    let private extractSubject result =
        let uri = SparqlNode.getUri result
        assert uri.StartsWith(ConceptDataNamespace)
        uri.Substring(ConceptDataNamespace.Length)

    let private extractProperty property value =
        let property_uri = SparqlNode.getUri property
        match property_uri with
        | RDFS t when t = "label" -> SparqlNode.getLiteral(value).AsString() |> RdfLabel |> RDF
        | RDFPattern t when t = "type" -> assert ((|Concept|_|)(SparqlNode.getUri value) = Some("Context") ); RDF RdfClass
        | Concept t -> match t with
                       | "contains" -> Contains <| extractSubject value
                       | "note"     -> Note     <| SparqlNode.getLiteral(value).AsString()
                       | "link"     -> Link     <| Uri(SparqlNode.getLiteral(value).AsString())
                       | _          -> raise    <| NotSupportedException($"Concept type {t} is not supported")
        | _ -> failwith $"Unrecognized property %s{property_uri} with %A{value}"

    let private extract (r :ISparqlResult) =
        let subject = r["subject"] |> extractSubject
        let data = extractProperty r["property"] r["value"]
        subject, data
        
    let private from(id, props :ConceptProperties seq) =
        let new_concept = { Concept.Empty with id = id }
        props.fold(new_concept, fun concept p ->
                   match p with
                   | RDF (RdfLabel label) -> { concept with name=label }
                   | RDF         RdfClass ->   concept
                   | Contains          id -> { concept with contains=concept.contains.Add(id) }
                   | Note note            -> { concept with note=Some(note) }
                   | Link link            -> { concept with link=Some(link) }
                   | _                    -> failwith $"Not support property %A{p}"
                   )
        
    let private groupConcepts (result :SparqlResultSet) = 
        Map.ofSeq <| query {
            for r in result do
            let key, prop = extract r
            groupValBy prop key into g
            select (g.Key, from(g.Key, g))
        }
        
    let private updateState state graph_result =
        let map = groupConcepts graph_result
        let existing, new' = map.Values.toArray() |> Array.partition (fun concept -> state.concepts.containsKey(concept.id))
        let changes = new'.map(Add >> Concept).append(existing.map (Update >> Concept))
        struct (changes, map)
        
    let fetch (db :Stardog) id state = async {
        let! graph_result = db.FetchConcept3 id
        let struct (changes, map) = updateState state graph_result
        return struct (changes, map.tryGet(id))
    }
        
    let fetchMany (db :Stardog) ids state = async {
        let ids = ids |> Seq.toArray
        if ids.Length = 0 then
            return struct (Seq.empty, Seq.empty)
        else
            let! graph_result = db.FetchConcepts ids
            let struct (changes, map) = updateState state graph_result
            return struct (changes, ids.choose(map.tryGet))
    }

type Server(db :Stardog) =
    let mutable snapshot = { version = 0UL; tags = Set.empty; concepts = Map.empty }

    let transaction_agent = MailboxProcessor.Start(fun inbox ->
        let rec loop() = async {
            let! updater = inbox.Receive()
            
            let! changes = updater snapshot
            snapshot <- ChangeLogs.apply snapshot changes
            
            return! loop()
        }
        loop()
    )

    member private _.transact (operation :TransactionResult<'T>) = async {
        let response = TaskCompletionSource<'T>()
        
        let updater state = async {
            try
                let! changes, result = operation state
                response.SetResult(result)
                return changes
            with
            | e -> response.SetException(e)
                   return Seq.empty
        }
        transaction_agent.Post(updater)
        
        return! Async.AwaitTask response.Task
    }
    
    member my.fetch id = async {
        let state = snapshot
        match state.concepts.TryFind(id) with
        | Some v -> return ValueSome v
        | None -> return! my.transact(Operations.fetch db id)
    }
    
    member my.fetch ids = async {
        let state = snapshot
        let existed, need_fetches = ids |> Seq.map (fun i -> i, state.concepts.tryGet(i))
                                        |> Seq.toList
                                        |> List.partition (snd >> ValueOption.isSome)
        let existed = existed.map(snd >> unwrap)
        let need_fetches = need_fetches.map(fst)
        let! fetched_concepts = my.transact(Operations.fetchMany db need_fetches)
        return existed.append(fetched_concepts)
    }