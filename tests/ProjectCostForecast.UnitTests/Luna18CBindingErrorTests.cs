using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna18CBindingErrorTests
{
    private static readonly object BindingTraceGate = new();

    [Fact]
    public void Binding_capture_is_scoped_and_keeps_the_surface_name_and_binding_path()
    {
        lock (BindingTraceGate)
        {
            var source = PresentationTraceSources.DataBindingSource;
            var originalLevel = source.Switch.Level;
            using var capture = new WpfBindingErrorCapture("synthetic surface");
            capture.Attach();

            source.TraceEvent(
                TraceEventType.Error,
                40,
                "BindingExpression path error: 'MissingPath' property not found.");

            var error = Assert.Single(capture.Errors);
            Assert.Contains("synthetic surface", error, StringComparison.Ordinal);
            Assert.Contains("MissingPath", error, StringComparison.Ordinal);

            capture.Dispose();
            Assert.Equal(originalLevel, source.Switch.Level);
            Assert.DoesNotContain(capture, source.Listeners.Cast<TraceListener>());
        }
    }

    [Fact]
    [Trait("Category", "Wpf")]
    public void Representative_main_window_surfaces_have_no_unexpected_binding_errors()
    {
        lock (BindingTraceGate)
        {
            Luna11TestSupport.RunOnSta(() =>
            {
                var app = new ProjectCostForecast.App.App();
                app.InitializeComponent();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                using var capture = new WpfBindingErrorCapture("main-window smoke");
                capture.Attach();

                var window = new MainWindow
                {
                    Width = 1400,
                    Height = 900,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 0,
                    Top = 0
                };
                app.MainWindow = window;
                window.Show();

                PumpSurface(window, "main forecast");
                SelectWorkspace(window, "Resources");
                PumpSurface(window, "resources");
                SelectWorkspace(window, "CTC Forecast");
                SelectDetailWorkspace(window, 0);
                PumpSurface(window, "ledger");
                SelectWorkspace(window, "Schedule");
                PumpSurface(window, "schedule");
                SelectWorkspace(window, "Monthly Report");
                PumpSurface(window, "monthly report");

                SelectWorkspace(window, "CTC Forecast");
                ViewSavedMonthSurface(window);
                PumpSurface(window, "saved month");
                ((MainWindowViewModel)window.DataContext).CloseSavedMonthView();
                PumpSurface(window, "current month after saved month");

                ((MainWindowViewModel)window.DataContext).IsDirty = false;
                window.Close();
                PumpDispatcher();
                app.Shutdown();
                if (!app.Dispatcher.HasShutdownStarted)
                {
                    app.Dispatcher.InvokeShutdown();
                }

                var errors = FormatErrors(capture.Errors);
                Assert.True(
                    errors.Length == 0,
                    $"Unexpected WPF binding errors:\n{string.Join(Environment.NewLine, errors)}");
            });
        }
    }

    [Fact]
    public void Binding_gate_source_contract_keeps_allow_listing_out_of_application_resources()
    {
        var source = File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Services",
            "WpfBindingErrorCapture.cs"));
        var testSource = File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "tests",
            "ProjectCostForecast.UnitTests",
            "Luna18CBindingErrorTests.cs"));

        Assert.Contains("PresentationTraceSources.DataBindingSource", source, StringComparison.Ordinal);
        Assert.Contains("source.Listeners.Add(this)", source, StringComparison.Ordinal);
        Assert.Contains("source.Listeners.Remove(this)", source, StringComparison.Ordinal);
        Assert.Contains("source.Switch.Level = previousLevel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PresentationTraceSources", File.ReadAllText(Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "App.xaml")), StringComparison.Ordinal);
        Assert.Contains("main forecast", testSource, StringComparison.Ordinal);
        Assert.Contains("ledger", testSource, StringComparison.Ordinal);
        Assert.Contains("schedule", testSource, StringComparison.Ordinal);
        Assert.Contains("monthly report", testSource, StringComparison.Ordinal);
        Assert.Contains("saved month", testSource, StringComparison.Ordinal);
    }

    private static void SelectWorkspace(MainWindow window, string workspaceKey)
    {
        var tab = ((TabControl)window.FindName("WorkspaceTabControl")).Items
            .OfType<TabItem>()
            .Single(item => string.Equals(item.Tag?.ToString(), workspaceKey, StringComparison.OrdinalIgnoreCase));
        tab.IsSelected = true;
    }

    private static void SelectDetailWorkspace(MainWindow window, int index)
    {
        var tabs = (TabControl)window.FindName("LedgerWorkspaceTabControl");
        tabs.SelectedIndex = index;
    }

    private static void ViewSavedMonthSurface(MainWindow window)
    {
        var viewModel = (MainWindowViewModel)window.DataContext;
        var sourceLine = viewModel.ForecastLines.First();
        var snapshot = new SavedMonthSnapshot
        {
            Period = "26-08",
            SavedAt = DateTimeOffset.UtcNow,
            ForecastLines =
            [
                new SavedMonthForecastLine
                {
                    RowNumber = sourceLine.RowNumber,
                    TaskNumber = sourceLine.TaskNumber,
                    ResourceName = sourceLine.ResourceName,
                    ProjectCode = sourceLine.ProjectCode,
                    CostToDate = sourceLine.CostToDate,
                    CurrentPeriodForecast = sourceLine.MonthForecast,
                    CostToComplete = sourceLine.TotalForecastCtc,
                    FinalForecast = sourceLine.PlannedCostFcc,
                    Budget = sourceLine.Budget,
                    TotalBudgetVariance = sourceLine.TotalBudgetVariance,
                    VarianceFromPreviousMonth = sourceLine.VarianceLastMonthToDate,
                    MonthlyForecasts = sourceLine.MonthlyForecasts.Select(month => new SavedMonthPeriodAmount
                    {
                        PeriodLabel = month.PeriodLabel,
                        PeriodStartDate = month.PeriodStartDate,
                        Amount = month.Amount
                    }).ToList()
                }
            ]
        };

        viewModel.ViewSavedMonthSnapshot(snapshot);
    }

    private static void PumpSurface(MainWindow window, string surfaceName)
    {
        window.UpdateLayout();
        PumpDispatcher();
        window.UpdateLayout();
        Assert.True(window.IsVisible, $"The {surfaceName} surface did not remain visible during the smoke path.");
    }

    private static void PumpDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => { }));
    }

    private static string[] FormatErrors(IReadOnlyList<string> errors)
    {
        return errors
            .Select(error => error.Trim())
            .Where(error => error.Length > 0)
            .ToArray();
    }
}
