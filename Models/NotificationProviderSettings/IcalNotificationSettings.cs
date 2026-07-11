using CommunityToolkit.Mvvm.ComponentModel;

namespace iCalClassIsland.Models.NotificationProviderSettings;

/// <summary>
/// iCal 日程提醒设置（对标 ClassIsland ClassNotificationSettings）
/// </summary>
public class IcalNotificationSettings : ObservableRecipient
{
    private bool _isEventPreparingNotificationEnabled = true;
    private bool _isEventStartNotificationEnabled = true;
    private bool _isEventEndNotificationEnabled = true;
    private int _eventPreparingDeltaTime = 60;
    private bool _isSpeechEnabledOnEventPreparing = true;
    private bool _isSpeechEnabledOnEventStart = true;
    private bool _isSpeechEnabledOnEventEnd = true;
    private string _eventPreparingOverlayText = "准备开始，请回到座位并保持安静。";
    private string _eventPreparingMaskText = "即将开始";
    private string _eventStartMaskText = "开始";
    private string _eventEndMaskText = "结束";
    private string _eventEndOverlayText = "";
    private bool _showExtraInfo;
    private bool _showLocationInExtraInfo = true;
    private bool _showDescriptionInExtraInfo = true;
    private bool _stripLocationFromDescription;

    /// <summary>是否启用日程准备提醒</summary>
    public bool IsEventPreparingNotificationEnabled
    {
        get => _isEventPreparingNotificationEnabled;
        set => SetProperty(ref _isEventPreparingNotificationEnabled, value);
    }

    /// <summary>是否启用日程开始提醒</summary>
    public bool IsEventStartNotificationEnabled
    {
        get => _isEventStartNotificationEnabled;
        set => SetProperty(ref _isEventStartNotificationEnabled, value);
    }

    /// <summary>是否启用日程结束提醒</summary>
    public bool IsEventEndNotificationEnabled
    {
        get => _isEventEndNotificationEnabled;
        set => SetProperty(ref _isEventEndNotificationEnabled, value);
    }

    /// <summary>日程准备提醒提前秒数</summary>
    public int EventPreparingDeltaTime
    {
        get => _eventPreparingDeltaTime;
        set => SetProperty(ref _eventPreparingDeltaTime, value);
    }

    /// <summary>日程准备时是否启用语音</summary>
    public bool IsSpeechEnabledOnEventPreparing
    {
        get => _isSpeechEnabledOnEventPreparing;
        set => SetProperty(ref _isSpeechEnabledOnEventPreparing, value);
    }

    /// <summary>日程开始时是否启用语音</summary>
    public bool IsSpeechEnabledOnEventStart
    {
        get => _isSpeechEnabledOnEventStart;
        set => SetProperty(ref _isSpeechEnabledOnEventStart, value);
    }

    /// <summary>日程结束时是否启用语音</summary>
    public bool IsSpeechEnabledOnEventEnd
    {
        get => _isSpeechEnabledOnEventEnd;
        set => SetProperty(ref _isSpeechEnabledOnEventEnd, value);
    }

    /// <summary>日程准备提醒正文文字</summary>
    public string EventPreparingOverlayText
    {
        get => _eventPreparingOverlayText;
        set => SetProperty(ref _eventPreparingOverlayText, value);
    }

    /// <summary>日程准备提醒遮罩文字</summary>
    public string EventPreparingMaskText
    {
        get => _eventPreparingMaskText;
        set => SetProperty(ref _eventPreparingMaskText, value);
    }

    /// <summary>日程开始提醒遮罩文字</summary>
    public string EventStartMaskText
    {
        get => _eventStartMaskText;
        set => SetProperty(ref _eventStartMaskText, value);
    }

    /// <summary>日程结束提醒遮罩文字</summary>
    public string EventEndMaskText
    {
        get => _eventEndMaskText;
        set => SetProperty(ref _eventEndMaskText, value);
    }

    /// <summary>日程结束提醒正文文字</summary>
    public string EventEndOverlayText
    {
        get => _eventEndOverlayText;
        set => SetProperty(ref _eventEndOverlayText, value);
    }

    /// <summary>是否显示事件额外信息（地点、描述）</summary>
    public bool ShowExtraInfo
    {
        get => _showExtraInfo;
        set => SetProperty(ref _showExtraInfo, value);
    }

    /// <summary>在额外信息中显示地点</summary>
    public bool ShowLocationInExtraInfo
    {
        get => _showLocationInExtraInfo;
        set => SetProperty(ref _showLocationInExtraInfo, value);
    }

    /// <summary>在额外信息中显示描述</summary>
    public bool ShowDescriptionInExtraInfo
    {
        get => _showDescriptionInExtraInfo;
        set => SetProperty(ref _showDescriptionInExtraInfo, value);
    }

    /// <summary>从描述中去除与地点重复的信息</summary>
    public bool StripLocationFromDescription
    {
        get => _stripLocationFromDescription;
        set => SetProperty(ref _stripLocationFromDescription, value);
    }
}
