using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using iCalClassIsland.Controls.NotificationProviders;
using iCalClassIsland.Models;
using iCalClassIsland.Models.NotificationProviderSettings;

namespace iCalClassIsland.Services.NotificationProviders;

[NotificationProviderInfo("fce6e0bd-5072-435b-8656-51be1c244ba2", "iCal 日程提醒", "",
    "在 iCal 日程开始前、开始时和结束时发出醒目提醒，并预告下一个日程。")]
[NotificationChannelInfo(PrepareChannelId, "日程准备提醒", "", description: "在日程开始前指定时间发出提醒。")]
[NotificationChannelInfo(StartChannelId, "日程开始提醒", "", description: "在日程开始时发出提醒。")]
[NotificationChannelInfo(EndChannelId, "日程结束提醒", "", description: "在日程结束时发出提醒。")]
public class IcalNotificationProvider : NotificationProviderBase<IcalNotificationSettings>
{
    public const string PrepareChannelId = "ba53ddee-28a7-44c4-a2f4-7642cd63ebb6";
    public const string StartChannelId = "578a313c-aa46-4eb7-867b-2a26b478f410";
    public const string EndChannelId = "fc76a611-57d1-42de-82fb-22ecb71fb257";

    private bool _isPrepareNotified;

    private NotificationRequest? _prepareNotificationRequest;

    private ILessonsService LessonsService { get; }
    private IExactTimeService ExactTimeService { get; }
    private IcalStateService IcalStateService { get; }

    public IcalNotificationProvider(
        INotificationHostService notificationHostService,
        ILessonsService lessonsService,
        IExactTimeService exactTimeService,
        IcalStateService icalStateService)
    {
        LessonsService = lessonsService;
        ExactTimeService = exactTimeService;
        IcalStateService = icalStateService;

        IcalStateService.EventStart += OnEventStart;
        IcalStateService.EventEnd += OnEventEnd;
        LessonsService.PostMainTimerTicked += OnTimerTick;
    }

    #region Timer Tick — Prepare Reminder

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var nextStart = IcalStateService.NextEventStart;
        if (nextStart == null)
        {
            // No upcoming event — cancel any lingering prepare notification
            if (_isPrepareNotified)
            {
                CancelPrepareNotification();
            }
            return;
        }

        var now = ExactTimeService.GetCurrentLocalDateTime();
        var remaining = nextStart.Value - now;
        var deltaThreshold = TimeSpan.FromSeconds(Settings.EventPreparingDeltaTime);

