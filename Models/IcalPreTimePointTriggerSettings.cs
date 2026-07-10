namespace iCalClassIsland.Models;

/// <summary>
/// iCal 时间参考点类型
/// </summary>
public enum IcalTimePoint
{
    /// <summary>事件开始</summary>
    EventStart,
    /// <summary>事件结束</summary>
    EventEnd,
    /// <summary>一天结束</summary>
    DayEnd
}

/// <summary>
/// iCal 特定时间点前触发器的设置
/// </summary>
public class IcalPreTimePointTriggerSettings : System.ComponentModel.INotifyPropertyChanged
{
    private IcalTimePoint _targetTimePoint = IcalTimePoint.EventStart;
    private double _timeSeconds = 60;

    /// <summary>目标时间参考点</summary>
    public IcalTimePoint TargetTimePoint
    {
        get => _targetTimePoint;
        set { if (value == _targetTimePoint) return; _targetTimePoint = value; OnPropertyChanged(); }
    }

    /// <summary>提前秒数</summary>
    public double TimeSeconds
    {
        get => _timeSeconds;
        set { if (Math.Abs(value - _timeSeconds) < 0.001) return; _timeSeconds = value; OnPropertyChanged(); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
