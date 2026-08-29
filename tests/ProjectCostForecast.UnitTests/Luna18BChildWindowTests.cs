using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna18BChildWindowTests
{
    [Fact]
    public async Task Cancellation_is_observed_without_reporting_a_failure()
    {
        using var cancellation = new CancellationTokenSource();
        var failures = new List<Exception>();
        cancellation.Cancel();

        var result = await ObservedAsyncOperation.RunAsync(
            async token => await Task.Delay(Timeout.InfiniteTimeSpan, token),
            cancellation.Token,
            failures.Add);

        Assert.Equal(ObservedAsyncOperationStatus.Canceled, result.Status);
        Assert.Null(result.Exception);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Non_cancellation_failure_is_observed_and_sent_to_diagnostics()
    {
        var failures = new List<Exception>();
        var result = await ObservedAsyncOperation.RunAsync(
            _ => Task.FromException(new InvalidOperationException("comparison failed")),
            CancellationToken.None,
            failures.Add);

        var exception = Assert.Single(failures);
        Assert.Equal(ObservedAsyncOperationStatus.Failed, result.Status);
        Assert.Same(exception, result.Exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Schedule_comparison_uses_an_observed_close_aware_task_boundary()
    {
        var source = File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "ScheduleComparisonWindow.cs"));

        Assert.Contains("ObservedAsyncOperation.RunAsync", source, StringComparison.Ordinal);
        Assert.Contains("await refreshTask", source, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll", source, StringComparison.Ordinal);
        Assert.Contains("_lifetimeCancellation.Cancel()", source, StringComparison.Ordinal);
        Assert.Contains("IsRefreshActive", source, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", source, StringComparison.Ordinal);
        Assert.Contains("schedule-comparison.refresh", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Action(async", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_window_owns_and_detaches_the_schedule_comparison_child()
    {
        var source = File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "MainWindow.ScheduleCommands.cs"));
        var lifecycle = File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "MainWindow.Lifecycle.cs"));

        Assert.Contains("Closed += ScheduleComparisonWindow_Closed", source, StringComparison.Ordinal);
        Assert.Contains("window.Closed -= ScheduleComparisonWindow_Closed", source, StringComparison.Ordinal);
        Assert.Contains("_scheduleComparisonWindow?.Close()", lifecycle, StringComparison.Ordinal);
    }
}
