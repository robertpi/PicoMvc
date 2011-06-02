module Strangelights.PicoMvc.Spark
open System.Web
open System.IO
open Spark

let defaultSparkResultAction =

    let canTreat (context: PicoContext) _ = 
        context.Request.UrlExtension = ".html" && 
        File.Exists (HttpContext.Current.Server.MapPath(sprintf "~/views/%s.spark" context.Request.UrlPart))

    let engine = new SparkViewEngine()

    let serializeAsJson (context: PicoContext) (model: obj) =
        let desc = new SparkViewDescriptor()
        desc.AddTemplate(sprintf "%s.spark" context.Request.UrlPart) |> ignore
        let modelType = model.GetType().FullName
        desc.AddAccessor(sprintf "%s model" modelType, sprintf "(%s)Globals[\"model\"]" modelType) |> ignore
        let view = engine.CreateInstance(desc) :?> SparkViewBase
        view.Globals.Add("model", model)
        view.RenderView(context.Response.ResponceStream)

    { CanTreatResult = canTreat
      ResultAction = serializeAsJson }
