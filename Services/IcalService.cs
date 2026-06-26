using System.Text.RegularExpressions;
using iCalClassIsland.Models;
using Microsoft.Extensions.Logging;

namespace iCalClassIsland.Services;

/// <summary>
/// iCal 文件解析服务，负责读取 .ics 文件并提取事件
/// </summary>
public partial class IcalService
{
    private readonly ILogger<IcalService> _logger;

    private List<IcalCalendarEvent>? _cachedEvents;
    private string? _cachedFilePath;
    private DateTime _cacheDate;
    private DateTime _lastFileWriteTime;

    public IcalService(ILogger<IcalService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取今天的所有非全天事件（使用 ClassIsland 精确时间）
    /// </summary>
    public List<IcalCalendarEvent> GetTodayEvents(string icalFilePath, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(icalFilePath) || !File.Exists(icalFilePath))
        {
            return [];
        }

        var fileWriteTime = File.GetLastWriteTime(icalFilePath);

        // 如果文件路径没变、文件没被修改、且缓存是今天的，则使用缓存
        if (_cachedFilePath == icalFilePath &&
            _lastFileWriteTime == fileWriteTime &&
            _cacheDate.Date == now.Date &&
            _cachedEvents != null)
        {
            return _cachedEvents;
        }

        try
        {
            var rawText = File.ReadAllText(icalFilePath);
            var allEvents = ParseIcalEvents(rawText);
            var todayEvents = allEvents
                .Where(e => !e.IsAllDay && IsEventOnDate(e, now.Date))
                .OrderBy(e => e.Start)
                .ToList();

            _cachedEvents = todayEvents;
            _cachedFilePath = icalFilePath;
            _lastFileWriteTime = fileWriteTime;
            _cacheDate = now;

            _logger.LogDebug("解析 iCal 文件成功，今天共 {Count} 个非全天事件", todayEvents.Count);
            return todayEvents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 iCal 文件失败: {Path}", icalFilePath);
            return _cachedEvents ?? [];
        }
    }

    /// <summary>
    /// 获取指定日期范围内的所有非全天事件
    /// </summary>
    public List<IcalCalendarEvent> GetEvents(string icalFilePath, DateTime from, DateTime to)
    {
        if (string.IsNullOrWhiteSpace(icalFilePath) || !File.Exists(icalFilePath))
            return [];

        try
        {
            var rawText = File.ReadAllText(icalFilePath);
            var allEvents = ParseIcalEvents(rawText);
            return allEvents
                .Where(e => !e.IsAllDay && e.Start < to && e.End > from)
                .OrderBy(e => e.Start)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 iCal 文件失败: {Path}", icalFilePath);
            return [];
        }
    }

    /// <summary>
    /// 异步刷新，强制重新读取文件
    /// </summary>
    public Task RefreshAsync(string icalFilePath, DateTime now)
    {
        _cachedEvents = null;
        return Task.Run(() => GetTodayEvents(icalFilePath, now));
    }

    /// <summary>
    /// 判断事件是否在指定日期发生
    /// </summary>
    private static bool IsEventOnDate(IcalCalendarEvent e, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        return e.Start < dayEnd && e.End > dayStart;
    }

    /// <summary>
    /// 解析 iCal 文本中的所有事件（展开重复规则）
    /// </summary>
    internal List<IcalCalendarEvent> ParseIcalEvents(string icalText)
    {
        var events = new List<IcalCalendarEvent>();

        var unfolded = UnfoldLines(icalText);

        var veventPattern = VEventRegex();
        var matches = veventPattern.Matches(unfolded);

        foreach (Match match in matches)
        {
            var veventBlock = match.Groups[1].Value;
            var expandedEvents = ParseVEventWithRecurrence(veventBlock);
            if (expandedEvents != null)
            {
                events.AddRange(expandedEvents);
            }
        }

        return events;
    }

