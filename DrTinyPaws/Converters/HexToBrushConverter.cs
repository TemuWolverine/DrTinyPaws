using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace DrTinyPaws.Converters;

public class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            if (Color.TryParse(hex, out var color))
            {
                return new SolidColorBrush(color);
            }
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}