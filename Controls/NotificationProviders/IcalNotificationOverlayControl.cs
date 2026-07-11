using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;

namespace iCalClassIsland.Controls.NotificationProviders;

/// <summary>
/// iCal 提醒覆盖控件，对标 ClassIsland 的 ClassNotificationProviderControl。
/// 纯 C# 构建 UI，避免插件 AssemblyLoadContext 的 XAML 类型解析问题。
/// </summary>
public class IcalNotificationOverlayControl : UserControl, INotifyPropertyChanged
{
    private object? _element;
    private string _message = "";
    private int _slideIndex;
    private string _nextEventSummary = "";
    private string _nextEventStart = "";
    private string _nextEventExtraInfo = "";
    private string _currentEventSummary = "";
    private string _currentEventExtraInfo = "";
    private DateTime _targetTime;
    private string _countdownText = "";
    private bool _showExtraInfo;

    private readonly IExactTimeService _exactTimeService;
    private readonly DispatcherTimer _slideTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ListBox _mainListBox;

    public object? Element
    {
        get => _element;
        set { if (Equals(value, _element)) return; _element = value; OnPropertyChanged(); }
    }

    public string Message
    {
        get => _message;
        set { if (value == _message) return; _message = value; OnPropertyChanged(); }
    }

    public int SlideIndex
    {
        get => _slideIndex;
        set { if (value == _slideIndex) return; _slideIndex = value; OnPropertyChanged(); }
    }

    public string NextEventSummary
    {
        get => _nextEventSummary;
        set { if (value == _nextEventSummary) return; _nextEventSummary = value; OnPropertyChanged(); }
    }

    public string NextEventStart
    {
        get => _nextEventStart;
        set { if (value == _nextEventStart) return; _nextEventStart = value; OnPropertyChanged(); }
    }

    public string NextEventExtraInfo
    {
        get => _nextEventExtraInfo;
        set { if (value == _nextEventExtraInfo) return; _nextEventExtraInfo = value; OnPropertyChanged(); }
    }

    public string CurrentEventSummary
    {
        get => _currentEventSummary;
        set { if (value == _currentEventSummary) return; _currentEventSummary = value; OnPropertyChanged(); }
    }

    public string CurrentEventExtraInfo
    {
        get => _currentEventExtraInfo;
        set { if (value == _currentEventExtraInfo) return; _currentEventExtraInfo = value; OnPropertyChanged(); }
    }

    public DateTime TargetTime
    {
        get => _targetTime;
        set { _targetTime = value; UpdateCountdown(); }
    }

    public string CountdownText
    {
        get => _countdownText;
        set { if (value == _countdownText) return; _countdownText = value; OnPropertyChanged(); }
    }

    public bool ShowExtraInfo
    {
        get => _showExtraInfo;
        set { if (value == _showExtraInfo) return; _showExtraInfo = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 构建准备提醒覆盖布局：左列倒计时，右列下个事件信息
    /// </summary>
    public static IcalNotificationOverlayControl CreatePrepareOverlay(IExactTimeService exactTimeService)
    {
        var control = new IcalNotificationOverlayControl(exactTimeService);
        var smallFont = GetSecondaryFontSize();

        // 左列：倒计时
        var countdownLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(12, 0),
            Classes = { "l-instructive" },
            [!TextBlock.TextProperty] = new Avalonia.Data.Binding("CountdownText")
            {
                Source = control
            }
        };

        // 右列：下个事件信息
        var rightPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 0)
        };

        var nextLabel = new TextBlock
        {
            Text = "下个日程：",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = smallFont,
            Classes = { "l-instructive" }
        };