    /// <summary>
    /// 展开折叠的行（RFC 5545 §3.1）
    /// </summary>
    internal string UnfoldLines(string icalText)
    {
        var normalized = icalText.Replace("\r\n", "\n").Replace('\r', '\n');
        return LineFoldRegex().Replace(normalized, "");
    }

    /// <summary>
    /// 解析单个 VEVENT 块，展开 RRULE 重复
    /// </summary>
    private static List<IcalCalendarEvent>? ParseVEventWithRecurrence(string veventBlock)
    {
        var props = ParseProperties(veventBlock);

        // 解析基本属性
        var uid = GetPropertyValue(props, "UID");
        var summary = UnescapeText(GetPropertyValue(props, "SUMMARY") ?? "");
        var description = UnescapeText(GetPropertyValue(props, "DESCRIPTION") ?? "");
        var location = UnescapeText(GetPropertyValue(props, "LOCATION") ?? "");

        // 解析 DTSTART 和 DTEND
        var dtStartRaw = GetPropertyWithParams(props, "DTSTART");
        var dtEndRaw = GetPropertyWithParams(props, "DTEND");

        if (string.IsNullOrEmpty(dtStartRaw.value) || string.IsNullOrEmpty(dtEndRaw.value))
        {
            return null;
        }

        var isStartDateOnly = HasValueDateParam(dtStartRaw.parameters);
        var isEndDateOnly = HasValueDateParam(dtEndRaw.parameters);

        var start = ParseDateTime(dtStartRaw.value, isStartDateOnly);
        var end = ParseDateTime(dtEndRaw.value, isEndDateOnly);

        if (start == null || end == null)
        {
            return null;
        }

        var isAllDay = isStartDateOnly || isEndDateOnly ||
                       (start.Value.TimeOfDay == TimeSpan.Zero && end.Value.TimeOfDay == TimeSpan.Zero &&
                        (end.Value - start.Value).TotalHours >= 23.5);

        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "(无标题)";
        }

        // 创建基础事件
        var baseEvent = new IcalCalendarEvent
        {
            Uid = uid,
            Summary = summary,
            Description = description,
            Location = location,
            Start = start.Value,
            End = end.Value,
            IsAllDay = isAllDay,
        };

        // 解析 RRULE 并展开重复
        var rruleValue = GetPropertyValue(props, "RRULE");
        if (string.IsNullOrEmpty(rruleValue))
        {
            return [baseEvent]; // 无重复规则，返回单个事件
        }

