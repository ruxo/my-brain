module Tirax.KMS.Stardog

open System
open System.Runtime.CompilerServices
open System.Threading
open RZ.FSharp.Extension
open Tirax.KMS.Domain
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Query.Builder
open VDS.RDF.Query.Builder.Expressions
open VDS.RDF.Query.Expressions
open VDS.RDF.Query.Expressions.Comparison
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
    
module ModuleNamespaces =
    let private tryNamespace (namespace' :string) (s :string) =
        if s.StartsWith(namespace')
        then ValueSome(struct (namespace', s.Substring(namespace'.Length)))
        else ValueNone
        
    let getModelId s =
        match tryNamespace ConceptDataNamespace s with
        | ValueSome v -> v.snd()
        | ValueNone   -> failwithf $"Invalid model URI: {s}"

let private StardogDefaultNamespace = UriFactory.Create("tag:stardog:api:context:default")

let private configureNamespaces(namespaces :INamespaceMapper) =
    namespaces.AddNamespace(""    , UriFactory.Create(ConceptNamespace    ))
    namespaces.AddNamespace("tag" , UriFactory.Create(TagNamespace        ))
    namespaces.AddNamespace("xsd" , UriFactory.Create(XmlSchemaNamespace  ))
    namespaces.AddNamespace("rdf" , UriFactory.Create(RdfNamespace        ))
    namespaces.AddNamespace("rdfs", UriFactory.Create(RdfsNamespace       ))
    namespaces.AddNamespace("m"   , UriFactory.Create(ConceptDataNamespace))
    
let private namespace_mapper = NamespaceMapper(empty=true)
configureNamespaces(namespace_mapper)
    
type Graph with
    static member inline namespaceMap (g :Graph) = g.NamespaceMap
    
let createGraph() =
    Graph(StardogDefaultNamespace)
    |> sideEffect (Graph.namespaceMap >> configureNamespaces)

[<Struct; IsReadOnly; NoComparison; NoEquality>]
type KmsSubject =
    { subject :IUriNode
      g       :Graph }
      
    member my.isContext =
        let p = my.g.createRdfsUri "type"
        let t = my.g.createSchemaUri "Context"
        in  Triple(my.subject, p, t)
        
    member my.hasName(name :string) =
        let p = my.g.createRdfsUri "label"
        let t = my.g.CreateLiteralNode(name)
        in  Triple(my.subject, p, t)
        
    member my.contains(cid :ConceptId) =
        let p = my.g.createSchemaUri "contains"
        let t = my.g.createModelUri(cid)
        in  Triple(my.subject, p, t)
        
    member my.hasNote(note) =
        let p = my.g.createSchemaUri "note"
        let t = my.g.CreateLiteralNode(note)
        in  Triple(my.subject, p, t)
        
    member my.hasLink(link :Uri) =
        let p = my.g.createSchemaUri "link"
        let t = my.g.CreateLiteralNode(link.ToString())
        in  Triple(my.subject, p, t)
        
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
            if concept.link.IsSome then subject.hasLink(concept.link.Value)
            yield! seq { for tag in concept.tags -> subject.hasTag(tag) }
        }
    
    member g.toTriples(tag :ConceptTag) =
        seq {
            let subject = g.createSubject(tag.id)
            subject.isContext
            subject.hasName(tag.name)
        }

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
                    if concept.link.IsSome then subject.hasLink(concept.link.Value)
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
    type ToTriples =
        static member inline ($) (_: ToTriples, _: Concept)    = Unchecked.defaultof<ConceptToTriples>
        static member inline ($) (_: ToTriples, _: ConceptTag) = Unchecked.defaultof<TagToTriples>
        
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

// TODO: properly encode concept ID
let sanitize_id (concept_id :ConceptId) = concept_id

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
    
type Stardog(connection) =
    let connector = StardogConnector(connection.Host, connection.Database, connection.User, connection.Password)
    
    member private my.RawQuery<'T>(s) :Async<'T> =
        async {
            let! token = Async.CancellationToken
            let! result = connector.QueryAsync(s, token) |> Async.AwaitTask
            return downcast result
        }

    member my.Query(s) = my.RawQuery<SparqlResultSet>(s)

    member my.FetchConcepts concept_ids =
        let id_list =
            concept_ids |> Seq.map (sanitize_id >> sprintf "m:%s") |> String.concat ","

        let q = id_list |> sprintf """
SELECT *
{
  ?subject ?property ?value.
  FILTER(?subject IN (%s))
}
    """
        in async {
            let! result = my.Query q
            printfn $"Loading {nameof my.FetchConcepts}:{result.Count} results."
            return result
        }

    member my.FetchConcept3 concept_id =
        let q = concept_id |> sanitize_id |> sprintf """
SELECT ?subject ?property ?value
{
  {
    {
      ?s :contains ?subject .    
    }
    UNION
    {
      ?s :contains/:contains ?subject .
    }
    ?subject ?property ?value.
  }
  UNION
  {
    ?s ?property ?value .
    BIND(?s AS ?subject)
  }
  FILTER(?s = m:%s)
}
    """
        in async {
            let! result = my.Query q
            printfn $"Loading {result.Count} results."
            return result
        }
        
    member private my.apply(graph, updates :inref<GraphUpdate>, change_log :inref<ModelChange>) =
        match change_log with
        | ConceptChange concept_change -> GraphUpdate.apply(graph, &updates, &concept_change)
        | Tag tag_change               -> GraphUpdate.apply(graph, &updates, &tag_change)
        
    member my.apply(change_logs :ModelChange seq) =
        let graph = createGraph()
        let updates = GraphUpdate.empty
        async {
            let updates = change_logs |> Seq.fold (fun last change -> my.apply(graph, &last, &change)) updates
            let! token = Async.CancellationToken
            do! connector.UpdateGraphAsync(graph.Name.ToString(), updates.adding, updates.removing, token) |> Async.AwaitTask
        }
        
    member private my.Search(q :string, cancel_token :CancellationToken) =
        async {
            let! result = connector.QueryAsync(q, cancel_token) |> Async.AwaitTask
            let result = unbox<SparqlResultSet>(result)
            assert(result.ResultsType = SparqlResultsType.VariableBindings)
            return seq {
                for r in result.Results do
                cancel_token.ThrowIfCancellationRequested()
                let node :IUriNode = downcast r["subject"]
                ModuleNamespaces.getModelId(node.Uri.ToString())
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
        my.Search(q, cancel_token)
        
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
        my.Search(q, cancel_token)