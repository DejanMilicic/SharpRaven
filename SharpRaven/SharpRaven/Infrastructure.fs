module Infrastructure

open Raven.Client.Documents
open Raven.Client.Documents.Indexes
open System.Reflection
open SharpRaven
open System.Linq.Expressions
open Microsoft.FSharp.Quotations
open System
open FSharp.Quotations.Evaluator

module Persistence =

  let rec translateSimpleExpr expr =
    match expr with
    | Patterns.Var(var) ->
        // Variable access
        Expression.Variable(var.Type, var.Name) :> Expression
    | Patterns.PropertyGet(Some inst, pi, []) ->
        // Getter of an instance property
        let instExpr = translateSimpleExpr inst
        Expression.Property(instExpr, pi) :> Expression
    | Patterns.Call(Some inst, mi, args) ->
        // Method call - translate instance & arguments recursively
        let argsExpr = Seq.map translateSimpleExpr args
        let instExpr = translateSimpleExpr inst
        Expression.Call(instExpr, mi, argsExpr) :> Expression
    | Patterns.Call(None, mi, args) ->
        // Static method call - no instance
        let argsExpr = Seq.map translateSimpleExpr args
        Expression.Call(mi, argsExpr) :> Expression
    | _ -> failwith "not supported"

  /// Translates a simple F# quotation to a lambda expression
  let translateLambda (expr:Expr<'T -> 'R>) =
    match expr with
    | Patterns.Lambda(v, body) ->
        // Build LINQ style lambda expression
        let bodyExpr = translateSimpleExpr body
        let paramExpr = Expression.Parameter(v.Type, v.Name)
        Expression.Lambda<Func<'T, 'R>>(paramExpr)
    | _ -> failwith "not supported"

  let ToIndexExpression (expr:Expr<seq<'a> -> seq<'b>>) =
      match expr with
      | Patterns.Lambda(v, body) ->
          // Build LINQ style lambda expression
          let bodyExpr = translateSimpleExpr body
          let paramExpr = Expression.Parameter(v.Type, v.Name)
          Expression.Lambda<Func<seq<'a>, System.Collections.IEnumerable>>(bodyExpr, paramExpr)
      | _ -> failwith "not supported"

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
        this.Map <- QuotationEvaluator.ToLinqExpression map
        this.Reduce <- QuotationEvaluator.ToLinqExpression reduce


  let configure (store : IDocumentStore) =
      store.Initialize () |> ignore
      IndexCreation.CreateIndexes(Assembly.GetExecutingAssembly(), store)


  let Store = 
      let store = new DocumentStore ()
      store.Urls <- [|"http://127.0.0.1:8080"|]
      store.Database <- "SharpRaven"
      configure store
      store