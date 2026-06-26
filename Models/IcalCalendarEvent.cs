namespace iCalClassIsland.Models;

/// <summary>
/// 表示从 iCal 文件中解析出的单个日历事件
/// </summary>
public class IcalCalendarEvent
{
    /// <summary>
    /// 事件唯一标识符
    /// </summary>
    public string? Uid { get; set; }

    /// <summary>
    /// 事件标题/摘要
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// 事件描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 事件地点
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 事件开始时间
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// 事件结束时间
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>
    /// 是否为全天事件
    /// </summary>
    public bool IsAllDay { get; set; }
}
