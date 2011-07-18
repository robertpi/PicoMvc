module Strangelights.PicoMvc.Spark
open System.Web
open System.IO
open Spark

let defaultSparkResultAction =

    let canTreat (context: PicoContext) _ = 
        context.Request.UrlExtension = ".html" && 
        File.Exists (HttpContext.Current.Server.MapPath(sprintf "~/views/%s.spark" context.Request.UrlPart))

    let engine = new SparkViewEngine()

    let serializeAsJson (context: PicoContext) (model: RenderingData) =
        let desc = new SparkViewDescriptor()
        desc.AddTemplate(sprintf "%s.spark" context.Request.UrlPart) |> ignore
        match model.Model with
        | Some x ->
            let modelType = x.GetType().FullName
            desc.AddAccessor(sprintf "%s model" modelType, sprintf "(%s)Globals[\"model\"]" modelType) |> ignore
        | None -> ()
        let view = engine.CreateInstance(desc) :?> SparkViewBase
        match model.Model with
        | Some x ->
            view.Globals.Add("model", x)
        | None -> ()
        view.RenderView(context.Response.ResponceStream)

    { CanTreatResult = canTreat
      ResultAction = serializeAsJson }
