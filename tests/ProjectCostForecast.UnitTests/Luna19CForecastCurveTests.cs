using ProjectCostForecast.App;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna19CForecastCurveTests
{
    [Theory]
    [InlineData(ForecastCurvePresets.SCurve, ForecastCurveProfile.SCurve)]
    [InlineData(ForecastCurvePresets.LazyCurve, ForecastCurveProfile.Bell)]
    [InlineData(ForecastCurvePresets.FrontHeavy, ForecastCurveProfile.FrontLoaded)]
    [InlineData(ForecastCurvePresets.BackHeavy, ForecastCurveProfile.BackLoaded)]
    public void Preview_and_applied_curve_use_the_same_normalized_distribution(
        string preset,
        ForecastCurveProfile profile)
    {
        var existingValues = new[] { 125.13m, 240.27m, 410.41m, 88.76m, 369.99m };

        var preview = ForecastCurvePresets.Apply(preset, existingValues);
        var applied = ForecastCurveService.Distribute(existingValues.Sum(), existingValues.Length, profile);

        Assert.Equal(applied, preview);
        Assert.Equal(existingValues.Sum(), preview.Sum());
    }

    [Fact]
    public void Existing_preview_is_a_non_allocating_identity_projection()
    {
        var existingValues = new[] { 12.345m, -2.5m, 80m };

        var result = ForecastCurvePresets.Apply(ForecastCurvePresets.Existing, existingValues);

        Assert.Equal(existingValues, result);
    }

    [Fact]
    public void Canonical_allocator_handles_zero_one_and_invalid_period_inputs()
    {
        Assert.Empty(ForecastCurveMath.Distribute(500m, 0, ForecastCurveProfile.Linear));
        Assert.Equal(
            [500m],
            ForecastCurveMath.Distribute(500m, 1, ForecastCurveProfile.SCurve));

        var invalidProfile = ForecastCurveMath.Distribute(100m, 4, (ForecastCurveProfile)999);

        Assert.Equal(
            ForecastCurveMath.Distribute(100m, 4, ForecastCurveProfile.Linear),
            invalidProfile);
    }

    [Fact]
    public void Canonical_allocator_preserves_negative_totals_and_rounding_residuals()
    {
        var positiveResidual = ForecastCurveMath.Distribute(1.01m, 3, ForecastCurveProfile.Linear);
        var negativeResidual = ForecastCurveMath.Distribute(-1.01m, 3, ForecastCurveProfile.Linear);

        Assert.Equal([0.33m, 0.34m, 0.34m], positiveResidual);
        Assert.Equal([-0.33m, -0.34m, -0.34m], negativeResidual);
        Assert.Equal(1.01m, positiveResidual.Sum());
        Assert.Equal(-1.01m, negativeResidual.Sum());
        Assert.All(negativeResidual, value => Assert.True(value <= 0));
    }

    [Fact]
    public void User_shapes_are_resampled_by_the_canonical_allocator()
    {
        var shape = ForecastCurveMath.CaptureNormalizedShape([10m, 30m, 60m]);
        var result = ForecastCurveMath.ApplyNormalizedShape([100m, 100m, 100m], shape);

        Assert.Equal(1m, shape.Sum());
        Assert.Equal([30m, 90m, 180m], result);
        Assert.Equal(300m, result.Sum());
    }

    [Fact]
    public void Curve_adapters_contain_no_duplicate_profile_business_formula()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var windowSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ProjectCostForecast.App",
            "ForecastCurveWindow.cs"));
        var serviceSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ProjectCostForecast.App",
            "Services",
            "ForecastCurveService.cs"));
        var presetStart = windowSource.IndexOf("public static class ForecastCurvePresets", StringComparison.Ordinal);
        var mathStart = windowSource.IndexOf("public static class ForecastCurveMath", StringComparison.Ordinal);
        var presetSource = windowSource[presetStart..mathStart];
        var mathSource = windowSource[mathStart..];

        Assert.Contains("ForecastCurveMath.Distribute", presetSource, StringComparison.Ordinal);
        Assert.Contains("ForecastCurveMath.CaptureNormalizedShape", presetSource, StringComparison.Ordinal);
        Assert.Contains("ForecastCurveMath.ApplyNormalizedShape", presetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("weightTotal", presetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Sin", presetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Exp", presetSource, StringComparison.Ordinal);
        Assert.Contains("ForecastCurveMath.Distribute", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static double Weight", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Exp", serviceSource, StringComparison.Ordinal);
        Assert.Contains("MidpointRounding.AwayFromZero", mathSource, StringComparison.Ordinal);
        Assert.Contains("private static decimal[] Allocate", mathSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", mathSource, StringComparison.Ordinal);
    }
}
