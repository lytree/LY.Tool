# 闪屏卡死修复 + FluentWindow/旧布局拆分为独立启动项目

## 概述

当前分支存在两个问题：
1. **Host App 启动后卡在闪屏界面**，无法过渡到主窗口
2. 需要将 FluentWindow 布局和旧 UrsaWindow 布局拆分为两个独立可运行的启动项目

---

## 第一部分：闪屏卡死根因分析

### 根因链（3 层问题叠加）

| # | 文件 | 行号 | 问题 | 影响 |
|---|------|------|------|------|
| 1 | `App.axaml.cs` | L60 | `OnUIThreadUnhandledException` 中 `e.Handled = true` 吞掉所有 UI 线程异常 | 闪屏过渡中的任何异常被静默吞掉，无崩溃无报错 |
| 2 | `MvvmSplashWindow.axaml.cs` | L52 | `Dispatcher.Post(async () => {...})` 是 `async void`，异常直接进 Dispatcher | `CreateNextWindow()` / `MainWindow()` 构造 / `MainViewViewModel()` 构造中的异常被吞掉 |
| 3 | `MvvmSplashWindow.axaml` | L15 | `CountDown="{x:Null}"` 禁用了 Ursa 内置的自动过渡机制 | 过渡完全依赖手动订阅 `RequestClose`，无后备路径 |
| 4 | `MvvmSplashWindow.axaml.cs` | L32+L36 | `base.OnDataContextChanged(e)` 可能导致 Ursa 2.1.x 基类也订阅 `RequestClose`，造成双重订阅 | 两个处理器各创建一个 MainWindow，第二个对已关闭的 splash 调用 `Close()` 抛异常 |
| 5 | `App.axaml.cs` | L75,78,98 | `Initialize()` 中三处 `.GetAwaiter().GetResult()` 同步阻塞 UI 线程 | 闪屏显示前 UI 已冻结数秒 |
| 6 | `MainWindow.axaml` | L15 | `x:DataType="MainWindowViewModel"` 与实际 DataContext `MainViewViewModel` 不匹配 | 编译绑定可能生成错误代码 |

### 最可能的发生顺序

```
App.Initialize() → 同步阻塞（插件加载）→ OnFrameworkInitializationCompleted()
→ 创建 MvvmSplashWindow + SplashViewModel → 闪屏显示，进度从 0→100%
→ RequestClose 触发 → OnSplashRequestClose → Dispatcher.Post(async () => ...)
→ CreateNextWindow() → new MainWindow() → InitializeComponent()
→ 某处抛异常（可能是 XAML 资源缺失、UrsaWindow 初始化变更、ControlTheme 解析失败）
→ 异常进入 async void → Dispatcher → OnUIThreadUnhandledException → e.Handled = true
→ 闪屏停在 100%，永不过渡
```

---

## 第二部分：修复方案

### 2.1 修复闪屏过渡异常处理

**文件**: `src/LYBox.UrsaWindow/Views/MvvmSplashWindow.axaml.cs`

- 移除 `base.OnDataContextChanged(e)` 调用中的双重订阅风险：不调用 base，或检查是否已订阅
- 在 `Dispatcher.Post` 的 async lambda 中添加 `try/catch`，捕获异常后输出到日志并 fallback 直接显示 MainWindow
- 添加 `_transitioned` 守卫的原子性确保

```csharp
private void OnSplashRequestClose(object? sender, object? e)
{
    if (_transitioned) return;
    _transitioned = true;

    if (DataContext is SplashViewModel vm)
        vm.RequestClose -= OnSplashRequestClose;

    Dispatcher.Post(async () =>
    {
        try
        {
            var nextWindow = await CreateNextWindow();
            if (nextWindow is not null)
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.MainWindow = nextWindow;
                nextWindow.Show();
                Close();
            }
        }
        catch (Exception ex)
        {
            // 过渡失败时记录异常并尝试直接显示主窗口
            System.Diagnostics.Debug.WriteLine($"Splash transition failed: {ex}");
            try
            {
                var navSvc = ServiceLocator.GetService<INavigationService>();
                var menuSvc = ServiceLocator.GetService<IMenuConfigurationService>();
                var fallback = new MainWindow
                {
                    DataContext = new MainViewViewModel(navSvc!, menuSvc!)
                };
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.MainWindow = fallback;
                fallback.Show();
                Close();
            }
            catch
            {
                // 最终 fallback 也失败，至少不永远卡在闪屏
                Close();
            }
        }
    });
}
```

### 2.2 修复异常吞掉问题

