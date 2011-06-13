namespace Strangelights.PicoMvc
open System
open System.IO
open System.Text
open System.Web
open System.Web.Routing
open System.Web.SessionState
open Strangelights.Log4f

module AspNet =
    let processRequest (urlName: string) (routingTables: RoutingTable) (encoding: Encoding) (requestContext: RequestContext) (httpContext:HttpContext) actions =
        let dict = new Dictionary<string, string>()
        for x in httpContext.Request.Params.AllKeys do dict.Add(x, httpContext.Request.Params.[x])
        let fullUrl = string requestContext.RouteData.Values.[urlName]
        let urlPart, urlExtension  = 
            if String.IsNullOrEmpty fullUrl || fullUrl = "/" then
                "/", ""
            else 
                let dir, file = Path.GetDirectoryName fullUrl, Path.GetFileNameWithoutExtension fullUrl
                let urlPart = Path.Combine(dir, file)
                let urlExtension = Path.GetExtension fullUrl
                urlPart, urlExtension
        let request = new PicoRequest(urlPart, urlExtension, httpContext.Request.HttpMethod, dict, httpContext.Request.InputStream, new StreamReader(httpContext.Request.InputStream, httpContext.Request.ContentEncoding))
        use outstream = new StreamWriter(httpContext.Response.OutputStream, encoding)
        let response = new PicoResponse(httpContext.Response.OutputStream, outstream, fun x -> httpContext.Response.StatusCode <- x)
        httpContext.Response.ContentEncoding <- encoding
        // TODO hack, would be better to give developer the control of this
        httpContext.Response.ContentType <- sprintf "%s; charset=%s" httpContext.Response.ContentType encoding.WebName 
        let context = new PicoContext(request, response)
        ControllerMapper.handleRequest routingTables context actions


// takes a function that defines how the http request is handled
type PicoMvcRouteHandler(urlName: string, routingTables: RoutingTable, actions, ?encoding: Encoding) =
    static let logger = LogManager.getLogger()

    let encoding = match encoding with None -> new System.Text.UTF8Encoding(false) :> Encoding | Some x -> x

    // implement the interface that's used to route request
    interface IRouteHandler with
        member x.GetHttpHandler(requestContext: RequestContext) =
            // use an object expression to implement IHttpHandler
            { new IHttpHandler with 
                member x.IsReusable = false
                // handles the actual request processing
                member x.ProcessRequest(httpContext:HttpContext) =
                    AspNet.processRequest urlName routingTables encoding requestContext httpContext actions }

// TODO async version could interesting, but not for today ...
//type AsyncPicoMvcRouteHandler(urlName: string, routingTables: RoutingTables, ?encoding: Encoding) =
//    static let logger = log4net.LogManager.GetLogger(typeof<AsyncPicoMvcRouteHandler>)
//
//    let encoding = match encoding with None -> System.Text.Encoding.UTF8 | Some x -> x
//
//    // implement the interface that's used to route request
//    interface IRouteHandler with
//        member x.GetHttpHandler(requestContext: RequestContext) =
//            // use an object expression to implement IHttpHandler
//            { new IHttpAsyncHandler with 
//                member x.IsReusable = false
//                // handles the actual request processing
//                member x.ProcessRequest(httpContext:HttpContext) =
//                    AspNet.processRequest logger urlName routingTables encoding requestContext httpContext
//                member x.BeginProcessRequest(context: HttpContext , cb: AsyncCallback, extraData: obj) = null :> IAsyncResult 
//                member x.EndProcessRequest(result: IAsyncResult) = () } :> IHttpHandler

