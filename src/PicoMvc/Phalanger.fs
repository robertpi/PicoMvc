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

    let phalanagerView (context: PicoContext) (model: obj) =
        use request_context = RequestContext.Initialize(ApplicationContext.Default, HttpContext.Current)
        use byteOut = HttpContext.Current.Response.OutputStream

        // current PHP script context:
        let phpContext = ScriptContext.CurrentContext

        // redirect PHP output to our output stream:
        use uftOut = new StreamWriter(byteOut)
        phpContext.Output <- uftOut
        phpContext.OutputStream <- byteOut // byte stream output

        // declare some global variables:
        Operators.SetVariable(phpContext, null, "model", model)

        phpContext.Include(sprintf "%s.php" context.Request.UrlPart, false) |> ignore

        //let viewPath = getViewPath context
        // evaluate our code:
//        DynamicCode.Eval(
//            File.ReadAllText viewPath,   // the code to evaluate
//            false,  // explicit evaluation (phalanger internal stuff)
//            phpContext,// current execution script context
//            null,   // local variables (when the code is being evaluated from within the function context)
//            null,   // reference to "$this" (when the code is being evaluated from within the instance method)
//            null,   // current class context (when the code is being evaluated from within a method, class context is used to determine visibility of other fields and methods)
//            viewPath, // file name of the script containing the evaluated code (used for error reporting and debug information, you can notice the debugger will step into this file, when you hit F11)
//            1,      // line position of the code in the script file, used for error reporting and debugging information
//            1,      // column ...
//            -1,     // i actually dont know
//            null    // current namespace, used when CLR mode is enabled (e.g. WinForms etc.)
//            ) |> ignore

    { CanTreatResult = canTreat
      ResultAction = phalanagerView }
