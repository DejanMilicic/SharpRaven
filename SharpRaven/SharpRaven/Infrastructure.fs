module Infrastructure

open Raven.Client.Documents
open Raven.Client.Documents.Indexes
open System.Reflection
open System.Collections.Generic

module Persistence =



  type Users_ByName () as this =
    inherit AbstractJavaScriptIndexCreationTask ()
    do
      this.Maps <-
        HashSet<string> ["
        map('Users', function(u){
          return {
              Name: u.Name,
              Count: 1
          }
        })
        "]
      this.Reduce <- "
        groupBy(x => x.Name)
        .aggregate(g => {
            return {
                Name: g.key,
                Count: g.values.reduce((count, val) => val.Count + count, 0)
            };
        })
      "


  let configure (store : IDocumentStore) =
      store.Initialize () |> ignore
      IndexCreation.CreateIndexes(Assembly.GetExecutingAssembly(), store)

  let Store = 
      let store = new DocumentStore ()
      store.Urls <- [|"http://127.0.0.1:8080"|]
      store.Database <- "SharpRaven"
      configure store
      store