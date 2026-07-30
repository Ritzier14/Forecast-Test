using System.Globalization;
using System.Text;

namespace ProjectCostForecast.App.Services;

public static class SpreadsheetClipboardService
{
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalised.EndsWith('\n'))
        {
            normalised = normalised[..^1];
        }

        return normalised
            .Split('\n')
            .Select(row => (IReadOnlyList<string>)row.Split('\t'))
            .ToList();
    }

    public static string Serialize(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        return string.Join(Environment.NewLine, rows.Select(row => string.Join('\t', row)));
    }

    public static int Apply(
        IReadOnlyList<IReadOnlyList<string>> values,
        int startRow,
        int startColumn,
        Func<int, int, bool> canWrite,
        Action<int, int, string> write)
    {
        var written = 0;
        for (var rowOffset = 0; rowOffset < values.Count; rowOffset++)
        {
            var row = values[rowOffset];
            for (var columnOffset = 0; columnOffset < row.Count; columnOffset++)
            {
                var rowIndex = startRow + rowOffset;
                var columnIndex = startColumn + columnOffset;
                if (!canWrite(rowIndex, columnIndex))
                {
                    continue;
                }

                write(rowIndex, columnIndex, row[columnOffset]);
                written++;
            }
        }

        return written;
    }

    public static bool TryConvert(string text, Type destinationType, out object? value)
    {
        var targetType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;
        if (string.IsNullOrWhiteSpace(text))
        {
            value = Nullable.GetUnderlyingType(destinationType) is not null || !targetType.IsValueType
                ? null
                : Activator.CreateInstance(targetType);
            return true;
        }

        var trimmed = text.Trim();
        if (targetType == typeof(string))
        {
            value = text;
            return true;
        }

        if (targetType == typeof(DateOnly))
        {
            var converted = DateOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date)
                || DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
            value = date;
            return converted;
        }

        if (targetType == typeof(DateTime))
        {
            var converted = DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date)
                || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
            value = date;
            return converted;
        }

        if (targetType.IsEnum)
        {
            var converted = Enum.TryParse(targetType, trimmed, true, out var enumValue);
            value = enumValue;
            return converted;
        }

        var currentCultureText = RemoveCurrencySymbol(trimmed, CultureInfo.CurrentCulture);
        if (TryChangeType(currentCultureText, targetType, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        var invariantText = RemoveCurrencySymbol(trimmed, CultureInfo.InvariantCulture);
        if (TryChangeType(invariantText, targetType, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static string RemoveCurrencySymbol(string text, CultureInfo culture)
    {
        var symbol = culture.NumberFormat.CurrencySymbol;
        var withoutCultureSymbol = string.IsNullOrEmpty(symbol)
            ? text
            : text.Replace(symbol, string.Empty, StringComparison.Ordinal);
        return withoutCultureSymbol.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static bool TryChangeType(string text, Type targetType, CultureInfo culture, out object? value)
    {
        try
        {
            value = Convert.ChangeType(text, targetType, culture);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            value = null;
            return false;
        }
    }
}
