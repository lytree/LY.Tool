open System
open System.IO
open System.Reflection.PortableExecutable
open System.Reflection.Metadata

let dllPath = @"f:\Code\Dotnet\AvaloniaTemplate\packages\irihi.ursa\2.2.0\lib\net10.0\Ursa.dll"

use fs = File.OpenRead(dllPath)
use pe = new PEReader(fs)
let md = pe.GetMetadataReader()

let typeDefs = md.TypeDefinitions |> Seq.map md.GetTypeDefinition |> Seq.toArray

let findType name =
    typeDefs
    |> Array.tryFind (fun t -> md.GetString(t.Name) = name)

let printMembers (td: TypeDefinition) =
    printfn "TYPE: %s.%s" (md.GetString(td.Namespace)) (md.GetString(td.Name))
    // properties
    for h in td.GetProperties() do
        let p = md.GetPropertyDefinition(h)
        printfn "  PROP: %s" (md.GetString(p.Name))
    // methods (public only heuristics: just list names)
    for h in td.GetMethods() do
        let m = md.GetMethodDefinition(h)
        let mname = md.GetString(m.Name)
        if not (mname.StartsWith("get_") || mname.StartsWith("set_")) then
            printfn "  METH: %s" mname

let names = [ "ImageViewer"; "PathPicker"; "ThemeVariantMapper"; "ThemeVariantMapping"; "Anchor"; "AnchorItem"; "Badge"; "Avatar"; "Marquee"; "NumberDisplayer"; "DualBadge"; "QrCode"; "AspectRatioLayout"; "DisableContainer"; "TwoTonePathIcon"; "VirtualizingUniformGrid" ]
for n in names do
    match findType n with
    | Some td -> printfn "FOUND: %s.%s" (md.GetString(td.Namespace)) (md.GetString(td.Name))
    | None -> printfn "MISSING: %s" n

printfn "--- Done ---"
