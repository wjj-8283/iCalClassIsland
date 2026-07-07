using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using iCalClassIsland.Services;

namespace iCalClassIsland.Services.Automation.Triggers;

[TriggerInfo("ical.eventStart", "iCal：上课", "")]
public class IcalEventStartTrigger(IcalStateService stateService) : TriggerBase
{
    public override void Loaded()
    {
        stateService.EventStart += OnEventStart;
    }

    public override void UnLoaded()
    {
        stateService.EventStart -= OnEventStart;
    }

    private void OnEventStart(object? sender, EventArgs e)
    {
        Trigger();
    }
}
