using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna24ClosureEvidenceTests
{
    [Fact]
    public void Finding_matrix_covers_every_audit_finding_and_records_f13_cleanup()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var matrix = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "audit",
            "LUNA-24-FINDING-MATRIX.md"));

        for (var finding = 0; finding <= 20; finding++)
        {
            Assert.Contains($"F-{finding:D2}", matrix);
        }

        Assert.Contains("There are no unresolved P0 findings.", matrix);
        Assert.Contains("| F-13 | P1 | Fixed", matrix);
        Assert.Contains("2026-08-29", matrix);
        Assert.Contains("exact 17", matrix);
        Assert.Contains("does not claim SOL-00 final", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("explicit approval pending", matrix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sol_handoff_contains_independent_gate_and_deferred_risk_instructions()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var handoff = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "audit",
            "LUNA_HANDOFF.md"));

        Assert.Contains("Sol Ultra", handoff);
        Assert.Contains("verify.ps1", handoff);
        Assert.Contains("all 184 discovered tests", handoff);
        Assert.Contains("428 retained smoke assertions", handoff);
        Assert.Contains("LUNA-24-FINDING-MATRIX.md", handoff);
        Assert.Contains("clean checkout", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicitly delegated", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sol selected the cleanup option", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("git rm --cached", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactly 16 files", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("local copies remain", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("completed", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No signed installer/MSIX", handoff);
        Assert.Contains("P1", handoff);
    }
}
