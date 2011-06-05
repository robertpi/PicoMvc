namespace Controllers
open Strangelights.PicoMvc


[<Controller>]
module Helloworld =
    let get () =
        Result "world"
