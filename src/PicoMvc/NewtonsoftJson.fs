module Strangelights.PicoMvc.NewtonsoftJson
open Newtonsoft.Json
open Microsoft.FSharp.Reflection
open Strangelights.PicoMvc

let defaultJsonResultAction =
    let canTreat _ _ = true
    let serializeAsJson (context: PicoContext) (model: RenderingData) =
        let serializer = JsonSerializer.Create(new JsonSerializerSettings())
        match model.Model with
        | Some x -> serializer.Serialize(context.Response.ResponceStream, x)
        | None -> ()
        match model.Error with
        | Some x -> serializer.Serialize(context.Response.ResponceStream, x)
        | None -> ()
    { CanTreatResult = canTreat
      ResultAction = serializeAsJson }

let defaultJsonRecordParameterAction =
    let canTreat _ _ (x: System.Type) =
        FSharpType.IsRecord x
    let deserializeAsJson (context: PicoContext) _ (x: System.Type) =
        let serializer = JsonSerializer.Create(new JsonSerializerSettings())
        serializer.Deserialize(context.Request.RequestStream, x)
    { CanTreatParameter = canTreat
      ParameterAction = deserializeAsJson }