        if (remaining > TimeSpan.Zero && remaining <= deltaThreshold && Settings.IsEventPreparingNotificationEnabled)
        {
            if (_isPrepareNotified)
                return;

            _isPrepareNotified = true;
            var deltaTime = CalculateDeltaTime(Settings.EventPreparingDeltaTime, remaining);
            var prepareRequest = _prepareNotificationRequest = BuildPrepareNotificationRequest(deltaTime);
            List<NotificationRequest> requests = [prepareRequest];

            if (Settings.IsEventStartNotificationEnabled)
            {
                var startRequest = BuildStartNotificationRequest();
                requests.Add(startRequest);
            }

            ShowChainedNotifications(requests.ToArray());
        }
        else if (_isPrepareNotified)
        {
            CancelPrepareNotification();
        }
    }

    private static TimeSpan CalculateDeltaTime(int settingsDeltaTime, TimeSpan remaining)
    {
        var deltaTime = TimeSpan.FromSeconds(settingsDeltaTime) - remaining;
        return deltaTime > TimeSpan.FromSeconds(10) ? remaining : TimeSpan.FromSeconds(settingsDeltaTime);
    }

    private void CancelPrepareNotification()
    {
        _prepareNotificationRequest?.Cancel();
        _prepareNotificationRequest = null;
        _isPrepareNotified = false;
    }

    #endregion

    #region Event Start Reminder

    private void OnEventStart(object? sender, EventArgs e)
    {
        if (!Settings.IsEventStartNotificationEnabled)
            return;

        // Cancel any lingering prepare chain — the event has started, prepare is obsolete
        if (_isPrepareNotified)
        {
            CancelPrepareNotification();
        }

        Channel(StartChannelId).ShowNotification(BuildStartNotificationRequest());
    }

    #endregion

    #region Event End Reminder

    private void OnEventEnd(object? sender, EventArgs e)
    {
        if (!Settings.IsEventEndNotificationEnabled)
            return;

        // Skip event-end notification when there's no next event —
        // the "day end" reminder (IcalAfterSchoolNotificationProvider) handles the final wrap-up.
        if (IcalStateService.NextEventSummary == null)
            return;

        var overlayText = Settings.EventEndOverlayText;
        var showOverlayText = !string.IsNullOrWhiteSpace(overlayText);
        var extraInfo = Settings.ShowExtraInfo ? FormatExtraInfo(
            IcalStateService.NextEventDescription, IcalStateService.NextEventLocation, Settings.ShowDescriptionInExtraInfo, Settings.ShowLocationInExtraInfo, Settings.StripLocationFromDescription) : "";

        var overlayControl = IcalNotificationOverlayControl.CreateEndOverlay(ExactTimeService);
        overlayControl.CurrentEventSummary = IcalStateService.PreviousEventSummary ?? "";
        overlayControl.NextEventSummary = IcalStateService.NextEventSummary ?? "";
        overlayControl.NextEventStart = IcalStateService.NextEventStart?.ToString("HH:mm") ?? "";
        overlayControl.Message = overlayText;
        overlayControl.ShowExtraInfo = Settings.ShowExtraInfo;
        overlayControl.CurrentEventExtraInfo = Settings.ShowExtraInfo
            ? FormatExtraInfo(IcalStateService.CurrentEventDescription, IcalStateService.CurrentEventLocation, Settings.ShowDescriptionInExtraInfo, Settings.ShowLocationInExtraInfo, Settings.StripLocationFromDescription)
            : "";

        var overlayContent = new NotificationContent(overlayControl)
        {
            Duration = showOverlayText ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(10),
            SpeechContent = $"「{IcalStateService.PreviousEventSummary}」已结束。" +
                            $"下一个日程是：{IcalStateService.NextEventSummary}。" +
                            (!string.IsNullOrEmpty(extraInfo) ? $"额外信息：{extraInfo}。" : "") +
                            $"{overlayText}",
            IsSpeechEnabled = Settings.IsSpeechEnabledOnEventEnd
        };

        Channel(EndChannelId).ShowNotification(new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(Settings.EventEndMaskText,
                rightIcon: "lucide()",
                factory: x =>
                {
                    x.Duration = TimeSpan.FromSeconds(2);
                    x.SpeechContent = $"「{IcalStateService.PreviousEventSummary}」已结束。" +
                                      $"下一个日程是：{IcalStateService.NextEventSummary}。" +
                                      (!string.IsNullOrEmpty(extraInfo) ? $"额外信息：{extraInfo}。" : "");
                    x.IsSpeechEnabled = Settings.IsSpeechEnabledOnEventEnd;
                }),
            OverlayContent = overlayContent
        });
    }

    #endregion

    #region Notification Request Builders

    private NotificationRequest BuildPrepareNotificationRequest(TimeSpan deltaTime)
    {
        var nextSummary = IcalStateService.NextEventSummary ?? "未知日程";
        var nextStartTime = IcalStateService.NextEventStart?.ToString("HH:mm") ?? "";
        var overlayText = Settings.EventPreparingOverlayText;
        var extraInfo = Settings.ShowExtraInfo
            ? FormatExtraInfo(IcalStateService.NextEventDescription, IcalStateService.NextEventLocation, Settings.ShowDescriptionInExtraInfo, Settings.ShowLocationInExtraInfo, Settings.StripLocationFromDescription)
            : "";

        var overlayControl = IcalNotificationOverlayControl.CreatePrepareOverlay(ExactTimeService);
        overlayControl.NextEventSummary = nextSummary;
        overlayControl.NextEventStart = nextStartTime;
        overlayControl.NextEventExtraInfo = extraInfo;
        overlayControl.ShowExtraInfo = Settings.ShowExtraInfo;
        overlayControl.Message = overlayText;
        overlayControl.TargetTime = IcalStateService.NextEventStart ?? ExactTimeService.GetCurrentLocalDateTime();
        overlayControl.CurrentEventSummary = IcalStateService.CurrentEventSummary ?? "";

        var request = new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(
                Settings.EventPreparingMaskText,
                rightIcon: "lucide()",
                factory: x =>
                {
                    x.SpeechContent = $"距开始还剩{FormatTimeSpan(deltaTime)}。";
                    x.Duration = TimeSpan.FromSeconds(3);
                    x.IsSpeechEnabled = Settings.IsSpeechEnabledOnEventPreparing;
                }),
            OverlayContent = new NotificationContent(overlayControl)
            {
                SpeechContent = $"{overlayText} 下一个日程是：{nextSummary}" +
                                (!string.IsNullOrEmpty(extraInfo) ? $"，{extraInfo}" : ""),
                EndTime = new DateTime(DateOnly.FromDateTime(ExactTimeService.GetCurrentLocalDateTime()),
                    TimeOnly.FromDateTime(IcalStateService.NextEventStart ?? ExactTimeService.GetCurrentLocalDateTime())),
                IsSpeechEnabled = Settings.IsSpeechEnabledOnEventPreparing
            },
            ChannelId = Guid.Parse(PrepareChannelId)
        };

        return request;
    }

    private NotificationRequest BuildStartNotificationRequest()
    {
        var nextSummary = IcalStateService.CurrentEventSummary ?? "未知日程";
        var request = new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(
                Settings.EventStartMaskText,
                rightIcon: "lucide()",
                factory: x =>
                {
                    x.SpeechContent = $"「{nextSummary}」开始了。";
                    x.IsSpeechEnabled = Settings.IsSpeechEnabledOnEventStart;
                }),
            ChannelId = Guid.Parse(StartChannelId)
        };

        return request;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 格式化 TimeSpan 为人类可读的中文格式（如 "1分30秒"）
    /// </summary>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds < 60)
            return $"{(int)timeSpan.TotalSeconds}秒";
        var minutes = (int)timeSpan.TotalMinutes;
        var seconds = timeSpan.Seconds;
        return seconds > 0 ? $"{minutes}分{seconds}秒" : $"{minutes}分钟";
    }

    /// <summary>
    /// 格式化事件额外信息，模仿 ClassIsland FormatTeacher 的小字显示方式
    /// </summary>
    internal static string FormatExtraInfo(string? description, string? location,
        bool showDescription = true, bool showLocation = true, bool stripLocationFromDescription = false)
    {
        // 多行文本转单行
        if (!string.IsNullOrWhiteSpace(description))
        {
            description = description.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            description = System.Text.RegularExpressions.Regex.Replace(description, @"\s+", " ").Trim();
        }

        if (stripLocationFromDescription && !string.IsNullOrWhiteSpace(location) && !string.IsNullOrWhiteSpace(description))
        {
            // 对地点拆词，在描述中逐个移除匹配的词
            var delimiters = new[] { ' ', '\n', '\r', '\t', '|', ',', ';', '/', '\\', '—', '-', '：', ':' };
            var locationTokens = location.Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 2);
            foreach (var token in locationTokens)
            {
                description = System.Text.RegularExpressions.Regex.Replace(
                    description, System.Text.RegularExpressions.Regex.Escape(token), "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            description = System.Text.RegularExpressions.Regex.Replace(description, @"\s+", " ").Trim();
        }

        var parts = new List<string>();
        if (showLocation && !string.IsNullOrWhiteSpace(location))
            parts.Add(location);
        if (showDescription && !string.IsNullOrWhiteSpace(description))
            parts.Add(description);
        return string.Join(" | ", parts);
    }

    #endregion
}
