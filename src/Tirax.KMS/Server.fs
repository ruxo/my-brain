module Tirax.KMS.Server

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open System.Threading.Tasks
open RZ.FSharp.Extension
open RZ.FSharp.Extension.ValueOption
open RZ.FSharp.Extension.ValueResult
open VDS.RDF
open VDS.RDF.Query
open Tirax.KMS
open Tirax.KMS.Domain
open Tirax.KMS.Stardog

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

let StardogDefaultNamespace = Uri("tag:stardog:context:default")

let private configureNamespaces(namespaces :INamespaceMapper) =
    namespaces.AddNamespace(""    , UriFactory.Create(ConceptNamespace    ))
    namespaces.AddNamespace("m"   , UriFactory.Create(ConceptDataNamespace))
    namespaces.AddNamespace("xsd" , UriFactory.Create(XmlSchemaNamespace  ))
    namespaces.AddNamespace("rdf" , UriFactory.Create(RdfNamespace        ))
    namespaces.AddNamespace("rdfs", UriFactory.Create(RdfsNamespace       ))
    
let private createGraph() =
    Graph(StardogDefaultNamespace)
    |> sideEffect (Graph.namespaceMap >> configureNamespaces)

let private tryNamespace (namespace' :string) (s :string) =
    if s.StartsWith(namespace')
    then ValueSome(struct (namespace', s.Substring(namespace'.Length)))
    else ValueNone
    
[<return: Struct>]
let (|ConceptType|_|)= tryNamespace ConceptNamespace
[<return: Struct>]
let (|XMLSchema|_|)  = tryNamespace XmlSchemaNamespace
[<return: Struct>]
let (|RDFPattern|_|) = tryNamespace RdfNamespace
[<return: Struct>]
let (|RDFS|_|)       = tryNamespace RdfsNamespace

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
    | RdfClass of cls:struct (string * string)
    | RdfSubclass of struct (string * string)
    | RdfProperty
    | RdfLabel of label:string
    
[<Struct; IsReadOnly>]
type ConceptProperties =
    | RDF of rdf_prop:RdfProperties
    | Contains of content:string
    | Note of note:string
    | Link of link:Uri
    | Tag of tag:ConceptId
    
    member my.asLabel() =
        match my with
        | RDF (RdfLabel name) -> ValueSome name
        | _ -> ValueNone
        
    static member inline asLabel (my :ConceptProperties) = my.asLabel()

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
            return! SparqlNode.parse (datatype.snd()) n.Value
        }
        v.unwrap()

let private extractSubject result =
    let uri = SparqlNode.getUri result
    assert uri.StartsWith(ConceptDataNamespace)
    uri.Substring(ConceptDataNamespace.Length)

let private extractProperty property value =
    let property_uri = SparqlNode.getUri property
    match property_uri with
    | RDFS       (_,t) when t = "label" -> SparqlNode.getLiteral(value).AsString() |> RdfLabel |> RDF
    | RDFS       (_,t) when t = "type"  -> match SparqlNode.getUri value with
                                           | RDFPattern  t -> RDF (RdfClass t)
                                           | ConceptType t -> RDF (RdfClass t)
                                           | XMLSchema   t -> RDF (RdfClass t)
                                           | RDFS        t -> RDF (RdfClass t)
                                           | _ -> failwithf $"Unrecognize type {value}"
    | RDFPattern (_,t) when t = "type"  -> match SparqlNode.getUri value with
                                           | RDFPattern  t -> RDF (RdfClass t)
                                           | ConceptType t -> RDF (RdfClass t)
                                           | XMLSchema   t -> RDF (RdfClass t)
                                           | RDFS        t -> RDF (RdfClass t)
                                           | _ -> failwithf $"Unrecognize type {value}"
                                      
    | RDFS (_,id as t) when id = "subClassOf" -> RDF (RdfSubclass t)
    | ConceptType (_,t) -> match t with
                           | "contains" -> Contains <| extractSubject value
                           | "note"     -> Note     <| SparqlNode.getLiteral(value).AsString()
                           | "link"     -> Link     <| Uri(SparqlNode.getLiteral(value).AsString())
                           | _          -> raise    <| NotSupportedException($"Concept type {t} is not supported")
    | _ -> failwith $"Unrecognized property %s{property_uri} with %A{value}"

let private extract (r :ISparqlResult) =
    let subject = r["subject"] |> extractSubject
    let data = extractProperty r["property"] r["value"]
    subject, data
        
let private groupProperties (result :SparqlResultSet) = 
    query {
        for r in result do
        let key, prop = extract r
        groupValBy prop key into g
        select struct (g.Key, g :> ConceptProperties seq)
    }
        
type Stardog with
    member my.GetTags() =
        let q = """
SELECT *
{
    ?subject ?property ?target.
    ?subject rdfs:subClassOf :Tag.
    FILTER(?property != rdf:type)
}
"""
        async {
            let! result = my.Query q
            let tags = groupProperties(result)
            return tags.map(fun struct (id, props) ->
                                { id   = id
                                  name = props.tryPick(ConceptProperties.asLabel).defaultValue(id) })
        }

[<Struct; IsReadOnly;NoComparison;NoEquality>]
type ServerState = {
    version :uint64
    
    tags :Map<ConceptId, ConceptTag>
    
    concepts :Map<ConceptId, Concept>
}

module ChangeLogs =
    let apply state changes =
        let new' = changes |> Seq.fold (fun last -> function
                                        | ConceptChange c         -> { last with concepts = c.apply(last.concepts, fun ct -> ct.id) }
                                        | ModelChange.Tag t -> { last with tags = t.apply(last.tags, fun tag -> tag.id) }
                                        ) state
        { new' with version = new'.version + 1UL }
    
