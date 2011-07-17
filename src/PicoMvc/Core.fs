namespace Strangelights.PicoMvc
open System
open System.IO
open System.Text
open System.Web
open System.Web.Routing
open System.Web.SessionState
open System.Reflection
open Microsoft.FSharp.Reflection
open Strangelights.Log4f
type Dictionary<'a, 'b> = System.Collections.Generic.Dictionary<'a, 'b>

type ControllerAttribute() =
    inherit Attribute()

type DynamicControllerAttribute() =
    inherit Attribute()


type ControllerResult =
    | Result of obj
    | Redirect of string
    | Error of int * string
    | NoResult

type ErrorMessage =
    { Code: int
      Error: string }

type Cookie =
    { Domain: option<string>
      Expires: option<DateTime>
      HttpOnly: bool
      Name: string
      Path: string
      Secure: bool
      //Value: option<string>
      Values: Map<string,string> }
    with
        member x.AddOrAlterValue key value =
            { x with Values = Map.add key value x.Values }
        member x.RemoveValue key =
            { x with Values = Map.remove key x.Values }
        static member Make name = 
            { Domain = None
              Expires = None
              HttpOnly = false
              Name = name
              Path = "/"
              Secure = false
              //Value = None
              Values = Map.empty }
        static member Make (name, key, value) = 
            { Domain = None
              Expires = None
              HttpOnly = false
              Name = name
              Path = "/"
              Secure = false
              //Value = None
              Values = Map.add key value Map.empty }

type PicoRequest(urlPart: string, 
                 urlExtension: string: string, 
                 verb: string, 
                 parameters: Map<string, string>, 
                 headers: Map<string, string>, 
                 cookies: Map<string, Cookie>, 
                 rawStream: Stream, 
                 requestStream: StreamReader) =
    member x.UrlPart = urlPart
    member x.UrlExtension = urlExtension
    member x.Verb = verb
    member x.Parameters = parameters
    member x.Headers = headers
    member x.Cookies = cookies
    member x.RawStream = rawStream
    member x.RequestStream = requestStream

type PicoResponse(rawStream: Stream, 
                  responceStream: StreamWriter,
                  // TODO get headers working
//                  defaultHeaders: Map<string, string>,
//                  overrideDefaultHeaders: Map<string, string> -> unit,
                  setStatusCode: int -> unit, 
                  writeCookie: Cookie -> unit,
                  redirect: string -> unit) =
    member x.RawStream = rawStream
    member x.ResponceStream = responceStream
                  // TODO get headers working
//    member x.DefaultHeaders = defaultHeaders
//    member x.OverrideDefaultHeaders headers = overrideDefaultHeaders headers
    member x.SetStatusCode code = setStatusCode code
    member x.WriteCookie cookie = writeCookie cookie
    member x.Redirect url = redirect url

type PicoContext(request: PicoRequest, response: PicoResponse, mapPath: string -> string) =
    member x.Request = request
    member x.Response = response
    member x.MapPath path = mapPath path


