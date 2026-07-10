using ClassIsland.Core.Abstractions.Services;
using iCalClassIsland.Models;
using Microsoft.Extensions.Logging;

namespace iCalClassIsland.Services;

/// <summary>
/// 跟踪 iCal 事件状态变化，在事件开始、事件结束、当日日程全部结束时触发事件。
/// 供自动化触发器订阅，实现与 ClassIsland 课程表一致的信号机制。
/// </summary>
public class IcalStateService
{
    private readonly ILogger<IcalStateService> _logger;
    private readonly IRulesetService? _rulesetService;
    private string? _currentEventUid;
    private bool _dayEndFired;
    private DateTime _lastResetDate;

    /// <summary>当 iCal 事件开始时触发（等效于「上课」）</summary>
    public event EventHandler? EventStart;

    /// <summary>当 iCal 事件结束时触发（等效于「下课」）</summary>
    public event EventHandler? EventEnd;

    /// <summary>当当天所有 iCal 事件都已结束时触发（等效于「放学」）</summary>
    public event EventHandler? DayEnd;

    /// <summary>下一个事件的开始时间（供 PreTimePoint 触发器使用）</summary>
    public DateTime? NextEventStart { get; private set; }

    /// <summary>当前进行中事件的结束时间（供 PreTimePoint 触发器使用）</summary>
    public DateTime? CurrentEventEnd { get; private set; }

    /// <summary>当天最后一个事件的结束时间（供 PreTimePoint 触发器使用）</summary>
    public DateTime? LastEventEnd { get; private set; }

    /// <summary>当前进行中事件的摘要（供规则使用）</summary>
    public string? CurrentEventSummary { get; private set; }

    /// <summary>上一个已结束事件的摘要（供规则使用）</summary>
    public string? PreviousEventSummary { get; private set; }

    /// <summary>下一个即将开始事件的摘要（供规则使用）</summary>
    public string? NextEventSummary { get; private set; }

    public IcalStateService(ILogger<IcalStateService> logger, IRulesetService? rulesetService = null)
    {
        _logger = logger;
        _rulesetService = rulesetService;
    }

    /// <summary>
    /// 根据当前事件列表和时间更新状态，自动检测状态变化并触发相应事件。
    /// 应在每次 iCal 数据刷新后调用。
    /// </summary>
    public void UpdateState(List<IcalCalendarEvent> todayEvents, DateTime now)
    {
        // 日期变更时重置
        if (now.Date != _lastResetDate)
        {
            _dayEndFired = false;
            _currentEventUid = null;
            _lastResetDate = now.Date;
        }

        // 计算时间点信息（供 PreTimePoint 触发器使用）
        NextEventStart = todayEvents
            .Where(e => e.Start > now)
            .OrderBy(e => e.Start)
            .Select(e => (DateTime?)e.Start)
            .FirstOrDefault();
        CurrentEventEnd = todayEvents
            .Where(e => e.Start <= now && now < e.End)
            .Select(e => (DateTime?)e.End)
            .FirstOrDefault();
        LastEventEnd = todayEvents.Count > 0
            ? todayEvents.Max(e => e.End)
            : null;

        // 计算事件摘要（供规则使用）
        var prevCurrent = CurrentEventSummary;
        var prevPrevious = PreviousEventSummary;
        var prevNext = NextEventSummary;

        var currentEvent = todayEvents.FirstOrDefault(e => e.Start <= now && now < e.End);
        CurrentEventSummary = currentEvent?.Summary;
        PreviousEventSummary = todayEvents
            .Where(e => e.End <= now)
            .OrderByDescending(e => e.End)
            .Select(e => e.Summary)
            .FirstOrDefault();
        NextEventSummary = todayEvents
            .Where(e => e.Start > now)
            .OrderBy(e => e.Start)
            .Select(e => e.Summary)
            .FirstOrDefault();

        // 摘要变化时通知规则集重新评估
        if (prevCurrent != CurrentEventSummary || prevPrevious != PreviousEventSummary || prevNext != NextEventSummary)
        {
            _rulesetService?.NotifyStatusChanged();
        }

        // 找到当前正在进行的事件（第一个仍在进行中的非全天事件）
        var current = todayEvents.FirstOrDefault(e =>
            !e.IsAllDay && e.Start <= now && now < e.End);
        // 使用 UID 或 Start+Summary 作为稳定标识
        var newUid = current == null ? null : (current.Uid ?? $"{current.Start:O}|{current.Summary}");

        // 检测事件切换
        if (newUid != _currentEventUid)
        {
            // 上一个事件结束
            if (_currentEventUid != null)
            {
                _logger.LogInformation("iCal 事件结束（下课）: {Uid}", _currentEventUid);
                EventEnd?.Invoke(this, EventArgs.Empty);
            }

            // 新事件开始
            if (newUid != null)
            {
                _logger.LogInformation("iCal 事件开始（上课）: {Summary}", current!.Summary);
                EventStart?.Invoke(this, EventArgs.Empty);
            }

            _currentEventUid = newUid;
        }

        // 检测当天所有事件已结束
        if (!_dayEndFired && todayEvents.Count > 0 && todayEvents.All(e => now >= e.End))
        {
            _dayEndFired = true;
            _logger.LogInformation("iCal 当天事件全部结束（放学）");
            DayEnd?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>重置状态（插件重载时调用）</summary>
    public void Reset()
    {
        _currentEventUid = null;
        _dayEndFired = false;
    }
}
