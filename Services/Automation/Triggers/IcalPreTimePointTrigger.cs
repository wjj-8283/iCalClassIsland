using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using iCalClassIsland.Models;

namespace iCalClassIsland.Services.Automation.Triggers;

[TriggerInfo("ical.preTimePoint", "iCal：特定时间点前", "")]
public class IcalPreTimePointTrigger : TriggerBase<IcalPreTimePointTriggerSettings>
{
    private readonly IcalStateService _stateService;
    private readonly IExactTimeService _exactTimeService;
    private readonly ILessonsService _lessonsService;
    private DateTime _lastCheckTime;

    public IcalPreTimePointTrigger(
        IcalStateService stateService,
        IExactTimeService exactTimeService,
        ILessonsService lessonsService)
    {
        _stateService = stateService;
        _exactTimeService = exactTimeService;
        _lessonsService = lessonsService;
    }

    public override void Loaded()
    {
        _lastCheckTime = _exactTimeService.GetCurrentLocalDateTime();
        _lessonsService.PostMainTimerTicked += OnTimerTicked;
    }

    public override void UnLoaded()
    {
        _lessonsService.PostMainTimerTicked -= OnTimerTicked;
    }

    private void OnTimerTicked(object? sender, EventArgs e)
    {
        var now = _exactTimeService.GetCurrentLocalDateTime();

        try
        {
            if (double.IsNaN(Settings.TimeSeconds) || Settings.TimeSeconds < 0)
                return;

            // 根据设置获取目标时间点
            DateTime? targetDateTime = Settings.TargetTimePoint switch
            {
                IcalTimePoint.EventStart => _stateService.NextEventStart,
                IcalTimePoint.EventEnd => _stateService.CurrentEventEnd,
                IcalTimePoint.DayEnd => _stateService.LastEventEnd,
                _ => null,
            };

            if (targetDateTime == null)
                return;

            // 计算触发阈值时间 = 目标时间 - 提前秒数
            var thresholdTime = targetDateTime.Value - TimeSpan.FromSeconds(Settings.TimeSeconds);

            // 检查是否在两次 tick 之间跨越了阈值
            if (_lastCheckTime < thresholdTime && thresholdTime <= now)
            {
                Trigger();
            }
        }
        finally
        {
            _lastCheckTime = now;
        }
    }
}
