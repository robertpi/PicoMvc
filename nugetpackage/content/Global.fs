namespace PicoMvc.Demo

open System
open System.IO
open System.Web
open System.Web.Routing
open System.Reflection
open Microsoft.FSharp.Reflection
open Strangelights.PicoMvc

type Global() =
    inherit System.Web.HttpApplication() 

    static member RegisterRoutes(routes:RouteCollection) =
        let routingTables = RoutingTable.LoadFromCurrentAssemblies()
        let actions =
            { TreatParameterAction = [  ]
              TreatResultAction = [  ] }
        routes.Add(new Route("{*url}", new PicoMvcRouteHandler("url", routingTables, actions)))

    member x.Start() =
        log4net.Config.XmlConfigurator.Configure()
        Global.RegisterRoutes(RouteTable.Routes)
