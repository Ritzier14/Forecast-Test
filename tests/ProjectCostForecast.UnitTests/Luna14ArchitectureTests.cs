using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna14ArchitectureTests
{
    private static readonly string[] ForbiddenModelReferences =
    [
        "System.Windows",
        "Microsoft.Win32",
        "MainWindow",
        "ImageSource",
        "Brush",
        "Visibility",
        "DataGrid",
        "MessageBox"
    ];

    [Fact]
    public void Model_candidate_set_has_no_wpf_window_or_control_dependency()
    {
        var modelRoot = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Models");
        var violations = Directory.EnumerateFiles(modelRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(path => FindForbiddenReferences(
                Path.GetRelativePath(Luna11TestSupport.RepositoryRoot, path),
                File.ReadAllText(path)))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Architecture_rule_rejects_a_deliberately_introduced_forbidden_reference()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => AssertModelSourceAllowed(
            "deliberate-forbidden-reference.cs",
            "using System.Windows.Media;\npublic sealed class Candidate { }"));

        Assert.Contains("System.Windows", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Presentation_projections_and_converters_are_kept_outside_the_model_folder()
    {
        var presentationRoot = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Presentation");
        var expectedFiles = new[]
        {
            "AccountingNoDecimalsConverter.cs",
            "ForecastGroupSummaryConverter.cs",
            "GridColumnHighlightState.cs",
            "GridColumnPresentationState.cs",
            "GridColumnRoleState.cs",
            "GridHoverState.cs",
            "GridSelectionStatus.cs",
            "GridSelectionVisualState.cs",
            "NonNegativeDoubleConverter.cs",
            "ScheduleGridConverters.cs",
            "SummaryPresentationModels.cs"
        };

        Assert.All(expectedFiles, fileName => Assert.True(
            File.Exists(Path.Combine(presentationRoot, fileName)),
            $"Expected presentation file was not found: {fileName}"));

        var xamlSources = new[]
        {
            "src/ProjectCostForecast.App/App.xaml",
            "src/ProjectCostForecast.App/MainWindow.xaml",
            "src/ProjectCostForecast.App/TaskCategoryEditorWindow.xaml"
        };
        var stalePrefixes = new[]
        {
            "models:AccountingNoDecimalsConverter",
            "models:NonNegativeDoubleConverter",
            "models:ForecastGroupSummaryConverter",
            "models:ForecastGroupMonthSummaryConverter",
            "models:DateOnlyTextConverter",
            "models:OutlineIndentConverter",
            "models:GridColumn",
            "models:GridHoverState",
            "models:GridSelection"
        };

        foreach (var relativePath in xamlSources)
        {
            var source = File.ReadAllText(Path.Combine(Luna11TestSupport.RepositoryRoot, relativePath));
            Assert.DoesNotContain(stalePrefixes, prefix => source.Contains(prefix, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Monthly_forecast_locking_keeps_plain_editability_notifications()
    {
        var monthlyForecast = new MonthlyForecast();
        var changedProperties = new List<string?>();
        monthlyForecast.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        monthlyForecast.IsLocked = true;

        Assert.False(monthlyForecast.IsEditable);
        Assert.Contains(nameof(MonthlyForecast.IsEditable), changedProperties);

        changedProperties.Clear();
        monthlyForecast.IsLocked = false;

        Assert.True(monthlyForecast.IsEditable);
        Assert.Contains(nameof(MonthlyForecast.IsEditable), changedProperties);
    }

    [Fact]
    [Trait("Category", "Wpf")]
    public void Presentation_projections_preserve_grid_locking_colour_icon_and_summary_state()
    {
        Luna11TestSupport.RunOnSta(() =>
        {
            var monthColumn = new ForecastMonthColumnDefinition
            {
                Key = "26-09",
                PrimaryLabel = "Sep",
                SecondaryLabel = "26",
                IsEditable = false,
                IsTotal = true
            };

            Assert.Equal("26-09", monthColumn.Key);
            Assert.False(monthColumn.IsEditable);
            Assert.True(monthColumn.IsTotal);
            Assert.Equal(Colors.White, ((SolidColorBrush)monthColumn.PrimaryBackground).Color);
            Assert.Equal(Color.FromRgb(0xEE, 0xF3, 0xF8), ((SolidColorBrush)monthColumn.ValueBorderBrush).Color);
            Assert.Equal(Visibility.Collapsed, monthColumn.LeftSolidSeparatorVisibility);
            Assert.Equal(Visibility.Collapsed, monthColumn.RightDashedSeparatorVisibility);

            var column = new DataGridTextColumn();
            GridColumnRoleState.SetRole(column, GridColumnRoleState.ForecastComments);
            GridColumnHighlightState.SetIsHighlighted(column, true);
            GridColumnPresentationState.SetIconGlyph(column, "C");
            GridColumnPresentationState.SetHeaderColorSpec(column, "#2563EB");
            GridColumnPresentationState.SetColumnBackground(column, Brushes.LightBlue);
            Assert.Equal(GridColumnRoleState.ForecastComments, GridColumnRoleState.GetRole(column));
            Assert.True(GridColumnHighlightState.GetIsHighlighted(column));
            Assert.Equal("C", GridColumnPresentationState.GetIconGlyph(column));
            Assert.Equal("#2563EB", GridColumnPresentationState.GetHeaderColorSpec(column));
            Assert.Same(Brushes.LightBlue, GridColumnPresentationState.GetColumnBackground(column));

            var grid = new DataGrid();
            GridSelectionStatus.SetText(grid, "Count: 2 resources");
            Assert.Equal("Count: 2 resources", GridSelectionStatus.GetText(grid));

            var cell = new DataGridCell();
            GridSelectionVisualState.SetIsCurrentRow(cell, true);
            GridSelectionVisualState.SetIsLockedCell(cell, true);
            GridSelectionVisualState.SetIsCellSelected(cell, true);
            GridSelectionVisualState.SetIsFillHandleCell(cell, true);
            Assert.True(GridSelectionVisualState.GetIsCurrentRow(cell));
            Assert.True(GridSelectionVisualState.GetIsLockedCell(cell));
            Assert.True(GridSelectionVisualState.GetIsCellSelected(cell));
            Assert.True(GridSelectionVisualState.GetIsFillHandleCell(cell));

            var pill = new KpiPill
            {
                Key = "forecast",
                Name = "Forecast",
                ValueText = "$1,250",
                ComparisonText = "+4%",
                IconPath = "/Assets/Icons/png/ic_kpi_forecast_20.png",
                ComparisonVisibility = Visibility.Visible
            };
            Assert.Equal("$1,250", pill.ValueText);
            Assert.Equal("+4%", pill.ComparisonText);
            Assert.Equal(Visibility.Visible, pill.ComparisonVisibility);

            var workspaceTab = new WorkspaceViewTab
            {
                Name = "Forecast",
                IconKey = "missing-icon.png",
                IconColorHex = "#2563EB"
            };
            Assert.IsAssignableFrom<ImageSource>(workspaceTab.IconPreview);
            var workspaceJson = JsonSerializer.Serialize(workspaceTab);
            Assert.DoesNotContain("IconPreview", workspaceJson, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("current-v1.json")]
    [InlineData("legacy-unversioned.json")]
    public void Existing_project_fixtures_round_trip_forecast_summary_and_workspace_values_without_wpf_state(string fixtureName)
    {
        using var directory = new Luna11TemporaryDirectory();
        var sourcePath = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "tests",
            "ProjectCostForecast.UnitTests",
            "Fixtures",
            "ProjectFiles",
            fixtureName);
        var outputPath = Path.Combine(directory.Root, "forecast-summary-round-trip.json");
        var service = new ProjectFileService();
        var dataset = service.Load(sourcePath);

        dataset.ForecastLines.Add(new ForecastLine
        {
            RowNumber = 991,
            TaskNumber = "LUNA-14",
            ResourceName = "Presentation boundary",
            ProjectCode = "AUDIT",
            MonthlyForecasts =
            [
                new MonthlyForecast
                {
                    PeriodLabel = "26-09",
                    PeriodStartDate = Luna11TestSupport.GetCalendarMonthStart("26-09"),
                    Amount = 1250.50m,
                    IsLocked = true
                }
            ]
        });
        dataset.CategorySummaries.Add(new CategorySummary
        {
            ProjectCode = "AUDIT",
            TotalForecast = 1250.50m,
            CostToDate = 400m,
            Budget = 2000m,
            TotalBudgetVariance = 349.50m
        });
        dataset.WorkspaceViews.Add(new WorkspaceViewLayout
        {
            WorkspaceKey = "CTC Forecast",
            ContentKey = "Boundary",
            Name = "Boundary view",
            IconKey = "ic_tab_forecast_16.png",
            IconColorHex = "#2563EB",
            ShowZeroAsBlank = false
        });

        service.Save(outputPath, dataset);
        var json = File.ReadAllText(outputPath);
        var reopened = service.Load(outputPath);
        var forecastLine = Assert.Single(reopened.ForecastLines, line => line.TaskNumber == "LUNA-14");
        var monthlyForecast = Assert.Single(forecastLine.MonthlyForecasts, forecast => forecast.PeriodLabel == "26-09");
        var categorySummary = Assert.Single(reopened.CategorySummaries, summary => summary.ProjectCode == "AUDIT");
        var workspace = Assert.Single(reopened.WorkspaceViews, view => view.ContentKey == "Boundary");

        Assert.Equal("Presentation boundary", forecastLine.ResourceName);
        Assert.Equal(1250.50m, monthlyForecast.Amount);
        Assert.True(monthlyForecast.IsLocked);
        Assert.Equal(Luna11TestSupport.GetCalendarMonthStart("26-09"), monthlyForecast.PeriodStartDate);
        Assert.Equal(1250.50m, categorySummary.TotalForecast);
        Assert.Equal("ic_tab_forecast_16.png", workspace.IconKey);
        Assert.Equal("#2563EB", workspace.IconColorHex);
        Assert.False(workspace.ShowZeroAsBlank);
        Assert.DoesNotContain("BackgroundBrush", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ForegroundBrush", json, StringComparison.Ordinal);
        Assert.DoesNotContain("IconPreview", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ComparisonVisibility", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PrimaryBackground", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ValueBorderBrush", json, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> FindForbiddenReferences(string path, string source)
    {
        return ForbiddenModelReferences
            .Where(source.Contains)
            .Select(reference => $"{path}: {reference}")
            .ToArray();
    }

    private static void AssertModelSourceAllowed(string path, string source)
    {
        var violations = FindForbiddenReferences(path, source);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, violations));
        }
    }
}
