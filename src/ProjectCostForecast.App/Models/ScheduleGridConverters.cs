using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProjectCostForecast.App.Models;

public sealed class DateOnlyTextConverter : IValueConverter
{
    private static readonly string[] AcceptedFormats =
        ["d/MM/yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "d MMM yyyy", "d-MMM-yy", "d/MM/yy"];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DateOnly date ? date.ToString("d/MM/yyyy", culture) : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (var format in AcceptedFormats)
        {
            if (DateOnly.TryParseExact(text, format, culture, DateTimeStyles.None, out var exact))
            {
                return exact;
            }
        }

        return DateOnly.TryParse(text, culture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DependencyProperty.UnsetValue;
    }
}

public sealed class ForecastAmountTextConverter : IValueConverter
{
    public static readonly ForecastAmountTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string text
            && text.Contains("BlankZero", StringComparison.OrdinalIgnoreCase)
            && value is decimal decimalValue
            && decimalValue == 0m)
        {
            return string.Empty;
        }

        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return 0m;
        }

        if (parameter is string converterParameter
            && converterParameter.Contains("ForecastMonth", StringComparison.OrdinalIgnoreCase))
        {
            return AccountingNoDecimalsConverter.TryParseForecastMonthInput(text, culture, out var forecastAmount)
                ? forecastAmount
                : DependencyProperty.UnsetValue;
        }

        return decimal.TryParse(text, NumberStyles.Any, culture, out var amount)
            ? amount
            : DependencyProperty.UnsetValue;
    }
}

public sealed class VarianceBrushConverter : IValueConverter
{
    public static readonly VarianceBrushConverter Instance = new();

    private static readonly System.Windows.Media.Brush PositiveBrush = BrushFactory.Frozen(0x15, 0x80, 0x3D);
    private static readonly System.Windows.Media.Brush NegativeBrush = BrushFactory.Frozen(0xDC, 0x26, 0x26);
    private static readonly System.Windows.Media.Brush NeutralBrush = BrushFactory.Frozen(0x0F, 0x17, 0x2A);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => (decimal)doubleValue,
            int intValue => intValue,
            _ => 0m
        };

        return amount > 0 ? PositiveBrush : amount < 0 ? NegativeBrush : NeutralBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class OutlineIndentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int outlineLevel ? outlineLevel : 0;
        return new Thickness(4 + (level * 18), 0, 4, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
