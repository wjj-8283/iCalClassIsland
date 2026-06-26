using System.Collections.ObjectModel;
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

[SettingsPageInfo("ical-classisland.settings", "iCal 日程", category: SettingsPageCategory.External)]
public partial class IcalSettingsPage : SettingsPageBase
{
    private readonly Plugin _plugin;

    public IcalSettingsPage()
    {
        InitializeComponent();
        _plugin = IAppHost.GetService<Plugin>()!;

        // 绑定文件列表
        FileListBox.ItemsSource = _plugin.PluginSettings.IcalFilePaths;

        // 刷新间隔
        NumRefreshInterval.Value = _plugin.PluginSettings.RefreshIntervalMinutes;
        NumRefreshInterval.ValueChanged += (_, _) =>
            _plugin.PluginSettings.RefreshIntervalMinutes = (int)(NumRefreshInterval.Value ?? 5);

        // 列表变更监听
        _plugin.PluginSettings.IcalFilePaths.CollectionChanged += (_, _) => _plugin.NotifyConfigChanged();

        UpdateStatus();
    }

    private async void OnAddFile(object? s, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 iCal 文件",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("iCal 文件") { Patterns = ["*.ics", "*.ical", "*.ifb"] },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
            ]
        });

        foreach (var f in files)
        {
            var path = f.Path.LocalPath;
            if (!_plugin.PluginSettings.IcalFilePaths.Contains(path))
                _plugin.PluginSettings.IcalFilePaths.Add(path);
        }
        if (files.Count > 0) { await _plugin.RefreshAsync(); UpdateStatus(); }
    }

    private void OnRemoveFile(object? s, RoutedEventArgs e)
    {
        var selected = FileListBox.SelectedItem as string;
        if (selected != null)
            _plugin.PluginSettings.IcalFilePaths.Remove(selected);
        UpdateStatus();
    }

    private async void OnRefreshClick(object? s, RoutedEventArgs e)
    {
        StatusText.Text = "正在刷新...";
        await _plugin.RefreshAsync();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var paths = _plugin.PluginSettings.IcalFilePaths;
        if (paths.Count == 0)
        {
            StatusText.Text = "请添加至少一个 iCal 文件。";
            return;
        }

        var missing = paths.Where(f => !File.Exists(f)).ToList();
        var valid = paths.Where(File.Exists).ToList();

        var svc = IAppHost.TryGetService<IcalService>();
        var ts = IAppHost.TryGetService<IExactTimeService>();
        var now = ts?.GetCurrentLocalDateTime() ?? DateTime.Now;

        int total = 0;
        foreach (var f in valid)
            total += svc?.GetTodayEvents(f, now).Count ?? 0;

        var msg = $"共 {paths.Count} 个文件，当天 {total} 个事件。";
        if (missing.Count > 0)
            msg += $"\n未找到: {string.Join(", ", missing.Select(Path.GetFileName))}";
        StatusText.Text = msg;
    }
}