**文件**: `src/launcher/LYBox.Launcher.Desktop/App.axaml.cs`

- 在 `OnUIThreadUnhandledException` 中添加 DEBUG 条件编译：DEBUG 模式下不吞异常（让崩溃暴露问题），RELEASE 模式下保持吞掉但记录日志
- 添加 `Console.Error.WriteLine` 作为日志文件之外的备用输出

```csharp
private static void OnUIThreadUnhandledException(object? sender, Avalonia.Threading.DispatcherUnhandledExceptionEventArgs e)
{
    LogGlobalException("UIThreadUnhandledException", e.Exception);
    Console.Error.WriteLine($"[UIThreadUnhandledException] {e.Exception}");
#if DEBUG
    // DEBUG 模式下不吞异常，让问题暴露
    e.Handled = false;
#else
    e.Handled = true;
#endif
}
```

### 2.3 修复 x:DataType 不匹配

**文件**: `src/LYBox.UrsaWindow/Views/MainWindow.axaml`

- 将 `x:DataType="viewModels:MainWindowViewModel"` 改为 `x:DataType="viewModels:MainViewViewModel"`

### 2.4 移除 `CountDown="{x:Null}"` 或设置合理倒计时

**文件**: `src/LYBox.UrsaWindow/Views/MvvmSplashWindow.axaml`

- 移除 `CountDown="{x:Null}"`，改为设置 `CountDown="5000"`（5秒后备过渡），确保即使 `RequestClose` 订阅失败，Ursa 基类的倒计时也能触发 `CreateNextWindow()`

---

## 第三部分：FluentWindow 与旧布局拆分为独立启动项目

### 当前状态

两个项目已经是结构独立的：
- `LYBox.FluentWindow`（`Exe`）— 仅引用 Avalonia + CommunityToolkit.Mvvm，零插件耦合
- `LYBox.Launcher.Desktop`（`WinExe`）+ `LYBox.UrsaWindow`（`Library`）— 完整插件系统

### 需要的改进

#### 3.1 为 FluentWindow 项目添加 `AvaloniaUseCompiledBindingsByDefault`

**文件**: `src/LYBox.FluentWindow/LYBox.FluentWindow.csproj`

添加 `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`

#### 3.2 为 FluentWindow 项目添加 `ApplicationIcon`

**文件**: `src/LYBox.FluentWindow/LYBox.FluentWindow.csproj`

添加 `<ApplicationIcon>Assets\lybox.ico</ApplicationIcon>`（需要先确认 Assets 目录中是否有 ico 文件，如果没有则跳过）

#### 3.3 确保 launch.json 多布局调试配置完整

当前已有 6 个布局调试配置（3 Host + 3 FluentWindow），已满足需求。无需额外修改。

#### 3.4 确认解决方案包含两个独立启动项目

`Core.slnx` 已包含两个项目，无需修改。

---

## 第四部分：验证步骤

1. **构建验证**：
   - `dotnet build Core.slnx` — 0 错误
   - `dotnet build src/LYBox.FluentWindow/LYBox.FluentWindow.csproj` — 0 错误

2. **闪屏修复验证**：
   - `dotnet run --project src/launcher/LYBox.Launcher.Desktop` — 闪屏应显示后过渡到主窗口
   - `dotnet run --project src/launcher/LYBox.Launcher.Desktop -- --no-splash` — 直接显示主窗口
   - 检查 `logs/` 目录下是否有 UIThreadUnhandledException 日志

3. **FluentWindow 独立运行验证**：
   - `dotnet run --project src/LYBox.FluentWindow` — 标准布局
   - `dotnet run --project src/LYBox.FluentWindow -- --acrylic` — Acrylic 布局
   - `dotnet run --project src/LYBox.FluentWindow -- --dialog` — 对话框模式

---

## 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `src/LYBox.UrsaWindow/Views/MvvmSplashWindow.axaml.cs` | 修改 | 添加 try/catch + fallback 过渡逻辑 |
| `src/LYBox.UrsaWindow/Views/MvvmSplashWindow.axaml` | 修改 | 移除 `CountDown="{x:Null}"`，设置后备倒计时 |
| `src/LYBox.UrsaWindow/Views/MainWindow.axaml` | 修改 | 修复 `x:DataType` 为 `MainViewViewModel` |
| `src/launcher/LYBox.Launcher.Desktop/App.axaml.cs` | 修改 | DEBUG 模式下不吞异常 |
| `src/LYBox.FluentWindow/LYBox.FluentWindow.csproj` | 修改 | 添加 `AvaloniaUseCompiledBindingsByDefault` |
