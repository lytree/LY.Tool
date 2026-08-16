using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class MetadataGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 单次收集所有类声明（含符号语义信息），为后续 O(N) 索引复用。
        // 避免对每个 [GenerateMetadata] 类重复遍历整个 Compilation 造成 O(N²)。
        var allClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) =>
                {
                    var classDecl = (ClassDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    return symbol is null
                        ? (FullName: (string?)null, Decl: (ClassDeclarationSyntax?)null)
                        : (FullName: (string?)symbol.ToDisplayString(), Decl: (ClassDeclarationSyntax?)classDecl);
                })
            .Where(x => x.Decl is not null)
            .Select((x, _) => (FullName: x.FullName!, Decl: x.Decl!))
            .Collect();

        var targetClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) =>
                {
                    var classDecl = (ClassDeclarationSyntax)ctx.Node;
                    var hasGenerateMetadata = classDecl.AttributeLists
                        .SelectMany(al => al.Attributes)
                        .Any(a => a.Name.ToString().Contains("GenerateMetadata"));
                    return hasGenerateMetadata
                        ? (Name: classDecl.Identifier.Text, Namespace: GetNamespace(classDecl))
                        : (Name: "", Namespace: "");
                })
            .Where(x => x.Name.Length > 0)
            .Select((x, _) => x);

        // 从 MSBuild 注入的 csproj 属性读取插件元数据（单一事实来源，见 O-1/O-8）。
        // 属性经 LYBox.Plugin.Shared.props 中的 <CompilerVisibleProperty> 暴露为 build_property.*。
        var metadata = context.AnalyzerConfigOptionsProvider
            .Select((provider, _) => new PluginMetadataInfo(
                GetMsBuildProperty(provider, "PluginName"),
                GetMsBuildProperty(provider, "PluginVersion"),
                GetMsBuildProperty(provider, "PluginAuthor"),
                GetMsBuildProperty(provider, "PluginDescription"),
                GetMsBuildProperty(provider, "PluginId"),
                GetMsBuildProperty(provider, "MinPluginSdkVersion"),
                GetMsBuildProperty(provider, "PluginKind"),
                GetMsBuildProperty(provider, "PluginWwwroot"),
                GetMsBuildProperty(provider, "PluginEntryPage")));

        var combined = targetClasses.Combine(allClasses).Combine(metadata);

        context.RegisterSourceOutput(combined, (ctx, data) =>
        {
            var ((target, allClassInfos), meta) = data;

            // 建立全限定名 → 类声明的索引，供 GetFullTypeName 精确匹配。
            var fullNameToClass = new Dictionary<string, ClassDeclarationSyntax>(StringComparer.Ordinal);
            foreach (var c in allClassInfos)
            {
                fullNameToClass.TryAdd(c.FullName, c.Decl);
            }

            var viewLines = new StringBuilder();
            var navLines = new StringBuilder();
            var menuData = new List<(string Header, string Key, string? Parent, string? IconName, string? Status, int Order)>();

            foreach (var c in allClassInfos)
            {
                var cls = c.Decl;
                var vmName = cls.Identifier.Text;
                var vmNs = GetNamespace(cls);
                var fullVmName = $"{vmNs}.{vmName}";

                if (TryGetAttr(cls, "ViewMap", out var vAttr))
                {
                    var vTypeShort = GetArg(vAttr!, 0);
                    var vTypeFull = GetFullTypeName(vTypeShort, fullNameToClass);
                    viewLines.AppendLine($"        yield return new KeyValuePair<Type, ViewFactory>(typeof({fullVmName}), () => new {vTypeFull}());");
                }

                if (TryGetAttr(cls, "NavigationItem", out var nAttr))
                {
                    var navKey = GetArg(nAttr!, 0);
                    navLines.AppendLine($"        {{ {navKey}, () => new {fullVmName}() }},");
                }

                if (TryGetAttr(cls, "Menu", out var mAttr))
                {
                    menuData.Add(ParseMenu(mAttr!));
                }
            }

            var ns = target.Namespace;
            var className = target.Name;
            var menuAddLines = GenerateMenuAddStatements(menuData);

            var metaLines = new StringBuilder();
            metaLines.AppendLine($"        public string Name => {Str(meta.Name ?? className)};");
            metaLines.AppendLine($"        public string Version => {Str(meta.Version ?? "1.0.0")};");
            metaLines.AppendLine($"        public string Author => {Str(meta.Author ?? string.Empty)};");
            metaLines.AppendLine($"        public string Description => {Str(meta.Description ?? string.Empty)};");
            metaLines.AppendLine($"        public string PluginId => {Str(meta.PluginId ?? className)};");
            metaLines.AppendLine($"        public string MinPluginSdkVersion => {Str(meta.MinSdkVersion ?? "0.0.0")};");

            // 仅 Web 插件（csproj PluginKind=Web）生成 IWebPlugin.Web 描述符（S2 BC-2）。
            var baseInterfaces = "IPlugin, IPluginMetadata";
            var webLines = new StringBuilder();
            var webUsing = string.Empty;
            if (string.Equals(meta.Kind, "Web", StringComparison.OrdinalIgnoreCase))
            {
                baseInterfaces += ", IWebPlugin";
                webUsing = "using LYBox.Plugin.Shared.Web;\r\n";
                webLines.AppendLine($"        public IWebPluginDescriptor Web => new WebPluginDescriptor(");
                webLines.AppendLine($"            {Str(meta.Wwwroot ?? "wwwroot")}, {Str(meta.EntryPage ?? "index.html")});");
            }

            var source = $@"// <auto-generated />
#nullable enable
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.ViewModels;
{webUsing}using Microsoft.Extensions.DependencyInjection;

namespace {ns}
{{
    public partial class {className} : {baseInterfaces}
    {{
{metaLines}
{webLines}        public IEnumerable<KeyValuePair<Type, ViewFactory>> GetViewDefinitions()
        {{
{viewLines}
        }}

        public Dictionary<string, ViewModelFactory> GetNavigationItems() => new Dictionary<string, ViewModelFactory>
        {{
{navLines}
        }};

        public List<KeyValuePair<string?, MenuItemViewModel>> GetMenuItems()
        {{
            var allItems = new List<(string? Parent, MenuItemViewModel Item, int Order)>();
{menuAddLines}
            return MenuItemTreeBuilder.BuildTree(allItems);
        }}
    }}
}}";
            ctx.AddSource($"{className}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static string GenerateMenuAddStatements(List<(string Header, string Key, string? Parent, string? IconName, string? Status, int Order)> data)
    {
        var sb = new StringBuilder();
        foreach (var d in data)
        {
            var iconNameProp = d.IconName != null ? $", MenuIconName = {d.IconName}" : "";
            sb.AppendLine($@"            allItems.Add(({d.Parent ?? "null"}, new MenuItemViewModel {{ MenuHeader = {d.Header}, Key = {d.Key}{iconNameProp}, Status = {d.Status ?? "null"}, Order = {d.Order} }}, {d.Order}));");
        }
        return sb.ToString();
    }

    private static bool TryGetAttr(ClassDeclarationSyntax cls, string attrName, out AttributeSyntax? attr)
    {
        attr = cls.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString().Contains(attrName));

        return attr != null;
    }

    private static string GetArg(AttributeSyntax attr, int index)
    {
        if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count <= index)
            return "null";

        var arg = attr.ArgumentList.Arguments[index].Expression;

        if (arg is TypeOfExpressionSyntax typeofExp)
            return typeofExp.Type.ToString();

        return arg.ToString();
    }

    private static string GetNamespace(ClassDeclarationSyntax cls) =>
        (cls.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()) ?? "Global";

    private static string? GetMsBuildProperty(AnalyzerConfigOptionsProvider provider, string name) =>
        provider.GlobalOptions.TryGetValue($"build_property.{name}", out var value) ? value : null;

    private static string Str(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private sealed class PluginMetadataInfo
    {
        public PluginMetadataInfo(string? name, string? version, string? author, string? description, string? pluginId, string? minSdkVersion, string? kind, string? wwwroot, string? entryPage)
        {
            Name = name;
            Version = version;
            Author = author;
            Description = description;
            PluginId = pluginId;
            MinSdkVersion = minSdkVersion;
            Kind = kind;
            Wwwroot = wwwroot;
            EntryPage = entryPage;
        }

        public string? Name { get; }
        public string? Version { get; }
        public string? Author { get; }
        public string? Description { get; }
        public string? PluginId { get; }
        public string? MinSdkVersion { get; }
        public string? Kind { get; }
        public string? Wwwroot { get; }
        public string? EntryPage { get; }
    }

    private static string GetFullTypeName(string shortName, Dictionary<string, ClassDeclarationSyntax> fullNameToClass)
    {
        // 优先精确匹配全限定名，避免跨命名空间同名类误配。
        foreach (var (fullName, cls) in fullNameToClass)
        {
            if (cls.Identifier.Text == shortName)
            {
                return $"{GetNamespace(cls)}.{shortName}";
            }
        }
        return shortName;
    }

    private static (string Header, string Key, string? Parent, string? IconName, string? Status, int Order)
    ParseMenu(AttributeSyntax attr)
    {
        string header = "null";
        string key = "null";
        string? parent = "null";
        string? iconName = null;
        string? status = "null";
        int order = 100;

        if (attr.ArgumentList != null)
        {
            var args = attr.ArgumentList.Arguments;

            if (args.Count >= 1 && args[0].NameEquals == null)
                header = args[0].Expression.ToString();

            if (args.Count >= 2 && args[1].NameEquals == null)
                key = args[1].Expression.ToString();

            if (args.Count >= 3 && args[2].NameEquals == null)
                parent = args[2].Expression.ToString();

            foreach (var arg in args.Where(a => a.NameEquals != null))
            {
                var name = arg.NameEquals!.Name.Identifier.Text;
                var expression = arg.Expression.ToString();

                switch (name)
                {
                    case "Header": header = expression; break;
                    case "Key": key = expression; break;
                    case "ParentKey": parent = expression; break;
                    case "IconName": iconName = expression; break;
                    case "Status": status = expression; break;
                    case "Order":
                        if (!int.TryParse(expression, out order)) order = 100;
                        break;
                }
            }
        }

        return (header, key, parent, iconName, status, order);
    }
}
