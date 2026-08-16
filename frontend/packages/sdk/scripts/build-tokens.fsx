(*
 * build-tokens.fsx —— 从 Design Token 单一数据源生成 lybox-theme.css
 *
 * 读取 src/theme/tokens.json，生成 src/theme/lybox-theme.css 的三个 CSS 块：
 *   1. :root                           —— 浅色值（light）+ 静态值（value）
 *   2. :root[data-theme="dark"]        —— 深色值（dark）
 *   3. @media (prefers-color-scheme: dark) —— 深色值（与 [data-theme="dark"] 相同的变量集）
 *
 * 运行（.NET F# Interactive）：
 *   dotnet fsi build-tokens.fsx
 *
 * tokens.json 结构说明：
 *   - 主题化令牌（color、shadow）：含 light / dark 两个子对象，各含 value
 *   - 静态令牌（radius、spacing、fontSize、fontFamily）：直接含 value
 *   - 每个令牌含 cssVar（CSS 变量名）与 section（分组注释）
 *)
open System
open System.IO
open System.Text.Json

let scriptDir = __SOURCE_DIRECTORY__
let tokensPath = Path.GetFullPath(Path.Combine(scriptDir, "..", "src", "theme", "tokens.json"))
let cssPath = Path.GetFullPath(Path.Combine(scriptDir, "..", "src", "theme", "lybox-theme.css"))

// ----------------------------------------------------------------------------
// 解析 tokens.json
// ----------------------------------------------------------------------------
/// 读取令牌值：light/dark 为对象（含 value），静态 value 为字符串。
let readValue (tok: JsonElement) (prop: string) : string option =
    match tok.TryGetProperty prop with
    | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString())
    | true, v when v.ValueKind = JsonValueKind.Object ->
        match v.TryGetProperty "value" with
        | true, valElem -> Some(valElem.GetString())
        | _ -> None
    | _ -> None

type Token =
    { CssVar: string
      Section: string
      Light: string option
      Dark: string option
      Static: string option }

let root = JsonDocument.Parse(File.ReadAllText tokensPath).RootElement

let tokens: Token list =
    [ for cat in root.EnumerateObject() do
          if cat.Name.StartsWith("$") then
              () // 跳过元数据属性（$schema、$description、$version）
          elif cat.Value.ValueKind = JsonValueKind.Object then
              for tok in cat.Value.EnumerateObject() do
                  let element = tok.Value
                  let mutable hasCssVar = false
                  let mutable cssVar = ""
                  let mutable tmp = JsonElement()
                  if element.TryGetProperty("cssVar", &tmp) then
                      hasCssVar <- true
                      cssVar <- tmp.GetString()
                  if hasCssVar then
                      yield
                          { CssVar = cssVar
                            Section = element.GetProperty("section").GetString()
                            Light = readValue element "light"
                            Dark = readValue element "dark"
                            Static = readValue element "value" } ]

// ----------------------------------------------------------------------------
// 分组顺序（与宿主 FluentDesign/Light.axaml 的视觉层次一致）
// ----------------------------------------------------------------------------
let sectionOrder =
    [ "主色（Accent）"
      "次级色"
      "三级色"
      "信息色"
      "成功色"
      "警告色"
      "危险色"
      "文本色（4 级灰度）"
      "链接色"
      "背景色（5 级）"
      "填充色（叠加透明度）"
      "边框色"
      "禁用态"
      "导航"
      "遮罩"
      "高亮"
      "卡片（语义 Brush，对应 FluentCardBackgroundBrush 等）"
      "阴影"
      "尺寸（对应 Themes/Shared/Variables.axaml）"
      "间距（FluentSpacing 体系）"
      "边框圆角（FluentBorderRadius 体系）"
      "字号"
      "字体族" ]

let sectionRank (section: string) =
    match List.tryFindIndex ((=) section) sectionOrder with
    | Some i -> i
    | None -> sectionOrder.Length

// 主题化令牌（有 light/dark）用于深色块；静态令牌（仅 value）只进 :root。
let themed = tokens |> List.filter (fun t -> t.Light.IsSome && t.Dark.IsSome)
let statics = tokens |> List.filter (fun t -> t.Static.IsSome && t.Light.IsNone && t.Dark.IsNone)

// 按 section 分组，并在 section 内保持 tokens.json 出现顺序
let groupBySection (list: Token list) =
    list
    |> List.groupBy (fun t -> t.Section)
    |> List.sortBy (fun (s, _) -> sectionRank s)

