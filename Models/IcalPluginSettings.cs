using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace iCalClassIsland.Models;

/// <summary>
/// 插件级别的设置，保存在插件配置目录中
/// </summary>
public class IcalPluginSettings : INotifyPropertyChanged
{
    private string _icalFilePath = "";
    private int _refreshIntervalMinutes = 5;

    /// <summary>
    /// iCal 文件的路径
    /// </summary>
    public string IcalFilePath
    {
        get => _icalFilePath;
        set
        {
            if (value == _icalFilePath) return;
            _icalFilePath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 自动刷新间隔（分钟）
    /// </summary>
    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set
        {
            if (value == _refreshIntervalMinutes) return;
            _refreshIntervalMinutes = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
