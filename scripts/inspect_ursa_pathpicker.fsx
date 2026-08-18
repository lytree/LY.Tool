#r @"../packages/irihi.ursa/2.2.0/lib/net10.0/Ursa.dll"

open System
open System.Reflection

let asm = Assembly.LoadFrom(@"../packages/irihi.ursa/2.2.0/lib/net10.0/Ursa.dll")
printfn "Assembly: %s" asm.FullName

let pathPicker = asm.GetTypes() |> Array.tryFind (fun t -> t.FullName = "Ursa.Controls.PathPicker")
match pathPicker with
| None -> printfn "PathPicker type NOT found"
| Some t ->
    printfn "PathPicker found: %s" t.FullName
    // Look for CommandProperty and Command
    let props = t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.FlattenHierarchy)
    for p in props do
        if p.Name.Contains("Command") || p.Name.Contains("Picker") then
            printfn "  Property: %s : %s" p.Name (p.PropertyType.FullName)

    // Also inspect base classes for the Command dependency property type
    let rec walk (ty: Type) =
        if ty = null then () else
        printfn "  Base: %s" ty.FullName
        walk ty.BaseType
    walk t

    // Find the CommandProperty static field to determine parameter type
    let commandProp = t.GetField("CommandProperty", BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.FlattenHierarchy)
    match commandProp with
    | null -> printfn "CommandProperty field not found"
    | f ->
        printfn "CommandProperty field type: %s" f.FieldType.FullName
        let dp = f.GetValue(null)
        let dpType = dp.GetType()
        printfn "DP actual type: %s" dpType.FullName
        // Try to get property type from the DP
        let ptProp = dpType.GetProperty("PropertyType", BindingFlags.Public ||| BindingFlags.Instance)
        if ptProp <> null then
            printfn "DP PropertyType: %s" ((ptProp.GetValue(dp) :?> Type).FullName)
