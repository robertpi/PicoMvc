module Strangelights.PicoMvc.Phalanger
open System
open System.IO
open System.Web
open Strangelights.PicoMvc
open PHP.Core

let defaultPhalangerResultAction =
    let canTreat (context: PicoContext) _ =
        let scriptExists() =
            let phpAssems =
                AppDomain.CurrentDomain.GetAssemblies() 
                |> Seq.filter (fun x -> x.IsDefined(typeof<ScriptAssemblyAttribute>, false))

            let phpTypes =
                phpAssems
                |> Seq.map (fun x -> x.GetTypes())
                |> Seq.concat
            let scriptName = sprintf "<%s.php>" context.Request.UrlPart
            phpTypes
            |> Seq.exists(fun x -> x.FullName.StartsWith(scriptName))
        context.Request.UrlExtension = ".html" && scriptExists()

    let phalanagerView (context: PicoContext) (model: RenderingData) =
        use request_context = RequestContext.Initialize(ApplicationContext.Default, HttpContext.Current)
        use byteOut = HttpContext.Current.Response.OutputStream

        // current PHP script context:
        let phpContext = ScriptContext.CurrentContext

        // redirect PHP output to our output stream:
        use uftOut = new StreamWriter(byteOut)
        phpContext.Output <- uftOut
        phpContext.OutputStream <- byteOut // byte stream output

            // declare some global variables:
        match model.Model with
        | Some x ->
            Operators.SetVariable(phpContext, null, "model", x)
        | None -> ()

        phpContext.Include(sprintf "%s.php" context.Request.UrlPart, false) |> ignore


    { CanTreatResult = canTreat
      ResultAction = phalanagerView }
