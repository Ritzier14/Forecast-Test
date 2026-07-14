using System.Windows.Media;

namespace ProjectCostForecast.App;

public static class BrushFactory
{
    public sealed record HeaderGradientSpec(string BaseHex, string VariantKey, double TopBlend, double MiddleBlend, double BottomShade);
    public sealed record HeaderGradientStop(double Offset, string Hex, double Opacity);
    public sealed record AdvancedHeaderGradientSpec(string GradientType, string Direction, IReadOnlyList<HeaderGradientStop> Stops);

    private static readonly IReadOnlyDictionary<string, HeaderGradientSpec> HeaderGradientVariants =
        new Dictionary<string, HeaderGradientSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["Soft"] = new("#DCE6F7", "Soft", 0.68, 0.34, 0.03),
            ["Balanced"] = new("#DCE6F7", "Balanced", 0.55, 0.22, 0.06),
            ["Strong"] = new("#DCE6F7", "Strong", 0.42, 0.10, 0.10)
        };

    private const string AdvancedGradientPrefix = "gradient:v1";

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

    public static Brush FrozenHeaderGradient(string baseHex)
    {
        if (TryParseAdvancedHeaderGradientSpec(baseHex, out var advanced))
        {
            var advancedBrush = CreateAdvancedGradientBrush(advanced);
            advancedBrush.Freeze();
            return advancedBrush;
        }

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

    public static bool TryParseAdvancedHeaderGradientSpec(string? value, out AdvancedHeaderGradientSpec spec)
    {
        spec = new AdvancedHeaderGradientSpec("Linear", "Right", []);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 4 || !string.Equals(parts[0], AdvancedGradientPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var gradientType = NormalizeGradientType(parts[1]);
        var direction = NormalizeGradientDirection(parts[2]);
        var stops = new List<HeaderGradientStop>();
        foreach (var stopPart in parts[3].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var stopValues = stopPart.Split('@', StringSplitOptions.TrimEntries);
            if (stopValues.Length < 3
                || !TryParseHexColor(stopValues[0], out _)
                || !double.TryParse(stopValues[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var offset)
                || !double.TryParse(stopValues[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var opacity))
            {
                continue;
            }

            stops.Add(new HeaderGradientStop(Math.Clamp(offset, 0, 1), NormalizeHex(stopValues[0]), Math.Clamp(opacity, 0, 1)));
        }

        if (stops.Count < 2)
        {
            return false;
        }

        spec = new AdvancedHeaderGradientSpec(gradientType, direction, stops.OrderBy(stop => stop.Offset).ToList());
        return true;
    }

    public static AdvancedHeaderGradientSpec ToAdvancedHeaderGradientSpec(string? value)
    {
        if (TryParseAdvancedHeaderGradientSpec(value, out var advanced))
        {
            return advanced;
        }

        var legacy = ParseHeaderGradientSpec(value);
        var baseColor = (Color)ColorConverter.ConvertFromString(legacy.BaseHex);
        var stops = new List<HeaderGradientStop>
        {
            new(0, ToHex(Blend(baseColor, Colors.White, legacy.TopBlend)), 1),
            new(0.48, ToHex(Blend(baseColor, Colors.White, legacy.MiddleBlend)), 1),
            new(1, ToHex(Blend(baseColor, Colors.Black, legacy.BottomShade)), 1)
        };
        return new AdvancedHeaderGradientSpec("Linear", "Down", stops);
    }

    public static string SerializeAdvancedHeaderGradientSpec(AdvancedHeaderGradientSpec spec)
    {
        var stops = spec.Stops
            .OrderBy(stop => stop.Offset)
            .Select(stop => $"{NormalizeHex(stop.Hex)}@{stop.Offset.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}@{stop.Opacity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
        return $"{AdvancedGradientPrefix}|{NormalizeGradientType(spec.GradientType)}|{NormalizeGradientDirection(spec.Direction)}|{string.Join(';', stops)}";
    }

    public static HeaderGradientSpec ParseHeaderGradientSpec(string? value)
    {
        if (TryParseAdvancedHeaderGradientSpec(value, out var advanced))
        {
            var anchor = advanced.Stops.OrderBy(stop => Math.Abs(stop.Offset - 0.5)).First();
            return GetHeaderGradientVariant(anchor.Hex, "Balanced");
        }

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

        if (TryParseAdvancedHeaderGradientSpec(value, out _))
        {
            return true;
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

    private static GradientBrush CreateAdvancedGradientBrush(AdvancedHeaderGradientSpec spec)
    {
        GradientBrush brush;
        if (string.Equals(spec.GradientType, "Radial", StringComparison.OrdinalIgnoreCase))
        {
            var radial = new RadialGradientBrush
            {
                Center = new System.Windows.Point(0.5, 0.5),
                RadiusX = 0.72,
                RadiusY = 0.72,
                GradientOrigin = DirectionToGradientOrigin(spec.Direction)
            };
            brush = radial;
        }
        else
        {
            var (start, end) = DirectionToLinearPoints(spec.Direction);
            brush = new LinearGradientBrush
            {
                StartPoint = start,
                EndPoint = end
            };
        }

        foreach (var stop in spec.Stops.OrderBy(stop => stop.Offset))
        {
            var color = (Color)ColorConverter.ConvertFromString(NormalizeHex(stop.Hex));
            color.A = (byte)Math.Round(Math.Clamp(stop.Opacity, 0, 1) * 255);
            brush.GradientStops.Add(new GradientStop(color, Math.Clamp(stop.Offset, 0, 1)));
        }

        return brush;
    }

    private static (System.Windows.Point Start, System.Windows.Point End) DirectionToLinearPoints(string direction)
    {
        return NormalizeGradientDirection(direction) switch
        {
            "Left" => (new System.Windows.Point(1, 0.5), new System.Windows.Point(0, 0.5)),
            "Down" => (new System.Windows.Point(0.5, 0), new System.Windows.Point(0.5, 1)),
            "Up" => (new System.Windows.Point(0.5, 1), new System.Windows.Point(0.5, 0)),
            "DownRight" => (new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)),
            "UpRight" => (new System.Windows.Point(0, 1), new System.Windows.Point(1, 0)),
            "DownLeft" => (new System.Windows.Point(1, 0), new System.Windows.Point(0, 1)),
            "UpLeft" => (new System.Windows.Point(1, 1), new System.Windows.Point(0, 0)),
            _ => (new System.Windows.Point(0, 0.5), new System.Windows.Point(1, 0.5))
        };
    }

    private static System.Windows.Point DirectionToGradientOrigin(string direction)
    {
        return NormalizeGradientDirection(direction) switch
        {
            "Left" => new System.Windows.Point(0.1, 0.5),
            "Down" => new System.Windows.Point(0.5, 0.9),
            "Up" => new System.Windows.Point(0.5, 0.1),
            "DownRight" => new System.Windows.Point(0.85, 0.85),
            "UpRight" => new System.Windows.Point(0.85, 0.15),
            "DownLeft" => new System.Windows.Point(0.15, 0.85),
            "UpLeft" => new System.Windows.Point(0.15, 0.15),
            _ => new System.Windows.Point(0.9, 0.5)
        };
    }

    private static string NormalizeGradientType(string? value)
    {
        return string.Equals(value, "Radial", StringComparison.OrdinalIgnoreCase) ? "Radial" : "Linear";
    }

    private static string NormalizeGradientDirection(string? value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase) switch
        {
            "Left" => "Left",
            "Down" => "Down",
            "Up" => "Up",
            "DownRight" => "DownRight",
            "UpRight" => "UpRight",
            "DownLeft" => "DownLeft",
            "UpLeft" => "UpLeft",
            _ => "Right"
        };
    }

    private static string NormalizeHex(string hex)
    {
        return TryParseHexColor(hex, out var color) ? ToHex(color) : "#000000";
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
