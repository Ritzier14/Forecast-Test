using System.Diagnostics;

using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna22RepositoryHygieneTests
{
    private static readonly string[] F13CleanupPaths =
    [
        "Temp/data_anonymised.xlsx",
        "release/ProjectCostForecast/ClosedXML.Parser.dll",
        "release/ProjectCostForecast/ClosedXML.dll",
        "release/ProjectCostForecast/Data/InitialCostLoad.xlsx",
        "release/ProjectCostForecast/Data/SampleData.json",
        "release/ProjectCostForecast/Data/data_anonymised.xlsx",
        "release/ProjectCostForecast/DocumentFormat.OpenXml.Framework.dll",
        "release/ProjectCostForecast/DocumentFormat.OpenXml.dll",
        "release/ProjectCostForecast/ExcelNumberFormat.dll",
        "release/ProjectCostForecast/ProjectCostForecast.App.deps.json",
        "release/ProjectCostForecast/ProjectCostForecast.App.dll",
        "release/ProjectCostForecast/ProjectCostForecast.App.exe",
        "release/ProjectCostForecast/ProjectCostForecast.App.pdb",
        "release/ProjectCostForecast/ProjectCostForecast.App.runtimeconfig.json",
        "release/ProjectCostForecast/RBush.dll",
        "release/ProjectCostForecast/SixLabors.Fonts.dll",
        "release/ProjectCostForecast/System.IO.Packaging.dll"
    ];

    private static readonly string[] SourceFixturePaths =
    [
        "src/ProjectCostForecast.App/Data/data_anonymised.xlsx",
        "src/ProjectCostForecast.App/Data/SampleData.json",
        "src/ProjectCostForecast.App/Data/InitialCostLoad.xlsx"
    ];

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

    [Fact]
    public void F13_cleanup_untracks_exact_artifacts_and_preserves_source_fixture_tracking()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var ignored = RunGit(root, ["check-ignore", "--no-index", "--", .. F13CleanupPaths]);
        var trackedCleanup = RunGit(root, ["ls-files", "--", .. F13CleanupPaths]);
        var trackedSources = RunGit(root, ["ls-files", "--", .. SourceFixturePaths]);

        Assert.Equal(
            F13CleanupPaths.OrderBy(path => path),
            SplitGitLines(ignored).OrderBy(path => path));
        Assert.Empty(SplitGitLines(trackedCleanup));
        Assert.Equal(
            SourceFixturePaths.OrderBy(path => path),
            SplitGitLines(trackedSources).OrderBy(path => path));

        var cleanup = File.ReadAllText(Path.Combine(root, "docs", "audit", "F-13-CLEANUP.md"));
        Assert.Contains("2026-08-29", cleanup);
        Assert.Contains("git rm --cached", cleanup);
        Assert.Contains("preserves every local copy on disk", cleanup);
        Assert.Contains("SOL-00 remains", cleanup);
    }

    private static string RunGit(string root, IReadOnlyList<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Git could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"Git command failed: {error}");
        return output;
    }

    private static string[] SplitGitLines(string output)
    {
        return output.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
