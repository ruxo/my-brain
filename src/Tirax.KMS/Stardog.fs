module Tirax.KMS.Stardog

open System
open System.Runtime.CompilerServices
open RZ.FSharp.Extension
open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Storage
open Domain

type Subject =
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
    static member inline namespaceMap (g :Graph) = g.NamespaceMap
    
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

let StardogDefaultNamespace = UriFactory.Create("stardog:context:default")

let private configureNamespaces(namespaces :INamespaceMapper) =
    namespaces.AddNamespace(""    , UriFactory.Create(ConceptNamespace    ))
    namespaces.AddNamespace("m"   , UriFactory.Create(ConceptDataNamespace))
    namespaces.AddNamespace("tag" , UriFactory.Create(TagNamespace        ))
    namespaces.AddNamespace("xsd" , UriFactory.Create(XmlSchemaNamespace  ))
    namespaces.AddNamespace("rdf" , UriFactory.Create(RdfNamespace        ))
    namespaces.AddNamespace("rdfs", UriFactory.Create(RdfsNamespace       ))
    
let private createGraph() =
    Graph(StardogDefaultNamespace)
    |> sideEffect (Graph.namespaceMap >> configureNamespaces)

[<Struct; IsReadOnly>]
type StardogConnection =
    { Host :string
      Database :string
      User :string
      Password :string }

    static member from(s :string) =
        let toKeyValue (kv :string) =
            let p = kv.Split('=') in struct (p[0], p[1])

        let parts = s.Split(';').map(toKeyValue).toMap ()
        in { Host = parts["Server"]
             Database = parts["Database"]
             User = parts["User"]
             Password = parts["Password"] }

// TODO: properly encode concept ID
let sanitize_id (concept_id :ConceptId) = concept_id

type private GraphUpdate = { adding :Triple seq; removing :Triple seq }
    
module private GraphUpdate =
    let empty = { adding = Seq.empty; removing = Seq.empty }

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
        
    member private my.apply(graph :Graph, updates :inref<_>, concept_change :inref<_>) =
        match concept_change with
        | Add    concept    -> { updates with adding   = updates.adding  .append(concept |> graph.toTriples) }
        | Delete concept    -> { updates with removing = updates.removing.append(concept |> graph.toTriples) }
        | Update (old,new') -> { updates with removing = updates.removing.append(old     |> graph.toTriples)
                                              adding   = updates.adding  .append(new'    |> graph.toTriples) }
        
    member private my.apply(graph :Graph, updates :inref<_>, concept_change :inref<ModelOperationType<ConceptTag>>) =
        match concept_change with
        | Add    concept    -> { updates with adding   = updates.adding  .append(concept |> graph.toTriples) }
        | Delete concept    -> { updates with removing = updates.removing.append(concept |> graph.toTriples) }
        | Update (old,new') -> { updates with removing = updates.removing.append(old     |> graph.toTriples)
                                              adding   = updates.adding  .append(new'    |> graph.toTriples) }
        
    member private my.apply(graph, updates :inref<GraphUpdate>, change_log) =
        match change_log with
        | ConceptChange concept_change -> my.apply(graph, &updates, &concept_change)
        | Tag tag_change         -> my.apply(graph, &updates, &tag_change)
        
    member my.apply(change_logs :ModelChange seq) =
        let graph = createGraph()
        let updates = GraphUpdate.empty
        async {
            let updates = change_logs |> Seq.fold (fun last change -> my.apply(graph, &last, change)) updates
            let! token = Async.CancellationToken
            do! connector.UpdateGraphAsync(graph.Name.ToString(), updates.adding, updates.removing, token) |> Async.AwaitTask
        }