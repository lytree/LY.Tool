using System.Globalization;
using System.Resources;

namespace LYBox.Plugin.Shared.Services;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }

    string GetString(string key);

    string GetString(string key, string fallback);

    string GetString(string key, params object[] args);

    void SetCulture(CultureInfo culture);

    void RegisterResourceManager(ResourceManager manager, string prefix = "");

    /// <summary>
    /// 进入批量注册模式：期间 <see cref="RegisterResourceManager"/> 只更新字典，延迟缓存重建，
    /// 直到调用 <see cref="EndBatchRegistration"/> 后统一重建一次，避免启动期逐插件全量重建。
    /// 必须与 <see cref="EndBatchRegistration"/> 成对调用（可用 try/finally 保证）。
    /// </summary>
    void BeginBatchRegistration();

    /// <summary>
    /// 结束批量注册模式。若此前进入了批量模式，此处触发一次统一缓存重建。
    /// </summary>
    void EndBatchRegistration();

    event EventHandler<CultureInfo>? CultureChanged;
}
