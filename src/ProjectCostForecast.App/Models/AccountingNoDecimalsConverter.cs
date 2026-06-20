using System.Globalization;
using System.Windows.Data;

namespace ProjectCostForecast.App.Models;

public sealed class AccountingNoDecimalsConverter : IValueConverter
{
    public static string FormatAccounting(
        decimal value,
        CultureInfo culture,
        int decimals = 0,
        bool blankZero = false,
        bool showCurrencySymbol = false,
        bool compactMillions = false,
        int compactMillionDecimals = 2)
    {
        if (compactMillions && Math.Abs(value) >= 1_000_000m)
        {
            var compactValue = decimal.Round(
                Math.Abs(value) / 1_000_000m,
                Math.Max(0, compactMillionDecimals),
                MidpointRounding.AwayFromZero);
            if (compactValue == 0m)
            {
                return blankZero ? string.Empty : "-";
            }

            var compactText = $"{compactValue.ToString($"N{Math.Max(0, compactMillionDecimals)}", culture)}m";
            if (showCurrencySymbol)
            {
                compactText = $"{culture.NumberFormat.CurrencySymbol}{compactText}";
            }

            return value < 0 ? $"({compactText})" : compactText;
        }

        var roundedValue = decimal.Round(value, Math.Max(0, decimals), MidpointRounding.AwayFromZero);
        if (roundedValue == 0m)
        {
            return blankZero ? string.Empty : "-";
        }

        var format = showCurrencySymbol ? $"C{Math.Max(0, decimals)}" : $"N{Math.Max(0, decimals)}";
        var formatted = showCurrencySymbol
            ? Math.Abs(roundedValue).ToString(format, culture)
            : Math.Abs(roundedValue).ToString(format, culture);

        return roundedValue < 0
            ? $"({formatted})"
            : formatted;
    }

    public static bool TryParseForecastMonthInput(object? value, CultureInfo culture, out decimal amount)
    {
        amount = 0m;
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text == "-")
        {
            return true;
        }

        var usesMillionSuffix = text.EndsWith("m", StringComparison.OrdinalIgnoreCase)
            || text.EndsWith("mil", StringComparison.OrdinalIgnoreCase)
            || text.EndsWith("million", StringComparison.OrdinalIgnoreCase);
        var normalized = text
            .Replace("million", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("mil", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("m", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(", "-", StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!decimal.TryParse(normalized, NumberStyles.Any, culture, out var parsed))
        {
            return false;
        }

        if (usesMillionSuffix || LooksLikeCompactMillionInput(text, culture, parsed))
        {
            amount = parsed * 1_000_000m;
            return true;
        }

        amount = parsed;
        return true;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryConvertToDecimal(value, out var numericValue))
        {
            return string.Empty;
        }

        var options = ParseOptions(parameter);
        return FormatAccounting(
            numericValue,
            culture,
            options.Decimals,
            options.BlankZero,
            options.ShowCurrencySymbol,
            options.CompactMillions,
            options.CompactMillionDecimals);
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

    private static bool LooksLikeCompactMillionInput(string text, CultureInfo culture, decimal parsed)
    {
        if (Math.Abs(parsed) >= 1000m)
        {
            return false;
        }

        var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
        var groupSeparator = culture.NumberFormat.NumberGroupSeparator;
        return text.Contains(decimalSeparator, StringComparison.Ordinal)
               && !string.IsNullOrWhiteSpace(decimalSeparator)
               && (string.IsNullOrWhiteSpace(groupSeparator) || !text.Contains(groupSeparator, StringComparison.Ordinal));
    }

    private static AccountingFormatOptions ParseOptions(object? parameter)
    {
        var options = new AccountingFormatOptions();
        if (parameter is not string text || string.IsNullOrWhiteSpace(text))
        {
            return options;
        }

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(part, "BlankZero", StringComparison.OrdinalIgnoreCase))
            {
                options.BlankZero = true;
                continue;
            }

            if (string.Equals(part, "ShowCurrency", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowCurrencySymbol = true;
                continue;
            }

            if (part.StartsWith("Decimals:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(part["Decimals:".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimals))
            {
                options.Decimals = Math.Max(0, decimals);
                continue;
            }

            if (string.Equals(part, "CompactMillions", StringComparison.OrdinalIgnoreCase))
            {
                options.CompactMillions = true;
                continue;
            }

            if (part.StartsWith("CompactMillions:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(part["CompactMillions:".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var compactDecimals))
            {
                options.CompactMillions = true;
                options.CompactMillionDecimals = Math.Max(0, compactDecimals);
            }
        }

        return options;
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

    private sealed class AccountingFormatOptions
    {
        public bool BlankZero { get; set; }
        public bool ShowCurrencySymbol { get; set; }
        public int Decimals { get; set; }
        public bool CompactMillions { get; set; }
        public int CompactMillionDecimals { get; set; } = 2;
    }
}
