module Tirax.KMS.Stardog

open System
open System.Runtime.CompilerServices
open System.Threading
open Microsoft.Extensions.Logging
open RZ.FSharp.Extension
open Tirax.KMS.Domain
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Query.Expressions.Primary
open VDS.RDF.Storage

[<Literal>]
let ConceptNamespace = "http://ruxoz.net/rdfs/schema/knowledge/"

[<Literal>]
let ConceptDataNamespace = "http://ruxoz.net/rdfs/model/knowledge/"

[<Literal>]
let TagNamespace = "http://ruxoz.net/rdfs/model/tag/"

[<Literal>]
let XmlSchemaNamespace = "http://www.w3.org/2001/XMLSchema#"

[<Literal>]
let RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#"

[<Literal>]
let RdfsNamespace = "http://www.w3.org/2000/01/rdf-schema#"

module Keywords =
    let RdfsLabel = Uri(RdfsNamespace + "label")
    let a = Uri(RdfsNamespace + "type")
    let Context = Uri(ConceptNamespace + "Context")
    let contains = Uri(ConceptNamespace + "contains")
    let link = Uri(ConceptNamespace + "link")
    let Link = Uri(ConceptNamespace + "Link")
    let note = Uri(ConceptNamespace + "note")
    let Note = Uri(ConceptNamespace + "Note")
    
module Properties =
    let a = UriNode(Keywords.a)
    let contains = UriNode(Keywords.contains)
    let label = UriNode(Keywords.RdfsLabel)
    let link = UriNode(Keywords.link)
    let note = UriNode(Keywords.note)
    
module ObjectTypes =
    let Context = UriNode(Keywords.Context)
    let Link = UriNode(Keywords.Link)
    let Note = UriNode(Keywords.Note)
    