        var summaryText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
            [!TextBlock.TextProperty] = new Avalonia.Data.Binding("NextEventSummary")
            {
                Source = control
            }
        };

        var extraInfoText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = smallFont,
            Classes = { "l-secondary" },
            [!TextBlock.TextProperty] = new Avalonia.Data.Binding("NextEventExtraInfo")
            {
                Source = control
            },
            [!TextBlock.IsVisibleProperty] = new Avalonia.Data.Binding("ShowExtraInfo")
            {
                Source = control
            }
        };

        rightPanel.Children.Add(nextLabel);
        rightPanel.Children.Add(summaryText);
        rightPanel.Children.Add(extraInfoText);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(countdownLabel, 0);
        Grid.SetColumn(rightPanel, 1);
        grid.Children.Add(countdownLabel);
        grid.Children.Add(rightPanel);

        control.Element = grid;
        return control;
    }

    /// <summary>
    /// 构建结束提醒覆盖布局：左列当前事件结束，右列下个事件信息
    /// </summary>
    public static IcalNotificationOverlayControl CreateEndOverlay(IExactTimeService exactTimeService)
    {
        var control = new IcalNotificationOverlayControl(exactTimeService);
        var smallFont = GetSecondaryFontSize();

        // 左列：本节已结束
        var leftPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(12, 0),
            Orientation = Orientation.Horizontal
        };

        var endedLabel = new TextBlock
        {
            Text = "上一事件「",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = smallFont,
            Classes = { "l-instructive" }
        };

        var currentSummary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
            [!TextBlock.TextProperty] = new Avalonia.Data.Binding("CurrentEventSummary")
            {
                Source = control
            }
        };

        var endedSuffix = new TextBlock
        {
            Text = "」已结束",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = smallFont,
            Classes = { "l-instructive" }
        };

        leftPanel.Children.Add(endedLabel);
        leftPanel.Children.Add(currentSummary);
        leftPanel.Children.Add(endedSuffix);

        // 右列：下个事件信息
        var rightPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 0)
        };

        var nextLabel = new TextBlock
        {
            Text = "下个日程是：",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = smallFont,
            Classes = { "l-instructive" }
        };

        var nextSummary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold,
            [!TextBlock.TextProperty] = new Avalonia.Data.Binding("NextEventSummary")
            {
                Source = control
            }
        };

        var timeText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2, 0, 0, 0),
            FontSize = smallFont,
            Classes = { "l-secondary" },
            [!TextBlock.TextProperty] = new Avalonia.Data.Binding("NextEventStart")
            {
                Source = control
            }
        };

        rightPanel.Children.Add(nextLabel);
        rightPanel.Children.Add(nextSummary);
        rightPanel.Children.Add(timeText);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(leftPanel, 0);
        Grid.SetColumn(rightPanel, 1);
        grid.Children.Add(leftPanel);
        grid.Children.Add(rightPanel);

        control.Element = grid;
        return control;
    }

    private IcalNotificationOverlayControl(IExactTimeService exactTimeService)
    {
        _exactTimeService = exactTimeService;

        _mainListBox = new ListBox
        {
            Classes = { "sliding" }
        };

        // Item 0: Event info panel
        var eventInfoItem = new ListBoxItem
        {
            FontSize = Application.Current?.FindResource("MainWindowBodyFontSize") as double? ?? 14,
            Foreground = Application.Current?.FindResource("TextFillColorPrimaryBrush") as IBrush,
            Content = new ContentPresenter
            {
                [!ContentPresenter.ContentProperty] = new Avalonia.Data.Binding("Element")
                {
                    Source = this
                }
            }
        };

        // Item 1: Custom message
        var messageItem = new ListBoxItem
        {
            FontSize = Application.Current?.FindResource("MainWindowBodyFontSize") as double? ?? 14,
            Foreground = Application.Current?.FindResource("TextFillColorPrimaryBrush") as IBrush,
            Content = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                [!TextBlock.TextProperty] = new Avalonia.Data.Binding("Message")
                {
                    Source = this
                }
            }
        };

        _mainListBox.Items.Add(eventInfoItem);
        _mainListBox.Items.Add(messageItem);
        _mainListBox.SelectedIndex = 0;

        var rootGrid = new Grid { Margin = new Thickness(16, 0) };
        rootGrid.Children.Add(_mainListBox);
        Content = rootGrid;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _slideTimer.Tick += OnSlideTick;
        _slideTimer.Start();

        _countdownTimer.Tick += OnCountdownTick;
        UpdateCountdown();
        _countdownTimer.Start();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _slideTimer.Stop();
        _slideTimer.Tick -= OnSlideTick;

        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
    }

    private void OnSlideTick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Message))
            return;
        _mainListBox.SelectedIndex = SlideIndex = SlideIndex == 1 ? 0 : 1;
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        var remaining = TargetTime - _exactTimeService.GetCurrentLocalDateTime();
        if (remaining.TotalSeconds <= 0)
        {
            CountdownText = "即将开始";
        }
        else
        {
            CountdownText = $"距开始还剩 {FormatTimeSpan(remaining)}";
        }
    }

    private static double GetSecondaryFontSize() =>
        Application.Current?.FindResource("MainWindowSecondaryFontSize") as double? ?? 13.0;

    private static string FormatTimeSpan(TimeSpan span)
    {
        if (span.TotalSeconds <= 0) return "0 秒";

        var parts = new List<string>(3);
        if (span.Hours > 0) parts.Add($"{span.Hours} 小时");
        if (span.Minutes > 0)
        {
            if (span.Seconds > 0) parts.Add($"{span.Minutes} 分");
            else parts.Add($"{span.Minutes} 分钟");
        }
        if (span.Seconds > 0) parts.Add($"{span.Seconds} 秒");

        return string.Join(" ", parts);
    }

    #region INotifyPropertyChanged

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
