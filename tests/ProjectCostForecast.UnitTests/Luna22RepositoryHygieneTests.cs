using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna22RepositoryHygieneTests
{
    [Fact]
    public void Normal_application_package_contains_only_the_approved_startup_workbook()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var project = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ProjectCostForecast.App",
            "ProjectCostForecast.App.csproj"));

        Assert.Contains("<Content Include=\"Data\\data_anonymised.xlsx\">", project);
        Assert.DoesNotContain("<Content Include=\"Data\\SampleData.json\">", project);
        Assert.DoesNotContain("<Content Include=\"Data\\InitialCostLoad.xlsx\">", project);
    }

    [Fact]
    public void CI_and_secret_scan_controls_are_least_privilege_and_redacted()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "verify.yml"));
        var ignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        var scanner = File.ReadAllText(Path.Combine(root, "scripts", "scan-secrets.ps1"));
        var performance = File.ReadAllText(Path.Combine(root, "scripts", "verify-performance.ps1"));
        var dataReview = File.ReadAllText(Path.Combine(root, "docs", "audit", "LUNA-22-BUNDLED-DATA-REVIEW.md"));

        Assert.Contains("permissions:", workflow);
        Assert.Contains("contents: read", workflow);
        Assert.Contains("verify.ps1", workflow);
        Assert.Contains("audit-dependencies.ps1", workflow);
        Assert.Contains("verify-performance.ps1", workflow);
        Assert.Contains("max(50 ms, 25%)", File.ReadAllText(Path.Combine(root, "docs", "ARCHITECTURE.md")));
        Assert.Contains("MedianMilliseconds", performance);
        Assert.Contains("P95Milliseconds", performance);
        Assert.Contains("at least 20 samples", File.ReadAllText(Path.Combine(root, "docs", "ARCHITECTURE.md")));
        Assert.Contains("scan-secrets.ps1", workflow);
        Assert.Contains("upload-artifact", workflow);
        Assert.Contains("/release/ProjectCostForecast/", ignore);
        Assert.Contains("/Temp/", ignore);
        Assert.Contains("rev-list", scanner);
        Assert.Contains("'grep', '-I', '-l'", scanner);
        Assert.Contains("FindingCount", scanner);
        Assert.Contains("approval", dataReview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source_workbook/1.Mar 26.xlsm", dataReview);
    }
}
