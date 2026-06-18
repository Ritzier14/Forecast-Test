using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public sealed class ScheduleComparisonWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly DataGrid _currentGrid;
    private readonly DataGrid _baselineGrid;
    private readonly TextBlock _summaryText;
    private readonly ComboBox _baselineSelector;
    private readonly IReadOnlyList<string> _baselineNames;
    private CancellationTokenSource? _refreshCancellation;

    public ScheduleComparisonWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        Title = "Programme and baseline comparison";
        Width = 1280;
        Height = 720;
        MinWidth = 900;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushFactory.Frozen("#F4F6F8");

        var root = new DockPanel { Margin = new Thickness(14) };
        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        _summaryText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#475569")
        };
        _baselineNames = _viewModel.ScheduleBaselineNames.ToList();
        _baselineSelector = new ComboBox
        {
            Width = 190,
            Margin = new Thickness(8, 0, 0, 0),
            ItemsSource = _baselineNames,
            SelectedItem = _viewModel.ActiveScheduleBaselineName
        };
        _baselineSelector.SelectionChanged += (_, _) =>
        {
            if (_baselineSelector.SelectedItem is string)
            {
                QueueRefreshRows();
            }
        };
        DockPanel.SetDock(_baselineSelector, Dock.Right);
        toolbar.Children.Add(_baselineSelector);
        toolbar.Children.Add(_summaryText);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var comparison = new Grid();
        comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _currentGrid = BuildGrid("Current programme", isBaseline: false);
        comparison.Children.Add(_currentGrid);
        var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetColumn(splitter, 1);
        comparison.Children.Add(splitter);
        _baselineGrid = BuildGrid("Selected baseline", isBaseline: true);
        Grid.SetColumn(_baselineGrid, 2);
        comparison.Children.Add(_baselineGrid);
        root.Children.Add(comparison);

        Content = root;
        Loaded += (_, _) => QueueRefreshRows();
        Closed += (_, _) => _refreshCancellation?.Cancel();
    }

    private DataGrid BuildGrid(string label, bool isBaseline)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.All,
            AlternatingRowBackground = BrushFactory.Frozen("#F8FAFC"),
            EnableRowVirtualization = true,
            EnableColumnVirtualization = true
        };
        ScrollViewer.SetCanContentScroll(grid, true);
        grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(nameof(ScheduleComparisonRow.Id)), Width = 70 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Activity", Binding = new Binding(nameof(ScheduleComparisonRow.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Start", Binding = new Binding(isBaseline ? nameof(ScheduleComparisonRow.BaselineStartText) : nameof(ScheduleComparisonRow.CurrentStartText)), Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Finish", Binding = new Binding(isBaseline ? nameof(ScheduleComparisonRow.BaselineFinishText) : nameof(ScheduleComparisonRow.CurrentFinishText)), Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = isBaseline ? "Variance" : "Float", Binding = new Binding(isBaseline ? nameof(ScheduleComparisonRow.VarianceText) : nameof(ScheduleComparisonRow.FloatText)), Width = 75 });
        grid.Columns.Add(new DataGridTextColumn { Header = isBaseline ? "Captured" : "Progress", Binding = new Binding(isBaseline ? nameof(ScheduleComparisonRow.BaselineStatus) : nameof(ScheduleComparisonRow.ProgressText)), Width = 80 });
        return grid;
    }

    private void QueueRefreshRows()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation = new CancellationTokenSource();
        var cancellationToken = _refreshCancellation.Token;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () => await RefreshRowsAsync(cancellationToken)));
    }

    private async Task RefreshRowsAsync(CancellationToken cancellationToken)
    {
        using var loadMeasure = GridPerformanceDiagnostics.Measure("schedule-comparison-load");
        try
        {
            var baselineName = _baselineSelector.SelectedItem as string ?? _viewModel.ActiveScheduleBaselineName;
            var baseline = _viewModel.ScheduleDataRef.Baselines.FirstOrDefault(item =>
                string.Equals(item.Name, baselineName, StringComparison.OrdinalIgnoreCase));
            var snapshots = _viewModel.ScheduleActivities.Select(activity =>
            {
                var entry = baseline?.FindEntry(activity.Id);
                var calendar = _viewModel.ScheduleDataRef.ResolveCalendar(activity.CalendarId);
                return new ScheduleComparisonSnapshot(
                    activity.Id,
                    activity.Name,
                    activity.EarlyStart,
                    activity.EarlyFinish,
                    activity.TotalFloatDays,
                    activity.PercentComplete,
                    activity.IsCritical,
                    entry?.Start,
                    entry?.Finish,
                    calendar);
            }).ToList();

            _summaryText.Text = $"Loading comparison for {snapshots.Count:N0} activities...";
            var rows = await Task.Run(() => snapshots.Select(snapshot =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var variance = snapshot.BaselineFinish is { } baselineFinish && snapshot.CurrentFinish is { } currentFinish
                    ? SchedulingService.CountWorkingDaysSigned(snapshot.Calendar, baselineFinish, currentFinish)
                    : (int?)null;
                return new ScheduleComparisonRow(snapshot, variance);
            }).ToList(), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _currentGrid.ItemsSource = rows;
            _baselineGrid.ItemsSource = rows;
            var slipping = rows.Count(row => row.VarianceDays > 0);
            var ahead = rows.Count(row => row.VarianceDays < 0);
            _summaryText.Text = $"Baseline: {baseline?.Name ?? "None"}   Slipping: {slipping}   Ahead: {ahead}   Critical: {snapshots.Count(activity => activity.IsCritical)}";
        }
        catch (OperationCanceledException)
        {
            // A newer baseline selection or a closing window superseded this refresh.
        }
    }

    private sealed record ScheduleComparisonSnapshot(
        string Id,
        string Name,
        DateOnly? CurrentStart,
        DateOnly? CurrentFinish,
        int? TotalFloatDays,
        double PercentComplete,
        bool IsCritical,
        DateOnly? BaselineStart,
        DateOnly? BaselineFinish,
        ScheduleCalendar Calendar);

    private sealed class ScheduleComparisonRow(ScheduleComparisonSnapshot activity, int? varianceDays)
    {
        public string Id => activity.Id;
        public string Name => activity.Name;
        public string CurrentStartText => Format(activity.CurrentStart);
        public string CurrentFinishText => Format(activity.CurrentFinish);
        public string BaselineStartText => Format(activity.BaselineStart);
        public string BaselineFinishText => Format(activity.BaselineFinish);
        public int? VarianceDays => varianceDays;
        public string VarianceText => varianceDays is null ? string.Empty : $"{varianceDays:+0;-0;0}d";
        public string FloatText => activity.TotalFloatDays is { } value ? $"{value}d" : string.Empty;
        public string ProgressText => $"{activity.PercentComplete:0}%";
        public string BaselineStatus => activity.BaselineStart is null && activity.BaselineFinish is null ? "Not captured" : "Captured";
        private static string Format(DateOnly? date) => date?.ToString("d/MM/yyyy") ?? string.Empty;
    }
}
