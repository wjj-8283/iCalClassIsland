using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace iCalClassIsland.Models;

/// <summary>
/// iCal 事件内容包含规则设置
/// </summary>
public class IcalEventContainsRuleSettings : INotifyPropertyChanged
{
    private string _text = "";

    /// <summary>
    /// 要匹配的文本（包含即成立，不区分大小写）
    /// </summary>
    public string Text
    {
        get => _text;
        set { if (value == _text) return; _text = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 判断给定字符串是否包含设置中的文本（不区分大小写）
    /// </summary>
    public bool IsMatching(string? str)
    {
        if (string.IsNullOrEmpty(Text))
            return false;
        if (string.IsNullOrEmpty(str))
            return false;
        return str.Contains(Text, StringComparison.OrdinalIgnoreCase);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
