using System.Globalization;
using System.Windows.Data;

namespace ProjectCostForecast.App.Models;

public sealed class AccountingNoDecimalsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryConvertToDecimal(value, out var numericValue))
        {
            return string.Empty;
        }

        var roundedValue = decimal.Round(numericValue, 0, MidpointRounding.AwayFromZero);
        if (roundedValue == 0)
        {
            if (parameter is string text && string.Equals(text, "BlankZero", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return "-";
        }

        if (roundedValue < 0)
        {
            return $"({Math.Abs(roundedValue).ToString("C0", culture)})";
        }

        return roundedValue.ToString("C0", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return 0m;
        }

        var text = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text == "-")
        {
            return 0m;
        }

        var normalised = text.Replace("(", "-", StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(normalised, NumberStyles.Currency, culture, out var numericValue)
            ? numericValue
            : 0m;
    }

    private static bool TryConvertToDecimal(object? value, out decimal numericValue)
    {
        switch (value)
        {
            case null:
                numericValue = 0;
                return false;
            case decimal decimalValue:
                numericValue = decimalValue;
                return true;
            case int intValue:
                numericValue = intValue;
                return true;
            case long longValue:
                numericValue = longValue;
                return true;
            case double doubleValue:
                numericValue = (decimal)doubleValue;
                return true;
            case float floatValue:
                numericValue = (decimal)floatValue;
                return true;
            default:
                return decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out numericValue);
        }
    }
}