module ModuleNamespaces =
    [<Literal>]
    let SchemaTag = ""
    [<Literal>]
    let ModelTag = "m"
    [<Literal>]
    let TagTag = "tag"
    [<Literal>]
    let RdfTag = "rdf"
    [<Literal>]
    let RdfsTag = "rdfs"
    [<Literal>]
    let XmlTag = "xsd"
    
    let StardogDefaultNamespace = UriFactory.Create("tag:stardog:api:context:default")

    let private tryNamespace (namespace' :string) (s :string) =
        if s.StartsWith(namespace')
        then ValueSome(struct (namespace', s.Substring(namespace'.Length)))
        else ValueNone
        
    let getModelId s =
        match tryNamespace ConceptDataNamespace s with
        | ValueSome v -> v.snd()
        | ValueNone   -> failwithf $"Invalid model URI: {s}"

    let configureNamespaces(namespaces :INamespaceMapper) =
        namespaces.AddNamespace(SchemaTag, UriFactory.Create(ConceptNamespace    ))
        namespaces.AddNamespace(TagTag   , UriFactory.Create(TagNamespace        ))
        namespaces.AddNamespace(XmlTag   , UriFactory.Create(XmlSchemaNamespace  ))
        namespaces.AddNamespace(RdfTag   , UriFactory.Create(RdfNamespace        ))
        namespaces.AddNamespace(RdfsTag  , UriFactory.Create(RdfsNamespace       ))
        namespaces.AddNamespace(ModelTag , UriFactory.Create(ConceptDataNamespace))
    
let private namespace_mapper = NamespaceMapper(empty=true)
ModuleNamespaces.configureNamespaces(namespace_mapper)
    
type Graph with
    static member inline namespaceMap (g :Graph) = g.NamespaceMap
    
let createGraph() =
    Graph(ModuleNamespaces.StardogDefaultNamespace)
    |> sideEffect (Graph.namespaceMap >> ModuleNamespaces.configureNamespaces)

[<Struct; IsReadOnly; NoComparison; NoEquality>]
type KmsSubject =
    { subject :IUriNode
      g       :Graph }
      
    member my.isContext =
        Triple(my.subject, Properties.a, ObjectTypes.Context)
        
    member my.isLink =
        Triple(my.subject, Properties.a, ObjectTypes.Link)
        
    member my.hasName(name :string) =
        let t = my.g.CreateLiteralNode(name)
        in Triple(my.subject, Properties.label, t)
        
    member my.contains(cid :ConceptId) =
        let t = my.g.createModelUri(cid)
        in Triple(my.subject, Properties.contains, t)
        
    member my.hasNote(note) =
        let t = my.g.CreateLiteralNode(note)
        in Triple(my.subject, Properties.note, t)
        
    member my.hasLink(link :ConceptLink) =
        let g = my.g
        match link with
        | PureLink uri    -> Triple(my.subject, Properties.link, g.CreateLiteralNode(uri.ToString()))
        | Link concept_id -> Triple(my.subject, Properties.link, g.createModelUri(concept_id))
        
    member my.hasTag(tag) =
        let p = my.g.createSchemaUri "tag"
        let t = my.g.createTagUri(tag)
        in  Triple(my.subject, p, t)
        
and Graph with
    member inline g.createUri      (name :string) = g.CreateUriNode(name)
    
    member g.createModelUri (name :string) = g.createUri $"m:{name}"
    member g.createSchemaUri(name :string) = g.createUri $":{name}"
    member g.createTagUri   (name :string) = g.createUri $"tag:{name}"
    member g.createRdfsUri  (name :string) = g.createUri $"rdfs:{name}"
    
    member inline g.createSubject(name) = { subject = g.createModelUri(name); g = g }
    
    member g.toTriples(concept :Concept) =
        seq {
            let subject = g.createSubject(concept.id)
            subject.isContext
            subject.hasName(concept.name)
            yield! seq { for cid in concept.contains -> subject.contains(cid) }
            if concept.note.IsSome then subject.hasNote(concept.note.Value)
            for link in concept.link do
                subject.hasLink(link)
            yield! seq { for tag in concept.tags -> subject.hasTag(tag) }
        }
    
    member g.toTriples(tag :ConceptTag) =
        seq {
            let subject = g.createSubject(tag.id)
            subject.isContext
            subject.hasName(tag.name)
        }
        
open ModuleNamespaces

[<Struct; IsReadOnly; NoComparison>]
type ModelVerbs =
    | IsType
    | DefineName
    | SubClassOf
    | Contains
    | HasNote
    | HasLink
    | HasTag
            
exception InvalidNodeOperation of string * INode

type EntityType = (struct (string * string))

[<Extension>]
type EntityTypeExtension =
    [<Extension>]
    static member inline Tag(entity :EntityType) = entity.fst()
    [<Extension>]
    static member inline Name(entity :EntityType) = entity.snd()

type INode with
    member node.TryExtractUri() :EntityType voption =
        match node with
        | :? IUriNode as uri ->
            let ok, name = namespace_mapper.ReduceToQName(uri.Uri.ToString())
            if ok then
                let parts = name.Split(':')
                ValueSome(struct (parts[0], parts[1]))
            else
                ValueNone
        | _ -> ValueNone
        
    member node.ExtractUri() :EntityType =
        match node.TryExtractUri() with
        | ValueSome v -> v
        | ValueNone   -> raise(InvalidNodeOperation("Not an URI node", node))
        
    member node.AsConceptId =
        let entity = node.ExtractUri() 
        match entity with
        | ModelTag, name -> name
        | _ -> raise(InvalidNodeOperation($"%A{entity} is not a model name", node))
        
    member node.AsVerb =
        let entity = node.ExtractUri()
        match entity with
        | RdfTag   , "type"
        | RdfsTag  , "type"      -> IsType
        | RdfsTag  , "label"     -> DefineName
        | RdfsTag  , "subClassOf"-> SubClassOf
        | SchemaTag, "contains"  -> Contains
        | SchemaTag, "note"      -> HasNote
        | SchemaTag, "link"      -> HasLink
        | SchemaTag, "tag"       -> HasTag
        | _ -> raise(InvalidNodeOperation($"Unknown verb %A{entity}", node))
        
    member node.AsString =
        match node with
        | :? ILiteralNode as literal -> literal.Value
        | _ -> raise(InvalidNodeOperation("Not a literal node", node))
        
// ==================================== OBJECT MATERIALIZATION ========================================
module Materialization =
    [<Struct; IsReadOnly; NoComparison; NoEquality>]
    type PropertyDefinition =
        | TypeOf     of type':EntityType
        | Name       of name:string
        | SubClassOf of class':EntityType
        | HasConcept of id:ConceptId
        | HasNote    of note:string
        | HasLink    of link:ConceptLink
        | HasTag     of tag:ConceptId
        
        static member inline TryGetName(p) = match p with Name name -> ValueSome name | _ -> ValueNone
        
    let extractLink(value: INode) =
        match value.TryExtractUri() with
        | ValueNone                    -> HasLink(PureLink(URI value.AsString))
        | ValueSome(ModelTag, link_id) -> HasLink(Link link_id)
        | ValueSome entity             -> raise(InvalidNodeOperation($"%A{entity} is not a valid link node", value))
    
    let extractProperty(property :INode, value :INode) =
        match property.AsVerb with
        | IsType                -> TypeOf(value.ExtractUri())
        | ModelVerbs.SubClassOf -> SubClassOf(value.ExtractUri())
        | DefineName            -> Name value.AsString
        | Contains              -> HasConcept value.AsConceptId
        | ModelVerbs.HasNote    -> HasNote value.AsString
        | ModelVerbs.HasLink    -> extractLink value
        | ModelVerbs.HasTag     -> HasTag(value.AsConceptId)
            
    let extract (r :ISparqlResult) =
        let concept_id = r["subject"].AsConceptId
        let definition = extractProperty(r["property"], r["value"])
        struct (concept_id, definition)
        
    let groupProperties (object_properties :struct (ConceptId * PropertyDefinition) seq) = 
        query {
            for id, prop in object_properties do
            groupValBy prop id into g
            select struct (g.Key, g :> PropertyDefinition seq)
        }
        
    let groupObjectProperties = Seq.map extract >> groupProperties
        
    exception IncompatibleProperty of string * PropertyDefinition
    exception ObjectDataCorrupted  of string

    let createConcept(struct (id, props)) =
        let new_concept = { Concept.empty with id = id }
        props
        |> Seq.fold (fun concept p ->
                       match p with
                       | TypeOf(SchemaTag, "Context") -> concept
                       | Name name     -> { concept with Concept.name=name }
                       | HasTag tag    -> { concept with tags=concept.tags.Add(tag) }
                       | HasConcept id -> { concept with contains=concept.contains.Add(id) }
                       | HasNote note  -> { concept with note=ValueSome(note) }
                       | HasLink link  -> { concept with link=concept.link.Add(link) }
                       | TypeOf(type') -> raise(ObjectDataCorrupted $"Unrecognize type %A{type'} of an object %s{id}")
                       | SubClassOf _  -> raise(ObjectDataCorrupted $"Found SubClassOf property in an object %s{id}")
                    ) new_concept
        
    let inline createConcepts object_properties =
        object_properties |> Seq.map(createConcept)
    
    let createLinkObject struct (id, props :PropertyDefinition seq) =
        let mutable description = ValueNone
        let mutable uri = ValueNone
        for p in props do
            match p with
            | TypeOf(SchemaTag, "Link") -> ()  // correct type for Link
            | Name name                 -> assert description.IsNone; description <- ValueSome(name)
            | HasLink(PureLink link)    -> assert uri.IsNone        ; uri <- ValueSome(link)
            | _                         -> raise(IncompatibleProperty("Not support property for link", p))
        if uri.IsNone then raise(ObjectDataCorrupted "LinkObject is missing a URI!")
        let uri = uri.Value
        { id=id; name=description; uri=uri}
    
// ========================================== GRAPH UPDATE ============================================

open Materialization

module ToTriples =
    type ToTriples<'T> =
        abstract member toTriples: Graph -> 'T -> Triple seq
        
    [<Struct; IsReadOnly; NoComparison; NoEquality>]
    type ConceptToTriples =
        interface ToTriples<Concept> with
            member _.toTriples g concept =
                seq {
                    let subject = g.createSubject(concept.id)
                    subject.isContext
                    subject.hasName(concept.name)
                    yield! seq { for cid in concept.contains -> subject.contains(cid) }
                    if concept.note.IsSome then subject.hasNote(concept.note.Value)
                    yield! seq { for link in concept.link -> subject.hasLink(link) }
                    yield! seq { for tag in concept.tags -> subject.hasTag(tag) }
                }
                
    [<Struct; IsReadOnly; NoComparison; NoEquality>]
    type TagToTriples =
        interface ToTriples<ConceptTag> with
            member _.toTriples g tag =
                seq {
                    let subject = g.createSubject(tag.id)
                    subject.isContext
                    subject.hasName(tag.name)
                }
                
    [<Struct; IsReadOnly; NoComparison; NoEquality>]
    type LinkToTriples =
        interface ToTriples<LinkObject> with
            member _.toTriples g link =
                let subject = g.createSubject(link.id)
                seq {
                    subject.isLink
                    if link.name.IsSome then subject.hasName(link.name.Value)
                    subject.hasLink(PureLink link.uri)
                }
                
    [<Struct; IsReadOnly; NoComparison; NoEquality>]
    type ToTriples =
        static member inline ($) (_: ToTriples, _: Concept)    = Unchecked.defaultof<ConceptToTriples>
        static member inline ($) (_: ToTriples, _: ConceptTag) = Unchecked.defaultof<TagToTriples>
        static member inline ($) (_: ToTriples, _: LinkObject) = Unchecked.defaultof<LinkToTriples>
        
    let inline with' (x :'T) :^m when ^m :> ToTriples<'T> = Unchecked.defaultof<ToTriples> $ x
    
