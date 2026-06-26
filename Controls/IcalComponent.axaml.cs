using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Shared;
using iCalClassIsland.Models;
using iCalClassIsland.Services;

namespace iCalClassIsland.Controls;

[ComponentInfo("F8B3A2D1-7C54-4E90-9F12-3A8D6B1C5E07", "iCal 日程", "", "显示 iCal 文件中当天的日程安排。")]
public partial class IcalComponent : ComponentBase<IcalComponentSettings>
{
    private readonly IExactTimeService _exactTimeService;
    private DispatcherTimer? _timer;
    private int _tickCounter;
    private List<IcalCalendarEvent> _todayEvents = [];
    private bool _hasEvents;

    private readonly Dictionary<IcalCalendarEvent, EventRowControls> _rowControls = [];

    public bool HasEvents
    {
        get => _hasEvents;
        set { if (value == _hasEvents) return; _hasEvents = value; OnPropertyChanged(); }
    }

    public IcalComponent(IExactTimeService exactTimeService)
    {
        _exactTimeService = exactTimeService;
        DataContext = this;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    private void OnAttached(object? s, VisualTreeAttachmentEventArgs e)
    {
        // 订阅配置变更
        var plugin = IAppHost.TryGetService<Plugin>();
        if (plugin != null) plugin.ConfigChanged += OnConfigChanged;

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, OnTick);
        _timer.Start();
        RefreshEvents();
    }

    private void OnDetached(object? s, VisualTreeAttachmentEventArgs e)
    {
        var plugin = IAppHost.TryGetService<Plugin>();
        if (plugin != null) plugin.ConfigChanged -= OnConfigChanged;
        _timer?.Stop();
        _timer = null;
    }

    private void OnConfigChanged() => Dispatcher.UIThread.Post(RefreshEvents);

    private void OnTick(object? s, EventArgs e)
    {
        _tickCounter++;
        if (_tickCounter % 600 == 0) { RefreshEvents(); return; }          // 30s 刷新
        if (_todayEvents.Count == 0 && _tickCounter % 40 == 0) { RefreshEvents(); return; } // 无事件时 2s 重试
        UpdateDisplay();
    }

    private IcalComponentSettings S => Settings; // 简写

    private void RefreshEvents()
    {
        try
        {
            var svc = IAppHost.TryGetService<IcalService>();
            var p = IAppHost.TryGetService<Plugin>()?.PluginSettings;
            _todayEvents = (svc != null && p != null && !string.IsNullOrWhiteSpace(p.IcalFilePath))
                ? svc.GetTodayEvents(p.IcalFilePath, _exactTimeService.GetCurrentLocalDateTime()) : [];
        }
        catch { _todayEvents = []; }

        var now = _exactTimeService.GetCurrentLocalDateTime();
        var allEnded = _todayEvents.Count > 0 && _todayEvents.All(e => now >= e.End);

        if (_todayEvents.Count == 0)
        {
            // 无事件
            HasEvents = false;
            EventRow.IsVisible = false;
            Placeholder.IsVisible = true;
            var p = IAppHost.TryGetService<Plugin>()?.PluginSettings;
            if (p != null && !string.IsNullOrWhiteSpace(p.IcalFilePath) && !File.Exists(p.IcalFilePath))
                Placeholder.Text = $"找不到文件: {Path.GetFileName(p.IcalFilePath)}";
            else
                Placeholder.Text = S.ShowPlaceholderOnEmpty ? S.PlaceholderTextNoEvents : "";
        }
        else if (allEnded)
        {
            // 今日全部结束
            HasEvents = false;
            EventRow.IsVisible = false;
            Placeholder.IsVisible = true;
            Placeholder.Text = S.ShowPlaceholderOnEmpty ? S.PlaceholderTextAllEnded : "";
        }
        else
        {
            // 有事件
            HasEvents = true;
            EventRow.IsVisible = true;
            Placeholder.IsVisible = false;
        }

        RebuildEventRows();
        UpdateDisplay();
    }

    private void RebuildEventRows()
    {
        _rowControls.Clear();
        EventRow.Children.Clear();
        EventRow.ColumnDefinitions.Clear();

        for (int i = 0; i < _todayEvents.Count; i++)
        {
            var ctrl = BuildEventRow(_todayEvents[i]);
            _rowControls[_todayEvents[i]] = ctrl;

            EventRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(ctrl.Root, i);
            EventRow.Children.Add(ctrl.Root);
        }
    }

    /// <summary>
    /// 构建单个事件行（复制 LessonControlExpanded 布局）
    /// </summary>
    private EventRowControls BuildEventRow(IcalCalendarEvent evt)
    {
        // 根 Grid（进度条叠加用）
        var root = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // ---- 水平行 ----
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // 左侧间距
        row.Children.Add(new Border { Width = 16 });

        // 标题
        var titleBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = evt.Summary
        };
        titleBlock.Bind(TextBlock.FontSizeProperty, new Avalonia.Data.Binding
        {
            Source = this,
            Path = "MainWindowEmphasizedFontSize",
            Mode = Avalonia.Data.BindingMode.OneWay,
            // Just use dynamic resource
        });
        // 简化：直接用资源键
        var appResources = Application.Current?.Resources;
        titleBlock.SetValue(TextBlock.FontSizeProperty,
            (appResources?.TryGetValue("MainWindowEmphasizedFontSize", out var fs) == true ? fs as double? : null) ?? 16.0);
        titleBlock.SetValue(TextBlock.FontWeightProperty, FontWeight.Bold);

