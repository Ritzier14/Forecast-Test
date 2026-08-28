using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna11WpfGridCoverageTests
{
    [Fact]
    [Trait("Category", "Wpf")]
    public void Wpf_grid_layout_and_shared_selection_regressions_run_on_a_dedicated_sta_thread()
    {
        Luna11TestSupport.RunOnSta(() =>
        {
            var migratedMonthWidth = Luna11TestSupport.InvokeForecastWidthMigration(112d, 78d, 70d, isTotal: false);
            Assert.InRange(migratedMonthWidth, 78d - 0.01d, 78d + 0.01d);
            var migratedTotalMonthWidth = Luna11TestSupport.InvokeForecastWidthMigration(120d, 96d, 84d, isTotal: true);
            Assert.InRange(migratedTotalMonthWidth, 96d - 0.01d, 96d + 0.01d);
            var preservedCustomMonthWidth = Luna11TestSupport.InvokeForecastWidthMigration(143d, 78d, 70d, isTotal: false);
            Assert.InRange(preservedCustomMonthWidth, 143d - 0.01d, 143d + 0.01d);

            var resetWidthColumn = new DataGridTextColumn { Header = "Reset width test", Width = 137d };
            InvokeEnsureColumnPresentation(resetWidthColumn);
            resetWidthColumn.Width = 243d;
            Assert.True(InvokeResetColumnWidthToDefault(resetWidthColumn));
            Assert.InRange(resetWidthColumn.Width.DisplayValue, 137d - 0.01d, 137d + 0.01d);

            var forecastVirtualization = GetForecastGridVirtualizationSettings();
            Assert.True(forecastVirtualization.Rows);
            Assert.True(forecastVirtualization.Columns);
            Assert.True(forecastVirtualization.Panel);
            Assert.True(forecastVirtualization.Grouping);

            RunSharedGridControlRegressionTests();

            var resizableForecastLine = new ForecastLine();
            Assert.False(resizableForecastLine.HasCustomRowHeight);
            resizableForecastLine.SetRowDisplayHeight(96);
            Assert.True(resizableForecastLine.HasCustomRowHeight);
            Assert.InRange(resizableForecastLine.RowDisplayHeight, 96d - 0.01d, 96d + 0.01d);
            resizableForecastLine.SetRowDisplayHeight(1);
            Assert.InRange(resizableForecastLine.RowDisplayHeight, 30d - 0.01d, 30d + 0.01d);
            resizableForecastLine.SetRowDisplayHeight(999);
            Assert.InRange(resizableForecastLine.RowDisplayHeight, 600d - 0.01d, 600d + 0.01d);
        });
    }

    private static (bool Rows, bool Columns, bool Panel, bool Grouping) GetForecastGridVirtualizationSettings()
    {
        (bool Rows, bool Columns, bool Panel, bool Grouping) result = default;
        Luna11TestSupport.RunOnSta(() =>
        {
            var grid = new DataGrid { Name = "ForecastLinesGrid" };
            var method = typeof(MainWindow).GetMethod(
                "ConfigureForecastGridPerformance",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(MainWindow).FullName, "ConfigureForecastGridPerformance");
            method.Invoke(null, [grid]);
            result = (
                grid.EnableRowVirtualization,
                grid.EnableColumnVirtualization,
                VirtualizingPanel.GetIsVirtualizing(grid),
                VirtualizingPanel.GetIsVirtualizingWhenGrouping(grid));
        });
        return result;
    }

    private static void InvokeEnsureColumnPresentation(DataGridColumn column)
    {
        var method = typeof(MainWindow).GetMethod(
            "EnsureColumnPresentation",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(MainWindow).FullName, "EnsureColumnPresentation");
        method.Invoke(null, [column]);
    }

    private static bool InvokeResetColumnWidthToDefault(DataGridColumn column)
    {
        var method = typeof(MainWindow).GetMethod(
            "ResetColumnWidthToDefault",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(MainWindow).FullName, "ResetColumnWidthToDefault");
        return (bool)(method.Invoke(null, [column])
            ?? throw new InvalidOperationException("Column width reset returned null."));
    }

    private static void RunSharedGridControlRegressionTests()
    {
        var appSourceRoot = Path.Combine(Luna11TestSupport.RepositoryRoot, "src", "ProjectCostForecast.App");
        var sourceFiles = Directory.GetFiles(appSourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var rawGridDeclarations = sourceFiles
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                        ? @"<DataGrid(?=[\s>])"
                        : @"\bnew\s+DataGrid\s*(?=\{)")
                .Select(_ => path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Empty(rawGridDeclarations);

        var forecastInteractionSource = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.ForecastGridInteraction.cs"));
        var ganttSource = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.Gantt.cs"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.xaml"));
        var gridBuilderSource = File.ReadAllText(Path.Combine(appSourceRoot, "MainWindow.GridBuilders.cs"));
        Assert.DoesNotContain("ApplyLiveForecastRowHeight", forecastInteractionSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<Setter Property=\"Height\" Value=\"{Binding RowDisplayHeight}\"",
            mainWindowXaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FrameworkElement.HeightProperty,\r\n            new Binding(nameof(ForecastLine.RowDisplayHeight))",
            gridBuilderSource,
            StringComparison.Ordinal);
        Assert.True(
            ganttSource.Contains("ScheduleGrid.EnableRowVirtualization = false;", StringComparison.Ordinal)
            && ganttSource.Contains("VirtualizingPanel.SetIsVirtualizing(ScheduleGrid, false);", StringComparison.Ordinal)
            && ganttSource.Contains("ScrollViewer.SetCanContentScroll(ScheduleGrid, false);", StringComparison.Ordinal));

        foreach (var profile in Enum.GetValues<ProjectDataGridProfile>())
        {
            var profileGrid = new ProjectDataGrid { Profile = profile };
            ProjectDataGridProfiles.Apply(profileGrid, profile);
            Assert.Equal(profile, ProjectDataGridProfiles.GetProfile(profileGrid));
            Assert.Equal(DataGridSelectionMode.Extended, profileGrid.SelectionMode);
            Assert.True(profileGrid.CanUserResizeRows);
            Assert.True(profileGrid.ModifierClickSelectsWholeRows);
            if (profile == ProjectDataGridProfile.Schedule)
            {
                Assert.Equal(DataGridSelectionUnit.FullRow, profileGrid.SelectionUnit);
            }
        }

        var pixelScrollGrid = new ProjectDataGrid
        {
            EnableRowVirtualization = false
        };
        VirtualizingPanel.SetIsVirtualizing(pixelScrollGrid, false);
        ScrollViewer.SetCanContentScroll(pixelScrollGrid, false);
        ProjectDataGridProfiles.Apply(pixelScrollGrid, ProjectDataGridProfile.Schedule);
        Assert.False(ScrollViewer.GetCanContentScroll(pixelScrollGrid));
        Assert.False(pixelScrollGrid.EnableRowVirtualization);
        Assert.False(VirtualizingPanel.GetIsVirtualizing(pixelScrollGrid));

        var rows = Enumerable.Range(1, 8)
            .Select(index => new SharedGridTestRow($"Row {index}", index * 10, index <= 4 ? "A" : "B"))
            .ToList();
        var centeredTextStyle = new Style(typeof(TextBlock));
        centeredTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        centeredTextStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 2, 6, 2)));
        var grid = new ProjectDataGrid
        {
            Profile = ProjectDataGridProfile.Forecast,
            ItemsSource = rows,
            AutoGenerateColumns = false,
            Width = 420,
            Height = 250,
            RowHeight = 30,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            EnableRowVirtualization = true,
            EnableColumnVirtualization = true
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(SharedGridTestRow.Name)),
            ElementStyle = centeredTextStyle,
            Width = 220
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SharedGridTestRow.Value)),
            ElementStyle = centeredTextStyle,
            Width = 120
        });
        var constrainedRowStyle = new Style(typeof(DataGridRow));
        var constrainedRowTrigger = new DataTrigger
        {
            Binding = new Binding(nameof(SharedGridTestRow.Name)),
            Value = rows[^1].Name
        };
        constrainedRowTrigger.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 72d));
        constrainedRowStyle.Triggers.Add(constrainedRowTrigger);
        grid.RowStyle = constrainedRowStyle;
        grid.SetRowHeight(rows[^1], 30d);
        using var presentationSource = new HwndSource(new HwndSourceParameters("SharedGridRegressionHost")
        {
            Width = 420,
            Height = 250,
            WindowStyle = unchecked((int)0x80000000)
        });
        presentationSource.RootVisual = grid;
        ArrangeSharedGrid(grid);
        grid.ScrollIntoView(rows[0]);
        ArrangeSharedGrid(grid);
        var row = grid.ItemContainerGenerator.ContainerFromItem(rows[0]) as DataGridRow
            ?? throw new InvalidOperationException("Shared grid did not realize its first row.");
        foreach (var targetHeight in new[] { 30d, 31d, 42d, 63d, 96d, 137d, 300d, 600d })
        {
            grid.SetRowHeight(rows[0], targetHeight);
            ArrangeSharedGrid(grid);
            Assert.InRange(row.ActualHeight, targetHeight - 0.75d, targetHeight + 0.75d);
            var cells = FindVisualDescendantsForTest<DataGridCell>(row).ToList();
            Assert.True(cells.Count >= 2);
            Assert.All(cells, cell => Assert.Equal(DependencyProperty.UnsetValue, cell.ReadLocalValue(FrameworkElement.HeightProperty)));
            Assert.All(cells, cell => Assert.InRange(cell.ActualHeight, row.ActualHeight - 1d, row.ActualHeight + 1d));
            var text = FindVisualDescendantsForTest<TextBlock>(cells[0]).FirstOrDefault()
                ?? throw new InvalidOperationException("Shared grid cell did not create its text content.");
            var textTop = text.TranslatePoint(new Point(0, 0), row).Y;
            var textCenter = textTop + (text.ActualHeight / 2d);
            Assert.InRange(textCenter, (row.ActualHeight / 2d) - 1d, (row.ActualHeight / 2d) + 1d);
        }
        grid.ResetRowHeight(rows[0]);
        ArrangeSharedGrid(grid);
        Assert.InRange(row.ActualHeight, 30d - 0.75d, 30d + 0.75d);
        Assert.Null(grid.GetRowHeight(rows[0]));
        grid.ScrollIntoView(rows[^1]);
        ArrangeSharedGrid(grid);
        var constrainedRow = grid.ItemContainerGenerator.ContainerFromItem(rows[^1]) as DataGridRow
            ?? throw new InvalidOperationException("Shared grid did not realize its constrained row.");
        Assert.InRange(constrainedRow.ActualHeight, 72d - 0.75d, 72d + 0.75d);
        Assert.InRange(grid.GetRowHeight(rows[^1]) ?? 0d, 72d - 0.01d, 72d + 0.01d);
        Assert.All(
            FindVisualDescendantsForTest<DataGridCell>(constrainedRow),
            cell => Assert.InRange(cell.ActualHeight, constrainedRow.ActualHeight - 1d, constrainedRow.ActualHeight + 1d));

        var groupedRowsView = new ListCollectionView(rows);
        groupedRowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SharedGridTestRow.Group)));
        grid.ItemsSource = groupedRowsView;
        ArrangeSharedGrid(grid);
        Assert.NotNull(groupedRowsView.Groups);
        Assert.True(groupedRowsView.Groups!.Count > 0);
        var selectRowsMethod = typeof(ProjectDataGrid).GetMethod(
            "SelectWholeRowsFromModifierClick",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(ProjectDataGrid).FullName, "SelectWholeRowsFromModifierClick");
        var selectionAnchorField = typeof(ProjectDataGrid).GetField(
            "_rowSelectionAnchor",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(ProjectDataGrid).FullName, "_rowSelectionAnchor");
        object? modifierCurrentItem = null;
        grid.ModifierRowSelectionCompleted += (_, eventArgs) => modifierCurrentItem = eventArgs.CurrentItem;
        selectionAnchorField.SetValue(grid, rows[1]);
        selectRowsMethod.Invoke(grid, [rows[4], ModifierKeys.Shift]);
        var selectedRangeItems = grid.SelectedCells.Select(cell => cell.Item).Distinct(ReferenceEqualityComparer.Instance).ToList();
        Assert.Equal(rows.Skip(1).Take(4), selectedRangeItems);
        Assert.True(ReferenceEquals(grid.CurrentCell.Item, rows[4]) && ReferenceEquals(modifierCurrentItem, rows[4]));
        selectionAnchorField.SetValue(grid, rows[5]);
        selectRowsMethod.Invoke(grid, [rows[3], ModifierKeys.Shift]);
        selectedRangeItems = grid.SelectedCells.Select(cell => cell.Item).Distinct(ReferenceEqualityComparer.Instance).ToList();
        Assert.Equal(rows.Skip(3).Take(3), selectedRangeItems);
        Assert.True(ReferenceEquals(grid.CurrentCell.Item, rows[3]) && ReferenceEquals(modifierCurrentItem, rows[3]));
        selectRowsMethod.Invoke(grid, [rows[4], ModifierKeys.Control]);
        selectedRangeItems = grid.SelectedCells.Select(cell => cell.Item).Distinct(ReferenceEqualityComparer.Instance).ToList();
        Assert.DoesNotContain(rows[4], selectedRangeItems);
        Assert.Contains(rows[3], selectedRangeItems);
        Assert.Contains(rows[5], selectedRangeItems);
        Assert.True(ReferenceEquals(grid.CurrentCell.Item, rows[3]) && ReferenceEquals(modifierCurrentItem, rows[3]));
        grid.SelectedCells.Clear();
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        selectionAnchorField.SetValue(grid, rows[1]);
        selectRowsMethod.Invoke(grid, [rows[4], ModifierKeys.Shift]);
        var selectedFullRows = grid.SelectedItems.Cast<object>().ToList();
        Assert.Equal(rows.Skip(1).Take(4), selectedFullRows);
        Assert.True(ReferenceEquals(grid.CurrentCell.Item, rows[4]) && ReferenceEquals(modifierCurrentItem, rows[4]));
        selectRowsMethod.Invoke(grid, [rows[3], ModifierKeys.Control]);
        selectedFullRows = grid.SelectedItems.Cast<object>().ToList();
        Assert.DoesNotContain(rows[3], selectedFullRows);
        Assert.Contains(rows[1], selectedFullRows);
        Assert.Contains(rows[4], selectedFullRows);
        Assert.True(ReferenceEquals(grid.CurrentCell.Item, rows[1]) && ReferenceEquals(modifierCurrentItem, rows[1]));
    }

    private static void ArrangeSharedGrid(ProjectDataGrid grid)
    {
        var size = new Size(grid.Width, grid.Height);
        grid.ApplyTemplate();
        grid.Measure(size);
        grid.Arrange(new Rect(size));
        grid.UpdateLayout();
    }

    private static IEnumerable<T> FindVisualDescendantsForTest<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendantsForTest<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record SharedGridTestRow(string Name, int Value, string Group);
}
