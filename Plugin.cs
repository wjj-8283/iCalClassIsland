using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using iCalClassIsland.Controls;
using iCalClassIsland.Models;
using iCalClassIsland.Services;
using iCalClassIsland.Views;
using iCalClassIsland.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace iCalClassIsland;

[PluginEntrance]
public class Plugin : PluginBase
{
    public IcalPluginSettings PluginSettings { get; private set; } = new();
    public event Action? ConfigChanged;

    private string _configPath = "";

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<IcalService>();
        services.AddComponent<IcalComponent, IcalComponentSettingsControl>();
        services.AddSettingsPage<IcalSettingsPage>();
        services.AddSettingsPage<CalendarViewPage>();

        _configPath = Path.Combine(PluginConfigFolder, "Settings.json");

        AppBase.Current.AppStarted += (_, _) =>
        {
            PluginSettings = ConfigureFileHelper.LoadConfig<IcalPluginSettings>(_configPath);
            PluginSettings.PropertyChanged += (_, _) =>
            {
                ConfigureFileHelper.SaveConfig(_configPath, PluginSettings);
                ConfigChanged?.Invoke();
            };
            ConfigChanged?.Invoke();
        };
    }

    public async Task RefreshAsync()
    {
        var service = IAppHost.TryGetService<IcalService>();
        var timeService = IAppHost.TryGetService<IExactTimeService>();
        if (service != null)
        {
            var now = timeService?.GetCurrentLocalDateTime() ?? DateTime.Now;
            await service.RefreshAsync(PluginSettings.IcalFilePath, now);
            ConfigChanged?.Invoke();
        }
    }

    public void NotifyConfigChanged() => ConfigChanged?.Invoke();
}
