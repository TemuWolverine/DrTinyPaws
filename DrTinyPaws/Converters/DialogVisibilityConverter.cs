using System;
using System.Globalization;
using Avalonia.Data.Converters;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Converters;

public class DialogVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DialogViewType current && parameter is string targetStr)
        {
            if (Enum.TryParse<DialogViewType>(targetStr, out var target))
            {
                return current == target;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
