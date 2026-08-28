using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11NegativeControlTests
{
    [Fact]
    public void Discovered_assertions_report_a_representative_broken_expectation()
    {
        var failure = Record.Exception(() => Assert.Equal("expected", "deliberately broken"));

        Assert.NotNull(failure);
        Assert.IsAssignableFrom<Xunit.Sdk.EqualException>(failure);
        Assert.Contains("expected", failure.Message, StringComparison.Ordinal);
        Assert.Contains("deliberately broken", failure.Message, StringComparison.Ordinal);
    }
}
