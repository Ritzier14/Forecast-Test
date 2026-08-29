using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna18ALifecycleTests
{
    [Fact]
    public void Main_window_has_one_named_lifecycle_owner_without_legacy_gantt_wiring()
    {
        var mainWindow = ReadSource("src/ProjectCostForecast.App/MainWindow.xaml.cs");
        var gantt = ReadSource("src/ProjectCostForecast.App/MainWindow.Gantt.cs");
        var lifecycle = ReadSource("src/ProjectCostForecast.App/MainWindow.Lifecycle.cs");

        Assert.Equal(1, Count(mainWindow, "Loaded += MainWindow_Loaded;"));
        Assert.Equal(1, Count(mainWindow, "DataContextChanged += MainWindow_DataContextChanged;"));
        Assert.Equal(1, Count(mainWindow, "Unloaded += MainWindow_Unloaded;"));
        Assert.Equal(1, Count(mainWindow, "Closed += MainWindow_Closed;"));
        Assert.DoesNotContain("Loaded += (_, _)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContextChanged += (_, _)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeGanttChart", gantt, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow_GanttLoaded", gantt, StringComparison.Ordinal);
        Assert.Contains("UnwireGanttVisualSubscriptions", lifecycle, StringComparison.Ordinal);
    }

    [Fact]
    public void Lifecycle_cleanup_removes_routed_handlers_and_invalidates_pending_work()
    {
        var lifecycle = ReadSource("src/ProjectCostForecast.App/MainWindow.Lifecycle.cs");

        Assert.Contains("MainWindow_Unloaded", lifecycle, StringComparison.Ordinal);
        Assert.Contains("MainWindow_Closed", lifecycle, StringComparison.Ordinal);
        Assert.Contains("DetachWindowVisualSubscriptions", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CancelPendingWindowWork", lifecycle, StringComparison.Ordinal);
        Assert.Contains("UIElement.PreviewMouseLeftButtonDownEvent", lifecycle, StringComparison.Ordinal);
        Assert.Contains("FrameworkElement.ContextMenuOpeningEvent", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_mainWindowLifetimeVersion++", lifecycle, StringComparison.Ordinal);
        Assert.Contains("lifetimeVersion != getLifetimeVersion()", lifecycle, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.HasShutdownStarted", lifecycle, StringComparison.Ordinal);
    }

    [Fact]
    public void Closing_the_main_window_disposes_view_model_timer_and_dispatcher_ownership()
    {
        var lifecycle = ReadSource("src/ProjectCostForecast.App/MainWindow.Lifecycle.cs");
        var viewModelLifecycle = ReadSource("src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.Lifecycle.cs");
        var refresh = ReadSource("src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.Refresh.cs");

        Assert.Contains("viewModel.Dispose();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class MainWindowViewModel", viewModelLifecycle, StringComparison.Ordinal);
        Assert.Contains("IDisposable", ReadSource("src/ProjectCostForecast.App/ViewModels/MainWindowViewModel.cs"), StringComparison.Ordinal);
        Assert.Contains("_preferenceSaveTimer.Tick -= PreferenceSaveTimer_Tick", viewModelLifecycle, StringComparison.Ordinal);
        Assert.Contains("_searchRefreshTimer.Tick -= SearchRefreshTimer_Tick", viewModelLifecycle, StringComparison.Ordinal);
        Assert.Contains("_refreshDispatcherOperation?.Abort()", viewModelLifecycle, StringComparison.Ordinal);
        Assert.Contains("_scheduleRecalculationOperation?.Abort()", viewModelLifecycle, StringComparison.Ordinal);
        Assert.Contains("if (_disposed)", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void Disposed_refresh_coordinator_ignores_a_callback_already_queued_on_the_dispatcher()
    {
        var scheduled = new List<Action>();
        var executed = new List<RefreshRequest>();
        using var coordinator = new RefreshCoordinator(scheduled.Add, executed.Add);

        coordinator.Request(new RefreshRequest(RefreshProjection.All, "close"));
        Assert.Single(scheduled);

        coordinator.Dispose();
        scheduled[0]();

        Assert.Empty(executed);
        Assert.False(coordinator.HasPendingRequest);
    }

    private static string ReadSource(string relativePath) => File.ReadAllText(
        Path.Combine(Luna11TestSupport.RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
