using System.Threading.Tasks;
using LYBox.Plugin.BTSou.Models;
using LYBox.Plugin.BTSou.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.BTSou.ViewModels;

/// <summary>
/// BTSOU 举报页。对应原程序举报窗口（类 g）：
/// 提交违法资源举报信息（标题/磁链/实名/联系方式/危害类别/描述）。
/// </summary>
[NavigationItem("BTSou_Report")]
[Menu("NAV_BTSou_Report", "BTSou_Report", ParentKey = "NAV_BTSou", Order = 2)]
[ViewMap(typeof(Pages.ReportPage))]
public partial class ReportViewModel : ViewModelBase
{
    private readonly BTSouDatabaseService _db;

    public string[] Categories { get; } = ["色情", "赌博", "诈骗", "毒品", "侵权", "其他"];

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _magnetLink = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _idCard = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _statusText = "";

    /// <summary>生成器要求公共无参构造函数，服务经静态单例访问。</summary>
    public ReportViewModel()
    {
        _db = BTSouDatabaseService.Current;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        var entry = new ReportEntry
        {
            HardDiskSerial = BTSouDatabaseService.GetHardDiskSerial(),
            Title = Title,
            MagnetLink = MagnetLink,
            Name = Name,
            Email = Email,
            Phone = Phone,
            IdCard = IdCard,
            Category = Category,
            Description = Description
        };

        var error = entry.Validate();
        if (error != null)
        {
            StatusText = error;
            return;
        }

        try
        {
            await _db.SubmitReportAsync(entry);
            StatusText = "举报提交成功，等待处理中！";
        }
        catch
        {
            StatusText = "网络故障，请稍后再试！";
        }
    }
}
