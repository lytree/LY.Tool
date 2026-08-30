namespace LYBox.Plugin.TDLSharp.ViewModels;

/// <summary>
/// 统一处理脚本参数解析（<see cref="Dictionary{TKey, TValue}"/> → 强类型值）。
/// 取代 VM 中重复的 <c>bool.TryParse / int.TryParse / GetValueOrDefault</c> 模板。
/// </summary>
public sealed class ScriptParameterBag(Dictionary<string, string> raw)
{
    readonly Dictionary<string, string> _raw = raw;

    public string GetString(string key, string @default = "")
        => _raw.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : @default;

    public bool GetBool(string key, bool @default = false)
        => bool.TryParse(_raw.GetValueOrDefault(key), out var v) ? v : @default;

    public int GetInt(string key, int @default = 0)
        => int.TryParse(_raw.GetValueOrDefault(key), out var v) ? v : @default;
}
