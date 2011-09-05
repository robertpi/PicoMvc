namespace FSharpMVC2.Core

open System
open System.IO
open System.Web
open System.Web.Routing
open System.Reflection
open Newtonsoft.Json
open Raven.Client.Document
open Microsoft.FSharp.Reflection
open Strangelights.PicoMvc
open Strangelights.FsRavenDbTools.DocumentStoreExt
open Raven.Client
open Raven.Client.Indexes
open Raven.Client.Connection

type Global() =
    inherit System.Web.HttpApplication() 


    let (store: ref<DocumentStore>) = ref null

    member __.RegisterRoutes(routes:RouteCollection) =
        let routingTables = RoutingTable.LoadFromCurrentAssemblies()

        let ravenParameterAction =
            { CanTreatParameter = fun _ _ t -> t = typeof<IDocumentStore>
              ParameterAction = fun _ _ _ -> !store :> obj }

        let actions =
            { TreatModuleParameterActions = [ ]
              TreatHandlerParameterActions = [ ParamaterActions.defaultParameterAction; 
                                               ravenParameterAction; ]
              TreatResultActions = [ NewtonsoftJson.defaultJsonResultAction ] }
        routes.Add(new Route("{*url}", new PicoMvcRouteHandler("url", routingTables, actions)))

    member x.Start() =
        store := DocumentStore.OpenInitializedStore()
        let assem = Assembly.Load("WebHost")
        IndexCreation.CreateIndexes(assem, !store)
        //log4net.Config.XmlConfigurator.Configure()
        x.RegisterRoutes(RouteTable.Routes)

    member x.End() =
        (!store).Dispose()

