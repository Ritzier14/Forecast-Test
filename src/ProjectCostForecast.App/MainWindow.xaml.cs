using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow : Window
{
    private static readonly AccountingNoDecimalsConverter AccountingConverter = new();
    private const double DragThreshold = 6;
    private const string HeaderIconTag = "ColumnHeaderIcon";
    private const double ForecastYearBandHeight = 32;
    private const string BlanksFilterText = "(Blanks)";
    private static readonly SolidColorBrush RowHoverCellBrush = BrushFactory.Frozen("#F3F4F6");
    private static readonly SolidColorBrush HighlightedColumnCellBrush = BrushFactory.Frozen("#FEF3C7");
    private static readonly SolidColorBrush HighlightedColumnHeaderBrush = BrushFactory.Frozen("#FDE68A");
    private static readonly SolidColorBrush HoveredHighlightedCellBrush = BrushFactory.Frozen("#FDE68A");
    private const double WorkspaceViewEditorMinimumWidth = 34;
    private const double WorkspaceViewEditorExtraWidth = 14;
    private static readonly IReadOnlyList<ColumnIconOption> ColumnIconOptions =
    [
        new("Text", "T"),
        new("Number", "#"),
        new("Money", "$"),
        new("Percent", "%"),
        new("Month", "M"),
        new("Total", "Σ"),
        new("List", "☷"),
        new("Flag", "⚑"),
        new("Check", "✓"),
        new("Dot", "●")
    ];
    private static readonly IReadOnlyList<ColumnColourOption> ColumnColourOptions =
    [
        new("Default", "#FFFFFF", "#EAF0F8"),
        new("Gray", "#F1F5F9", "#E2E8F0"),
        new("Brown", "#F3E8DC", "#E7D5C4"),
        new("Orange", "#FED7AA", "#FDBA74"),
        new("Yellow", "#FEF3C7", "#FDE68A"),
        new("Green", "#DCFCE7", "#BBF7D0"),
        new("Blue", "#DBEAFE", "#BFDBFE"),
        new("Purple", "#EDE9FE", "#DDD6FE"),
        new("Pink", "#FCE7F3", "#FBCFE8"),
        new("Red", "#FEE2E2", "#FECACA")
    ];
    private readonly Dictionary<DataGrid, GridColumnFilterState> _gridColumnFilters = [];
    private readonly Dictionary<ICollectionView, Predicate<object>?> _baseViewFilters = [];
    private Point? _kpiRightDragStart;
    private double _kpiScrollStartOffset;
    private bool _kpiRightDragging;
    private Point? _ledgerChartRightDragStart;
    private double _ledgerChartScrollStartOffset;
    private bool _ledgerChartRightDragging;
    private Point? _gridRightDragStart;
    private double _gridHorizontalScrollStartOffset;
    private double _gridVerticalScrollStartOffset;
    private bool _gridRightDragging;
    private ScrollViewer? _activeGridScrollViewer;
    private ScrollViewer? _forecastGridScrollViewer;
    private MainWindowViewModel? _subscribedViewModel;
    private Point? _forecastLeftDragStart;
    private ForecastLine? _forecastDragLine;
    private Point? _workspaceTabDragStart;
    private TabItem? _workspaceDraggedTabItem;
    private Point? _workspaceViewDragStart;
    private WorkspaceViewTab? _workspaceDraggedView;
    private UIElement? _dimmedDragElement;
    private GridLength _detailWorkspaceExpandedWidth = new(1.25, GridUnitType.Star);
    private bool _forecastYearBandRebuildQueued;
    private bool _forecastGroupHeaderRefreshQueued;
    private string _forecastOverlayGeometrySignature = string.Empty;
    private bool _forecastOverlaysCleared = true;
    private bool _managementResourceWidthSyncActive;
    private bool _ganttRedrawQueued;
    private bool _scheduleGridColumnsAutoSized;
    private bool _ledgerChartScrollQueued;
    private bool _workspaceViewColumnStateQueued;
    private bool _detailWorkspaceViewColumnStateQueued;
    private System.Diagnostics.Stopwatch? _forecastGridFirstDrawTimer;
    private bool _forecastGridFirstDrawReported;
    private readonly HashSet<DataGrid> _rowHoverAttachedGrids = [];
    private readonly Dictionary<DataGrid, DataGridRow?> _hoveredRowsByGrid = [];
    private readonly HashSet<DataGrid> _selectionVisualRefreshQueued = [];
    private readonly Dictionary<DataGrid, HashSet<object>> _spreadsheetSelectionVisualPendingItems = [];
    private readonly HashSet<DataGrid> _spreadsheetSelectionVisualFullRefresh = [];
    private readonly Dictionary<DataGrid, DataGridCellInfo> _spreadsheetPreviousCurrentCells = [];
    private readonly Dictionary<DataGrid, SpreadsheetEditSnapshot> _spreadsheetEditSnapshots = [];
    private readonly Stack<SpreadsheetUndoBatch> _spreadsheetUndoStack = [];
    private readonly Stack<SpreadsheetUndoBatch> _spreadsheetRedoStack = [];
    private SpreadsheetFillDrag? _spreadsheetFillDrag;
    private readonly HashSet<ForecastGroupHeaderPresenter> _forecastGroupHeaderPresenters = [];
    private readonly Dictionary<int, double> _managementResourceColumnWidths = [];
    private readonly HashSet<DataGridColumn> _trackedManagementResourceColumns = [];
    private readonly Dictionary<string, string?> _workspaceHighlightedColumnKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _detailWorkspaceHighlightedColumnKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<DataGridColumn> _trackedForecastColumns = [];
    private static readonly HashSet<DataGridColumn> AutoSizedColumns = [];
    private static readonly DependencyPropertyDescriptor? ForecastColumnActualWidthDescriptor =
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));

    public MainWindow()
    {
        InitializeComponent();
        InitializeGanttChart();
        DataContext = new MainWindowViewModel();
        Loaded += (_, _) =>
        {
            ApplyWindowPreferences();
            WireViewModelSubscriptions();
            ApplySavedWorkspaceTabOrders();
            RebuildMonthlyPivotColumns();
            ConfigureSelectedMonthlyForecastGrid();
            StartForecastGridFirstDrawMeasure();
            RebuildForecastGridColumns();
            AttachColumnMenus(this);
            ApplyDefaultColumnPresentation(this);
            AttachGridPanHandlers(this);
            AttachSpreadsheetGridHandlers(this);
            AttachForecastGridScrollSync();
            QueueApplyCurrentWorkspaceViewColumnState();
            QueueApplyCurrentDetailWorkspaceViewColumnState();
            QueueScrollLedgerChartToEnd();
            RefreshForecastGridStatePills();
            ForecastGridHost.SizeChanged += ForecastGridHost_SizeChanged;
            QueueReportForecastGridFirstDraw();
        };
        DataContextChanged += (_, _) =>
        {
            ApplyWindowPreferences();
            WireViewModelSubscriptions();
            ApplySavedWorkspaceTabOrders();
            RebuildMonthlyPivotColumns();
            ConfigureSelectedMonthlyForecastGrid();
            RebuildForecastGridColumns();
            ApplyDefaultColumnPresentation(this);
            AttachSpreadsheetGridHandlers(this);
            AttachForecastGridScrollSync();
            RebuildForecastYearBands();
            QueueApplyCurrentWorkspaceViewColumnState();
            QueueApplyCurrentDetailWorkspaceViewColumnState();
            RefreshForecastGridStatePills();
            QueueScrollLedgerChartToEnd();
        };
    }

    private void StartForecastGridFirstDrawMeasure()
    {
        if (_forecastGridFirstDrawReported)
        {
            return;
        }

        _forecastGridFirstDrawTimer = System.Diagnostics.Stopwatch.StartNew();
    }

    private void QueueReportForecastGridFirstDraw()
    {
        if (_forecastGridFirstDrawReported || _forecastGridFirstDrawTimer is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (_forecastGridFirstDrawReported || _forecastGridFirstDrawTimer is null)
            {
                return;
            }

            _forecastGridFirstDrawTimer.Stop();
            _forecastGridFirstDrawReported = true;
            GridPerformanceDiagnostics.Observe(
                "forecast-grid-first-draw",
                _forecastGridFirstDrawTimer.Elapsed,
                $"rows={ForecastLinesGrid.Items.Count:N0} columns={ForecastLinesGrid.Columns.Count:N0}");
        }));
    }

    private void ForecastGridHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        using (GridPerformanceDiagnostics.Measure("forecast-grid-resize-queue"))
        {
            QueueRebuildForecastYearBands();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && !viewModel.ConfirmClose())
        {
            e.Cancel = true;
            return;
        }

        if (DataContext is MainWindowViewModel closingViewModel)
        {
            closingViewModel.SetStartInFullScreen(WindowState == WindowState.Maximized);
        }

        base.OnClosing(e);
    }

    private void WorkspaceTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != sender || DataContext is not MainWindowViewModel viewModel || WorkspaceTabControl.SelectedItem is not TabItem tabItem)
        {
            return;
        }

        var previousWorkspaceKey = viewModel.ActiveWorkspaceKey;
        CaptureCurrentWorkspaceViewColumnState();
        viewModel.ActiveWorkspaceKey = tabItem.Tag?.ToString() ?? tabItem.Header?.ToString() ?? "CTC Forecast";
        HandleHighlightScopeChange(previousWorkspaceKey, viewModel.ActiveWorkspaceKey, isDetailScope: false);
        QueueApplyCurrentWorkspaceViewColumnState();
        QueueAttachInteractiveGridHandlers();
        if (ReferenceEquals(tabItem, ManagementResourcesTab))
        {
            QueueSynchronizeManagementResourceGrids();
        }
    }

    private void ExpandForecastGroups_Click(object sender, RoutedEventArgs e)
    {
        SetForecastGroupExpansion(ForecastLinesGrid, true);
    }

    private void CollapseForecastGroups_Click(object sender, RoutedEventArgs e)
    {
        SetForecastGroupExpansion(ForecastLinesGrid, false);
    }

    private static void SetForecastGroupExpansion(DependencyObject root, bool isExpanded)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Expander expander)
            {
                expander.IsExpanded = isExpanded;
            }

            SetForecastGroupExpansion(child, isExpanded);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsAltKey(e))
        {
            UpdateHoveredForecastLineFromPointer();
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (IsAltKey(e))
        {
            ClearHoveredForecastLine();
        }
    }

    private static bool IsAltKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key is Key.LeftAlt or Key.RightAlt;
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindParent<TextBox>(source)?.DataContext is WorkspaceViewTab)
        {
            return;
        }

        viewModel.EndAllWorkspaceViewRenames();
    }

    private void WorkspaceViewTabControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var listBoxItem = FindParent<ListBoxItem>(source);
        if (listBoxItem?.DataContext is WorkspaceViewTab listView)
        {
            viewModel.BeginRenameWorkspaceView(listView);
            return;
        }

        var tabItem = FindParent<TabItem>(source);
        if (tabItem?.DataContext is WorkspaceViewTab view)
        {
            viewModel.BeginRenameWorkspaceView(view);
        }
    }

    private void LedgerWorkspaceTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != sender || DataContext is not MainWindowViewModel viewModel || LedgerWorkspaceTabControl.SelectedItem is not TabItem tabItem)
        {
            return;
        }

        var previousWorkspaceKey = viewModel.ActiveDetailWorkspaceKey;
        CaptureCurrentDetailWorkspaceViewColumnState();
        viewModel.ActiveDetailWorkspaceKey = GetDetailWorkspaceKeyFromTabHeader(tabItem.Header?.ToString());
        HandleHighlightScopeChange(previousWorkspaceKey, viewModel.ActiveDetailWorkspaceKey, isDetailScope: true);
        QueueApplyCurrentDetailWorkspaceViewColumnState();
        QueueAttachInteractiveGridHandlers();
    }

    private void CurrentPeriodActionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu })
        {
            return;
        }

        menu.PlacementTarget = (Button)sender;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        e.Handled = true;
    }
}
