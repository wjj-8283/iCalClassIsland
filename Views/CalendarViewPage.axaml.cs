using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using iCalClassIsland.Models;
using iCalClassIsland.Services;

namespace iCalClassIsland.Views;

[SettingsPageInfo("ical-classisland.calendar", "日历视图", category: SettingsPageCategory.External)]
public partial class CalendarViewPage : SettingsPageBase
{
    private DateTime _weekStart; // 周一
    private List<IcalCalendarEvent> _events = [];
    private static readonly string[] DayHeaders = ["一", "二", "三", "四", "五", "六", "日"];

    public CalendarViewPage()
    {
        var today = DateTime.Today;
        var dayOfWeek = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        _weekStart = today.AddDays(-dayOfWeek);
        InitializeComponent();
        Loaded += (_, _) => { LoadEvents(); BuildWeek(); };
    }

    private void LoadEvents()
    {
        var plugin = IAppHost.TryGetService<Plugin>();
        var svc = IAppHost.TryGetService<IcalService>();
        var ps = plugin?.PluginSettings;
        if (svc == null || ps == null || string.IsNullOrWhiteSpace(ps.IcalFilePath))
            return;

        var from = _weekStart.AddDays(-1);
        var to = _weekStart.AddDays(8);
        _events = svc.GetEvents(ps.IcalFilePath, from, to);
    }

    private void BuildWeek()
    {
        var weekEnd = _weekStart.AddDays(6);
        WeekLabel.Text = $"{_weekStart:MM月dd日} - {weekEnd:MM月dd日}";

        WeekGrid.Children.Clear();

        var today = DateTime.Today;
        for (int col = 0; col < 7; col++)
        {
            var date = _weekStart.AddDays(col);
            var column = BuildDayColumn(date, today);
            Grid.SetColumn(column, col);
            WeekGrid.Children.Add(column);
        }
    }

    private Border BuildDayColumn(DateTime date, DateTime today)
    {
        var cell = new Border { Classes = { "DayColumn" } };
        var stack = new StackPanel();

        // 日期头部
        var header = new TextBlock
        {
            Text = $"{DayHeaders[(int)date.DayOfWeek == 0 ? 6 : (int)date.DayOfWeek - 1]} {date:MM/dd}",
            Classes = { "DayHeader" }
        };
        if (date.Date == today) header.Foreground = Brushes.DodgerBlue;
        stack.Children.Add(header);

        // 分隔线
        stack.Children.Add(new Border
        {
            Height = 1,
            Background = Brushes.LightGray,
            Margin = new Avalonia.Thickness(0, 2)
        });

        // 当天事件
        var dayEnd = date.Date.AddDays(1);
        var dayEvents = _events
            .Where(e => e.Start < dayEnd && e.End > date.Date)
            .OrderBy(e => e.Start)
            .ToList();

        var now = DateTime.Now;
        foreach (var evt in dayEvents)
        {
            // 垂直排列：时间在上，标题在下，可换行
            var item = new StackPanel { Margin = new Avalonia.Thickness(0, 2, 0, 4) };

            item.Children.Add(new TextBlock
            {
                Text = evt.Start.ToString("HH:mm"),
                FontSize = 16,
                FontWeight = FontWeight.Bold
            });

            var titleBlock = new TextBlock
            {
                Text = evt.Summary,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (evt.End <= now)
                titleBlock.Foreground = Brushes.Gray;
            else if (evt.Start <= now && now < evt.End)
                titleBlock.Foreground = Brushes.DodgerBlue;
            item.Children.Add(titleBlock);
            stack.Children.Add(item);
        }

        if (dayEvents.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "无日程",
                FontSize = 16,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            });
        }

        cell.Child = stack;
        return cell;
    }

    private void OnPrevWeek(object? s, RoutedEventArgs e)
    {
        _weekStart = _weekStart.AddDays(-7);
        LoadEvents(); BuildWeek();
    }

    private void OnNextWeek(object? s, RoutedEventArgs e)
    {
        _weekStart = _weekStart.AddDays(7);
        LoadEvents(); BuildWeek();
    }
}
