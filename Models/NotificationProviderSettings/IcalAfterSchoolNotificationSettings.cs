using CommunityToolkit.Mvvm.ComponentModel;

namespace iCalClassIsland.Models.NotificationProviderSettings;

/// <summary>
/// iCal 一天结束提醒设置（对标 ClassIsland AfterSchoolNotificationProviderSettings）
/// </summary>
public class IcalAfterSchoolNotificationSettings : ObservableRecipient
{
    private bool _isEnabled = true;
    private string _notificationMsg = "今天的日程已全部结束。";
    private int _overlayDurationSeconds = 30;

    /// <summary>是否启用一天结束提醒</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>提醒显示文字</summary>
    public string NotificationMsg
    {
        get => _notificationMsg;
        set => SetProperty(ref _notificationMsg, value);
    }

    /// <summary>正文持续秒数</summary>
    public int OverlayDurationSeconds
    {
        get => _overlayDurationSeconds;
        set => SetProperty(ref _overlayDurationSeconds, value);
    }
}
