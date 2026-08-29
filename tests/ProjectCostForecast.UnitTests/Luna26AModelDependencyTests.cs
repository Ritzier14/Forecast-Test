using System.Text.RegularExpressions;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna26AModelDependencyTests
{
    private static readonly (string FileName, string Reference)[] KnownUpwardDependencies =
    [
        ("AuditEvent.cs", "Services.DateTimeContract"),
        ("ProjectDataset.cs", "Services.DateTimeContract"),
        ("ForecastLine.cs", "Services.FiscalPeriod")
    ];

    private static readonly string[] ForbiddenNamespaceLayers =
    [
        "Services",
        "Presentation",
        "ViewModels"
    ];

    private static readonly string[] ForbiddenPresentationTypes =
    [
        "Window",
        "Control",
        "Dialog",
        "MessageBox",
        "DataGrid",
        "Brush",
        "ImageSource",
        "Visibility"
    ];

    [Theory]
    [MemberData(nameof(KnownUpwardDependencyCases))]
    public void Characterized_model_to_service_dependencies_are_removed(
        string fileName,
        string reference)
    {
        var source = ReadModelSource(fileName);

        Assert.DoesNotContain(
            reference,
            StripCommentsAndStringLiterals(source),
            StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> KnownUpwardDependencyCases()
    {
        return KnownUpwardDependencies.Select(dependency =>
            new object[] { dependency.FileName, dependency.Reference });
    }

    [Fact]
    public void Model_candidate_root_has_no_forbidden_layer_or_presentation_dependency()
    {
        var modelRoot = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Models");
        var violations = Directory.EnumerateFiles(modelRoot, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(path => FindForbiddenReferences(
                Path.GetRelativePath(Luna11TestSupport.RepositoryRoot, path),
                File.ReadAllText(path)))
            .ToArray();

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("using ProjectCostForecast.App.Services;\npublic sealed class Candidate { }")]
    [InlineData("using ProjectCostForecast.App.Presentation;\npublic sealed class Candidate { }")]
    [InlineData("using ProjectCostForecast.App.ViewModels;\npublic sealed class Candidate { }")]
    [InlineData("public sealed class Candidate { public object Value => ProjectCostForecast.App.Services.DateTimeContract.NewZealandTimeZone; }")]
    [InlineData("public sealed class Candidate { public object Value => ProjectCostForecast.App.Presentation.ColorValueParser; }")]
    [InlineData("public sealed class Candidate { public object Value => ProjectCostForecast.App.ViewModels.MainWindowViewModel; }")]
    [InlineData("using System.Windows.Controls;\npublic sealed class Candidate { }")]
    [InlineData("public sealed class Candidate { public object Value => System.Windows.MessageBox.Show; }")]
    public void Architecture_rule_rejects_each_forbidden_layer_or_direction(string source)
    {
        var failure = Assert.Throws<InvalidOperationException>(() =>
            AssertModelSourceAllowed("deliberate-forbidden-reference.cs", source));

        Assert.NotEmpty(failure.Message);
    }

    [Fact]
    public void Architecture_rule_ignores_comments_and_string_literals_but_scans_qualified_code()
    {
        var harmlessSource = """
            // ProjectCostForecast.App.Services.DateTimeContract is prose.
            public sealed class Candidate
            {
                public string Text => "ProjectCostForecast.App.Presentation.ColorValue";
            }
            """;

        AssertModelSourceAllowed("harmless-reference-text.cs", harmlessSource);

        var actualSource = """
            public sealed class Candidate
            {
                public object Value => ProjectCostForecast.App.Services.DateTimeContract.NewZealandTimeZone;
            }
            """;

        Assert.Throws<InvalidOperationException>(() =>
            AssertModelSourceAllowed("actual-qualified-reference.cs", actualSource));
    }

    [Fact]
    public void Audit_and_unmatched_import_times_follow_the_nz_display_contract()
    {
        var instant = new DateTimeOffset(2027, 1, 1, 11, 0, 0, TimeSpan.Zero);
        var converter = new DateTimeDisplayConverter();

        Assert.Equal(
            DateTimeContract.FormatNewZealand(instant),
            converter.Convert(
                new AuditEvent { ChangedAt = instant }.ChangedAt,
                typeof(string),
                null,
                DateTimeContract.NewZealandDisplayCulture));
        Assert.Equal(
            DateTimeContract.FormatNewZealand(instant),
            converter.Convert(
                new UnmatchedImportCombination { RecordedAt = instant }.RecordedAt,
                typeof(string),
                null,
                DateTimeContract.NewZealandDisplayCulture));
    }

    [Fact]
    public void Forecast_comment_ordering_remains_deterministic_by_fiscal_period_then_recorded_at()
    {
        var line = new ForecastLine
        {
            ResourceName = "Resource A",
            MonthlyCommentHistory =
            [
                new ForecastMonthlyComment
                {
                    PeriodLabel = "26-09",
                    MonthLabel = "Mar 26",
                    ResourceName = "Resource A",
                    Text = "older period",
                    RecordedAt = new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero)
                },
                new ForecastMonthlyComment
                {
                    PeriodLabel = "26-10",
                    MonthLabel = "Apr 26",
                    ResourceName = "Resource A",
                    Text = "newest period",
                    RecordedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new ForecastMonthlyComment
                {
                    PeriodLabel = "26-10",
                    MonthLabel = "Apr 26",
                    ResourceName = "Resource A",
                    Text = "same period newer",
                    RecordedAt = new DateTimeOffset(2026, 4, 3, 0, 0, 0, TimeSpan.Zero)
                }
            ]
        };

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "Apr 26 - FY 26-10: Resource A: same period newer",
                "Apr 26 - FY 26-10: Resource A: newest period",
                "Mar 26 - FY 26-09: Resource A: older period"),
            line.AllMonthComments);
    }

    [Theory]
    [InlineData(null, false, 0, 0, int.MaxValue, 0)]
    [InlineData("", false, 0, 0, int.MaxValue, 0)]
    [InlineData("26-09", true, 2026, 9, 202609, 202609)]
    [InlineData(" 26-09 ", true, 2026, 9, 202609, 202609)]
    [InlineData("70-01", true, 1970, 1, 197001, 197001)]
    [InlineData("69-12", true, 2069, 12, 206912, 206912)]
    [InlineData("26--09", true, 2026, 9, 202609, 202609)]
    [InlineData("26-00", false, 0, 0, int.MaxValue, 0)]
    [InlineData("26-13", false, 0, 13, int.MaxValue, 0)]
    [InlineData("FY26-09", false, 0, 0, int.MaxValue, 0)]
    [InlineData("26-09-extra", false, 0, 0, int.MaxValue, 0)]
    public void Fiscal_period_service_and_model_projection_share_canonical_behavior(
        string? periodLabel,
        bool expectedParsed,
        int expectedYear,
        int expectedMonth,
        int expectedServiceSortKey,
        int expectedModelSortKey)
    {
        var parsed = FiscalPeriod.TryParseLabel(periodLabel, out var year, out var month);
        var modelComment = new ForecastMonthlyComment
        {
            PeriodLabel = periodLabel ?? string.Empty
        };

        Assert.Equal(expectedParsed, parsed);
        Assert.Equal(expectedYear, year);
        Assert.Equal(expectedMonth, month);
        Assert.Equal(expectedServiceSortKey, FiscalPeriod.SortKey(periodLabel));
        Assert.Equal(expectedModelSortKey, modelComment.PeriodSortKey);
    }

    [Fact]
    public void Fiscal_period_service_is_a_thin_delegate_to_the_model_safe_parser()
    {
        var serviceSource = File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Services",
            "FiscalPeriod.cs"));
        var code = StripCommentsAndStringLiterals(serviceSource);

        Assert.Matches(
            @"public\s+static\s+bool\s+TryParseLabel\s*\([^;]+?\)\s*=>\s*FiscalPeriodOrdering\.TryParseLabel\s*\([^;]+?\)\s*;",
            code);
        Assert.Matches(
            @"public\s+static\s+int\s+SortKey\s*\([^;]+?\)\s*=>\s*FiscalPeriodOrdering\.SortKey\s*\([^;]+?\)\s*;",
            code);
        Assert.DoesNotContain("StringSplitOptions", code, StringComparison.Ordinal);
    }

    private static string ReadModelSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Models",
            fileName));
    }

    private static IReadOnlyList<string> FindForbiddenReferences(string path, string source)
    {
        var code = StripCommentsAndStringLiterals(source);
        var violations = new List<string>();

        foreach (var layer in ForbiddenNamespaceLayers)
        {
            if (Regex.IsMatch(
                    code,
                    $@"\b(?:using|namespace)\s+(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?(?:global::)?(?:ProjectCostForecast\.App\.)?{layer}(?:\s*\.[A-Za-z_][A-Za-z0-9_]*)*\s*;",
                    RegexOptions.CultureInvariant)
                || Regex.IsMatch(
                    code,
                    $@"(?<![A-Za-z0-9_])(?:global::)?(?:ProjectCostForecast\.App\.)?{layer}(?=\s*\.)",
                    RegexOptions.CultureInvariant))
            {
                violations.Add($"{path}: {layer} namespace dependency");
            }
        }

        if (Regex.IsMatch(
                code,
                @"(?<![A-Za-z0-9_])(?:global::)?(?:System\.Windows(?:\.[A-Za-z_][A-Za-z0-9_]*)*|Microsoft\.Win32(?:\.[A-Za-z_][A-Za-z0-9_]*)*)",
                RegexOptions.CultureInvariant))
        {
            violations.Add($"{path}: WPF or Windows namespace dependency");
        }

        foreach (var typeName in ForbiddenPresentationTypes)
        {
            if (Regex.IsMatch(
                    code,
                    $@"(?<![A-Za-z0-9_]){typeName}(?![A-Za-z0-9_])",
                    RegexOptions.CultureInvariant))
            {
                violations.Add($"{path}: {typeName} presentation dependency");
            }
        }

        return violations;
    }

    private static void AssertModelSourceAllowed(string path, string source)
    {
        var violations = FindForbiddenReferences(path, source);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, violations));
        }
    }

    private static string StripCommentsAndStringLiterals(string source)
    {
        return Regex.Replace(
            source,
            @"//[^\r\n]*|/\*[\s\S]*?\*/|@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'",
            match => new string(match.Value.Select(character => character is '\r' or '\n' ? character : ' ').ToArray()),
            RegexOptions.CultureInvariant);
    }
}
