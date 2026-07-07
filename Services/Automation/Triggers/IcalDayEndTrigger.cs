using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using iCalClassIsland.Services;

namespace iCalClassIsland.Services.Automation.Triggers;

[TriggerInfo("ical.dayEnd", "iCal：一天结束", "")]
public class IcalDayEndTrigger(IcalStateService stateService) : TriggerBase
{
    public override void Loaded()
    {
        stateService.DayEnd += OnDayEnd;
    }

    public override void UnLoaded()
    {
        stateService.DayEnd -= OnDayEnd;
    }

    private void OnDayEnd(object? sender, EventArgs e)
    {
        Trigger();
    }
}