type TransactionResult<'T> = ServerState -> Async<struct (ChangeLogs * ValueResult<'T,exn>)>

module Operations =
    let private from (tag_map :Map<ConceptId,ConceptTag>) struct (id, props :ConceptProperties seq) =
        let new_concept = { Concept.empty with id = id }
        props.fold(new_concept, fun concept p ->
                   match p with
                   | RDF (RdfLabel label)    -> { concept with name=label }
                   | RDF (RdfClass (cls,id)) -> if cls = ConceptNamespace && tag_map.ContainsKey(id)
                                                then { concept with tags=concept.tags.Add(id) }
                                                else   concept
                   | Contains id             -> { concept with contains=concept.contains.Add(id) }
                   | Note note               -> { concept with note=ValueSome(note) }
                   | Link link               -> { concept with link=ValueSome(link) }
                   | _                       -> failwith $"Not support property %A{p}"
                   )
        
    let private getConcepts struct (tag_map, result :SparqlResultSet) =
        groupProperties(result).map(from tag_map)
        
    let private updateState state graph_result =
        let concepts = getConcepts(state.tags, graph_result).cache()
        let existing, new' = concepts.toArray() |> Array.partition (fun concept -> state.concepts.containsKey(concept.id))
        let existing_dict = existing.map(fun c -> struct (c.id, c)).toMap()
        let makeUpdate concept = Update(existing_dict[concept.id], concept) |> ModelChange.ConceptChange
        let changes = new'.map(Add >> ModelChange.ConceptChange).append(existing.map makeUpdate)
        struct (changes, concepts.toMap(fun c -> c.id))
        
    let fetch (db :Stardog) id state = async {
        let! graph_result = db.FetchConcept3 id
        let struct (changes, map) = updateState state graph_result
        return struct (changes, ValueOk <| map.tryGet(id))
    }
        
    let fetchMany (db :Stardog) ids state = async {
        let ids = ids |> Seq.toArray
        if ids.Length = 0 then
            return struct (Seq.empty, ValueOk Seq.empty)
        else
            let! graph_result = db.FetchConcepts ids
            let struct (changes, map) = updateState state graph_result
            return struct (changes, ids.choose(map.tryGet) |> ValueOk)
    }
    
    let addConcept(db :Stardog, concept, target) state =
        async {
            if state.concepts.ContainsKey(concept.id) then raise(Duplication concept.id)
            let! struct (changes, existing_concept) = state |> fetch db concept.id
            
            match existing_concept with
            | ValueError error     -> return struct (changes, ValueError error)
            | ValueOk(ValueSome _) -> return struct (changes, ValueError(Duplication concept.id))
            | ValueOk ValueNone    -> 
                let updated = { target with contains = target.contains.Add(concept.id) }
                let changes = seq {
                                  ConceptChange(Add concept)
                                  ConceptChange(Update(target, updated))
                              }
                do! db.apply(changes)
                return struct (changes, ValueOk updated)
        }

type Server(db :Stardog) =
    static let invalid_keyword_letters = Regex(@"[+\-&|!\^\\:~(){}\[\]/*?“]", RegexOptions.Compiled)
    
    let mutable snapshot =
        task {
            let! tags = db.GetTags()
            return { version = 0UL
                     tags = tags.toMap(fun tag -> tag.id)
                     concepts = Map.empty }
        }

    let transaction_agent = MailboxProcessor.Start(fun inbox ->
        let rec loop() = async {
            let! updater = inbox.Receive()
            
            let! state = Async.AwaitTask snapshot
            let! changes = updater(state)
            snapshot <- Task.FromResult(ChangeLogs.apply state changes)
            
            return! loop()
        }
        loop()
    )

    member private _.transact (operation :TransactionResult<'T>) = async {
        let response = TaskCompletionSource<'T>()
        
        let updater state = async {
            try
                let! changes, result = operation(state)
                match result with
                | ValueOk    v -> response.SetResult(v)
                | ValueError e -> response.SetException(e)
                return changes
            with
            | e -> response.SetException(e)
                   return Seq.empty
        }
        transaction_agent.Post(updater)
        
        return! Async.AwaitTask response.Task
    }
    
    member my.fetch id = async {
        let! state = Async.AwaitTask snapshot
        match state.concepts.TryFind(id) with
        | Some v -> return ValueSome v
        | None -> return! my.transact(Operations.fetch db id)
    }
    
    member my.fetch ids = async {
        let! state = Async.AwaitTask snapshot
        let existed, need_fetches = ids |> Seq.map (fun i -> i, state.concepts.tryGet(i))
                                        |> Seq.toList
                                        |> List.partition (snd >> ValueOption.isSome)
        let existed = existed.map(snd >> ValueOption.unwrap)
        let need_fetches = need_fetches.map(fst)
        let! fetched_concepts = my.transact(Operations.fetchMany db need_fetches)
        return existed.append(fetched_concepts)
    }
    
    member my.addConcept(new_concept, topic) =
        my.transact(Operations.addConcept(db, new_concept, topic))
        
    member my.search(keyword :string, cancel_token) =
        async {
            let sanitized = invalid_keyword_letters.Replace(keyword, String.Empty)
            let search = if sanitized.Length < 3 then db.SearchExact else db.PartialSearch
            let! concept_ids = search(sanitized, cancel_token)
            return! my.fetch(concept_ids)
        }