        return ExpandRecurrences(baseEvent, rruleValue);
    }

    /// <summary>
    /// 展开 RRULE 重复规则，返回所有符合条件的事件实例
    /// </summary>
    private static List<IcalCalendarEvent> ExpandRecurrences(IcalCalendarEvent baseEvent, string rrule)
    {
        var result = new List<IcalCalendarEvent>();

        // 解析 RRULE 各部分
        var parts = rrule.Split(';');
        string? freq = null;
        int interval = 1;
        DateTime? until = null;
        int? count = null;

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            switch (kv[0].ToUpperInvariant())
            {
                case "FREQ":
                    freq = kv[1].ToUpperInvariant();
                    break;
                case "INTERVAL":
                    if (int.TryParse(kv[1], out var iv) && iv > 0)
                        interval = iv;
                    break;
                case "UNTIL":
                    until = ParseDateTime(kv[1], false);
                    break;
                case "COUNT":
                    if (int.TryParse(kv[1], out var cnt))
                        count = cnt;
                    break;
            }
        }

        // 不支持的频率，回退到单个事件
        if (freq != "WEEKLY")
        {
            result.Add(baseEvent);
            return result;
        }

        var duration = baseEvent.End - baseEvent.Start;
        var current = baseEvent.Start;
        var maxDate = until ?? baseEvent.Start.AddYears(1);
        int occurrenceCount = 0;

        while (current <= maxDate)
        {
            if (count.HasValue && occurrenceCount >= count.Value)
                break;

            result.Add(new IcalCalendarEvent
            {
                Uid = baseEvent.Uid,
                Summary = baseEvent.Summary,
                Description = baseEvent.Description,
                Location = baseEvent.Location,
                Start = current,
                End = current + duration,
                IsAllDay = baseEvent.IsAllDay,
            });

            occurrenceCount++;
            current = current.AddDays(7 * interval);
        }

        return result;
    }

    /// <summary>
    /// 将 VEVENT 块的文本内容解析为属性字典
    /// </summary>
    private static Dictionary<string, List<(string parameters, string value)>> ParseProperties(string block)
    {
        var result = new Dictionary<string, List<(string parameters, string value)>>(StringComparer.OrdinalIgnoreCase);

        var propPattern = PropertyRegex();
        var matches = propPattern.Matches(block);

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value.ToUpperInvariant();
            var parameters = match.Groups[2].Value;
            var value = match.Groups[3].Value;

            if (!result.ContainsKey(name))
            {
                result[name] = [];
            }
            result[name].Add((parameters, value));
        }

        return result;
    }

    /// <summary>
    /// 获取指定属性名的第一个值
    /// </summary>
    private static string? GetPropertyValue(Dictionary<string, List<(string parameters, string value)>> props, string name)
    {
        if (props.TryGetValue(name.ToUpperInvariant(), out var values) && values.Count > 0)
        {
            return values[0].value;
        }
        return null;
    }

    /// <summary>
    /// 获取指定属性名的第一个值及其参数
    /// </summary>
    private static (string parameters, string value) GetPropertyWithParams(Dictionary<string, List<(string parameters, string value)>> props, string name)
    {
        if (props.TryGetValue(name.ToUpperInvariant(), out var values) && values.Count > 0)
        {
            return values[0];
        }
        return ("", "");
    }

    /// <summary>
    /// 检查属性参数中是否有 VALUE=DATE
    /// </summary>
    private static bool HasValueDateParam(string parameters)
    {
        return parameters.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析 iCal 日期时间格式（支持本地时间、UTC、日期格式）
    /// </summary>
    private static DateTime? ParseDateTime(string value, bool isDateOnly)
    {
        if (isDateOnly)
        {
            if (value.Length >= 8 &&
                DateTime.TryParseExact(value[..8], "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
            return null;
        }

        var cleanValue = value.Trim();

        // UTC 时间：YYYYMMDDTHHmmssZ
        if (cleanValue.EndsWith("Z"))
        {
            cleanValue = cleanValue.TrimEnd('Z');
            if (DateTime.TryParseExact(cleanValue, "yyyyMMddTHHmmss", null,
                    System.Globalization.DateTimeStyles.AssumeUniversal |
                    System.Globalization.DateTimeStyles.AdjustToUniversal, out var utcDate))
            {
                return utcDate.ToLocalTime();
            }
        }

        // 本地时间：YYYYMMDDTHHmmss
        if (DateTime.TryParseExact(cleanValue, "yyyyMMddTHHmmss", null,
                System.Globalization.DateTimeStyles.AssumeLocal, out var localDate))
        {
            return localDate;
        }

        return null;
    }

    /// <summary>
    /// 反转义 iCal 文本中的特殊字符
    /// </summary>
    private static string UnescapeText(string text)
    {
        return text
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\")
            .Replace("\\N", "\n")
            .Replace("\\n", "\n");
    }

    [GeneratedRegex(@"\n[ \t]", RegexOptions.Multiline)]
    private static partial Regex LineFoldRegex();

    [GeneratedRegex(@"BEGIN:VEVENT\s*\n(.*?)END:VEVENT", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex VEventRegex();

    [GeneratedRegex(@"^([A-Za-z-]+)((?:;[^:]*?)?):(.+)$", RegexOptions.Multiline)]
    private static partial Regex PropertyRegex();
}