// ----------------------------------------------------------------------------
// 生成 CSS
// ----------------------------------------------------------------------------
let renderDecl (cssVar: string, value: string) =
    $"  {cssVar}: {value};"

let renderSection (section: string, entries: (string * string) list) =
    let comment = "  /* " + section + " */"
    let lines = entries |> List.map renderDecl
    String.Join("\n", comment :: lines)

// 具体模式：浅色（:root）、深色（dark 块）、静态（:root）
let lightEntries =
    groupBySection themed
    |> List.map (fun (s, toks) ->
        (s, toks |> List.choose (fun t -> t.Light |> Option.map (fun v -> (t.CssVar, v)))))

let darkEntries =
    groupBySection themed
    |> List.map (fun (s, toks) ->
        (s, toks |> List.choose (fun t -> t.Dark |> Option.map (fun v -> (t.CssVar, v)))))

let staticEntries =
    groupBySection statics
    |> List.map (fun (s, toks) ->
        (s, toks |> List.choose (fun t -> t.Static |> Option.map (fun v -> (t.CssVar, v)))))

let renderEntries (entries: (string * (string * string) list) list) =
    String.Join("\n\n", entries |> List.map renderSection)

/// 渲染嵌套块内的条目（section 注释缩进 4，声明缩进 6；空白分隔行无尾随空格）
let renderNestedEntries (entries: (string * (string * string) list) list) =
    let renderNestedSection (section: string, decls: (string * string) list) =
        let comment = "    /* " + section + " */"
        let lines = decls |> List.map (fun (k, v) -> $"      {k}: {v};")
        String.Join("\n", comment :: lines)
    String.Join("\n\n", entries |> List.map renderNestedSection)

let header = """/**
 * LYBox Fluent Design Theme
 *
 * 与宿主 Avalonia UrsaFluentTheme 配色方案保持一致的 CSS 变量定义。
 * 颜色值源自 src/Layout/LYBox.Layout.Ursa/Theme/FluentDesign/Light.axaml 和 Dark.axaml。
 *
 * 本文件由脚本生成，请勿手改 —— 单一数据源见 src/theme/tokens.json。
 * 重新生成： dotnet fsi packages/sdk/scripts/build-tokens.fsx
 *
 * 用法：
 *   import '@lytree/sdk/css';
 *   // 或在 HTML 中：
 *   // <link rel="stylesheet" href="..." />
 *
 * 主题切换：
 *   <html data-theme="dark">  // 强制深色
 *   <html data-theme="light"> // 强制浅色
 *   // 不设置时跟随系统 prefers-color-scheme
 */
"""

let lightBlock =
    "/* ============================================\n"
    + " * 浅色主题（默认）\n"
    + " * 对应 FluentDesign/Light.axaml\n"
    + " * ============================================ */\n"
    + ":root {\n"
    + renderEntries lightEntries
    + "\n"
    + renderEntries staticEntries
    + "\n}"

let darkBlock =
    "/* ============================================\n"
    + " * 深色主题\n"
    + " * 对应 FluentDesign/Dark.axaml\n"
    + " * ============================================ */\n"
    + ":root[data-theme=\"dark\"] {\n"
    + renderEntries darkEntries
    + "\n}"

let systemBlock =
    "/* ============================================\n"
    + " * 系统主题跟随（无 data-theme 属性时）\n"
    + " * ============================================ */\n"
    + "@media (prefers-color-scheme: dark) {\n"
    + "  :root:not([data-theme=\"light\"]):not([data-theme=\"dark\"]) {\n"
    + renderNestedEntries darkEntries
    + "\n  }\n}"

let baseStyles = """/* ============================================
 * 基础元素样式（可选，使用语义变量）
 * ============================================ */
:root {
  color: var(--lybox-color-text-0);
  background-color: var(--lybox-color-background-0);
  font-family: var(--lybox-font-family);
}"""

let css =
    header
    + "\n"
    + lightBlock
    + "\n\n"
    + darkBlock
    + "\n\n"
    + systemBlock
    + "\n\n"
    + baseStyles
    + "\n"

File.WriteAllText(cssPath, css)

let count = tokens.Length
let themedCount = themed.Length
let staticCount = statics.Length
printfn "✅ 已从 tokens.json 生成 lybox-theme.css"
printfn "   令牌总数: %d（主题化 %d / 静态 %d）" count themedCount staticCount
printfn "   输出: %s" cssPath