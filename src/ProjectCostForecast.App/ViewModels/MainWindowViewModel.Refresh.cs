using System.Windows;
using System.Windows.Threading;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static readonly RefreshProjection ViewRefreshProjections =
        RefreshProjection.ForecastLinesView
        | RefreshProjection.RawTransactionsView
        | RefreshProjection.ResourceSummariesView;

    private readonly RefreshCoordinator _refreshCoordinator;

    public RefreshDiagnosticsSnapshot RefreshDiagnostics => _refreshCoordinator.Diagnostics.Snapshot();

    public void FlushPendingRefreshes()
    {
        _refreshCoordinator.FlushNow();
    }

    internal void MeasureRefreshPhase(RefreshPhase phase, Action action)
    {
        _refreshCoordinator.Measure(phase, action);
    }

    private static void ScheduleRefreshWork(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle);
    }

    private void RequestRefresh(RefreshRequest request)
    {
        _refreshCoordinator.Request(request);
    }

    private void ExecuteRefreshRequest(RefreshRequest request)
    {
        var projections = request.Projections;

        if (request.Recalculate)
        {
            _refreshCoordinator.Measure(RefreshPhase.Calculation, () =>
            {
                SyncDatasetFromCollections();
                ApplyClosedForecastPeriodRule();
                _calculationService.Recalculate(_dataset);
            });
        }

        if (projections.HasFlag(RefreshProjection.CalculatedViews))
        {
            RebuildCalculatedViews(request.RebuildFilterLists);
        }

        if (projections.HasFlag(RefreshProjection.ForecastGrouping))
        {
            if (IsForecastEditTransactionActive())
            {
                RequestRefresh(request with
                {
                    Projections = RefreshProjection.ForecastGrouping,
                    Recalculate = false,
                    RebuildFilterLists = false,
                    MarkDirty = false,
                    Reason = string.Empty
                });
            }
            else
            {
                ApplyForecastGrouping();
            }

            projections &= ~RefreshProjection.ForecastGrouping;
        }

        var viewProjections = projections & RefreshProjection.DataViews;
        if (viewProjections != RefreshProjection.None)
        {
            if (IsForecastEditTransactionActive())
            {
                RequestRefresh(request with
                {
                    Projections = viewProjections,
                    Recalculate = false,
                    RebuildFilterLists = false,
                    MarkDirty = false,
                    Reason = string.Empty
                });
            }
            else
            {
                _refreshCoordinator.Measure(RefreshPhase.CollectionViews, () =>
                {
                    if (viewProjections.HasFlag(RefreshProjection.ForecastLinesView))
                    {
                        RefreshView(ForecastLinesView);
                    }

                    if (viewProjections.HasFlag(RefreshProjection.RawTransactionsView))
                    {
                        RefreshView(RawTransactionsView);
                    }

                    if (viewProjections.HasFlag(RefreshProjection.ResourceSummariesView))
                    {
                        RefreshView(ResourceSummariesView);
                    }

                    if (viewProjections.HasFlag(RefreshProjection.RawTransactionsPivot))
                    {
                        RebuildRawTransactionsPivotTable();
                    }
                });
            }
        }

        if (projections.HasFlag(RefreshProjection.Totals))
        {
            _refreshCoordinator.Measure(RefreshPhase.Totals, NotifyTotalsChanged);
        }

        if (projections.HasFlag(RefreshProjection.Ledger))
        {
            _refreshCoordinator.Measure(RefreshPhase.Ledger, NotifyLedgerChanged);
        }

        if (request.MarkDirty)
        {
            IsDirty = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            StatusText = $"{request.Reason}. {ValidationIssueCount} validation issue(s).";
        }
    }
}
