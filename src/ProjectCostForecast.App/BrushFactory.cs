using System.Windows.Media;

namespace ProjectCostForecast.App;

public static class BrushFactory
{
    public sealed record HeaderGradientSpec(string BaseHex, string VariantKey, double TopBlend, double MiddleBlend, double BottomShade);

    private static readonly IReadOnlyDictionary<string, HeaderGradientSpec> HeaderGradientVariants =
        new Dictionary<string, HeaderGradientSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["Soft"] = new("#DCE6F7", "Soft", 0.68, 0.34, 0.03),
            ["Balanced"] = new("#DCE6F7", "Balanced", 0.55, 0.22, 0.06),
            ["Strong"] = new("#DCE6F7", "Strong", 0.42, 0.10, 0.10)
        };

    public static SolidColorBrush Frozen(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    public static SolidColorBrush Frozen(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static LinearGradientBrush FrozenVerticalGradient(string topHex, string bottomHex)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0.5, 0),
            EndPoint = new System.Windows.Point(0.5, 1)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(topHex), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(bottomHex), 1));
        brush.Freeze();
        return brush;
    }

    public static LinearGradientBrush FrozenHeaderGradient(string baseHex)
    {
        var spec = ParseHeaderGradientSpec(baseHex);
        var color = (Color)ColorConverter.ConvertFromString(spec.BaseHex);
        var top = Blend(color, Colors.White, spec.TopBlend);
        var middle = Blend(color, Colors.White, spec.MiddleBlend);
        var bottom = Blend(color, Colors.Black, spec.BottomShade);
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0.5, 0),
            EndPoint = new System.Windows.Point(0.5, 1)
        };
        brush.GradientStops.Add(new GradientStop(top, 0));
        brush.GradientStops.Add(new GradientStop(middle, 0.48));
        brush.GradientStops.Add(new GradientStop(bottom, 1));
        brush.Freeze();
        return brush;
    }

    public static HeaderGradientSpec ParseHeaderGradientSpec(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GetHeaderGradientVariant("#DCE6F7", "Balanced");
        }

        var parts = value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return GetHeaderGradientVariant("#DCE6F7", "Balanced");
        }

        var baseHex = parts[0];
        if (!TryParseHexColor(baseHex, out _))
        {
            return GetHeaderGradientVariant("#DCE6F7", "Balanced");
        }

        var variantKey = parts.Length >= 2 ? parts[1] : "Balanced";
        if (parts.Length >= 5
            && double.TryParse(parts[2], out var topBlend)
            && double.TryParse(parts[3], out var middleBlend)
            && double.TryParse(parts[4], out var bottomShade))
        {
            return new HeaderGradientSpec(baseHex, variantKey, topBlend, middleBlend, bottomShade);
        }

        var preset = GetHeaderGradientVariant(baseHex, variantKey);
        return preset with { BaseHex = baseHex };
    }

    public static HeaderGradientSpec GetHeaderGradientVariant(string baseHex, string? variantKey)
    {
        if (!HeaderGradientVariants.TryGetValue(variantKey ?? string.Empty, out var preset))
        {
            preset = HeaderGradientVariants["Balanced"];
        }

        return preset with { BaseHex = baseHex };
    }

    public static string SerializeHeaderGradientSpec(string baseHex, string variantKey)
    {
        var spec = GetHeaderGradientVariant(baseHex, variantKey);
        return $"{spec.BaseHex}|{spec.VariantKey}|{spec.TopBlend:0.##}|{spec.MiddleBlend:0.##}|{spec.BottomShade:0.##}";
    }

    public static bool IsValidHeaderGradientSpec(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && TryParseHexColor(parts[0], out _);
    }

    public static bool TryParseHexColor(string? value, out Color color)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                color = default;
                return false;
            }

            color = (Color)ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            color = default;
            return false;
        }
    }

    private static Color Blend(Color color, Color target, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(color.R + ((target.R - color.R) * amount)),
            (byte)Math.Round(color.G + ((target.G - color.G) * amount)),
            (byte)Math.Round(color.B + ((target.B - color.B) * amount)));
    }
}
