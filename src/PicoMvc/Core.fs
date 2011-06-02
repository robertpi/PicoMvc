namespace Strangelights.PicoMvc
open System
open System.IO
open System.Text
open System.Web
open System.Web.Routing
open System.Web.SessionState
open System.Reflection
open Microsoft.FSharp.Reflection
type Dictionary<'a, 'b> = System.Collections.Generic.Dictionary<'a, 'b>

type ControlerAttribute() =
    inherit Attribute()


type ControlerResult =
    | Result of obj
    | NoResult
    | Error of int * string

type ErrorMessage =
    { Code: int
      Error: string }


type RoutingTable(handlersMap: Map<string * string, (string*Type)[] * (obj[] -> obj)> ) =
    static let httpVerbs = [ "get"; "put"; "post"; "delete"; ]
    static let logger = log4net.LogManager.GetLogger(typeof<RoutingTable>)

    member x.GetHandlerFunction (verb: string) url =
        let key = verb.ToLowerInvariant(), url
        if handlersMap.ContainsKey key then
            let handler = handlersMap.[key]
            Some handler
        else None

    static member LoadFromAssemblies(assems: Assembly[]) =
        do for assem in assems do logger.InfoFormat("Checking for controlers: {0}", assem.FullName)

        let rootHandlerModules =
            assems 
            |> Seq.collect (fun assem -> assem.GetTypes())
            |> Seq.filter (fun typ -> FSharpType.IsModule typ && typ.IsDefined(typeof<ControlerAttribute>, false))
        do for handler in rootHandlerModules do logger.InfoFormat("Found root handler: {0}", handler.FullName)

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
        do for entry in handlersMap do logger.InfoFormat("Found handler, (verb, url):{0}", entry.Key)
        new RoutingTable(handlersMap)

    static member LoadFromCurrentAssemblies() =
        let assems = AppDomain.CurrentDomain.GetAssemblies()
        RoutingTable.LoadFromAssemblies assems
    member x.AddHandler((verb, url), func) =
        let t = func.GetType()
        let rec getFunctionParameters acc t =
            if FSharpType.IsFunction t then
                let t, t' = FSharpType.GetFunctionElements t
                getFunctionParameters (t' :: acc) t
            else 
                List.rev (t :: acc) |> List.map (fun x -> "", x) |> List.toArray
        //FSharpValue.MakeFunction(
        if FSharpType.IsFunction t then
            let parameters = getFunctionParameters [] t
            let handlersMap' = handlersMap.Add((verb, url), func)
            new RoutingTable(handlersMap')
        else failwith "not a function"

type PicoRequest(urlPart: string, urlExtension: string: string, verb: string, parameters: Dictionary<string, string>, requestStream: StreamReader) =
    member x.UrlPart = urlPart
    member x.UrlExtension = urlExtension
    member x.Verb = verb
    member x.Parameters = parameters
    member x.RequestStream = requestStream

type PicoResponse(responceStream: StreamWriter, setStatusCode: int -> unit) =
    member x.ResponceStream = responceStream
    member x.SetStatusCode code = setStatusCode code

type PicoContext(request: PicoRequest, response: PicoResponse) =
    member x.Request = request
    member x.Response = response
     
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
    type t = class end
    let logger = log4net.LogManager.GetLogger(typeof<t>)

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


    let handleRequest (routingTables: RoutingTable) (context: PicoContext) (ioActions: IOActions) =
        let path = context.Request.UrlPart

        do logger.InfoFormat("Processing {0} request for {1}", context.Request.Verb, path)

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
            //logger.InfoFormat("Got handler {0}, {1} for {2} request for {3}", handler.Name, handler.DeclaringType.FullName, context.Request.Verb, path)
            let parameters =
                parametersTypes
                |> Array.map parameterOfType
            let paramPair = Seq.zip parametersTypes parameters |> Seq.map(fun ((name,_), v) -> sprintf "%s: %A" name v)
            logger.InfoFormat("Parameters for {0} request for {1}: {2}", context.Request.Verb, path, String.Join(", ", paramPair))
            let res = 
                try
                    let res = handler(parameters) :?> ControlerResult
                    logger.InfoFormat("Successfully handled {0} request for {1}", context.Request.Verb, path)
                    res
                with ex -> 
                    logger.ErrorFormat("Error handling {0} request for {1}, error was", context.Request.Verb, path, ex)
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
            | Error(code, message)  -> 
                context.Response.SetStatusCode code
                let res = { Code = code; Error = message } :> obj
                treatResult res
            | NoResult -> ()
        | None ->
            logger.WarnFormat("Did not find handler for {0} request for {1}", context.Request.Verb, path)
            // TODO raising an http exception doesn't really seem to work, why?
            //raise (new HttpException("not found", 404)) 
            context.Response.SetStatusCode 404
            context.Response.ResponceStream.Write("not found")