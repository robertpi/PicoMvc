module Strangelights.PicoMvc.NewtonsoftJson
open Newtonsoft.Json
open Microsoft.FSharp.Reflection
open Strangelights.PicoMvc

let defaultJsonResultAction =
    let canTreat _ _ = true
    let serializeAsJson (context: PicoContext) (model: obj) =
        let serializer = JsonSerializer.Create(new JsonSerializerSettings())
        serializer.Serialize(context.Response.ResponceStream, model)
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



