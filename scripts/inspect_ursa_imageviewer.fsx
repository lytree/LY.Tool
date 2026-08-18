#r @"../packages/irihi.ursa/2.2.0/lib/net10.0/Ursa.dll"

open System
open System.IO
open System.Reflection
open System.Runtime.Loader

// Register a resolver so Avalonia dependencies of Ursa 2.2.0 can be located.
let probeDirs =
    [ @"../packages/avalonia/12.0.2/lib/net10.0"
      @"../packages/irihi.avalonia.shared/0.5.0/lib/net10.0"
      @"../packages/irihi.avalonia.shared.contracts/0.5.0/lib/net10.0" ]
    |> List.map Path.GetFullPath
let resolver =
    Func<AssemblyLoadContext, AssemblyName, Assembly>(fun (ctx: AssemblyLoadContext) (name: AssemblyName) ->
        let asmName = name.Name + ".dll"
        let candidate =
            probeDirs
            |> List.map (fun d -> Path.Combine(d, asmName))
            |> List.tryFind File.Exists
        match candidate with
        | Some c -> ctx.LoadFromAssemblyPath(c)
        | None -> null)
AssemblyLoadContext.Default.add_Resolving resolver

let asm = Assembly.LoadFrom(Path.GetFullPath(@"../packages/irihi.ursa/2.2.0/lib/net10.0/Ursa.dll"))
printfn "Assembly: %s" asm.FullName

let dumpType (typeName: string) =
    printfn "========================================"
    let ty = asm.GetTypes() |> Array.tryFind (fun t -> t.FullName = typeName)
    match ty with
    | None -> printfn "%s NOT found" typeName
    | Some t ->
        printfn "%s found: %s" typeName t.FullName
        printfn "  Properties:"
        let props =
            t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.FlattenHierarchy)
        for p in props do
            printfn "    %s : %s" p.Name p.PropertyType.FullName
        printfn "  Methods (declared):"
        let methods =
            t.GetMethods(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
        for m in methods do
            let pars =
                m.GetParameters()
                |> Array.map (fun p -> sprintf "%s: %s" p.Name p.ParameterType.Name)
                |> String.concat ", "
            printfn "    %s(%s) : %s" m.Name pars m.ReturnType.Name
        printfn "  DependencyProperty fields:"
        let fields = t.GetFields(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.FlattenHierarchy)
        for f in fields do
            if f.Name.EndsWith("Property") then
                printfn "    %s : %s" f.Name f.FieldType.Name
        printfn "  Base: %s" (if t.BaseType <> null then t.BaseType.FullName else "<null>")

dumpType "Ursa.Controls.ImageViewer"
dumpType "Ursa.Controls.PathPicker"

// Also inspect the Command dependency property of PathPicker to learn its parameter type.
let pathPicker = asm.GetTypes() |> Array.find (fun t -> t.FullName = "Ursa.Controls.PathPicker")
let cmdPropField =
    pathPicker.GetField("CommandProperty", BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.FlattenHierarchy)
match cmdPropField with
| null -> printfn "PathPicker.CommandProperty field not found"
| f ->
    printfn "PathPicker.CommandProperty field type: %s" f.FieldType.FullName
    let dp = f.GetValue null
    let dpType = dp.GetType()
    let ptProp = dpType.GetProperty("PropertyType", BindingFlags.Public ||| BindingFlags.Instance)
    if ptProp <> null then
        printfn "PathPicker.Command DP PropertyType: %s" ((ptProp.GetValue dp :?> Type).FullName)
