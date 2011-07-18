namespace Controllers
open Strangelights.PicoMvc


[<Controller>]
module Helloworld =
    let get (context: PicoContext) =
        // TODO get headers working
        //let headers = context.Response.DefaultHeaders.Add("Pragma", "whatever")
        //context.Response.OverrideDefaultHeaders headers
        Result "wôrld"

[<DynamicController>]
module Toto =
    let accept (request: PicoRequest) =
        request.UrlPart.StartsWith("toto")
    let handle () =
        Result "toto"
