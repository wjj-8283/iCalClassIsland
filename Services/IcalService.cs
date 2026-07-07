using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using iCalClassIsland.Models;
using Microsoft.Extensions.Logging;

namespace iCalClassIsland.Services;

/// <summary>
/// iCal 文件解析服务，负责读取 .ics 文件并提取事件。
/// 支持本地文件路径和远程 URL（如 GitHub Raw），远程文件会自动缓存到本地。
/// </summary>
public partial class IcalService
{
    private readonly ILogger<IcalService> _logger;
    private readonly HttpClient _httpClient;
    private string _cacheDir = "";

    /// <summary>记录每个 URL 的远程访问状态（true=远程可访问，false=不可用，null=未检测）</summary>
    private readonly Dictionary<string, bool?> _remoteStatus = [];
    private readonly object _statusLock = new();

    /// <summary>每个数据源的原始文本缓存（按文件路径索引）</summary>
    private readonly Dictionary<string, CachedFileData> _textCache = [];
    private DateTime _cacheDate;

    private sealed class CachedFileData
    {
        /// <summary>原始 iCal 文本</summary>
        public string RawText { get; set; } = "";
        /// <summary>文件最后写入时间（本地文件）或 DateTime.MinValue（web URL）</summary>
        public DateTime LastWriteTime { get; set; }
    }

    /// <summary>
    /// 获取数据源的原始文本。先查内存缓存，缓存未命中则从文件/URL 读取并缓存。
    /// </summary>
    private string? GetRawText(string path)
    {
        var isWeb = IsWebUrl(path);
        var lastWrite = isWeb ? DateTime.MinValue : File.GetLastWriteTime(path);

        // 日期变了，清空缓存
        if (_cacheDate.Date != DateTime.Now.Date)
        {
            _textCache.Clear();
            _cacheDate = DateTime.Now;
        }

        // 命中缓存
        if (_textCache.TryGetValue(path, out var cached) &&
            (isWeb || cached.LastWriteTime == lastWrite))
        {
            return cached.RawText;
        }

        // 获取内容
        string? rawText;
        if (isWeb)
        {
            rawText = FetchIcalContent(path);
        }
        else
        {
            try { rawText = File.ReadAllText(path); }
            catch { rawText = null; }
        }

        if (rawText != null)
        {
            _textCache[path] = new CachedFileData { RawText = rawText, LastWriteTime = lastWrite };
        }

        return rawText;
    }

    public IcalService(ILogger<IcalService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("iCalClassIsland-Plugin/1.0");
    }

