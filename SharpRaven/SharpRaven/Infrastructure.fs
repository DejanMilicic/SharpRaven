module Infrastructure

open Raven.Client.Documents

module Persistence =
  let configure (store : IDocumentStore) =
      store.Initialize () |> ignore

  let Store = 
      let store = new DocumentStore ()
      store.Urls <- [|"http://127.0.0.1:8080"|]
      store.Database <- "SharpRaven"
      configure store
      store