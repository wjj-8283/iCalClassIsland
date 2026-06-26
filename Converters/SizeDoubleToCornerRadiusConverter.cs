using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace iCalClassIsland.Converters;

/// <summary>
/// 将 double 值转换为 CornerRadius（用于进度条边框圆角）
/// </summary>
public class SizeDoubleToCornerRadiusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return new CornerRadius(d);
        return new CornerRadius(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
