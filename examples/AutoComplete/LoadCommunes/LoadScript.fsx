#I @"..\packages\Newtonsoft.Json.4.0.2\lib\net40"
#r "Newtonsoft.Json.dll"
#I @"..\packages\RavenDB.1.0.0.427\lib\net40"
#r "Raven.Client.Lightweight.dll"
#r "Raven.Abstractions.dll"
#I @"..\packages\FsRavenDbTools.0.5.1\lib\net40"
#r "Strangelights.FsRavenDbTools.dll"
#I @"..\Common\bin\Debug"
#r "Common.dll"

open System
open System.IO
open Raven.Client.Document
open Strangelights.FsRavenDbTools.DocumentStoreExt
open Common

// The data comes from: http://www.galichon.com/codesgeo/data/ville.zip
// extract the excel file and save it as a CSV file 

let loadCommuneData() =
    use store = DocumentStore.OpenInitializedStore()
    let lines = File.ReadLines(Path.Combine(__SOURCE_DIRECTORY__, @"ville.csv"), System.Text.Encoding.Default)
    
    use session = store.OpenSession()
    session.Advanced.MaxNumberOfRequestsPerSession <- 30000
    lines
    |> Seq.skip 1
    |> Seq.iteri(fun i line ->
        let line = line.Split(';') 
        match line with
        | [|  name; nameCaps; postcode; inseeCode; region; latitude; longitude; eloignementf|] ->
            let id = sprintf "communes/%s" (inseeCode.Trim())
            printfn "Doing %i %s (%s)" i name id
            let place: Commune = 
                { Id = id
                  Name = name.Trim()
                  Postcode = postcode.Trim() }
            session.Store(place)
            if i % 1000 = 0 then session.SaveChanges()
        | line -> printfn "Error in line: %A" line)
    session.SaveChanges()

loadCommuneData()
