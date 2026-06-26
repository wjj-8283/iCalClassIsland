using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace iCalClassIsland.Models;

public class IcalPluginSettings : INotifyPropertyChanged
{
    private int _refreshIntervalMinutes = 5;

    /// <summary>iCal 文件路径列表</summary>
    public ObservableCollection<string> IcalFilePaths { get; set; } = [];

    /// <summary>自动刷新间隔（分钟）</summary>
    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set { if (value == _refreshIntervalMinutes) return; _refreshIntervalMinutes = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