    /// <summary>
    /// 初始化缓存目录（由 Plugin 在启动时调用）
    /// </summary>
    public void InitializeCache(string cacheDir)
    {
        _cacheDir = cacheDir;
        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// 判断路径是否为远程 URL
    /// </summary>
    private static bool IsWebUrl(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 根据 URL 计算缓存文件路径（基于 SHA256 哈希）
    /// </summary>
    private string GetCachePath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        return Path.Combine(_cacheDir, $"{hash}.ics");
    }

    /// <summary>
    /// 确保缓存目录存在
    /// </summary>
    private void EnsureCacheDir()
    {
        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// 获取今天的所有非全天事件（使用 ClassIsland 精确时间）
    /// </summary>
    public List<IcalCalendarEvent> GetTodayEvents(string icalFilePath, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(icalFilePath))
            return [];

        if (!IsWebUrl(icalFilePath) && !File.Exists(icalFilePath))
            return [];

        try
        {
            var rawText = GetRawText(icalFilePath);
            if (rawText == null)
                return [];

            var allEvents = ParseIcalEvents(rawText);
            return allEvents
                .Where(e => !e.IsAllDay && IsEventOnDate(e, now.Date))
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
    /// 获取指定日期范围内的所有非全天事件
    /// </summary>
    public List<IcalCalendarEvent> GetEvents(string icalFilePath, DateTime from, DateTime to)
    {
        if (string.IsNullOrWhiteSpace(icalFilePath))
            return [];

        if (!IsWebUrl(icalFilePath) && !File.Exists(icalFilePath))
            return [];

        try
        {
            var rawText = GetRawText(icalFilePath);
            if (rawText == null)
                return [];

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
    /// 多文件合并：获取今天事件
    /// </summary>
    public List<IcalCalendarEvent> GetTodayEventsMerged(IEnumerable<string> paths, DateTime now)
    {
        var all = new List<IcalCalendarEvent>();
        foreach (var path in paths)
            all.AddRange(GetTodayEvents(path, now));
        return all.OrderBy(e => e.Start).ToList();
    }

    /// <summary>
    /// 多文件合并：获取日期范围事件
    /// </summary>
    public List<IcalCalendarEvent> GetEventsMerged(IEnumerable<string> paths, DateTime from, DateTime to)
    {
        var all = new List<IcalCalendarEvent>();
        foreach (var path in paths)
            all.AddRange(GetEvents(path, from, to));
        return all.OrderBy(e => e.Start).ToList();
    }

    /// <summary>
    /// 获取远程 URL 的 iCal 内容，优先从远程获取，失败时回退到缓存。
    /// 返回 null 表示远程和缓存都不可用。
    /// </summary>
    private string? FetchIcalContent(string url)
    {
        EnsureCacheDir();
        var cachePath = GetCachePath(url);

        // 尝试从远程获取
        try
        {
            var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                // 缓存到本地
                File.WriteAllText(cachePath, content);
                SetRemoteStatus(url, true);
                _logger.LogDebug("从远程获取 iCal 成功: {Url}", url);
                return content;
            }

            _logger.LogWarning("远程返回非成功状态码 {StatusCode}: {Url}", (int)response.StatusCode, url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从远程获取 iCal 失败: {Url}", url);
        }

        // 回退到缓存
        SetRemoteStatus(url, false);
        if (File.Exists(cachePath))
        {
            _logger.LogInformation("使用缓存的 iCal: {Url} -> {CachePath}", url, cachePath);
            return File.ReadAllText(cachePath);
        }

        _logger.LogError("远程和缓存均不可用: {Url}", url);
        return null;
    }

    /// <summary>
    /// 预取并缓存一个 URL（在添加 URL 时调用）
    /// </summary>
    public bool TryFetchAndCache(string url)
    {
        EnsureCacheDir();
        var cachePath = GetCachePath(url);

        try
        {
            var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                File.WriteAllText(cachePath, content);
                SetRemoteStatus(url, true);
                _logger.LogInformation("预取并缓存 iCal 成功: {Url}", url);
                return true;
            }

            _logger.LogWarning("预取失败，状态码 {StatusCode}: {Url}", (int)response.StatusCode, url);
            SetRemoteStatus(url, false);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "预取失败: {Url}", url);
            SetRemoteStatus(url, false);
            return false;
        }
    }

    /// <summary>
    /// 删除 URL 对应的缓存文件
    /// </summary>
    public void RemoveCache(string url)
    {
        var cachePath = GetCachePath(url);
        try
        {
            if (File.Exists(cachePath))
                File.Delete(cachePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除缓存文件失败: {Path}", cachePath);
        }

        lock (_statusLock)
        {
            _remoteStatus.Remove(url);
        }
    }

    /// <summary>
    /// 获取指定 URL 的远程状态：true=在线，false=离线（使用缓存），null=未检测
    /// </summary>
    public bool? GetRemoteStatus(string url)
    {
        lock (_statusLock)
        {
            return _remoteStatus.TryGetValue(url, out var status) ? status : null;
        }
    }

    /// <summary>
    /// 判断一个路径是否为 web URL
    /// </summary>
    public static bool IsWebUrlPath(string path) => IsWebUrl(path);

    private void SetRemoteStatus(string url, bool available)
    {
        lock (_statusLock)
        {
            _remoteStatus[url] = available;
        }
    }
    public Task RefreshAsync(string icalFilePath, DateTime now)
    {
        _textCache.Remove(icalFilePath);
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
