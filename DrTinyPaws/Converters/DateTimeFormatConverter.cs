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
                var elapsed = now - dt;
                if (elapsed < TimeSpan.Zero)
                {
                    elapsed = TimeSpan.Zero;
                }

                if (elapsed.TotalMinutes < 1)
                {
                    return $"{elapsed.Seconds}s ago";
                }

                if (elapsed.TotalHours < 1)
                {
                    return $"{elapsed.Minutes}m ago";
                }

                if (elapsed.Minutes == 0)
                {
                    return $"{(int)elapsed.TotalHours}h ago";
                }

                return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m ago";
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