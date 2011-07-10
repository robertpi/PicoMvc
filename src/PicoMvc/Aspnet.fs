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
        let fullUrl = string requestContext.RouteData.Values.[urlName]
        let urlPart, urlExtension  = 
            if String.IsNullOrEmpty fullUrl || fullUrl = "/" then
                "/", ""
            else 
                let dotIndex = fullUrl.LastIndexOf(".")
                if dotIndex > 0 then
                    fullUrl.[ .. dotIndex - 1 ], fullUrl.[ dotIndex .. ]
                elif fullUrl.EndsWith("/") then
                    fullUrl.[ .. fullUrl.Length - 2], ""
                else fullUrl, ""
        let parameters = httpContext.Request.Params.AllKeys |> Seq.fold (fun acc x -> Map.add x httpContext.Request.Params.[x] acc) Map.empty
        let headers = httpContext.Request.Headers.AllKeys |> Seq.fold (fun acc x -> Map.add x httpContext.Request.Headers.[x] acc) Map.empty
        let makeCookie (cookie: HttpCookie) =
            { Domain = if cookie.Domain = null then None else Some cookie.Domain
              Expires = if cookie.Expires = DateTime.MinValue then None else Some cookie.Expires
              HttpOnly = cookie.HttpOnly
              Name = cookie.Name
              Path = cookie.Path
              Secure = cookie.Secure
              Values = cookie.Values.AllKeys |> Seq.fold (fun acc x -> Map.add x cookie.Values.[x] acc) Map.empty  }
        let cookies = httpContext.Request.Cookies.AllKeys |> Seq.fold (fun acc x -> Map.add x (makeCookie httpContext.Request.Cookies.[x]) acc) Map.empty
        let request = new PicoRequest(urlPart, urlExtension, httpContext.Request.HttpMethod, parameters, headers, cookies, httpContext.Request.InputStream, new StreamReader(httpContext.Request.InputStream, httpContext.Request.ContentEncoding))
        use outstream = new StreamWriter(httpContext.Response.OutputStream, encoding)
        let setStatus x = httpContext.Response.StatusCode <- x
        let writeCookie cookie =
            let httpCookie = 
//                if httpContext.Request.Cookies.AllKeys |> Seq.exists (fun name -> name = cookie.Name) then
//                    let c = httpContext.Request.Cookies.[cookie.Name]
//                    c.HttpOnly <- cookie.HttpOnly
//                    c.Path <- cookie.Path
//                    c.Secure <- cookie.Secure
//                    c
//                else
                    new HttpCookie(cookie.Name, 
                                   HttpOnly = cookie.HttpOnly, 
                                   Path = cookie.Path,
                                   Secure = cookie.Secure)
                    
            match cookie.Domain with
            | Some x -> httpCookie.Domain <- x | None -> ()
            match cookie.Expires with
            | Some x -> httpCookie.Expires <- x | None -> ()
//            match cookie.Value with
//            | Some x -> httpCookie.Value <- x | None -> ()
            for x in cookie.Values do httpCookie.Values.[x.Key] <- x.Value
            httpContext.Response.Cookies.Add(httpCookie)
        let redirect url = httpContext.Response.Redirect url
        let response = new PicoResponse(httpContext.Response.OutputStream, outstream, setStatus, writeCookie, redirect)
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

