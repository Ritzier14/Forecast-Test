namespace ProjectCostForecast.App;

/// <summary>
/// A persisted colour value that has no WPF dependency.  The parser accepts
/// the hex forms used by the project files and normalizes them deterministically
/// before a presentation layer turns them into a brush.
/// </summary>
internal readonly record struct ColorValue(byte Red, byte Green, byte Blue, byte Alpha = 255)
{
    public string ToHex(bool includeAlpha = false)
    {
        return includeAlpha || Alpha != byte.MaxValue
            ? $"#{Alpha:X2}{Red:X2}{Green:X2}{Blue:X2}"
            : $"#{Red:X2}{Green:X2}{Blue:X2}";
    }
}

internal static class ColorValueParser
{
    public static bool TryParseHex(string? value, out ColorValue color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length is not (3 or 4 or 6 or 8) || !IsHex(hex))
        {
            return false;
        }

        var offset = 0;
        var alpha = byte.MaxValue;
        if (hex.Length is 4 or 8)
        {
            alpha = ParseByte(hex, ref offset, hex.Length == 4);
        }

        var red = ParseByte(hex, ref offset, hex.Length is 3 or 4);
        var green = ParseByte(hex, ref offset, hex.Length is 3 or 4);
        var blue = ParseByte(hex, ref offset, hex.Length is 3 or 4);
        color = new ColorValue(red, green, blue, alpha);
        return true;
    }

    public static bool TryNormalizeHex(string? value, out string normalized, bool includeAlpha = false)
    {
        if (TryParseHex(value, out var color))
        {
            normalized = color.ToHex(includeAlpha);
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    public static string NormalizeHex(
        string? value,
        bool includeAlpha = false,
        string fallback = "#000000")
    {
        if (TryNormalizeHex(value, out var normalized, includeAlpha))
        {
            return normalized;
        }

        return TryNormalizeHex(fallback, out normalized, includeAlpha)
            ? normalized
            : includeAlpha ? "#FF000000" : "#000000";
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static byte ParseByte(string value, ref int offset, bool compact)
    {
        if (compact)
        {
            var nibble = ParseNibble(value[offset++]);
            return (byte)((nibble << 4) | nibble);
        }

        var result = Convert.ToByte(value.Substring(offset, 2), 16);
        offset += 2;
        return result;
    }

    private static byte ParseNibble(char value)
    {
        return value <= '9'
            ? (byte)(value - '0')
            : (byte)(char.ToUpperInvariant(value) - 'A' + 10);
    }
}

internal sealed record ColorPaletteEntry(string Name, string? Hex);

internal static class ColorPalette
{
    public static readonly IReadOnlyList<ColorPaletteEntry> IconColours =
    [
        new("Default", null),
        new("Slate", "#475569"),
        new("Blue", "#2563EB"),
        new("Green", "#16A34A"),
        new("Orange", "#EA580C"),
        new("Red", "#DC2626"),
        new("Purple", "#7C3AED")
    ];

    public static string GetLabel(string? hex)
    {
        var trimmed = (hex ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "Default";
        }

        var normalized = ColorValueParser.NormalizeHex(trimmed);
        return normalized switch
        {
            "#EDF8F0" or "#16A34A" => "Green",
            "#FFF4E7" or "#EA580C" => "Orange",
            "#EEF5FF" or "#2563EB" => "Blue",
            "#F5F0FF" or "#7C3AED" => "Purple",
            "#ECF9FA" => "Cyan",
            "#FFF0F5" or "#DC2626" => "Pink",
            "#475569" => "Slate",
            _ => hex ?? "Default"
        };
    }
}
