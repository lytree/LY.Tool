using Avalonia.Plugin.ProDataGrid.CustomDrawing;
using Avalonia.Plugin.ProDataGrid.Pages;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyCustomDrawingLiveUpdates")]
[Menu("NAV_CustomDrawingLiveUpdates", "KeyCustomDrawingLiveUpdates", "NAV_ProDataGrid")]
[ViewMap(typeof(CustomDrawingLiveUpdatesPage))]
public partial class CustomDrawingLiveUpdatesViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();
    private SkiaAnimatedTextCellDrawOperationFactory? _factory;
    private bool _isRunning;
    private int _intervalMs = 33;
    private long _frameCount;
    private float _phase;

    public CustomDrawingLiveUpdatesViewModel()
    {
        Rows = [];
        StartCommand = new RelayCommand(Start, () => !IsRunning);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        ResetRowsCommand = new RelayCommand(ResetRows);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_intervalMs)
        };
        _timer.Tick += (_, __) => Tick();

        ResetRows();
    }

    public ObservableCollection<CustomDrawingLiveUpdatesRow> Rows { get; }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ResetRowsCommand { get; }

    [ObservableProperty] private bool _isRunningField;

    partial void OnIsRunningFieldChanged(bool value)
    {
        IsRunning = value;
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(RunState));
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                IsRunningField = value;
                StartCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(RunState));
            }
        }
    }

    [ObservableProperty] private int _intervalMsField = 33;

    partial void OnIntervalMsFieldChanged(int value)
    {
        int next = Math.Max(1, value);
        IntervalMs = next;
    }

    public int IntervalMs
    {
        get => _intervalMs;
        set
        {
            int next = Math.Max(1, value);
            if (SetProperty(ref _intervalMs, next))
            {
                _timer.Interval = TimeSpan.FromMilliseconds(next);
            }
        }
    }

    [ObservableProperty] private long _frameCountField;

    public long FrameCount
    {
        get => _frameCount;
        private set => SetProperty(ref _frameCount, value);
    }

    public float Phase
    {
        get => _phase;
        private set => SetProperty(ref _phase, value);
    }

    public string RunState => IsRunning ? "运行中" : "已停止";

    public void AttachFactory(SkiaAnimatedTextCellDrawOperationFactory? factory)
    {
        _factory = factory;
        if (_factory is not null)
        {
            _factory.SetPhase(Phase);
        }
    }

    public void OnAttached() => Start();
    public void OnDetached() => Stop();

    private void Start()
    {
        if (IsRunning) return;
        _timer.Interval = TimeSpan.FromMilliseconds(_intervalMs);
        _timer.Start();
        IsRunning = true;
    }

    private void Stop()
    {
        if (!IsRunning) return;
        _timer.Stop();
        IsRunning = false;
    }

    private void ResetRows()
    {
        FrameCount = 0;
        Phase = 0f;

        Rows.Clear();
        for (int i = 0; i < 300; i++)
        {
            Rows.Add(new CustomDrawingLiveUpdatesRow
            {
                Id = i + 1,
                Symbol = $"SYM{i % 100:D3}",
                Message = CreateMessage(i)
            });
        }

        _factory?.SetPhaseAndInvalidate(Phase);
    }

    private void Tick()
    {
        Phase += 0.085f;
        FrameCount++;
        _factory?.SetPhaseAndInvalidate(Phase);
    }

    private string CreateMessage(int index)
    {
        string[] words =
        [
            "延迟", "指标", "渲染", "单元格", "缓存", "虚拟化",
            "前景色", "选择", "测量", "排列", "失效"
        ];

        int wordA = _random.Next(words.Length);
        int wordB = _random.Next(words.Length);
        int wordC = _random.Next(words.Length);

        return $"第 {index + 1} 行: {words[wordA]} {words[wordB]} {words[wordC]} 脉冲流";
    }
}

public sealed class CustomDrawingLiveUpdatesRow
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
