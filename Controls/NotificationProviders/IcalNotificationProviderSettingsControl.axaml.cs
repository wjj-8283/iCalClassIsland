using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using iCalClassIsland.Models.NotificationProviderSettings;

namespace iCalClassIsland.Controls.NotificationProviders;

/// <summary>
/// IcalNotificationProviderSettingsControl.axaml 的交互逻辑
/// </summary>
public partial class IcalNotificationProviderSettingsControl : NotificationProviderControlBase<IcalNotificationSettings>
{
    public IcalNotificationProviderSettingsControl()
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
