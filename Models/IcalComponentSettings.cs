using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace iCalClassIsland.Models;

/// <summary>
/// 组件级别的设置
/// </summary>
public class IcalComponentSettings : INotifyPropertyChanged
{
    private int _extraInfoType;
    private bool _isCountdownEnabled;
    private int _countdownSeconds = 60;
    private bool _hideFinishedEvents;
    private bool _showProgressBar = true;
    private bool _showPlaceholderOnEmpty = true;
    private string _placeholderTextNoEvents = "今天没有日程。";
    private string _placeholderTextAllEnded = "今日日程已全部结束。";
    private double _scheduleSpacing = 1.0;
    private bool _showExtraInfoOnTimePoint = true;
    private int _tomorrowShowMode; // 0=不显示, 1=占位时显示, 2=始终显示

    public int ExtraInfoType { get => _extraInfoType; set { if (value == _extraInfoType) return; _extraInfoType = value; OnPropertyChanged(); } }
    public bool IsCountdownEnabled { get => _isCountdownEnabled; set { if (value == _isCountdownEnabled) return; _isCountdownEnabled = value; OnPropertyChanged(); } }
    public int CountdownSeconds { get => _countdownSeconds; set { if (value == _countdownSeconds) return; _countdownSeconds = value; OnPropertyChanged(); } }
    public bool HideFinishedEvents { get => _hideFinishedEvents; set { if (value == _hideFinishedEvents) return; _hideFinishedEvents = value; OnPropertyChanged(); } }
    public bool ShowProgressBar { get => _showProgressBar; set { if (value == _showProgressBar) return; _showProgressBar = value; OnPropertyChanged(); } }
    public bool ShowPlaceholderOnEmpty { get => _showPlaceholderOnEmpty; set { if (value == _showPlaceholderOnEmpty) return; _showPlaceholderOnEmpty = value; OnPropertyChanged(); } }
    public string PlaceholderTextNoEvents { get => _placeholderTextNoEvents; set { if (value == _placeholderTextNoEvents) return; _placeholderTextNoEvents = value; OnPropertyChanged(); } }
    public string PlaceholderTextAllEnded { get => _placeholderTextAllEnded; set { if (value == _placeholderTextAllEnded) return; _placeholderTextAllEnded = value; OnPropertyChanged(); } }
    public double ScheduleSpacing { get => _scheduleSpacing; set { if (value == _scheduleSpacing) return; _scheduleSpacing = value; OnPropertyChanged(); } }
    public bool ShowExtraInfoOnTimePoint { get => _showExtraInfoOnTimePoint; set { if (value == _showExtraInfoOnTimePoint) return; _showExtraInfoOnTimePoint = value; OnPropertyChanged(); } }
    /// <summary>显示明天日程的模式：0=不显示, 1=今日结束后, 2=今日空时, 3=始终</summary>
    public int TomorrowShowMode { get => _tomorrowShowMode; set { if (value == _tomorrowShowMode) return; _tomorrowShowMode = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