        var highlightBox = new HighlightBox { Content = titleBlock };
        var titleGrid = new Grid();
        titleGrid.Children.Add(highlightBox);
        row.Children.Add(titleGrid);

        // 额外信息区
        var extraInfoPanel = new Panel { Margin = new Thickness(6, 0, 0, 0) };

        // 时间范围文本
        var timeBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Text = $"{evt.Start:HH:mm}"
        };
        timeBlock.SetValue(TextBlock.FontSizeProperty,
            (appResources?.TryGetValue("MainWindowSecondaryFontSize", out var fs2) == true ? fs2 as double? : null) ?? 12.0);
        if (appResources?.TryGetValue("MaterialDesignBodySecondaryBrush", out var fg) == true && fg is IBrush brush)
            timeBlock.SetValue(TextBlock.ForegroundProperty, brush);
        extraInfoPanel.Children.Add(timeBlock);

        row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, Children = { extraInfoPanel } });

        // 右侧间距
        row.Children.Add(new Border { Width = 16 });

        root.Children.Add(row);

        // ---- 进度条（叠加在底部） ----
        var progress = new ProgressBar { Height = 3, Minimum = 0, Maximum = 1, Value = 0, MinWidth = 0 };
        var canvas = new Canvas
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Height = 3,
            Margin = new Avalonia.Thickness(0, 0, 0, 0)
        };
        // 进度条宽度跟随 Canvas 实际宽度
        progress.Bind(ProgressBar.WidthProperty, new Avalonia.Data.Binding("Bounds.Width")
        {
            Source = canvas,
            Mode = Avalonia.Data.BindingMode.OneWay
        });
        canvas.Children.Add(progress);
        root.Children.Add(canvas);

        return new EventRowControls
        {
            Root = root,
            TitleBlock = titleBlock,
            TimeBlock = timeBlock,
            ProgressBar = progress,
        };
    }

    private void UpdateDisplay()
    {
        var now = _exactTimeService.GetCurrentLocalDateTime();

        for (int i = 0; i < _todayEvents.Count; i++)
        {
            var evt = _todayEvents[i];
            if (!_rowControls.TryGetValue(evt, out var ctrl)) continue;

            var isPast = evt.End <= now;
            var isCurrent = evt.Start <= now && now < evt.End;

            var totalSec = (evt.End - evt.Start).TotalSeconds;
            var elapsedSec = (now - evt.Start).TotalSeconds;
            var leftSec = (evt.End - now).TotalSeconds;

            // 透明度 & 背景
            ctrl.Root.Opacity = isCurrent ? 1.0 : (isPast ? 0.35 : 0.55);
            if (isCurrent)
            {
                var appResources = Application.Current?.Resources;
                if (appResources?.TryGetValue("MaterialDesignPaperSecondaryBrush", out var bg) == true && bg is Avalonia.Media.IBrush brush)
                    ctrl.Root.Background = brush;
                else
                    ctrl.Root.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x55, 0x55, 0x55), 0.08);
            }
            else
            {
                ctrl.Root.Background = null;
            }

            // 进度条
            ctrl.ProgressBar.IsVisible = isCurrent && S.ShowProgressBar;
            ctrl.ProgressBar.Value = isCurrent && totalSec > 0 ? Math.Clamp(elapsedSec / totalSec, 0, 1) : (isPast ? 1 : 0);

            // 额外信息：当前事件根据设置显示，其他只显示开始时间
            if (isCurrent)
            {
                ctrl.TimeBlock.Text = S.ExtraInfoType switch
                {
                    0 => $"{evt.Start:HH:mm}-{evt.End:HH:mm}",
                    1 => $"-{Fmt(new TimeSpan().Add(TimeSpan.FromSeconds(elapsedSec)))}",
                    2 => $"-{Fmt(new TimeSpan().Add(TimeSpan.FromSeconds(leftSec)))}",
                    3 => Fmt(evt.End - evt.Start),
                    4 => totalSec > 0 ? $"-{elapsedSec / totalSec:P0}" : "100%",
                    _ => $"{evt.Start:HH:mm}-{evt.End:HH:mm}"
                };
            }
            else
            {
                ctrl.TimeBlock.Text = $"{evt.Start:HH:mm}";
            }
        }
    }

    private static string Fmt(TimeSpan ts)
    {
        if (ts.TotalSeconds < 0) return "0:00";
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    ~IcalComponent() { _timer?.Stop(); }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

internal class EventRowControls
{
    public Grid Root { get; set; } = null!;
    public TextBlock TitleBlock { get; set; } = null!;
    public TextBlock TimeBlock { get; set; } = null!;
    public ProgressBar ProgressBar { get; set; } = null!;
}
