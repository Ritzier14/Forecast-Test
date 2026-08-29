using System.Windows;
using System.Windows.Controls;
using ProjectCostForecast.App;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna19ARightClickGridPanTests
{
    [Fact]
    public void Pan_session_keeps_clicks_below_threshold_and_scrolls_in_the_opposite_direction()
    {
        var session = new RightClickGridPanSession();
        session.Begin(new Point(100, 100), horizontalOffset: 40, verticalOffset: 25);

        Assert.Null(session.TryMove(new Point(105.99, 105.99), rightButtonPressed: true, 100, 80));
        var move = session.TryMove(new Point(94, 90), rightButtonPressed: true, 100, 80);

        Assert.Equal(new RightClickGridPanMove(46, 35), move);
        Assert.True(session.IsDragging);
        Assert.True(session.End());
        Assert.False(session.IsActive);
        Assert.False(session.IsDragging);
    }

    [Fact]
    public void Pan_session_clamps_bounds_and_leaves_disabled_directions_at_zero()
    {
        var session = new RightClickGridPanSession();
        session.Begin(new Point(100, 100), horizontalOffset: 10, verticalOffset: 20);

        var lowerBoundMove = session.TryMove(new Point(200, 200), rightButtonPressed: true, 50, 0);

        Assert.Equal(new RightClickGridPanMove(0, 0), lowerBoundMove);

        session.Begin(new Point(100, 100), horizontalOffset: 10, verticalOffset: 20);
        var upperBoundMove = session.TryMove(new Point(-100, -100), rightButtonPressed: true, 50, 30);

        Assert.Equal(new RightClickGridPanMove(50, 30), upperBoundMove);
    }

    [Fact]
    public void Capture_loss_cancellation_clears_the_pan_session()
    {
        var session = new RightClickGridPanSession();
        session.Begin(new Point(20, 20), horizontalOffset: 12, verticalOffset: 8);
        Assert.NotNull(session.TryMove(new Point(30, 20), rightButtonPressed: true, 100, 100));

        // The attached behavior calls the same cancellation boundary from
        // LostMouseCapture, Escape, unload, and explicit detach.
        session.Cancel();

        Assert.False(session.IsActive);
        Assert.False(session.IsDragging);
        Assert.False(session.End());
    }

    [Fact]
    [Trait("Category", "Wpf")]
    public void Attached_behavior_can_be_enabled_cancelled_and_detached_on_sta()
    {
        Luna11TestSupport.RunOnSta(() =>
        {
            var grid = new DataGrid();
            RightClickGridPanBehavior.SetIsEnabled(grid, true);

            Assert.True(RightClickGridPanBehavior.GetIsEnabled(grid));
            Assert.False(RightClickGridPanBehavior.GetIsDragging(grid));

            RightClickGridPanBehavior.Cancel(grid);
            Assert.False(RightClickGridPanBehavior.GetIsDragging(grid));

            RightClickGridPanBehavior.SetIsEnabled(grid, false);
            Assert.False(RightClickGridPanBehavior.GetIsEnabled(grid));
            Assert.False(RightClickGridPanBehavior.GetIsDragging(grid));
        });
    }

    [Fact]
    public void One_canonical_behavior_is_wired_to_the_three_characterized_surfaces()
    {
        var root = Luna11TestSupport.RepositoryRoot;
        var behaviorSource = ReadSource(root, "src", "ProjectCostForecast.App", "Presentation", "RightClickGridPanBehavior.cs");
        var sessionSource = ReadSource(root, "src", "ProjectCostForecast.App", "Presentation", "RightClickGridPanSession.cs");
        var mainGridSource = ReadSource(root, "src", "ProjectCostForecast.App", "MainWindow.GridFilters.cs");
        var lifecycleSource = ReadSource(root, "src", "ProjectCostForecast.App", "MainWindow.Lifecycle.cs");
        var mainContextSource = ReadSource(root, "src", "ProjectCostForecast.App", "MainWindow.SpreadsheetGridInteraction.cs");
        var costSource = ReadSource(root, "src", "ProjectCostForecast.App", "CostCenterMappingWindow.cs");
        var editorSource = ReadSource(root, "src", "ProjectCostForecast.App", "TaskCategoryEditorWindow.xaml");
        var editorCodeSource = ReadSource(root, "src", "ProjectCostForecast.App", "TaskCategoryEditorWindow.xaml.cs");

        Assert.Contains("DragThreshold = 6d", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("LostMouseCapture", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("CaptureMouse", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("ReleaseMouseCapture", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("ScrollableWidth", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("ScrollableHeight", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("Escape", behaviorSource, StringComparison.Ordinal);
        Assert.Contains("RightClickGridPanBehavior.SetIsEnabled(grid, true)", mainGridSource, StringComparison.Ordinal);
        Assert.Contains("RightClickGridPanBehavior.SetIsEnabled(grid, false)", lifecycleSource, StringComparison.Ordinal);
        Assert.Contains("RightClickGridPanBehavior.GetIsDragging(grid)", mainContextSource, StringComparison.Ordinal);
        Assert.Contains("RightClickGridPanBehavior.SetIsEnabled(grid, true)", costSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AttachRightClickPan", costSource, StringComparison.Ordinal);
        Assert.Contains("local:RightClickGridPanBehavior.IsEnabled=\"True\"", editorSource, StringComparison.Ordinal);
        Assert.Contains("RightClickGridPanBehavior.Cancel(grid)", editorCodeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EditorGrid_PreviewMouseRightButtonDown", editorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EditorGrid_PreviewMouseMove", editorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EditorGrid_PreviewMouseRightButtonUp", editorSource, StringComparison.Ordinal);
        Assert.Contains("TryMove", sessionSource, StringComparison.Ordinal);
    }

    private static string ReadSource(string root, params string[] segments)
    {
        return File.ReadAllText(Path.Combine([root, ..segments]));
    }
}
