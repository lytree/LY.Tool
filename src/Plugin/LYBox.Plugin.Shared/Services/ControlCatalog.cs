using LYBox.Plugin.Shared.Models;

namespace LYBox.Plugin.Shared.Services;

/// <summary>
/// 控件目录单一事实来源（O-10）。
/// 原 ButtonsInputs 的 AutoCompleteBox / MultiAutoCompleteBox 两个演示 ViewModel 各维护一份
/// 60+ 项的重复演示表，任一插件增删控件需手工同步两处。此处收敛为共享静态目录，
/// 各演示 ViewModel 统一从此消费，消除跨插件的硬编码重复。
/// </summary>
public static class ControlCatalog
{
    /// <summary>获取全部控件演示项（中英文标题）。返回只读快照，调用方不应修改。</summary>
    public static IReadOnlyList<ControlData> GetAll() => All;

    private static readonly ControlData[] All =
    [
        new() { MenuHeader = "Button Group", Chinese = "按钮组" },
        new() { MenuHeader = "Icon Button", Chinese = "图标按钮" },
        new() { MenuHeader = "AutoCompleteBox", Chinese = "自动完成框" },
        new() { MenuHeader = "Class Input", Chinese = "类输入框" },
        new() { MenuHeader = "Enum Selector", Chinese = "枚举选择器" },
        new() { MenuHeader = "Form", Chinese = "表单" },
        new() { MenuHeader = "KeyGestureInput", Chinese = "快捷键输入" },
        new() { MenuHeader = "IPv4Box", Chinese = "IPv4输入框" },
        new() { MenuHeader = "MultiComboBox", Chinese = "多选组合框" },
        new() { MenuHeader = "Multi AutoCompleteBox", Chinese = "多项自动完成框" },
        new() { MenuHeader = "Numeric UpDown", Chinese = "数字上下调节" },
        new() { MenuHeader = "NumPad", Chinese = "数字键盘" },
        new() { MenuHeader = "PathPicker", Chinese = "路径选择器" },
        new() { MenuHeader = "PinCode", Chinese = "密码输入" },
        new() { MenuHeader = "RangeSlider", Chinese = "范围滑块" },
        new() { MenuHeader = "Rating", Chinese = "评分" },
        new() { MenuHeader = "Selection List", Chinese = "选择列表" },
        new() { MenuHeader = "TagInput", Chinese = "标签输入" },
        new() { MenuHeader = "Theme Toggler", Chinese = "主题切换" },
        new() { MenuHeader = "TreeComboBox", Chinese = "树形组合框" },
        new() { MenuHeader = "Dialog", Chinese = "对话框" },
        new() { MenuHeader = "Drawer", Chinese = "抽屉" },
        new() { MenuHeader = "Loading", Chinese = "加载" },
        new() { MenuHeader = "Message Box", Chinese = "消息框" },
        new() { MenuHeader = "Notification", Chinese = "通知" },
        new() { MenuHeader = "PopConfirm", Chinese = "气泡确认" },
        new() { MenuHeader = "Toast", Chinese = "吐司" },
        new() { MenuHeader = "Skeleton", Chinese = "骨架屏" },
        new() { MenuHeader = "Date Picker", Chinese = "日期选择器" },
        new() { MenuHeader = "Date Range Picker", Chinese = "日期范围选择器" },
        new() { MenuHeader = "Date Time Picker", Chinese = "日期时间选择器" },
        new() { MenuHeader = "Time Box", Chinese = "时间输入框" },
        new() { MenuHeader = "Time Picker", Chinese = "时间选择器" },
        new() { MenuHeader = "Time Range Picker", Chinese = "时间范围选择器" },
        new() { MenuHeader = "Clock", Chinese = "时钟" },
        new() { MenuHeader = "Anchor", Chinese = "锚点" },
        new() { MenuHeader = "Breadcrumb", Chinese = "面包屑" },
        new() { MenuHeader = "Nav Menu", Chinese = "导航菜单" },
        new() { MenuHeader = "Pagination", Chinese = "分页" },
        new() { MenuHeader = "ToolBar", Chinese = "工具栏" },
        new() { MenuHeader = "AspectRatioLayout", Chinese = "宽高比布局" },
        new() { MenuHeader = "Avatar", Chinese = "头像" },
        new() { MenuHeader = "Badge", Chinese = "徽章" },
        new() { MenuHeader = "Banner", Chinese = "横幅" },
        new() { MenuHeader = "Disable Container", Chinese = "禁用容器" },
        new() { MenuHeader = "Divider", Chinese = "分割线" },
        new() { MenuHeader = "DualBadge", Chinese = "双徽章" },
        new() { MenuHeader = "ImageViewer", Chinese = "图片查看器" },
        new() { MenuHeader = "ElasticWrapPanel", Chinese = "弹性换行面板" },
        new() { MenuHeader = "Marquee", Chinese = "跑马灯" },
        new() { MenuHeader = "Number Displayer", Chinese = "数字显示器" },
        new() { MenuHeader = "Scroll To", Chinese = "滚动到按钮" },
        new() { MenuHeader = "Timeline", Chinese = "时间轴" },
        new() { MenuHeader = "TwoTonePathIcon", Chinese = "双色路径图标" }
    ];
}