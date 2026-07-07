using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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
    private int _refreshVersion; // 用于丢弃过时的异步刷新结果

    private readonly Dictionary<IcalCalendarEvent, EventRowControls> _rowControls = [];
    private Grid? _idleRow;
    private TextBlock? _idleTimeBlock;
    private ProgressBar? _idleProgressBar;
    private Border? _idleBarTrack;
    private Border? _idleBarFill;
    private TextBlock? _idlePctText;
    private DateTime _idleGapStart;
    private DateTime _idleGapEnd;
    private int _idleGapIndex = -1;

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
        _ = RefreshEventsAsync();
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
            _ = RefreshEventsAsync();
            return;
        }
        // 无事件时降低刷新频率：每 5 分钟检查一次（6000 ticks × 50ms = 300s）
        if (_todayEvents.Count == 0 && _tickCounter % 6000 == 0)
        {
            _ = RefreshEventsAsync();
            return;
        }
        if (_todayEvents.Count > 0 || (_showingTomorrow && _tomorrowEvents.Count > 0))
        {
            UpdateDisplay();
            if (!_showingTomorrow && _todayEvents.All(e => now >= e.End))
                _ = RefreshEventsAsync();
        }
    }

    /// <summary>状态变化回调（放学等）</summary>
    private void OnCurrentTimeStateChanged(object? s, EventArgs e) => _ = RefreshEventsAsync();

    /// <summary>时间服务变更（调试改时间）</summary>
    private void OnTimeServiceChanged(object? s, PropertyChangedEventArgs e) => _ = RefreshEventsAsync();

    private CancellationTokenSource? _debounceCts;

    /// <summary>防抖刷新：避免短时间内重复刷新</summary>
    private void ScheduleRefresh()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        Task.Delay(300, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                _ = RefreshEventsAsync();
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void OnConfigChanged() => ScheduleRefresh();
    private void OnSettingsChanged(object? s, PropertyChangedEventArgs e) => ScheduleRefresh();

    // ---- 事件加载 ----

    private IcalComponentSettings S => Settings;

    /// <summary>
    /// 异步刷新事件数据：I/O 在后台线程执行，避免阻塞 UI
    /// </summary>
    private async Task RefreshEventsAsync()
    {
        var version = Interlocked.Increment(ref _refreshVersion);
        var now = _exactTimeService.GetCurrentLocalDateTime();
        _lastDate = now.Date;

        var svc = IAppHost.TryGetService<IcalService>();
        var paths = IAppHost.TryGetService<Plugin>()?.PluginSettings?.IcalFilePaths ?? [];

        List<IcalCalendarEvent> todayEvents, tomorrowEvents;

        if (svc != null && paths.Count > 0)
        {
            try
            {
                var pathsSnapshot = paths.ToList();
                // 在后台线程执行所有 I/O 操作
                todayEvents = await Task.Run(() => svc.GetTodayEventsMerged(pathsSnapshot, now));
                if (version != Volatile.Read(ref _refreshVersion)) return;
                var tomorrow = now.Date.AddDays(1);
                tomorrowEvents = await Task.Run(() => svc.GetEventsMerged(pathsSnapshot, tomorrow, tomorrow.AddDays(1)));
            }
            catch
            {
                if (version != Volatile.Read(ref _refreshVersion)) return;
                todayEvents = [];
                tomorrowEvents = [];
            }
        }
        else
        {
            todayEvents = [];
            tomorrowEvents = [];
        }

        if (version != Volatile.Read(ref _refreshVersion)) return;

        // 回到 UI 线程更新界面
        await Dispatcher.UIThread.InvokeAsync(() => UpdateEventsUi(version, now, todayEvents, tomorrowEvents));
    }

    private void UpdateEventsUi(int version, DateTime now,
        List<IcalCalendarEvent> todayEvents, List<IcalCalendarEvent> tomorrowEvents)
    {
        if (version != Volatile.Read(ref _refreshVersion)) return;

        _todayEvents = todayEvents;
        _tomorrowEvents = tomorrowEvents;

        // 通知状态服务，触发自动化信号（上课/下课/放学）
        IAppHost.TryGetService<IcalStateService>()?.UpdateState(_todayEvents, now);

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

    /// <summary>
    /// 获取当前空闲间隙：gapIndex=0第一个事件前，i+1=事件i与i+1之间，-1=无空闲
    /// </summary>
    private static bool TryGetIdleGap(List<IcalCalendarEvent> events, DateTime now,
        out int gapIndex, out DateTime gapStart, out DateTime gapEnd)
    {
        gapIndex = -1;
        gapStart = default;
        gapEnd = default;
        if (events.Count == 0) return false;
        if (now < events[0].Start)
        {
            gapIndex = 0;
            gapStart = now.Date;      // 今天零点
            gapEnd = events[0].Start;
            return true;
        }
        for (int i = 0; i < events.Count - 1; i++)
        {
            if (events[i].End <= now && now < events[i + 1].Start)
            {
                gapIndex = i + 1;
                gapStart = events[i].End;
                gapEnd = events[i + 1].Start;
                return true;
            }
        }
        return false;
    }

    // ---- UI 构建 ----

    private void RebuildEventRows()
    {
        _rowControls.Clear();
        _idleRow = null;
        _idleTimeBlock = null;
        _idleProgressBar = null;
        _idleBarTrack = null;
        _idleBarFill = null;
        _idlePctText = null;
        EventRow.Children.Clear();
        EventRow.ColumnDefinitions.Clear();

        var events = _showingTomorrow ? _tomorrowEvents : _todayEvents;
        var spacing = S.ScheduleSpacing;

        // 确定空闲标记位置
        var now = _exactTimeService.GetCurrentLocalDateTime();
        var hasGap = false;
        int idleIdx = -1;
        if (!_showingTomorrow && S.ShowIdleIndicator)
            hasGap = TryGetIdleGap(events, now, out idleIdx, out _idleGapStart, out _idleGapEnd);
        _idleGapIndex = hasGap ? idleIdx : -1;

        // 空闲标记的列定义：简化模式用 Auto，详细模式用 Star
        Func<ColumnDefinition> makeIdleCol = () => S.IdleIndicatorMode == 1
            ? new ColumnDefinition(GridLength.Auto)
            : new ColumnDefinition(GridLength.Star);

        int colIdx = 0;

        // 在第一个事件前插入空闲标记
        if (hasGap && idleIdx == 0)
        {
            var idle = BuildIdleRow(isBeforeFirst: true);
            idle.Margin = new Thickness(0);
            EventRow.ColumnDefinitions.Add(makeIdleCol());
            Grid.SetColumn(idle, colIdx++);
            EventRow.Children.Add(idle);
            _idleRow = idle;
        }

        for (int i = 0; i < events.Count; i++)
        {
            var ctrl = BuildEventRow(events[i]);
            _rowControls[events[i]] = ctrl;
            var margin = 4.0 * spacing;
            ctrl.Root.Margin = new Thickness(colIdx == 0 ? 0 : margin, 0, 0, 0);
            EventRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(ctrl.Root, colIdx++);
            EventRow.Children.Add(ctrl.Root);

            // 在事件 i 与 i+1 之间插入空闲标记
            if (hasGap && idleIdx == i + 1)
            {
                var idle = BuildIdleRow(isBeforeFirst: false);
                var top = S.IdleIndicatorMode == 1 ? 7.0 : 0.0;
                idle.Margin = new Thickness(margin, top, 0, 0);
                EventRow.ColumnDefinitions.Add(makeIdleCol());
                Grid.SetColumn(idle, colIdx++);
                EventRow.Children.Add(idle);
                _idleRow = idle;
            }
        }
    }

    private Grid BuildIdleRow(bool isBeforeFirst)
    {
        return S.IdleIndicatorMode == 1 ? BuildSimplifiedIdleRow(isBeforeFirst) : BuildDetailedIdleRow(isBeforeFirst);
    }

    /// <summary>详细模式空闲行：类似正常活动组件，含时间信息与进度条</summary>
    private Grid BuildDetailedIdleRow(bool isBeforeFirst)
    {
        var root = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Border { Width = 16 });

        var titleBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = S.IdleText
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
            VerticalAlignment = VerticalAlignment.Bottom
        };
        timeBlock.SetValue(TextBlock.FontSizeProperty,
            (appResources?.TryGetValue("MainWindowSecondaryFontSize", out var fs2) == true ? fs2 as double? : null) ?? 12.0);
        if (appResources?.TryGetValue("MaterialDesignBodySecondaryBrush", out var fg) == true && fg is IBrush b)
            timeBlock.SetValue(TextBlock.ForegroundProperty, b);
        _idleTimeBlock = timeBlock;

        var extraInfoPanel = new Panel { Margin = new Thickness(6, 0, 0, 0) };
        extraInfoPanel.Children.Add(timeBlock);
        row.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, Children = { extraInfoPanel } });
        row.Children.Add(new Border { Width = 16 });
        root.Children.Add(row);

        // 进度条（第一个事件前不显示，且受全局开关控制）
        var showProgress = !isBeforeFirst && S.ShowProgressBar;
        var progress = new ProgressBar
        {
            Height = 3,
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            MinWidth = 0,
            IsVisible = showProgress
        };
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
        _idleProgressBar = progress;

        // 高亮背景（类似当前活动）
        if (appResources?.TryGetValue("MaterialDesignPaperSecondaryBrush", out var bg) == true && bg is IBrush brush)
            root.Background = brush;
        else
            root.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55), 0.08);

        return root;
    }

    /// <summary>简化模式空闲行：第一个事件前显示"今天"标签，其余显示竖条+百分比</summary>
    private Grid BuildSimplifiedIdleRow(bool isBeforeFirst)
    {
        var root = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        if (isBeforeFirst)
        {
            // "今天" 标签，通过 GetResourceObservable 动态绑定，与 XAML {DynamicResource} 完全一致
            var badge = new Border
            {
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 2),
                CornerRadius = new CornerRadius(16)
            };
            badge.Bind(Border.BackgroundProperty, Application.Current!
                .GetResourceObservable("AccentFillColorDefaultBrush")
                .ToBinding());

            var label = new TextBlock
            {
                Text = "今天",
                Margin = new Thickness(0, 1, 0, -1)
            };
            label.Bind(TextBlock.ForegroundProperty, Application.Current!
                .GetResourceObservable("TextOnAccentFillColorPrimaryBrush")
                .ToBinding());

            badge.Child = label;
            root.Children.Add(badge);
            return root;
        }

        // 横条 + 百分比（事件之间）
        const double barW = 20.0;
        const double barH = 4.0;
        root.RowDefinitions = new RowDefinitions("Auto,Auto");
        root.RowSpacing = 2;

        var barContainer = new Grid
        {
            Width = barW,
            Height = barH,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true
        };
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var trackBg = isDark
            ? new SolidColorBrush(Color.FromRgb(255, 255, 255), 0.40)
            : new SolidColorBrush(Color.FromRgb(0, 0, 0), 0.20);

        var track = new Border
        {
            Width = barW,
            Height = barH,
            CornerRadius = new CornerRadius(2),
            Background = trackBg,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        barContainer.Children.Add(track);
        _idleBarTrack = track;

        var fill = new Border
        {
            Width = 0,
            Height = barH,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        fill.Bind(Border.BackgroundProperty, Application.Current!
            .GetResourceObservable("AccentFillColorDefaultBrush").ToBinding());
        barContainer.Children.Add(fill);
        _idleBarFill = fill;
        Grid.SetRow(barContainer, 0);
        root.Children.Add(barContainer);

        var pctText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        pctText.SetValue(TextBlock.FontSizeProperty, 8.0);
        Grid.SetRow(pctText, 1);
        root.Children.Add(pctText);
        _idlePctText = pctText;

        return root;
    }

    private void UpdateDisplay()
    {
        var now = _exactTimeService.GetCurrentLocalDateTime();
        var events = _showingTomorrow ? _tomorrowEvents : _todayEvents;

        // 每 50ms 检测事件状态变化，触发自动化信号
        if (!_showingTomorrow)
            IAppHost.TryGetService<IcalStateService>()?.UpdateState(_todayEvents, now);

        // 检测空闲标记位置是否变化（跨事件边界时重建布局）
        if (!_showingTomorrow && S.ShowIdleIndicator)
        {
            var gapChanged = TryGetIdleGap(events, now, out var newIdleIdx, out _idleGapStart, out _idleGapEnd);
            var newIdx = gapChanged ? newIdleIdx : -1;
            if (newIdx != _idleGapIndex)
            {
                RebuildEventRows();
                return;
            }
        }

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

            // 不设 Foreground 继承主题色，已结束事件半透明区分
            ctrl.Root.Opacity = isPast ? 0.5 : 1.0;
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

        // 更新空闲行显示
        if (_idleGapIndex >= 0 && (S.IdleIndicatorMode == 0 || S.IdleIndicatorMode == 1))
        {
            UpdateIdleRow(now);
        }
    }

    private void UpdateIdleRow(DateTime now)
    {
        var totalSec = (_idleGapEnd - _idleGapStart).TotalSeconds;
        if (totalSec <= 0) return;
        var elapsedSec = (now - _idleGapStart).TotalSeconds;
        var leftSec = (_idleGapEnd - now).TotalSeconds;
        var progress = Math.Clamp(elapsedSec / totalSec, 0, 1);

        if (S.IdleIndicatorMode == 0)
        {
            // 详细模式：更新时间文字和进度条
            if (_idleTimeBlock != null)
            {
                _idleTimeBlock.Text = S.ExtraInfoType switch
                {
                    0 => $"{_idleGapStart:HH:mm}-{_idleGapEnd:HH:mm}",
                    1 => Fmt(TimeSpan.FromSeconds(elapsedSec)),
                    2 => $"-{Fmt(TimeSpan.FromSeconds(leftSec))}",
                    3 => Fmt(_idleGapEnd - _idleGapStart),
                    4 => totalSec > 0 ? $"{progress:P0}" : "100%",
                    _ => $"{_idleGapStart:HH:mm}-{_idleGapEnd:HH:mm}"
                };
            }
            if (_idleProgressBar != null && _idleProgressBar.IsVisible)
            {
                _idleProgressBar.Value = progress;
            }
        }
        else if (S.IdleIndicatorMode == 1)
        {
            // 简化模式：更新横条 + 百分比 + 主题色
            if (_idleBarFill != null)
                _idleBarFill.Width = 20 * progress;
            if (_idlePctText != null)
                _idlePctText.Text = $"{progress:P0}";
            if (_idleBarTrack != null)
            {
                var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                _idleBarTrack.Background = isDark
                    ? new SolidColorBrush(Color.FromRgb(255, 255, 255), 0.40)
                    : new SolidColorBrush(Color.FromRgb(0, 0, 0), 0.10);
            }
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

    private static void SetDimColor(TextBlock tb, bool dim)
    {
        if (dim)
            tb.Classes.Add("l-secondary");
        else
            tb.Classes.Remove("l-secondary");
    }

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
