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

        var numericText = trimmed
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty, StringComparison.CurrentCulture)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);

        try
        {
            value = Convert.ChangeType(numericText, targetType, CultureInfo.CurrentCulture);
            return true;
        }
        catch
        {
            try
            {
                value = Convert.ChangeType(numericText, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }
    }
}
