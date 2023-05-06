module Tirax.KMS.Server

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open System.Threading.Tasks
open RZ.FSharp.Extension
open RZ.FSharp.Extension.ValueResult
open Tirax.KMS
open Tirax.KMS.Domain
open Tirax.KMS.Stardog

[<Struct; IsReadOnly;NoComparison;NoEquality>]
type ServerState =
    { version  :uint64
      tags     :Map<ConceptId, ConceptTag>
      concepts :Map<ConceptId, Concept>
      links    :Map<ConceptId, LinkObject>
      owners   :Map<ConceptId, ConceptId list> }
    
module ChangeLogs =
    let applyChange state change =
        match change with
        | ConceptChange c   -> { state with concepts = c.apply(state.concepts, fun ct -> ct.id) }
        | ModelChange.Tag t -> { state with tags     = t.apply(state.tags, fun tag -> tag.id) }
        | OwnerChange o     -> { state with owners   = o.applyKeyValue(state.owners) }
        | LinkObjectChange l-> { state with links    = l.apply(state.links, fun l -> l.id) }
    
    let apply state changes =
        let new' = changes |> Seq.fold applyChange state
        in { new' with version = new'.version + 1UL }
    
type TransactionResult<'T> = ServerState -> Async<struct (ChangeLogs * ValueResult<'T,exn>)>

module Operations =
    let fetch (db :Stardog) id _ = async {
        let!concepts = db.FetchConcept(id)
        let concepts = concepts.toArray()
        assert(concepts.Length <= 1)
        let changes = concepts |> Seq.map (Add >> ConceptChange)
        return struct (changes, ValueOk(concepts.tryFirst()))
    }
        
    let fetchMany (db :Stardog) ids _ = async {
        let ids = ids |> Seq.toArray
        if ids.Length = 0 then
            return struct (Seq.empty, ValueOk Seq.empty)
        else
            let!concepts = db.FetchConcepts ids
            let concepts = concepts.toArray()
            let changes  = concepts |> Seq.map (Add >> ConceptChange)
            return struct (changes, ValueOk concepts)
    }
    
    let fetchOwner(db :Stardog, concept_id) state =
        async {
            let! owners  = db.FetchOwner(concept_id)
            let  result  = owners.toList()
            let  changes = OwnerChange(let change = struct (concept_id, result)
                                       match state.owners.tryGet(concept_id) with
                                       | ValueNone   -> Add change
                                       | ValueSome v -> let old = struct (concept_id, v) in Update(old, change))
            return struct (Seq.singleton changes, ValueOk result)
        }
        
    let fetchLinks (db :Stardog) ids _ =
        async {
            let ids = ids |> Seq.toArray
            if ids.isEmpty() then
                return struct (Seq.empty, ValueOk(Seq.empty))
            else
                let! link = db.FetchLinks(ids)
                let changes = link |> Seq.map (Add >> LinkObjectChange)
                return struct (changes, ValueOk(link))
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
                
                let owner_list_changes = OwnerChange(Add struct (concept.id, [target.id])) |> Seq.singleton
                return struct (changes.append(owner_list_changes), ValueOk updated)
        }
        
    let updateConcept(db :Stardog, current_concept, new_concept) _ =
        assert(current_concept.id = new_concept.id)
        async {
            let changes = seq { ConceptChange(Update(current_concept, new_concept)) }
            do! db.apply(changes)
            return struct (changes, ValueOk ())
        }

type Server(db :Stardog) =
    static let invalid_keyword_letters = Regex(@"[+\-&|!\^\\:~(){}\[\]/*?“]", RegexOptions.Compiled)
    
    let mutable snapshot =
        task {
            let! tags = db.GetTags()
            return { version = 0UL
                     tags = tags.toMap(fun tag -> tag.id)
                     concepts = Map.empty
                     links = Map.empty
                     owners = Map.empty }
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
        let need_fetches = need_fetches.map(fst).toArray()
        if need_fetches.Length = 0
        then return existed
        else let! fetched_concepts = my.transact(Operations.fetchMany db need_fetches)
             return existed.append(fetched_concepts)
    }
    
    member server.FetchLink(links) =
        async {
            let! state = Async.AwaitTask snapshot
            
            let pures, link_ids        = links    |> Seq.toArray |> Array.partition(ConceptLink.IsPure)
            let existing, new_link_ids = link_ids |> Array.map ConceptLink.GetLinkId |> Array.partition(state.links.ContainsKey)
            let! new_links             = server.transact(Operations.fetchLinks db new_link_ids)
            
            let pure_links     = pures |> Seq.map ConceptLink.GetPureUri |> Seq.map (fun uri -> struct (uri, uri))
            let existing_links = existing |> Seq.map (fun id -> state.links[id])
            let display_links  =
                existing_links
                |> Seq.append new_links
                |> Seq.map (fun { id=_; name=name; uri=(URI uri) } -> struct (name.defaultValue(uri), uri)) 
            
            return pure_links |> Seq.append display_links
        }
    
    member my.addConcept(new_concept, topic) =
        my.transact(Operations.addConcept(db, new_concept, topic))
        
    member my.GetOwner(concept_id) =
        async {
            let! state = Async.AwaitTask snapshot
            let! owners = match state.owners.tryGet(concept_id) with
                          | ValueSome v -> async.Return(v)
                          | ValueNone -> my.transact(Operations.fetchOwner(db, concept_id))
            return! my.fetch(owners)
        }
        
    member my.search(keyword :string, cancel_token) =
        async {
            let sanitized = invalid_keyword_letters.Replace(keyword, String.Empty)
            let search = if sanitized.Length < 3 then db.SearchExact else db.PartialSearch
            let! concept_ids = search(sanitized, cancel_token)
            return! my.fetch(concept_ids)
        }
    
    member my.UpdateConceptName(concept_id :ConceptId, new_name) =
        async {
            match! my.fetch(concept_id) with
            | ValueNone -> ()
            | ValueSome concept -> let new_topic = { concept with name = new_name }
                                   do! my.transact(Operations.updateConcept(db, concept, new_topic))
        }