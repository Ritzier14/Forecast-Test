using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna23ReleaseTruthTests
{
    [Fact]
    public void Release_truth_matches_version_and_supported_file_boundaries()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var project = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ProjectCostForecast.App",
            "ProjectCostForecast.App.csproj"));
        var truth = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "audit",
            "LUNA-23-RELEASE-TRUTH.md"));

        Assert.Contains("<TargetFramework>net8.0-windows</TargetFramework>", project);
        Assert.Contains("<OutputType>WinExe</OutputType>", project);
        Assert.Contains("<Version>1.0.1</Version>", project);
        Assert.Contains("CurrentVersion", truth);
        Assert.Contains("format is version 1", truth);
        Assert.Contains("format 0", truth);
        Assert.Contains(".csv", truth);
        Assert.Contains(".xlsx", truth);
        Assert.Contains(".xlsm", truth);
        Assert.Contains("No server", truth);
        Assert.Contains("No silent file migration", truth);
    }

    [Fact]
    public void Release_documentation_links_gates_and_real_failure_boundaries()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var checklist = File.ReadAllText(Path.Combine(root, "docs", "RELEASE_CHECKLIST.md"));
        var architecture = File.ReadAllText(Path.Combine(root, "docs", "ARCHITECTURE.md"));
        var truth = File.ReadAllText(Path.Combine(root, "docs", "audit", "LUNA-23-RELEASE-TRUTH.md"));
        var closeSource = File.ReadAllText(Path.Combine(root, "src", "ProjectCostForecast.App", "MainWindow.xaml.cs"));
        var migrationSource = File.ReadAllText(Path.Combine(root, "src", "ProjectCostForecast.App", "Services", "ProjectDatasetMigrationPipeline.cs"));
        var recovery = File.ReadAllText(Path.Combine(root, "docs", "RECOVERY_RUNBOOK.md"));
        var diagnostics = File.ReadAllText(Path.Combine(root, "docs", "DIAGNOSTICS_RUNBOOK.md"));

        Assert.Contains("LUNA-23-RELEASE-TRUTH.md", readme);
        Assert.Contains("CI and repository-hygiene gate", checklist);
        Assert.Contains("signed installer or MSIX", checklist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LUNA-23", architecture);
        Assert.Contains("MainWindow_", closeSource);
        Assert.Contains("PromptForCloseDecision", closeSource);
        Assert.Contains("ConfirmClose", closeSource);
        Assert.Contains("YesNoCancel", closeSource);
        Assert.Contains("CurrentVersion", migrationSource);
        Assert.Contains("sourceVersion > CurrentVersion", migrationSource);
        Assert.Contains("new destination", recovery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pre-restore backup", recovery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Project values", diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No installer/signing claim", truth);
    }
}
