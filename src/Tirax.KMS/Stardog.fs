module Tirax.KMS.Stardog

open System.Runtime.CompilerServices
open RZ.FSharp.Extension
open VDS.RDF.Query
open VDS.RDF.Storage
open Domain

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