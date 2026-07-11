using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using iCalClassIsland.Models.NotificationProviderSettings;

namespace iCalClassIsland.Services.NotificationProviders;

[NotificationProviderInfo("421cc54a-99f6-48f4-b01e-c72933c3f60c", "iCal 一天结束提醒", "",
    "在当天的所有 iCal 日程结束后发出提醒。")]
public class IcalAfterSchoolNotificationProvider : NotificationProviderBase<IcalAfterSchoolNotificationSettings>
{
    private IExactTimeService ExactTimeService { get; }
    private static DateTime? _lastDayEndTime;

    public IcalAfterSchoolNotificationProvider(
        INotificationHostService notificationHostService,
        IExactTimeService exactTimeService,
        IcalStateService icalStateService)
    {
        ExactTimeService = exactTimeService;

        icalStateService.DayEnd += OnDayEnd;
    }

    private void OnDayEnd(object? sender, EventArgs e)
    {
        if (!Settings.IsEnabled)
            return;

        var now = ExactTimeService.GetCurrentLocalDateTime();

        // Grace period: prevent stale/repeat notifications within 10 seconds
        if (_lastDayEndTime != null && (now - _lastDayEndTime.Value).TotalSeconds < 10)
            return;

        _lastDayEndTime = now;

        ShowNotification(new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask("一天结束", rightIcon: "lucide()"),
            OverlayContent = NotificationContent.CreateSimpleTextContent(
                Settings.NotificationMsg,
                x => x.Duration = TimeSpan.FromSeconds(Settings.OverlayDurationSeconds))
        });
    }
}
