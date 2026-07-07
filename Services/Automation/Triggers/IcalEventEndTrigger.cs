using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using iCalClassIsland.Services;

namespace iCalClassIsland.Services.Automation.Triggers;

[TriggerInfo("ical.eventEnd", "iCal：下课", "")]
public class IcalEventEndTrigger(IcalStateService stateService) : TriggerBase
{
    public override void Loaded()
    {
        stateService.EventEnd += OnEventEnd;
    }

    public override void UnLoaded()
    {
        stateService.EventEnd -= OnEventEnd;
    }

    private void OnEventEnd(object? sender, EventArgs e)
    {
        Trigger();
    }
}
