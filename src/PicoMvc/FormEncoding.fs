module FormEncoding
open System
open System.Reflection
open Microsoft.FSharp.Reflection

let updateRecord (record: obj) (parameters: Map<string,string>) =
    let t = record.GetType()
    if FSharpType.IsRecord t then
        let failures = new ResizeArray<string>()
        let rec innerLoop pathBase record =
            if record <> null then
                let t = record.GetType()
                let fields = t.GetFields(BindingFlags.NonPublic ||| BindingFlags.Instance)
                for field in fields do
                    let fieldPath = 
                        let len = field.Name.Length
                        if String.IsNullOrEmpty pathBase then field.Name.[ .. len - 2]
                        else sprintf "%s.%s" pathBase field.Name.[ .. len - 2]
                    if FSharpType.IsRecord field.FieldType || 
                        (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() = typedefof<option<obj>> && 
                         FSharpType.IsRecord(field.FieldType.GetGenericArguments().[0])) then
                            innerLoop fieldPath (field.GetValue(record))
                    if parameters.ContainsKey fieldPath then
                        let parsedValueOpt =
                            match field.FieldType with
                            | fieldType when fieldType = typeof<string> || fieldType = typeof<option<string>> ->
                                Some (parameters.[fieldPath] :> obj)
                            | fieldType when fieldType = typeof<float> || fieldType = typeof<option<float>> ->
                                let succes, value = Double.TryParse(parameters.[fieldPath])
                                if succes then
                                    Some (value :> obj)
                                else
                                    failures.Add(fieldPath)
                                    None
                            | fieldType when fieldType = typeof<int> || fieldType = typeof<option<int>> ->
                                let succes, value = Int32.TryParse(parameters.[fieldPath])
                                if succes then
                                    Some (value  :> obj)
                                else
                                    failures.Add(fieldPath)
                                    None
                            | fieldType when fieldType = typeof<DateTime> || fieldType = typeof<option<DateTime>> ->
                                let succes, value = DateTime.TryParse(parameters.[fieldPath])
                                if succes then
                                    Some (value  :> obj)
                                else
                                    failures.Add(fieldPath)
                                    None
                            | fieldType when fieldType = typeof<Boolean> || fieldType = typeof<option<Boolean>> ->
                                let value = 
                                    if parameters.[fieldPath].Contains(",") then parameters.[fieldPath].Split(',').[0]
                                    else parameters.[fieldPath]
                                let succes, value = Boolean.TryParse(value)
                                if succes then
                                    Some (value :> obj)
                                else
                                    failures.Add(fieldPath)
                                    None
                            | fieldType when fieldType.IsEnum ->
                                let succes, value = 
                                    try
                                        true, Enum.Parse(fieldType, parameters.[fieldPath])
                                    with _ -> false, null
                                if succes then
                                    Some value
                                else
                                    failures.Add(fieldPath)
                                    None
                            | fieldType when fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() = typedefof<option<obj>> && fieldType.GetGenericArguments().[0].IsEnum ->
                                let succes, value = 
                                    try
                                        true, Enum.Parse(fieldType.GetGenericArguments().[0], parameters.[fieldPath])
                                    with _ -> false, null
                                if succes then
                                    Some value
                                else
                                    failures.Add(fieldPath)
                                    None
                            | _ -> None
                        match parsedValueOpt with
                        | Some x -> 
                            let optionType = typedefof<option<obj>>
                            if field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() = optionType then
                                let t = x.GetType()
                                let typedOption = optionType.MakeGenericType([|t|])
                                let ctor = typedOption.GetConstructor([|t|])
                                let x = ctor.Invoke([|x|])
                                field.SetValue(record, x)
                            else
                                field.SetValue(record, x)
                        | None -> ()
        innerLoop "" record |> ignore
        List.ofSeq failures
    else
        failwith "Type is not a record"
        []

