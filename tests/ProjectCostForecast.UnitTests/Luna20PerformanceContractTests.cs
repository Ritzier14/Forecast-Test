using System.Text.Json;

using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna20PerformanceContractTests
{
    [Fact]
    public void Forecast_only_edit_rebuilds_calculated_views_without_rebuilding_raw_transactions_pivot()
    {
        using var viewModel = Luna11TestSupport.CreateSeedViewModel();
        var forecast = viewModel.ForecastLines
            .SelectMany(line => line.MonthlyForecasts)
            .First(month => month.IsEditable);
        var before = viewModel.RefreshDiagnostics;

        forecast.Amount += 1m;
        viewModel.FlushPendingRefreshes();

        var after = viewModel.RefreshDiagnostics;
        Assert.Equal(
            1,
            after.GetPhaseCount(RefreshPhase.CalculatedViews)
            - before.GetPhaseCount(RefreshPhase.CalculatedViews));
        Assert.Equal(
            before.GetPhaseCount(RefreshPhase.RawTransactionsPivot),
            after.GetPhaseCount(RefreshPhase.RawTransactionsPivot));
    }

    [Fact]
    public void Workflow_uses_machine_matched_ci_baseline_and_both_profiles_remain_complete()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "verify.yml"));
        var verifier = File.ReadAllText(Path.Combine(root, "scripts", "verify-performance.ps1"));
        var packet = File.ReadAllText(Path.Combine(root, "docs", "audit", "LUNA-25-CI-PERFORMANCE.md"));
        var performanceStep = workflow.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Single(line => line.Contains("verify-performance.ps1", StringComparison.Ordinal));

        Assert.Contains(
            "-BaselinePath 'docs/audit/LUNA-20-PERFORMANCE-CI-BASELINE.json'",
            performanceStep);
        Assert.DoesNotContain("LUNA-20-PERFORMANCE-BASELINE.json", performanceStep);
        Assert.Contains(
            "[string]$BaselinePath = 'docs/audit/LUNA-20-PERFORMANCE-BASELINE.json'",
            verifier);

        using var localBaseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "docs",
            "audit",
            "LUNA-20-PERFORMANCE-BASELINE.json")));
        using var ciBaseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "docs",
            "audit",
            "LUNA-20-PERFORMANCE-CI-BASELINE.json")));

        Assert.Equal("baseline", localBaseline.RootElement.GetProperty("Mode").GetString());
        Assert.Equal("ci-baseline", ciBaseline.RootElement.GetProperty("Mode").GetString());
        Assert.Equal(3, localBaseline.RootElement.GetProperty("Datasets").GetArrayLength());
        Assert.Equal(3, ciBaseline.RootElement.GetProperty("Datasets").GetArrayLength());
        Assert.Equal(21, localBaseline.RootElement.GetProperty("Scenarios").GetArrayLength());
        Assert.Equal(21, ciBaseline.RootElement.GetProperty("Scenarios").GetArrayLength());
        Assert.Equal("win-x64", ciBaseline.RootElement.GetProperty("Runtime").GetProperty("RuntimeIdentifier").GetString());
        Assert.Equal(4, ciBaseline.RootElement.GetProperty("Runtime").GetProperty("ProcessorCount").GetInt32());
        Assert.Equal(
            "7505c80ea491a075cd287ee9d57e2d89b696802f",
            ciBaseline.RootElement.GetProperty("RepositoryCommit").GetString());
        Assert.Equal(GetDatasetNames(localBaseline.RootElement), GetDatasetNames(ciBaseline.RootElement));
        Assert.Equal(GetScenarioKeys(localBaseline.RootElement), GetScenarioKeys(ciBaseline.RootElement));

        foreach (var workload in new[] { "Small", "Normal", "Stress", "Data" })
        {
            Assert.Equal(
                localBaseline.RootElement.GetProperty("WorkloadPolicy").GetProperty(workload).GetString(),
                ciBaseline.RootElement.GetProperty("WorkloadPolicy").GetProperty(workload).GetString());
        }

        Assert.Contains("SOL-FAIL-1", packet);
        Assert.Contains("GitHub Actions Windows", packet);
        Assert.Contains("local/developer baseline", packet);
        Assert.Contains("4-core", packet);
        Assert.Contains("win-x64", packet);
        Assert.Contains("cross-machine", packet);
        Assert.Contains("not authoritative", packet);
        Assert.Contains("recapture", packet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not claim final SOL acceptance", packet, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetDatasetNames(JsonElement report)
    {
        return report.GetProperty("Datasets")
            .EnumerateArray()
            .Select(dataset => dataset.GetProperty("Name").GetString()!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetScenarioKeys(JsonElement report)
    {
        return report.GetProperty("Scenarios")
            .EnumerateArray()
            .Select(scenario => $"{scenario.GetProperty("Dataset").GetString()}/{scenario.GetProperty("Name").GetString()}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }
}
