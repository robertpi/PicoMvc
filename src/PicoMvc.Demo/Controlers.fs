namespace Controlers
open Strangelights.PicoMvc


[<Controler>]
module Helloworld =
    let get () =
        Result "world"
