using ClassIsland.Core.Abstractions.Controls;
using iCalClassIsland.Models;

namespace iCalClassIsland.Controls.TriggerSettingsControls;

/// <summary>
/// IcalPreTimePointTriggerSettingsControl.axaml 的交互逻辑
/// </summary>
public partial class IcalPreTimePointTriggerSettingsControl : TriggerSettingsControlBase<IcalPreTimePointTriggerSettings>
{
    public IcalPreTimePointTriggerSettingsControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Defer DataContext to Loaded — SettingsInternal is set by GetInstance after constructor
        DataContext = this;

        TimePointComboBox.SelectedIndex = Settings.TargetTimePoint switch
        {
            IcalTimePoint.EventStart => 0,
            IcalTimePoint.EventEnd => 1,
            IcalTimePoint.DayEnd => 2,
            _ => -1,
        };
    }

    public void TimePointComboBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        Settings.TargetTimePoint = TimePointComboBox.SelectedIndex switch
        {
            0 => IcalTimePoint.EventStart,
            1 => IcalTimePoint.EventEnd,
            2 => IcalTimePoint.DayEnd,
            _ => Settings.TargetTimePoint,
        };
    }
}
