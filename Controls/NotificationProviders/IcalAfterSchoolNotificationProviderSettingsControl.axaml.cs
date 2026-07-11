using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using iCalClassIsland.Models.NotificationProviderSettings;

namespace iCalClassIsland.Controls.NotificationProviders;

/// <summary>
/// IcalAfterSchoolNotificationProviderSettingsControl.axaml 的交互逻辑
/// </summary>
public partial class IcalAfterSchoolNotificationProviderSettingsControl : NotificationProviderControlBase<IcalAfterSchoolNotificationSettings>
{
    public IcalAfterSchoolNotificationProviderSettingsControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Defer DataContext to Loaded — SettingsInternal is set by GetInstance after constructor
        DataContext = this;
    }
}
