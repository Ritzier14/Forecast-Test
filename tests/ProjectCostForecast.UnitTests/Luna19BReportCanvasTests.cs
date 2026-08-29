using System.Windows;
using System.Windows.Media;
using ProjectCostForecast.App;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna19BReportCanvasTests
{
    [Theory]
    [InlineData("#abc", "#AABBCC")]
    [InlineData(" #1a2B3c ", "#1A2B3C")]
    [InlineData("#8abc", "#88AABBCC")]
    [InlineData("#801a2b3c", "#801A2B3C")]
    public void Pure_colour_parser_normalizes_supported_hex_forms(string value, string expected)
    {
        Assert.True(ColorValueParser.TryParseHex(value, out var parsed));
        Assert.Equal(expected, parsed.ToHex());
        Assert.Equal(expected, ColorValueParser.NormalizeHex(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("blue")]
    public void Pure_colour_parser_rejects_invalid_values(string? value)
    {
        Assert.False(ColorValueParser.TryParseHex(value, out _));
        Assert.False(ColorValueParser.TryNormalizeHex(value, out _));
        Assert.Equal("#000000", ColorValueParser.NormalizeHex(value));
    }

    [Fact]
    public void Colour_labels_and_icon_options_share_one_palette()
    {
        Assert.Equal("Default", ColorPalette.GetLabel(null));
        Assert.Equal("Green", ColorPalette.GetLabel("#16a34a"));
        Assert.Equal("Orange", ColorPalette.GetLabel("#FFF4E7"));
        Assert.Equal("#ABCDEF", ColorPalette.GetLabel("#ABCDEF"));
        Assert.Equal("Default", ColorPalette.IconColours[0].Name);
        Assert.Equal("#2563EB", ColorPalette.IconColours.Single(item => item.Name == "Blue").Hex);
    }

    [Fact]
    public void Canvas_positioning_clamps_each_axis_and_handles_non_finite_input()
    {
        Assert.Equal(
            new Point(0, 50),
            ReportCanvasObjectPositioning.ClampToCanvas(
                new Point(-20, 50),
                new Size(120, 40),
                canvasWidth: 100,
                canvasHeight: 90));
        Assert.Equal(
            new Point(0, 0),
            ReportCanvasObjectPositioning.ClampToCanvas(
                new Point(double.NaN, double.PositiveInfinity),
                new Size(120, 40),
                canvasWidth: 100,
                canvasHeight: 90));
        Assert.Equal(
            new Point(80, 50),
            ReportCanvasObjectPositioning.ClampToCanvas(
                new Point(200, 200),
                new Size(20, 40),
                canvasWidth: 100,
                canvasHeight: 90));
    }

    [Fact]
    [Trait("Category", "Wpf")]
    public void Shared_default_gradient_is_frozen_and_matches_all_default_stops()
    {
        Luna11TestSupport.RunOnSta(() =>
        {
            var gradient = BrushFactory.FrozenDefaultHeaderGradient();

            Assert.True(gradient.IsFrozen);
            Assert.Equal(new Point(0.5, 0), gradient.StartPoint);
            Assert.Equal(new Point(0.5, 1), gradient.EndPoint);
            Assert.Equal(3, gradient.GradientStops.Count);
            Assert.Equal(Color.FromRgb(0xF8, 0xFA, 0xFC), gradient.GradientStops[0].Color);
            Assert.Equal(Color.FromRgb(0xEC, 0xF1, 0xF6), gradient.GradientStops[1].Color);
            Assert.Equal(Color.FromRgb(0xE1, 0xE8, 0xF0), gradient.GradientStops[2].Color);
            Assert.Same(gradient, BrushFactory.FrozenDefaultHeaderGradient());
        });
    }

    [Fact]
    [Trait("Category", "Wpf")]
    public void Brush_factory_adapts_pure_rgba_values_at_the_wpf_boundary()
    {
        Luna11TestSupport.RunOnSta(() =>
        {
            Assert.True(BrushFactory.TryParseHexColor("#801A2B3C", out var color));
            Assert.Equal(Color.FromArgb(0x80, 0x1A, 0x2B, 0x3C), color);

            var fallback = BrushFactory.CreateSolidColor("not-a-colour", "#123456");
            Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), fallback.Color);
        });
    }

    [Fact]
    public void Report_canvas_cards_use_one_controller_and_one_selection_contract()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var controller = ReadSource(root, "src", "ProjectCostForecast.App", "Presentation", "ReportCanvasInteraction.cs");
        var objectCard = ReadSource(root, "src", "ProjectCostForecast.App", "MonthlyReportCanvasObjects.cs");
        var chartCard = ReadSource(root, "src", "ProjectCostForecast.App", "MonthlyReportCharts.cs");
        var interactions = ReadSource(root, "src", "ProjectCostForecast.App", "MonthlyReportCanvasInteractions.cs");
        var brushFactory = ReadSource(root, "src", "ProjectCostForecast.App", "BrushFactory.cs");
        var colorValue = ReadSource(root, "src", "ProjectCostForecast.App", "Presentation", "ColorValue.cs");

        Assert.Contains("interface IReportCanvasObjectHost", controller, StringComparison.Ordinal);
        Assert.Contains("ReportCanvasObjectPositioning.ClampToCanvas", controller, StringComparison.Ordinal);
        Assert.Contains("Panel.SetZIndex(_target, _originalZIndex)", controller, StringComparison.Ordinal);
        Assert.Contains("e.GetPosition(canvas)", controller, StringComparison.Ordinal);
        Assert.Contains("ReportCanvasDragController", objectCard, StringComparison.Ordinal);
        Assert.Contains("ReportCanvasDragController", chartCard, StringComparison.Ordinal);
        Assert.DoesNotContain("Header_MouseLeftButtonDown", objectCard, StringComparison.Ordinal);
        Assert.DoesNotContain("Header_MouseLeftButtonDown", chartCard, StringComparison.Ordinal);
        Assert.Contains("IReportCanvasObjectHost", interactions, StringComparison.Ordinal);
        Assert.Contains("ColorValueParser.TryParseHex", brushFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorConverter", brushFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", colorValue, StringComparison.Ordinal);

        var appSources = Directory.EnumerateFiles(
                Path.Combine(root, "src", "ProjectCostForecast.App"),
                "*.cs",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        Assert.DoesNotContain(appSources, source => source.Contains("ColorConverter", StringComparison.Ordinal));
    }

    private static string ReadSource(string root, params string[] segments)
    {
        return File.ReadAllText(Path.Combine([root, ..segments]));
    }
}
