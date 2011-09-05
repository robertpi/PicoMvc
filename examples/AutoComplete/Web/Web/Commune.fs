namespace Web
open System.Text.RegularExpressions
open Newtonsoft.Json
open Raven.Client.Document
open Raven.Client
open Microsoft.FSharp.Reflection
open Strangelights.PicoMvc
open Strangelights.FsRavenDbTools.DocumentStoreExt
open Strangelights.Log4f
open Common

type AutoCompleteResult =
    { id: string; 
      label: string; 
      value: string }

[<Controller>]
module Commune =
    let get (term: string) (store: IDocumentStore) =
        use session = store.OpenSession()
        let postcodeRegex = new Regex(@"^\d+$")

        let comQuery = session.Advanced.LuceneQuery<Commune>("Communes/Search")
        let comQuery =
            if postcodeRegex.IsMatch term then
                comQuery.WhereStartsWith("Postcode", term)
            else
                comQuery.WhereStartsWith("Name", term)
        let query = comQuery.Take(20)
        let res = query |> Seq.map (fun x -> { id = x.Id; label = sprintf "%s (%s)" x.Name x.Postcode; value = sprintf "%s (%s)" x.Name x.Postcode})
        Result res

