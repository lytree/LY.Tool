using System.Text;
using Avalonia.Platform;
using Avalonia.Threading;
using LYBox.Platforms.Abstraction.Models;
using LYBox.Platforms.Abstraction.Services;
using LYBox.Platforms.Linux.DBus;
using Tmds.DBus.Protocol;

namespace LYBox.Platforms.Linux.Services;

public class DesktopToastService : IDesktopToastService
{
    private readonly Dictionary<string, Action> _activationActions = new();

    private readonly Dictionary<DesktopToastContent, List<string>> _activationActionIds = new();

    private const string NotificationsServiceName = "org.freedesktop.Notifications";

    private static readonly ObjectPath NotificationsObjectPath = new("/org/freedesktop/Notifications");

    private readonly Dictionary<uint, DesktopToastContent> _activeNotifications = new();
    private IDisposable? _notificationActionSubscription;
    private IDisposable? _notificationCloseSubscription;
    private DBusConnection? _connection;
    private IList<string> _capabilities = [];

    private Notifications? _proxy;

    public void Dispose()
    {
        _notificationActionSubscription?.Dispose();
        _notificationCloseSubscription?.Dispose();
    }

    public string? LaunchActionId { get; }

    public async Task Initialize()
    {
        // DBusAddress.Session 在无 session bus 的环境（如无 GUI 的服务器/容器）下返回 null。
        _connection = new DBusConnection(DBusAddress.Session ?? throw new InvalidOperationException("No D-Bus session bus available"));

        await _connection.ConnectAsync();

        // DBusService 表示总线上的一个对端，CreateNotifications 是源生成器基于 XML 接口
        // 名 org.freedesktop.Notifications 末段生成的扩展方法，返回 Notifications 代理实例。
        var service = new DBusService(_connection, NotificationsServiceName);
        _proxy = service.CreateNotifications(NotificationsObjectPath);

        // 0.91.1 生成的 Watch*Async 签名为 Action<Exception?, (T1, T2)>，
        // 第一个参数为异常（通常为 null），第二个为信号载荷元组。
        _notificationActionSubscription = await _proxy.WatchActionInvokedAsync(
            (ex, args) => OnNotificationActionInvoked((args.Id, args.ActionKey))
        ).ConfigureAwait(false);

        _notificationCloseSubscription = await _proxy.WatchNotificationClosedAsync(
            (ex, args) => OnNotificationClosed((args.Id, args.Reason))
        ).ConfigureAwait(false);

        _capabilities = await _proxy.GetCapabilitiesAsync().ConfigureAwait(false);
    }

    private async Task<string> GenerateBodyImage(Uri? imageUri)
    {
        // TODO: 在 kde 中向提醒里加入 <img/> 会导致图片大小异常，目前没有比较好的解决方案，
        // 先暂时禁用图片显示功能。
        return "";

        if (!_capabilities.Contains("body-images"))
        {
            return "";
        }

        var img = await PrepareToastImageResourceAsync(imageUri);
        if (img == null)
        {
            return "";
        }

        return $"""

                <img src="{img}" style="width: 100%; height: auto" width="100" alt=""/>

                """;
    }


    private async Task<string> GenerateNotificationBody(DesktopToastContent notification)
    {
        var sb = new StringBuilder();

        sb.Append(await GenerateBodyImage(notification.HeroImageUri));
        sb.Append(notification.Body);
        sb.Append(await GenerateBodyImage(notification.InlineImageUri));

        return sb.ToString();
    }

    private void OnNotificationClosed((uint id, uint reason) @event)
    {
        if (!_activeNotifications.Remove(@event.id, out var notification)) return;

        //TODO: Not sure why but it calls this event twice sometimes
        //In this case the notification has already been removed from the dict.
        if (notification == null)
        {
            return;
        }

        CleanupNotification(notification);
    }

    private void OnNotificationActionInvoked((uint id, string actionKey) @event) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!_activeNotifications.TryGetValue(@event.id, out var notification)) return;

            if (@event.actionKey == "default")
            {
                notification.Activated?.Invoke(this, EventArgs.Empty);
            }
            else if (_activationActions.TryGetValue(@event.actionKey, out var action))
            {
                action();
            }

            CleanupNotification(notification);
        });

    public async Task InitializeAsync()
    {
        await Initialize();
    }

    public async Task ShowToastAsync(DesktopToastContent content)
    {
        List<string> actions = [];
        List<string> actionsDbus = ["default", ""];

        var body = await GenerateNotificationBody(content);
        foreach (var (text, action) in content.Buttons)
        {
            var actionId = Guid.NewGuid().ToString();
            _activationActions[actionId] = action;
            actions.Add(actionId);
            actionsDbus.Add(actionId);
            actionsDbus.Add(text);
        }

        // urgency 是 BYTE 类型（0=low, 1=normal, 2=critical），按 FreeDesktop 规范使用 VariantValue.Byte。
        var hints = new Dictionary<string, VariantValue>
        {
            ["urgency"] = VariantValue.Byte(1)
        };

        var id = await _proxy!.NotifyAsync(
            "Avalonia",
            0,
            (await PrepareToastImageResourceAsync(content.LogoImageUri))?.AbsolutePath ?? string.Empty,
            content.Title,
            body,
            actionsDbus.ToArray(),
            hints,
            5_000
        ).ConfigureAwait(false);

        _activationActionIds[content] = actions;
        _activeNotifications[id] = content;
    }

    void CleanupNotification(DesktopToastContent toast)
    {
        if (!_activationActionIds.TryGetValue(toast, out var actions))
        {
            return;
        }
        foreach (var i in actions)
        {
            _activationActions.Remove(i);
        }

        _activationActionIds.Remove(toast);
    }

    public async Task ShowToastAsync(string title, string body, Action? activated = null)
    {
        var desktopToastContent = new DesktopToastContent()
        {
            Title = title,
            Body = body
        };
        desktopToastContent.Activated += (_, _) => activated?.Invoke();
        await ShowToastAsync(desktopToastContent);
    }

    private async Task<Uri?> PrepareToastImageResourceAsync(Uri? sourceUri)
    {
        if (sourceUri == null)
        {
            return null;
        }

        try
        {
            switch (sourceUri.Scheme)
            {
                case "file":
                    return new Uri(sourceUri.AbsolutePath);
                case "avares":
                {
                    var stream = AssetLoader.Open(sourceUri);
                    var imagePath = Path.GetTempFileName();
                    await using var fileStream = File.Create(imagePath);
                    await stream.CopyToAsync(fileStream);
                    return new Uri(imagePath);
                }
                case "http" or "https":
                {
                    var imagePath = Path.GetTempFileName();
                    var client = new HttpClient();
                    var stream = await client.GetStreamAsync(sourceUri);
                    await using var fileStream = File.Create(imagePath);
                    await stream.CopyToAsync(fileStream);
                    return new Uri(imagePath);
                }
            }
        }
        catch (Exception)
        {
            return null;
        }
        return null;
    }

    public void ActivateNotificationAction(Guid id)
    {

    }
}
