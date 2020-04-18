module Infrastructure

open Raven.Client.Documents
open Raven.Client.Documents.Indexes
open System.Reflection
open SharpRaven

module Persistence =

  type UserReduceResult() =
    member val Id : string = null with get, set
    member val Name : string = null with get, set

  type UsersByName() as this =
    inherit AbstractIndexCreationTask<User, UserReduceResult>()

    let map = <@ fun (users:seq<User>) -> 
                    query { 
                        for u in users do
                        select (UserReduceResult(Id = u.Id, Name = u.Name))
                    } @>
    let reduce = <@ fun (results:seq<UserReduceResult>) ->
                        query {
                            for result in results do
                            groupBy result into g
                            select (UserReduceResult(Id = g.Key.Id, Name = g.Key.Name))
                        } @>
    do
        this.Map <- Expr.ToIndexExpression map
        this.Reduce <- Expr.ToIndexExpression reduce




  let configure (store : IDocumentStore) =
      IndexCreation.CreateIndexes(Assembly.GetExecutingAssembly(), store)
      store.Initialize () |> ignore

  let Store = 
      let store = new DocumentStore ()
      store.Urls <- [|"http://127.0.0.1:8080"|]
      store.Database <- "SharpRaven"
      configure store
      store