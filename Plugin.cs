using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using iCalClassIsland.Controls;
using iCalClassIsland.Controls.RuleSettingsControls;
using iCalClassIsland.Controls.TriggerSettingsControls;
using iCalClassIsland.Models;
using iCalClassIsland.Services;
using iCalClassIsland.Services.Automation.Triggers;
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
        services.AddSingleton<IcalStateService>();
        services.AddTrigger<IcalEventStartTrigger>();
        services.AddTrigger<IcalEventEndTrigger>();
        services.AddTrigger<IcalDayEndTrigger>();
        services.AddTrigger<IcalPreTimePointTrigger, IcalPreTimePointTriggerSettingsControl>();
        services.AddRule<IcalEventContainsRuleSettings, IcalEventContainsRuleSettingsControl>("ical.currentEventContains", "iCal：当前事件中包含", "");
        services.AddRule<IcalEventContainsRuleSettings, IcalEventContainsRuleSettingsControl>("ical.previousEventContains", "iCal：上个事件中包含", "");
        services.AddRule<IcalEventContainsRuleSettings, IcalEventContainsRuleSettingsControl>("ical.nextEventContains", "iCal：下个事件中包含", "");
        services.AddComponent<IcalComponent, IcalComponentSettingsControl>();
        services.AddSettingsPage<IcalSettingsPage>();
        services.AddSettingsPage<CalendarViewPage>();

        _configPath = Path.Combine(PluginConfigFolder, "Settings.json");

        AppBase.Current.AppStarted += (_, _) =>
        {
            // 初始化缓存目录（用于 web ICS 文件缓存）
            var cacheDir = Path.Combine(PluginConfigFolder, "Cache");
            var icalService = IAppHost.TryGetService<IcalService>();
            icalService?.InitializeCache(cacheDir);

            // 注册规则处理器
            var rulesetService = IAppHost.TryGetService<IRulesetService>();
            var stateService = IAppHost.TryGetService<IcalStateService>();
            if (rulesetService != null && stateService != null)
            {
                rulesetService.RegisterRuleHandler("ical.currentEventContains", settings =>
                {
                    if (settings is not IcalEventContainsRuleSettings s) return false;
                    return s.IsMatching(stateService.CurrentEventSummary);
                });
                rulesetService.RegisterRuleHandler("ical.previousEventContains", settings =>
                {
                    if (settings is not IcalEventContainsRuleSettings s) return false;
                    return s.IsMatching(stateService.PreviousEventSummary);
                });
                rulesetService.RegisterRuleHandler("ical.nextEventContains", settings =>
                {
                    if (settings is not IcalEventContainsRuleSettings s) return false;
                    return s.IsMatching(stateService.NextEventSummary);
                });
            }

            PluginSettings = ConfigureFileHelper.LoadConfig<IcalPluginSettings>(_configPath);
            PluginSettings.PropertyChanged += (_, _) =>
            {
                ConfigureFileHelper.SaveConfig(_configPath, PluginSettings);
                ConfigChanged?.Invoke();
            };
            PluginSettings.IcalFilePaths.CollectionChanged += (_, _) =>
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
            foreach (var path in PluginSettings.IcalFilePaths)
                await service.RefreshAsync(path, now);
            ConfigChanged?.Invoke();
        }
    }

    public void NotifyConfigChanged() => ConfigChanged?.Invoke();
}
