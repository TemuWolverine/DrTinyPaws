using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DrTinyPaws.Converters;

public class DateTimeFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            var now = DateTime.Now;
            if (dt.Date == now.Date)
            {
                return $"Today, {dt:hh:mm tt}";
            }
            if (dt.Date == now.Date.AddDays(-1))
            {
                return $"Yesterday, {dt:hh:mm tt}";
            }
            return $"{dt:ddd dd MMM, hh:mm tt}";
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}