using System.Globalization;
using System.Windows.Data;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App;

/// <summary>
/// Converts durable UTC instants to the documented NZ-local display format.
/// Display formatting is a WPF concern; models retain only the persisted
/// DateTimeOffset value.
/// </summary>
public sealed class DateTimeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset instant)
        {
            return string.Empty;
        }

        return parameter is string format && !string.IsNullOrWhiteSpace(format)
            ? DateTimeContract.FormatNewZealand(instant, format)
            : DateTimeContract.FormatNewZealand(instant);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