type RoutingTable private (staticHandlersMap: Map<string * string, (string*Type)[] * (obj[] -> obj)>, dynamicHandlers) =
    static let httpVerbs = [ "get"; "put"; "post"; "delete"; ]
    static let logger = LogManager.getLogger()

    let getDynamicFunction func =
        let t = func.GetType()
        if FSharpType.IsFunction t then
            let invokeMethod = 
                t.GetMethods()
                |> Seq.filter (fun x -> x.Name = "Invoke")
                |> Seq.sortBy (fun x -> x.GetParameters().Length)
                |> Seq.toList |> List.rev |> List.head
            let parameters = invokeMethod.GetParameters() |> Seq.map (fun x -> x.Name, x.ParameterType) |> Seq.toArray
            let invoke = fun parameters -> invokeMethod.Invoke(func, parameters)
            parameters, invoke
        else failwith "not a function"

    member x.GetHandlerFunction (verb: string) url =
        let key = verb.ToLowerInvariant(), url
        if staticHandlersMap.ContainsKey key then
            let handler = staticHandlersMap.[key]
            Some handler
        else
            List.tryFind (fun (accpetFunc, _) -> accpetFunc verb url) dynamicHandlers
            |> Option.map snd

    static member LoadFromAssemblies(assems: Assembly[]) =
        do for assem in assems do logger.Info "Checking for Controllers: %s" assem.FullName

        let fsModules =
            assems 
            |> Seq.collect (fun assem -> assem.GetTypes())
            |> Seq.filter (fun typ -> FSharpType.IsModule typ)
            |> Seq.toList
        let rootHandlerModules = fsModules |> Seq.filter (fun typ -> typ.IsDefined(typeof<ControllerAttribute>, false))
        do for handler in rootHandlerModules do logger.Info "Found root handler: %s" handler.FullName

        let rec walkSubHandlers (types: seq<Type>) =
            seq { for typ in types do
                    yield! walkSubHandlers (typ.GetNestedTypes())
                    if FSharpType.IsModule  typ then
                        yield typ }

        let allHandlers = walkSubHandlers rootHandlerModules
        let urlOfName (typ: Type) =
            let name =
                if typ.IsNested then
                    typ.FullName.[typ.Namespace.Length .. ].Replace('+', '/')
                else
                    typ.Name
            name.ToLowerInvariant()
        let handlerFromType (typ: Type) verb =
            let handler = typ.GetMethod(verb, BindingFlags.Static ||| BindingFlags.Public)
            if handler <> null then
                let parameters = handler.GetParameters() |> Array.map (fun p -> p.Name, p.ParameterType)
                Some (parameters, fun parameters -> handler.Invoke(null, parameters))
            else
                None
        let handlersMap = 
            allHandlers 
            |> Seq.collect (fun typ -> 
                            httpVerbs 
                            |> Seq.choose (fun verb ->  handlerFromType typ verb |> Option.map (fun x -> (verb, urlOfName typ), x) ))
            |> Map.ofSeq
        do for entry in handlersMap do logger.Info "Found handler, (verb, url): %A" entry.Key

        let dynamicHandlerModules = fsModules |> Seq.filter (fun typ -> typ.IsDefined(typeof<DynamicControllerAttribute>, false))
        do for handler in rootHandlerModules do logger.Info "Found dynamic handler: %s" handler.FullName

        let getDynamicHandler (typ: Type) =
            let acceptFunc = typ.GetMethod("accept", BindingFlags.Static ||| BindingFlags.Public)
            let func = typ.GetMethod("handle", BindingFlags.Static ||| BindingFlags.Public)
            if acceptFunc = null || func = null then
                failwithf "didn't find 'accept' and 'handle' in module %s" typ.Name
            // TODO map rough generic parameters to string ?
            let parameters = func.GetParameters() |> Array.map (fun p -> p.Name, p.ParameterType)
            let acceptFunc = fun (verb: string) (url: string) -> acceptFunc.Invoke(null, [| verb :> obj; url :> obj|]) :?> bool
            let func = parameters, fun parameters -> func.Invoke(null, parameters)
            acceptFunc, func

        let dynamicHandlers = dynamicHandlerModules |> Seq.map getDynamicHandler |> Seq.toList

        new RoutingTable(handlersMap, dynamicHandlers)

    static member LoadFromCurrentAssemblies() =
        let assems = AppDomain.CurrentDomain.GetAssemblies()
        RoutingTable.LoadFromAssemblies assems

    member x.AddStaticHandler((verb, url), func) =
        let dynamicFunc = getDynamicFunction func
        let handlersMap' = staticHandlersMap.Add((verb, url), dynamicFunc)
        new RoutingTable(handlersMap', dynamicHandlers)

    member x.AddDynamicHandler(acceptFunc: string -> string -> bool, func) =
        let dynamicFunc = getDynamicFunction func
        let handlersMap' = (acceptFunc, dynamicFunc) :: dynamicHandlers
        new RoutingTable(staticHandlersMap, handlersMap')
     
type ParameterAction =
    { CanTreatParameter: PicoContext -> string -> Type -> bool
      ParameterAction: PicoContext -> string -> Type -> obj }

type ResultAction =
    { CanTreatResult: PicoContext -> obj -> bool
      ResultAction: PicoContext -> obj -> unit }

type IOActions =
    { TreatParameterAction: List<ParameterAction>
      TreatResultAction: List<ResultAction> }

module ControllerMapper =
    let logger = LogManager.getLogger()

    let defaultParameterAction  =
        let canTreat (context: PicoContext) name _ =
            context.Request.Parameters.ContainsKey name
        let action (context: PicoContext) name t =
            match t with
            | x when x = typeof<string> -> context.Request.Parameters.[name] :> obj
            | x when x = typeof<int> -> int context.Request.Parameters.[name] :> obj
            | x when x = typeof<float> -> float context.Request.Parameters.[name] :> obj
            | _ -> null
        { CanTreatParameter = canTreat
          ParameterAction = action }

    let contextParameterAction  =
        let canTreat (context: PicoContext) _ t =
            t = typeof<PicoContext> || t = typeof<PicoResponse> || t = typeof<PicoRequest>
        let action (context: PicoContext) _ t =
            match t with
            | x when x = typeof<PicoContext> -> context :> obj
            | x when x = typeof<PicoRequest> -> context.Request :> obj
            | x when x = typeof<PicoResponse> -> context.Request :> obj
            | _ -> null
        { CanTreatParameter = canTreat
          ParameterAction = action }


    let handleRequest (routingTables: RoutingTable) (context: PicoContext) (ioActions: IOActions) =
        let path = context.Request.UrlPart

        logger.Info "Processing %s request for %s" context.Request.Verb path

        let handler = routingTables.GetHandlerFunction context.Request.Verb path
        let parameterOfType (name:string, t:Type) = 
            let res =
                ioActions.TreatParameterAction 
                |> List.tryFind (fun pa -> pa.CanTreatParameter context name t)
            match res with
            | Some pa -> pa.ParameterAction context name t
            | None -> null

        match handler with
        | Some (parametersTypes, handler) ->
            let parameters =
                parametersTypes
                |> Array.map parameterOfType
            let paramPair = Seq.zip parametersTypes parameters |> Seq.map(fun ((name,_), v) -> sprintf "%s: %A" name v)
            logger.Info "Parameters for %s request for %s: %s" context.Request.Verb path (String.Join(", ", paramPair))
            let res = 
                try
                    let res = handler(parameters) :?> ControllerResult
                    logger.Info "Successfully handled %s request for %s" context.Request.Verb path
                    res
                with ex -> 
                    logger.Error(ex, "Error handling %s request for %s, error was") context.Request.Verb path
                    Error (500, (ex.ToString()))
            let treatResult obj =
                let res =
                    ioActions.TreatResultAction 
                    |> List.tryFind (fun pa -> pa.CanTreatResult context obj)
                match res with
                | Some pa -> pa.ResultAction context obj
                | None -> ()
            match res with
            | Result obj -> treatResult obj
            | Redirect url -> context.Response.Redirect url
            | Error(code, message)  -> 
                context.Response.SetStatusCode code
                let res = { Code = code; Error = message } :> obj
                treatResult res
            | NoResult -> ()
        | None ->
            logger.Warn "Did not find handler for %s request for %s" context.Request.Verb path
            // TODO raising an http exception doesn't really seem to work, why?
            //raise (new HttpException("not found", 404)) 
            context.Response.SetStatusCode 404
            context.Response.ResponceStream.Write("not found")