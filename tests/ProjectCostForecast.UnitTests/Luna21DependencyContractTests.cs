using System.Text.Json;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna21DependencyContractTests
{
    [Fact]
    public void Locked_restore_and_closedxml_patch_are_committed()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        var appProject = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ProjectCostForecast.App",
            "ProjectCostForecast.App.csproj"));
        var verifier = File.ReadAllText(Path.Combine(root, "scripts", "verify.ps1"));

        Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", props);
        Assert.Contains("<NuGetAuditMode>all</NuGetAuditMode>", props);
        Assert.Contains("<PackageReference Include=\"ClosedXML\" Version=\"0.105.1\" />", appProject);
        Assert.Contains("'--locked-mode'", verifier);

        foreach (var relativePath in new[]
        {
            Path.Combine("src", "ProjectCostForecast.App", "packages.lock.json"),
            Path.Combine("tests", "ProjectCostForecast.Tests", "packages.lock.json"),
            Path.Combine("tests", "ProjectCostForecast.UnitTests", "packages.lock.json")
        })
        {
            Assert.True(File.Exists(Path.Combine(root, relativePath)), $"Missing lock file: {relativePath}");
        }

        var appLock = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ProjectCostForecast.App",
            "packages.lock.json"));
        Assert.Contains("\"resolved\": \"0.105.1\"", appLock);
    }

    [Fact]
    public void Dependency_audit_contract_records_transitive_inventory_and_no_vulnerabilities()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var auditScript = File.ReadAllText(Path.Combine(root, "scripts", "audit-dependencies.ps1"));
        var reportPath = Path.Combine(root, "docs", "audit", "LUNA-21-DEPENDENCY-AUDIT.json");

        Assert.Contains("--include-transitive", auditScript);
        Assert.Contains("--vulnerable", auditScript);
        Assert.Contains("FailOnVulnerability", auditScript);
        Assert.True(File.Exists(reportPath));

        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal(0, report.RootElement.GetProperty("VulnerabilityCount").GetInt32());
        Assert.Equal(22, report.RootElement.GetProperty("Packages").GetArrayLength());
        Assert.DoesNotContain("C:\\Users\\", File.ReadAllText(reportPath), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:/Users/", File.ReadAllText(reportPath), StringComparison.OrdinalIgnoreCase);
    }
}
