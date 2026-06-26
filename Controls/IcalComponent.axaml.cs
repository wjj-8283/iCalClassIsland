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
    private readonly ILessonsService _lessonsService;
    private int _tickCounter;
    private List<IcalCalendarEvent> _todayEvents = [];
    private List<IcalCalendarEvent> _tomorrowEvents = [];
    private bool _showingTomorrow;
    private DateTime _lastDate;

    private readonly Dictionary<IcalCalendarEvent, EventRowControls> _rowControls = [];

    public IcalComponent(IExactTimeService exactTimeService, ILessonsService lessonsService)
    {
        _exactTimeService = exactTimeService;
        _lessonsService = lessonsService;
        DataContext = this;
        VerticalAlignment = VerticalAlignment.Stretch;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    private void OnAttached(object? s, VisualTreeAttachmentEventArgs e)
    {
        // 使用 ClassIsland 主循环（与课程表组件一致）
        _lessonsService.PostMainTimerTicked += OnPostMainTimerTicked;
        _lessonsService.CurrentTimeStateChanged += OnCurrentTimeStateChanged;
        _exactTimeService.PropertyChanged += OnTimeServiceChanged;

        var plugin = IAppHost.TryGetService<Plugin>();
        if (plugin != null) plugin.ConfigChanged += OnConfigChanged;
        if (Settings is INotifyPropertyChanged npc) npc.PropertyChanged += OnSettingsChanged;

        _lastDate = _exactTimeService.GetCurrentLocalDateTime().Date;
        RefreshEvents();
    }

    private void OnDetached(object? s, VisualTreeAttachmentEventArgs e)
    {
        _lessonsService.PostMainTimerTicked -= OnPostMainTimerTicked;
        _lessonsService.CurrentTimeStateChanged -= OnCurrentTimeStateChanged;
        _exactTimeService.PropertyChanged -= OnTimeServiceChanged;

        var plugin = IAppHost.TryGetService<Plugin>();
        if (plugin != null) plugin.ConfigChanged -= OnConfigChanged;
        if (Settings is INotifyPropertyChanged npc) npc.PropertyChanged -= OnSettingsChanged;
    }

    // ---- 事件订阅（仿照 ScheduleComponent） ----

    /// <summary>主循环回调：50ms 更新显示 + 定期刷新</summary>
    private void OnPostMainTimerTicked(object? s, EventArgs e)
    {
        _tickCounter++;
        var now = _exactTimeService.GetCurrentLocalDateTime();
        var dayChanged = now.Date != _lastDate;

        if (dayChanged || _tickCounter % 600 == 0)
        {
            _lastDate = now.Date;
            RefreshEvents();
            return;
        }
        if (_todayEvents.Count == 0 && _tickCounter % 20 == 0)
        {
            RefreshEvents();
            return;
        }
        if (_todayEvents.Count > 0 || (_showingTomorrow && _tomorrowEvents.Count > 0))
        {
            UpdateDisplay();
            if (!_showingTomorrow && _todayEvents.All(e => now >= e.End))
                RefreshEvents();
        }
    }

    /// <summary>状态变化回调（放学等）</summary>
    private void OnCurrentTimeStateChanged(object? s, EventArgs e) => RefreshEvents();

    /// <summary>时间服务变更（调试改时间）</summary>
    private void OnTimeServiceChanged(object? s, PropertyChangedEventArgs e) => RefreshEvents();

    private void OnConfigChanged() => Dispatcher.UIThread.Post(RefreshEvents);
    private void OnSettingsChanged(object? s, PropertyChangedEventArgs e) => Dispatcher.UIThread.Post(RefreshEvents);

    // ---- 事件加载 ----

    private IcalComponentSettings S => Settings;

    private void RefreshEvents()
    {
        var now = _exactTimeService.GetCurrentLocalDateTime();
        _lastDate = now.Date;
        try
        {
            var svc = IAppHost.TryGetService<IcalService>();
            var p = IAppHost.TryGetService<Plugin>()?.PluginSettings;
            var paths = p?.IcalFilePaths ?? [];
            if (svc != null && paths.Count > 0)
            {
                _todayEvents = svc.GetTodayEventsMerged(paths, now);
                var tomorrow = now.Date.AddDays(1);
                _tomorrowEvents = svc.GetEventsMerged(paths, tomorrow, tomorrow.AddDays(1));
            }
            else { _todayEvents = []; _tomorrowEvents = []; }
        }
        catch { _todayEvents = []; _tomorrowEvents = []; }

        var allEnded = _todayEvents.Count > 0 && _todayEvents.All(e => now >= e.End);
        var showTomorrow = ShouldShowTomorrow(allEnded) && _tomorrowEvents.Count > 0;

        if (showTomorrow)
        {
            _showingTomorrow = true;
            TomorrowBadge.IsVisible = true;
            EventRow.IsVisible = true;
            Placeholder.IsVisible = false;
        }
        else if (_todayEvents.Count == 0)
        {
            _showingTomorrow = false;
            TomorrowBadge.IsVisible = false;
            EventRow.IsVisible = false;
            Placeholder.IsVisible = true;
            var p = IAppHost.TryGetService<Plugin>()?.PluginSettings;
            var missing = p?.IcalFilePaths.Where(f => !File.Exists(f)).ToList();
            if (missing != null && missing.Count > 0)
                Placeholder.Text = $"找不到文件: {Path.GetFileName(missing[0])}";
            else
                Placeholder.Text = S.ShowPlaceholderOnEmpty ? S.PlaceholderTextNoEvents : "";
        }
        else if (allEnded)
        {
            _showingTomorrow = false;
            TomorrowBadge.IsVisible = false;
            EventRow.IsVisible = false;
            Placeholder.IsVisible = true;
            Placeholder.Text = S.ShowPlaceholderOnEmpty ? S.PlaceholderTextAllEnded : "";
        }
        else
        {
            _showingTomorrow = false;
            TomorrowBadge.IsVisible = false;
            EventRow.IsVisible = true;
            Placeholder.IsVisible = false;
        }

        RebuildEventRows();
        UpdateDisplay();
    }

    private bool ShouldShowTomorrow(bool allEnded) => S.TomorrowShowMode switch
    {
        1 => _todayEvents.Count == 0 || allEnded,
        2 => true,
        _ => false
    };

    // ---- UI 构建 ----

    private void RebuildEventRows()
    {
        _rowControls.Clear();
        EventRow.Children.Clear();
        EventRow.ColumnDefinitions.Clear();

        var events = _showingTomorrow ? _tomorrowEvents : _todayEvents;
        for (int i = 0; i < events.Count; i++)
        {
            var ctrl = BuildEventRow(events[i]);
            _rowControls[events[i]] = ctrl;
            EventRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(ctrl.Root, i);
            EventRow.Children.Add(ctrl.Root);
        }
    }

    private void UpdateDisplay()
    {
        var now = _exactTimeService.GetCurrentLocalDateTime();
        var events = _showingTomorrow ? _tomorrowEvents : _todayEvents;

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            if (!_rowControls.TryGetValue(evt, out var ctrl)) continue;

            var isPast = evt.End <= now;
            var isCurrent = evt.Start <= now && now < evt.End;

            // 隐藏已结束事件
            if (S.HideFinishedEvents && isPast)
            {
                ctrl.Root.IsVisible = false;
                continue;
            }
            ctrl.Root.IsVisible = true;
            var totalSec = (evt.End - evt.Start).TotalSeconds;
            var elapsedSec = (now - evt.Start).TotalSeconds;
            var leftSec = (evt.End - now).TotalSeconds;

            ctrl.Root.Opacity = 1.0;
            ctrl.TitleBlock.Foreground = isCurrent
                ? (Application.Current?.Resources.TryGetValue("MaterialDesignBodyBrush", out var f) == true && f is IBrush b ? b : Brushes.Black)
                : (isPast ? Brushes.Gray : Brushes.Black);
            ctrl.TimeBlock.Foreground = isCurrent
                ? (Application.Current?.Resources.TryGetValue("MaterialDesignBodyBrush", out var fs) == true && fs is IBrush bs ? bs : Brushes.Black)
                : (isPast ? Brushes.Gray : Brushes.Black);
            if (isCurrent)
            {
                var appResources = Application.Current?.Resources;
                if (appResources?.TryGetValue("MaterialDesignPaperSecondaryBrush", out var bg) == true && bg is IBrush brush)
                    ctrl.Root.Background = brush;
                else
                    ctrl.Root.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55), 0.08);
            }
            else ctrl.Root.Background = null;

            ctrl.ProgressBar.IsVisible = isCurrent && S.ShowProgressBar;
            ctrl.ProgressBar.Value = isCurrent && totalSec > 0 ? Math.Clamp(elapsedSec / totalSec, 0, 1) : (isPast ? 1 : 0);

            ctrl.TimeBlock.Text = isCurrent
                ? S.ExtraInfoType switch
                {
                    0 => $"{evt.Start:HH:mm}-{evt.End:HH:mm}",
                    1 => Fmt(TimeSpan.FromSeconds(elapsedSec)),
                    2 => $"-{Fmt(TimeSpan.FromSeconds(leftSec))}",
                    3 => Fmt(evt.End - evt.Start),
                    4 => totalSec > 0 ? $"{elapsedSec / totalSec:P0}" : "100%",
                    _ => $"{evt.Start:HH:mm}-{evt.End:HH:mm}"
                }
                : $"{evt.Start:HH:mm}";
        }
    }

    private static string Fmt(TimeSpan ts) => ts.TotalHours >= 1
        ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
        : $"{ts.Minutes:D2}:{ts.Seconds:D2}";

    // ---- 事件行构建 ----

    private EventRowControls BuildEventRow(IcalCalendarEvent evt)
    {
        var root = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Border { Width = 16 });

        var titleBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = evt.Summary
        };
        var appResources = Application.Current?.Resources;
        titleBlock.SetValue(TextBlock.FontSizeProperty,
            (appResources?.TryGetValue("MainWindowEmphasizedFontSize", out var fs) == true ? fs as double? : null) ?? 16.0);
        titleBlock.SetValue(TextBlock.FontWeightProperty, FontWeight.Bold);
        var titleGrid = new Grid();
        titleGrid.Children.Add(new HighlightBox { Content = titleBlock });
        row.Children.Add(titleGrid);

        var timeBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Text = $"{evt.Start:HH:mm}"
        };
        timeBlock.SetValue(TextBlock.FontSizeProperty,
            (appResources?.TryGetValue("MainWindowSecondaryFontSize", out var fs2) == true ? fs2 as double? : null) ?? 12.0);
        if (appResources?.TryGetValue("MaterialDesignBodySecondaryBrush", out var fg) == true && fg is IBrush b)
            timeBlock.SetValue(TextBlock.ForegroundProperty, b);

        var extraInfoPanel = new Panel { Margin = new Thickness(6, 0, 0, 0) };
        extraInfoPanel.Children.Add(timeBlock);
        row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, Children = { extraInfoPanel } });
        row.Children.Add(new Border { Width = 16 });
        root.Children.Add(row);

        var progress = new ProgressBar { Height = 3, Minimum = 0, Maximum = 1, Value = 0, MinWidth = 0 };
        var canvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 3,
            Margin = new Thickness(0, 0, 0, 0)
        };
        progress.SetValue(Avalonia.Controls.Canvas.BottomProperty, 0.0);
        progress.Bind(ProgressBar.WidthProperty, new Avalonia.Data.Binding("Bounds.Width") { Source = canvas, Mode = Avalonia.Data.BindingMode.OneWay });
        canvas.Children.Add(progress);
        root.Children.Add(canvas);

        return new EventRowControls { Root = root, TitleBlock = titleBlock, TimeBlock = timeBlock, ProgressBar = progress };
    }

    ~IcalComponent() { }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    protected bool SetField<T>(ref T f, T v, [CallerMemberName] string? p = null) { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(p); return true; }
}

internal class EventRowControls
{
    public Grid Root { get; set; } = null!;
    public TextBlock TitleBlock { get; set; } = null!;
    public TextBlock TimeBlock { get; set; } = null!;
    public ProgressBar ProgressBar { get; set; } = null!;
}