[<Struct; IsReadOnly; NoComparison; NoEquality>]
type StardogConnection =
    { Host     :string
      Database :string
      User     :string
      Password :string }

    static member from(s :string) =
        let toKeyValue (kv :string) =
            let p = kv.Split('=') in struct (p[0], p[1])

        let parts = s.Split(';').map(toKeyValue).toMap ()
        in { Host     = parts["Server"]
             Database = parts["Database"]
             User     = parts["User"]
             Password = parts["Password"] }

let to_model_id (concept_id :ConceptId) = $"m:{concept_id}"

[<IsReadOnly; Struct; NoComparison; NoEquality>]
type private GraphUpdate = { adding :Triple seq; removing :Triple seq }
    
module private GraphUpdate =
    let empty = { adding = Seq.empty; removing = Seq.empty }
        
    let inline apply(graph :Graph, updates :inref<GraphUpdate>, tripleable_change :inref<ModelOperationType<'T>>) =
        let inline toTriples c = (ToTriples.with' c).toTriples graph c
            
        match tripleable_change with
        | Add    tripleable -> { updates with adding   = updates.adding  .append(tripleable |> toTriples) }
        | Delete tripleable -> { updates with removing = updates.removing.append(tripleable |> toTriples) }
        | Update (old,new') -> { updates with removing = updates.removing.append(old        |> toTriples)
                                              adding   = updates.adding  .append(new'       |> toTriples) }
    
type Stardog(logger :ILogger<Stardog>, connection) =
    let connector = StardogConnector(connection.Host, connection.Database, connection.User, connection.Password)
    
    member private my.RawQuery<'T>(s) :Async<'T> =
        async {
            let! token = Async.CancellationToken
            let! result = connector.QueryAsync(s, token) |> Async.AwaitTask
            return downcast result
        }

    member my.Query(s) = my.RawQuery<SparqlResultSet>(s)
        
    member my.FetchLinks(link_ids) =
        let link_expression = link_ids |> Seq.map to_model_id |> String.concat ","
        if String.IsNullOrEmpty(link_expression) then
            async.Return(Seq.empty)
        else
            let q = sprintf """SELECT * { ?subject ?property ?value. ?subject a :Link. FILTER(?subject IN (%s)) }""" link_expression
            async {
                let! result = my.Query(q)
                return result |> Seq.map extract
                              |> groupProperties
                              |> Seq.map createLinkObject
            }
            
    member private my.FetchConcepts(q) =
        async {
            let!result = my.Query q
            logger.LogDebug("Loading FetchConcepts:{Count} results", result.Count)
            let object_properties = groupObjectProperties result
            return createConcepts(object_properties)
        }

    member my.FetchConcepts concept_ids =
        let id_list = concept_ids |> Seq.map to_model_id |> String.concat ","
        let q = $" SELECT * {{ ?subject ?property ?value. FILTER(?subject IN (%s{id_list})) }} "
        my.FetchConcepts(q)

    member my.FetchConcept(concept_id) =
        let sanitized_id = to_model_id concept_id
        let q = sprintf """SELECT * { ?subject ?property ?value. FILTER(?subject = %s) }"""
        my.FetchConcepts(q sanitized_id)
        
    member my.FetchOwner concept_id =
        let q = sprintf "SELECT * { ?subject :contains %s. }" (to_model_id concept_id)
        async {
            let! result = my.Query q
            logger.LogDebug("Loading owner of {ConceptId}, got {Count} owners.", concept_id, result.Count)
            return seq { for node in result.Results -> node["subject"].ExtractUri().snd() }
        }
        
    member my.GetTags() =
        let q = """SELECT * { ?subject ?property ?target. ?subject rdfs:subClassOf :Tag. FILTER(?property != rdf:type) }"""
        async {
            let! result = my.Query q
            let object_properties = groupObjectProperties result
            return object_properties.map(fun struct (id, props) ->
                                            { id   = id
                                              name = props.tryPick(PropertyDefinition.TryGetName).defaultValue(id) })
        }
        
    member private my.apply(graph, updates :inref<GraphUpdate>, change_log :inref<ModelChange>) =
        match change_log with
        | ConceptChange concept_change -> GraphUpdate.apply(graph, &updates, &concept_change)
        | Tag tag_change               -> GraphUpdate.apply(graph, &updates, &tag_change)
        | OwnerChange _ -> updates // OwnerChange is a derived model, just ignore
        | LinkObjectChange link_change -> GraphUpdate.apply(graph, &updates, &link_change)
        
    member my.apply(change_logs :ModelChange seq) =
        let graph = createGraph()
        let updates = GraphUpdate.empty
        async {
            let updates = change_logs |> Seq.fold (fun last change -> my.apply(graph, &last, &change)) updates
            let! token = Async.CancellationToken
            do! connector.UpdateGraphAsync(graph.Name.ToString(), updates.adding, updates.removing, token) |> Async.AwaitTask
        }
        
    member private my.Search(keyword :string, q :string, cancel_token :CancellationToken) =
        async {
            let! result = connector.QueryAsync(q, cancel_token) |> Async.AwaitTask
            let result = unbox<SparqlResultSet>(result)
            assert(result.ResultsType = SparqlResultsType.VariableBindings)
            logger.LogDebug("Search {Keyword} found {Count} results.", keyword, result.Count)
            return seq {
                for r in result.Results do
                cancel_token.ThrowIfCancellationRequested()
                let node :IUriNode = downcast r["subject"]
                getModelId(node.Uri.ToString())
            }
        }
        
    member my.SearchExact(keyword :string, cancel_token :CancellationToken) =
        let safe_keyword_quoted = ConstantTerm(LiteralNode(keyword)).ToString()
        let q = safe_keyword_quoted |> sprintf """
SELECT ?subject WHERE
{
  ?subject rdfs:label ?label .
  ?subject rdf:type|rdfs:type :Context .
  FILTER(LCASE(?label) = %s)
}
LIMIT 50
"""
        my.Search(keyword, q, cancel_token)
        
    member my.PartialSearch(keyword :string, cancel_token :CancellationToken) =
        let safe_keyword_quoted = ConstantTerm(LiteralNode(keyword + "*")).ToString()
        let q = safe_keyword_quoted |> sprintf """
SELECT ?subject WHERE
{
  ?subject rdfs:label ?label .
  ?subject rdf:type|rdfs:type :Context .
  ?label <tag:stardog:api:property:textMatch> (%s 50).
}
"""
        my.Search(keyword, q, cancel_token)