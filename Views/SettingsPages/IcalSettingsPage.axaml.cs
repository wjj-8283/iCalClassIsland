using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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

    public string VersionText { get; }
    public string HashText { get; }
    public bool IsDirty { get; }

    public IcalSettingsPage()
    {
        InitializeComponent();
        _plugin = IAppHost.GetService<Plugin>()!;

        // Read version info from embedded assembly metadata
        var assembly = Assembly.GetExecutingAssembly();
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value);

        var rawTag = metadata.GetValueOrDefault("GitTag") ?? "unknown";
        var hash = metadata.GetValueOrDefault("GitHash") ?? "unknown";
        IsDirty = metadata.GetValueOrDefault("GitIsDirty") == "true";

        // Parse version from git describe format: tag-N-gXXXXXXX
        var version = rawTag;
        var gIndex = rawTag.LastIndexOf("-g");
        if (gIndex > 0)
        {
            var tagWithCommits = rawTag[..gIndex];       // "tag-N"
            var dashIndex = tagWithCommits.LastIndexOf('-');
            version = dashIndex > 0 ? tagWithCommits[..dashIndex] : tagWithCommits;
        }

        VersionText = version;
        HashText = hash;

        DataContext = this;

        // Load plugin icon for the about card
        var pluginDir = Path.GetDirectoryName(assembly.Location)!;
        var iconPath = Path.Combine(pluginDir, "icon.png");
        if (File.Exists(iconPath))
        {
            AboutIcon.Source = new Bitmap(iconPath);
        }

        // 绑定文件列表
        FileListBox.ItemsSource = _plugin.PluginSettings.IcalFilePaths;

        // 刷新间隔
        NumRefreshInterval.Value = _plugin.PluginSettings.RefreshIntervalMinutes;
        NumRefreshInterval.ValueChanged += (_, _) =>
            _plugin.PluginSettings.RefreshIntervalMinutes = (int)(NumRefreshInterval.Value ?? 5);

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

    private void OnAddUrl(object? s, RoutedEventArgs e)
    {
        UrlInputPanel.IsVisible = true;
        UrlTextBox.Text = "";
        UrlTextBox.Focus();
    }

    private async void OnConfirmUrl(object? s, RoutedEventArgs e)
    {
        var url = UrlTextBox.Text?.Trim();
        UrlInputPanel.IsVisible = false;

        if (string.IsNullOrWhiteSpace(url))
            return;

        // 基本 URL 验证
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "请输入有效的 HTTP/HTTPS URL。";
            return;
        }

        if (_plugin.PluginSettings.IcalFilePaths.Contains(url))
        {
            StatusText.Text = "该 URL 已存在。";
            return;
        }

        // 尝试预取并缓存
        StatusText.Text = $"正在从 {url} 获取 iCal 文件...";
        var svc = IAppHost.TryGetService<IcalService>();
        if (svc != null)
        {
            var success = svc.TryFetchAndCache(url);
            if (success)
            {
                _plugin.PluginSettings.IcalFilePaths.Add(url);
                StatusText.Text = $"已添加 URL 并缓存成功: {url}";
            }
            else
            {
                // 即使远程不可用，也添加到列表（可以手动刷新后使用缓存）
                _plugin.PluginSettings.IcalFilePaths.Add(url);
                StatusText.Text = $"已添加 URL 但远程获取失败: {url}\n将尝试使用缓存数据。";
            }
        }
        else
        {
            _plugin.PluginSettings.IcalFilePaths.Add(url);
        }

        await _plugin.RefreshAsync();
        UpdateStatus();
    }

    private void OnCancelUrl(object? s, RoutedEventArgs e)
    {
        UrlInputPanel.IsVisible = false;
    }

    private void OnRemoveFile(object? s, RoutedEventArgs e)
    {
        var selected = FileListBox.SelectedItem as string;
        if (selected != null)
        {
            _plugin.PluginSettings.IcalFilePaths.Remove(selected);
            // 如果是 web URL，清理缓存
            if (IcalService.IsWebUrlPath(selected))
            {
                var svc = IAppHost.TryGetService<IcalService>();
                svc?.RemoveCache(selected);
            }
        }
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

        var svc = IAppHost.TryGetService<IcalService>();
        var ts = IAppHost.TryGetService<IExactTimeService>();
        var now = ts?.GetCurrentLocalDateTime() ?? DateTime.Now;

        var webUrls = paths.Where(IcalService.IsWebUrlPath).ToList();
        var localPaths = paths.Where(p => !IcalService.IsWebUrlPath(p)).ToList();
        var missing = localPaths.Where(f => !File.Exists(f)).ToList();
        var valid = localPaths.Where(File.Exists).ToList();

        int total = 0;
        foreach (var f in valid)
            total += svc?.GetTodayEvents(f, now).Count ?? 0;

        // 也统计 web URL 的事件
        foreach (var url in webUrls)
            total += svc?.GetTodayEvents(url, now).Count ?? 0;

        var lines = new List<string>();
        lines.Add($"共 {paths.Count} 个数据源，当天 {total} 个事件。");

        if (missing.Count > 0)
            lines.Add($"⚠ 未找到本地文件: {string.Join(", ", missing.Select(Path.GetFileName))}");

        // 显示每个 web URL 的状态
        foreach (var url in webUrls)
        {
            var status = svc?.GetRemoteStatus(url);
            var displayUrl = url.Length > 60 ? url[..57] + "..." : url;
            switch (status)
            {
                case true:
                    lines.Add($"✓ 远程在线: {displayUrl}");
                    break;
                case false:
                    lines.Add($"⚠ 远程不可用，使用缓存: {displayUrl}");
                    break;
                case null:
                    lines.Add($"○ 等待检测: {displayUrl}");
                    break;
            }
        }

        StatusText.Text = string.Join("\n", lines);
    }

    private static void OnOpenGitHub(object? s, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/wjj-8283/iCalClassIsland") { UseShellExecute = true });

    private static void OnOpenIssue(object? s, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/wjj-8283/iCalClassIsland/issues/new") { UseShellExecute = true });

    private static void OnOpenLicense(object? s, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/wjj-8283/iCalClassIsland/blob/main/LICENSE") { UseShellExecute = true });
}
