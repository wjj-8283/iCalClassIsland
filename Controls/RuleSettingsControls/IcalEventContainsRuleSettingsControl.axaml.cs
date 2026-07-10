using ClassIsland.Core.Abstractions.Controls;
using iCalClassIsland.Models;

namespace iCalClassIsland.Controls.RuleSettingsControls;

/// <summary>
/// IcalEventContainsRuleSettingsControl.axaml 的交互逻辑
/// </summary>
public partial class IcalEventContainsRuleSettingsControl : RuleSettingsControlBase<IcalEventContainsRuleSettings>
{
    public IcalEventContainsRuleSettingsControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Defer DataContext to Loaded — SettingsInternal is set by GetInstance after constructor
        DataContext = this;
    }
}
