// 精确对比：滤除 LYBox 约定噪音（Semi→Fluent 资源、namespace、引号、空白、根节点属性），
// 输出真实的控件内容差异（token 级 LCS）。
open System
open System.IO
open System.Text.RegularExpressions

let pluginRoot = @"f:\Code\Dotnet\AvaloniaTemplate\plugins"
let upstreamPages = @"C:\Users\hiyan\AppData\Local\Temp\ursa-v2.2.0\repo\demo\Ursa.Demo\Pages"

let plugins = [ "LYBox.Plugin.ButtonsInputs"; "LYBox.Plugin.DateTime"; "LYBox.Plugin.DialogFeedbacks"; "LYBox.Plugin.LayoutDisplay"; "LYBox.Plugin.NavigationMenus" ]

// 正则：匹配元素名/属性名/值（含 DynamicResource、Binding 等）
let tokenRe = Regex(@"[A-Za-z_][A-Za-z0-9_.]*|\{DynamicResource\s+[^}]+\}|""[^""]*""", RegexOptions.Compiled)

let normQuote (s:string) = s.Replace("'", "\"")

// Semi 资源 -> 统一标记，避免 Fluent/Semi 差异干扰
let semiRe = Regex(@"\{DynamicResource\s+Semi[A-Za-z0-9]+\}", RegexOptions.Compiled)
let fluentRe = Regex(@"\{DynamicResource\s+Fluent[A-Za-z0-9]+\}", RegexOptions.Compiled)

let tokenize (plugin:string) (text:string) =
    let t = text.Replace("Ursa.Demo.ViewModels", plugin + ".ViewModels")
                .Replace("Ursa.Demo.Pages", plugin + ".Pages")
                .Replace("Ursa.Demo", plugin)
    let t2 = normQuote t
    let t3 = semiRe.Replace(t2, "{DR:SEMI}")
    let t4 = fluentRe.Replace(t3, "{DR:FLUENT}")
    // 去除根节点上的常见属性（x:Class/xmlns/设计期/x:DataType 前缀等已归一化）
    let tokens = tokenRe.Matches(t4) |> Seq.map (fun m -> m.Value) |> Seq.toArray
    tokens |> Array.filter (fun tok ->
        tok <> "UserControl" && tok <> "xmlns" && tok <> "http" && tok <> "schemas" &&
        tok <> "winfx" && tok <> "2006" && tok <> "xaml" && tok <> "https" &&
        tok <> "github" && tok <> "com" && tok <> "avaloniaui" && tok <> "irihi" &&
        tok <> "tech" && tok <> "ursa" && tok <> "openxmlformats" && tok <> "markup-compatibility" &&
        tok <> "expression" && tok <> "blend" && tok <> "2008" && tok <> "mc" &&
        tok <> "d" && tok <> "x" && tok <> "u" && tok <> "vm" && tok <> "viewModels" &&
        tok <> "using" && tok <> "clr-namespace" && tok <> "designheight" && tok <> "designwidth" &&
        tok <> "ignorable" && tok <> "datatype" && tok <> "class" && tok <> "compilebindings" &&
        not (tok.StartsWith("x:Class")) && not (tok.StartsWith("x:DataType")))

// LCS 求差异索引
let lcs (a:string[]) (b:string[]) =
    let n, m = a.Length, b.Length
    let dp = Array2D.create (n+1) (m+1) 0
    for i in n-1 .. -1 .. 0 do
        for j in m-1 .. -1 .. 0 do
            dp[i,j] <- if a[i] = b[j] then dp[i+1,j+1] + 1 else max dp[i+1,j] dp[i,j+1]
    // 回溯
    let rec back i j acc =
        if i < n && j < m && a[i] = b[j] then back (i+1) (j+1) acc
        elif j < m && (i >= n || dp[i, j+1] >= dp[i+1, j]) then back i (j+1) ((i, Some j, None) :: acc)
        elif i < n then back (i+1) j ((i, None, Some j) :: acc)
        else acc
    back 0 0 []

let report (plugin:string) (name:string) =
    let up = Path.Combine(upstreamPages, name)
    let plug = Path.Combine(pluginRoot, plugin, "Pages", name)
    if not (File.Exists up) then
        if File.Exists plug then printfn "  [UP-MISSING] %s (插件有,上游2.2.0无)" name
        ()
    elif not (File.Exists plug) then
        printfn "  [PLUG-MISSING] %s (上游有,插件缺 -> 需新增)" name
    else
        let a = tokenize plugin (File.ReadAllText up)
        let b = tokenize plugin (File.ReadAllText plug)
        let diffs = lcs a b
        if List.isEmpty diffs then ()
        else
            printfn "  [DIFF] %s  (up=%d tokens, plug=%d tokens, diff=%d)" name a.Length b.Length (List.length diffs)
            // 聚合显示前 12 处
            let shown = diffs |> List.truncate 12
            for (ai, ao, bo) in shown do
                let av = match ao with Some j -> a[j] | None -> "<DEL>" 
                let bv = match bo with Some j -> b[j] | None -> "<DEL>"
                match ao, bo with
                | Some _, Some _ -> printfn "      CHG  up=%s  plug=%s" av bv
                | Some _, None -> printfn "      ADD  up=%s" av
                | None, Some _ -> printfn "      REM  plug=%s" bv
                | None, None -> ()

printfn "=== 精确内容差异 (滤除 Fluent/Semi 约定、namespace、引号) ==="
for p in plugins do
    printfn "----- %s -----" p
    let dir = Path.Combine(pluginRoot, p, "Pages")
    if Directory.Exists dir then
        let names = Directory.GetFiles(dir, "*.axaml") |> Array.map Path.GetFileName |> Array.sort
        for n in names do report p n
        // 上游有但插件无
        for f in Directory.GetFiles(upstreamPages, "*.axaml") |> Array.map Path.GetFileName |> Array.sort do
            if not (Array.contains f names) then
                printfn "  [PLUG-MISSING] %s (上游有,插件缺 -> 需新增)" f
    printfn ""
