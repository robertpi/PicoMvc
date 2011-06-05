namespace Controllers
open Strangelights.PicoMvc


[<Controller>]
module Helloworld =
    let get () =
        Result "world"

[<DynamicController>]
module Toto =
    let accept (verb: string) (url: string) =
        url.StartsWith("toto")
    let handle () =
        Result "toto"
