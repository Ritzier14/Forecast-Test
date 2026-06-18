using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProjectCostForecast.App.Models;

public sealed class NonNegativeDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return 0d;
        }

        if (!TryConvertToDouble(value, culture, out var numericValue)
            || double.IsNaN(numericValue)
            || numericValue < 0d)
        {
            numericValue = 0d;
        }

        return targetType == typeof(GridLength)
            ? new GridLength(numericValue)
            : numericValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static bool TryConvertToDouble(object value, CultureInfo culture, out double numericValue)
    {
        try
        {
            numericValue = System.Convert.ToDouble(value, culture);
            return true;
        }
        catch (FormatException)
        {
            numericValue = 0d;
            return false;
        }
        catch (InvalidCastException)
        {
            numericValue = 0d;
            return false;
        }
    }
}
