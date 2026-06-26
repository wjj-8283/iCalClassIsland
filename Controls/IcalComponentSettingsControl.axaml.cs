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

        CmbExtraInfo.SelectedIndex = s.ExtraInfoType;
        CmbTomorrowMode.SelectedIndex = s.TomorrowShowMode;
        ChkCountdown.IsChecked = s.IsCountdownEnabled;
        NumCountdownSec.Value = s.CountdownSeconds;
        ChkProgress.IsChecked = s.ShowProgressBar;
        ChkHideFinished.IsChecked = s.HideFinishedEvents;
        NumSpacing.Value = (decimal)s.ScheduleSpacing;
        ChkPlaceholder.IsChecked = s.ShowPlaceholderOnEmpty;
        TxtNoEvents.Text = s.PlaceholderTextNoEvents;
        TxtAllEnded.Text = s.PlaceholderTextAllEnded;
        PnlCountdownSec.IsVisible = s.IsCountdownEnabled;

        CmbExtraInfo.SelectionChanged += (_, _) => s.ExtraInfoType = CmbExtraInfo.SelectedIndex;
        CmbTomorrowMode.SelectionChanged += (_, _) => s.TomorrowShowMode = CmbTomorrowMode.SelectedIndex;
        ChkCountdown.IsCheckedChanged += (_, _) => { s.IsCountdownEnabled = ChkCountdown.IsChecked == true; PnlCountdownSec.IsVisible = s.IsCountdownEnabled; };
        NumCountdownSec.ValueChanged += (_, _) => s.CountdownSeconds = (int)(NumCountdownSec.Value ?? 60);
        ChkProgress.IsCheckedChanged += (_, _) => s.ShowProgressBar = ChkProgress.IsChecked == true;
        ChkHideFinished.IsCheckedChanged += (_, _) => s.HideFinishedEvents = ChkHideFinished.IsChecked == true;
        NumSpacing.ValueChanged += (_, _) => s.ScheduleSpacing = (double)NumSpacing.Value;
        ChkPlaceholder.IsCheckedChanged += (_, _) => s.ShowPlaceholderOnEmpty = ChkPlaceholder.IsChecked == true;
        TxtNoEvents.TextChanged += (_, _) => s.PlaceholderTextNoEvents = TxtNoEvents.Text ?? "";
        TxtAllEnded.TextChanged += (_, _) => s.PlaceholderTextAllEnded = TxtAllEnded.Text ?? "";
    }
}
