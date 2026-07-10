using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace iCalClassIsland.Services.Automation.Triggers;

[TriggerInfo("ical.currentEventStateChanged", "iCal：当前事件状态变化时", "")]
public class IcalCurrentEventStateChangedTrigger(IcalStateService stateService) : TriggerBase
{
    public override void Loaded()
    {
        stateService.CurrentEventStateChanged += OnStateChanged;
    }

    public override void UnLoaded()
    {
        stateService.CurrentEventStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        Trigger();
    }
}
