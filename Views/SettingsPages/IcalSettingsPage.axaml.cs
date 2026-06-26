using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using iCalClassIsland.Services;

namespace iCalClassIsland.Views.SettingsPages;

/// <summary>
/// iCal 插件设置页面
/// </summary>
[SettingsPageInfo("ical-classisland.settings", "iCal 日程", category: SettingsPageCategory.External)]
public partial class IcalSettingsPage : SettingsPageBase, INotifyPropertyChanged
{
    private readonly Plugin _plugin;
    private string _icalFilePath = "";
    private int _refreshIntervalMinutes = 5;
    private string _statusMessage = "就绪";

    public string IcalFilePath
    {
        get => _icalFilePath;
        set
        {
            if (value == _icalFilePath) return;
            _icalFilePath = value;
            OnPropertyChanged();
            _plugin.PluginSettings.IcalFilePath = value;
            UpdateStatus();
        }
    }

    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set
        {
            if (value == _refreshIntervalMinutes) return;
            _refreshIntervalMinutes = value;
            OnPropertyChanged();
            _plugin.PluginSettings.RefreshIntervalMinutes = value;
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (value == _statusMessage) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public IcalSettingsPage()
    {
        DataContext = this;
        InitializeComponent();

        _plugin = IAppHost.GetService<Plugin>()!;

        // 从插件设置加载初始值
        _icalFilePath = _plugin.PluginSettings.IcalFilePath;
        _refreshIntervalMinutes = _plugin.PluginSettings.RefreshIntervalMinutes;

        UpdateStatus();
    }

    /// <summary>
    /// 浏览 iCal 文件
    /// </summary>
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 iCal 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("iCal 文件")
                {
                    Patterns = ["*.ics", "*.ical", "*.ifb"]
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = ["*.*"]
                }
            ]
        });

        if (files.Count > 0)
        {
            IcalFilePath = files[0].Path.LocalPath;
            await _plugin.RefreshAsync();
            UpdateStatus();
        }
    }

    /// <summary>
    /// 手动刷新
    /// </summary>
    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            StatusMessage = "正在刷新...";
            await _plugin.RefreshAsync();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 更新状态信息
    /// </summary>
    private void UpdateStatus()
    {
        if (string.IsNullOrWhiteSpace(IcalFilePath))
        {
            StatusMessage = "请先配置 iCal 文件路径。";
        }
        else if (!File.Exists(IcalFilePath))
        {
            StatusMessage = $"文件不存在: {IcalFilePath}";
        }
        else
        {
            var icalService = IAppHost.TryGetService<IcalService>();
            if (icalService != null)
            {
                var timeService = IAppHost.TryGetService<IExactTimeService>();
                var now = timeService?.GetCurrentLocalDateTime() ?? DateTime.Now;
                var events = icalService.GetTodayEvents(IcalFilePath, now);
                StatusMessage = $"已加载，当天共 {events.Count} 个非全天事件。文件: {IcalFilePath}";
            }
        }
    }

    // ---- INotifyPropertyChanged ----

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
