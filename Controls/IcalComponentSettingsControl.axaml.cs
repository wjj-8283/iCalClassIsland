using ClassIsland.Core.Abstractions.Controls;
using iCalClassIsland.Models;

namespace iCalClassIsland.Controls;

public partial class IcalComponentSettingsControl : ComponentBase<IcalComponentSettings>
{
    public IcalComponentSettingsControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var s = Settings;
        if (s == null) return;

        // 初始化所有控件值
        CmbExtraInfo.SelectedIndex = s.ExtraInfoType;
        ChkCountdown.IsChecked = s.IsCountdownEnabled;
        NumCountdownSec.Value = s.CountdownSeconds;
        ChkProgress.IsChecked = s.ShowProgressBar;
        ChkHideFinished.IsChecked = s.HideFinishedEvents;
        NumSpacing.Value = (decimal)s.ScheduleSpacing;
        ChkPlaceholder.IsChecked = s.ShowPlaceholderOnEmpty;
        TxtNoEvents.Text = s.PlaceholderTextNoEvents;
        TxtAllEnded.Text = s.PlaceholderTextAllEnded;
        PnlCountdownSec.IsVisible = s.IsCountdownEnabled;
        PnlNoEvents.IsVisible = s.ShowPlaceholderOnEmpty;
        PnlAllEnded.IsVisible = s.ShowPlaceholderOnEmpty;

        // 双向同步：控件变更 → 写回 Settings
        CmbExtraInfo.SelectionChanged += (_, _) => s.ExtraInfoType = CmbExtraInfo.SelectedIndex;
        ChkCountdown.IsCheckedChanged += (_, _) => { s.IsCountdownEnabled = ChkCountdown.IsChecked == true; PnlCountdownSec.IsVisible = s.IsCountdownEnabled; };
        NumCountdownSec.ValueChanged += (_, _) => s.CountdownSeconds = (int)(NumCountdownSec.Value ?? 60);
        ChkProgress.IsCheckedChanged += (_, _) => s.ShowProgressBar = ChkProgress.IsChecked == true;
        ChkHideFinished.IsCheckedChanged += (_, _) => s.HideFinishedEvents = ChkHideFinished.IsChecked == true;
        NumSpacing.ValueChanged += (_, _) => s.ScheduleSpacing = (double)NumSpacing.Value;
        ChkPlaceholder.IsCheckedChanged += (_, _) => { s.ShowPlaceholderOnEmpty = ChkPlaceholder.IsChecked == true; PnlNoEvents.IsVisible = s.ShowPlaceholderOnEmpty; PnlAllEnded.IsVisible = s.ShowPlaceholderOnEmpty; };
        TxtNoEvents.TextChanged += (_, _) => s.PlaceholderTextNoEvents = TxtNoEvents.Text ?? "";
        TxtAllEnded.TextChanged += (_, _) => s.PlaceholderTextAllEnded = TxtAllEnded.Text ?? "";
    }
}
