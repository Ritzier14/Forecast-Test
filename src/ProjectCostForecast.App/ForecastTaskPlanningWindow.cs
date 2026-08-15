using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private void OpenForecastTaskPlanningWindow(ForecastLine line)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var window = new ForecastTaskPlanningWindow(viewModel, line) { Owner = this };
        window.Show();
    }

    private MenuItem CreateOpenForecastTaskPlanningMenuItem(ForecastLine line)
    {
        var item = new MenuItem
        {
            Header = "Open task planning mini-Gantt"
        };
        item.Click += (_, _) => OpenForecastTaskPlanningWindow(line);
        return item;
    }
}

public sealed class ForecastTaskPlanningWindow : Window
{
    private const double TimelineLabelWidth = 174;
    private const double BasePeriodWidth = 88;
    private const double HeaderHeight = 54;
    private const double SectionHeaderHeight = 30;
    private const double TableGap = 12;
    private const double RowHeight = 52;
    private const double BarResizeHitThickness = 14;
    private const double CostLinesGridHeight = 142;
    private const double MinimumTimelineZoom = 0.45;
    private const double MaximumTimelineZoom = 2.5;

    private readonly MainWindowViewModel _viewModel;
    private readonly ForecastLine _line;
    private readonly List<MonthlyForecast> _periods;
    private readonly ObservableCollection<ForecastTaskPhase> _phaseView;
    private readonly ObservableCollection<ForecastTaskCostLine> _costLineView = [];
    private readonly List<ForecastLine> _taskResourceLines;
    private readonly Canvas _timelineCanvas;
    private readonly ScrollViewer _timelineScrollViewer;
    private readonly DataGrid _costLinesGrid;
    private readonly TextBox _newPhaseNameBox;
    private readonly ComboBox _curveProfileBox;
    private readonly ComboBox _curveFromBox;
    private readonly ComboBox _curveToBox;
    private readonly TextBox _curveTotalBox;
    private readonly TextBlock _statusText;
    private readonly Dictionary<ForecastTaskPhase, Border> _phaseBars = [];
    private readonly Dictionary<ForecastTaskCostLine, Border> _costBars = [];
    private readonly Dictionary<ForecastLine, Border> _resourceBars = [];
    private readonly List<FrozenTimelineElement> _frozenTimelineElements = [];

    private ForecastTaskPhase? _selectedPhase;
    private ForecastTaskCostLine? _selectedCostLine;
    private PhaseDragState? _phaseDrag;
    private CostLineDragState? _costLineDrag;
    private CurrentTaskDragState? _currentTaskDrag;
    private Border? _currentTaskBar;
    private int _currentTaskStart;
    private int _currentTaskEnd;
    private double _timelineZoom = 1;
    private Point? _timelinePanStart;
    private double _timelinePanStartHorizontalOffset;
    private double _timelinePanStartVerticalOffset;
    private readonly ForecastTaskCostLine _baseCostLine = new()
    {
        Name = "Forecast costs",
        IsAwarded = true
    };
    private readonly ForecastTaskCostLine _actualCostLine = new()
    {
        Name = "Actual cost to date",
        IsAwarded = true
    };
    private Button? _removePhaseButton;
    private Button? _showAllResourcesButton;
    private bool _showAllResources;
    private bool _rendering;
    private bool _initialTimelinePositionApplied;

    private double PeriodWidth => BasePeriodWidth * _timelineZoom;

    public ForecastTaskPlanningWindow(MainWindowViewModel viewModel, ForecastLine line)
    {
        _viewModel = viewModel;
        _line = line;
        _periods = line.MonthlyForecasts
            .OrderBy(month => FiscalPeriod.SortKey(month.PeriodLabel))
            .ToList();
        _line.TaskPhases ??= [];
        _line.TaskCostLines ??= [];
        EnsureDefaultPhases();
        _phaseView = new ObservableCollection<ForecastTaskPhase>(_line.TaskPhases);
        EnsureDefaultCostLines();
        _taskResourceLines = GetTaskResourceLines();
        SynchronizeRelatedTaskPhases();
        RefreshCostLineView();

        Title = $"Task planning - {DisplayLineName(line)}";
        Width = 1220;
        Height = 720;
        MinWidth = 900;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushFactory.Frozen("#F8FAFC");

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(BuildHeading());

        var controls = new StackPanel { Margin = new Thickness(0, 12, 0, 12) };
        controls.Children.Add(BuildPhaseControls());
        controls.Children.Add(BuildCurveControls());
        Grid.SetRow(controls, 1);
        root.Children.Add(controls);

        _costLinesGrid = BuildCostLinesGrid();

        _timelineCanvas = new Canvas
        {
            Background = Brushes.White,
            ClipToBounds = true
        };
        var timelineBorder = new Border
        {
            BorderBrush = BrushFactory.Frozen("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Background = Brushes.White,
            Child = _timelineScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = _timelineCanvas
            }
        };
        _timelineScrollViewer.PreviewMouseRightButtonDown += TimelineScrollViewer_PreviewMouseRightButtonDown;
        _timelineScrollViewer.PreviewMouseMove += TimelineScrollViewer_PreviewMouseMove;
        _timelineScrollViewer.PreviewMouseRightButtonUp += TimelineScrollViewer_PreviewMouseRightButtonUp;
        _timelineScrollViewer.PreviewMouseWheel += TimelineScrollViewer_PreviewMouseWheel;
        _timelineScrollViewer.ScrollChanged += TimelineScrollViewer_ScrollChanged;
        Grid.SetRow(timelineBorder, 2);
        root.Children.Add(timelineBorder);

        var costLinesPanel = BuildCostLinesPanel(_costLinesGrid);
        Grid.SetRow(costLinesPanel, 3);
        root.Children.Add(costLinesPanel);

        _statusText = new TextBlock
        {
            Foreground = BrushFactory.Frozen("#64748B"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(_statusText, 4);
        root.Children.Add(_statusText);

        Content = root;

        _newPhaseNameBox = FindNamedTextBox(controls, "New activity")
            ?? throw new InvalidOperationException("Task planning controls did not create the phase name box.");
        _curveProfileBox = FindNamedComboBox(controls, "Curve profile")
            ?? throw new InvalidOperationException("Task planning controls did not create the curve profile box.");
        _curveFromBox = FindNamedComboBox(controls, "From period")
            ?? throw new InvalidOperationException("Task planning controls did not create the curve start box.");
        _curveToBox = FindNamedComboBox(controls, "To period")
            ?? throw new InvalidOperationException("Task planning controls did not create the curve end box.");
        _curveTotalBox = FindNamedTextBox(controls, "Curve total")
            ?? throw new InvalidOperationException("Task planning controls did not create the curve total box.");

        SetInitialCurveControls();
        RenderTimeline();
        Loaded += ForecastTaskPlanningWindow_Loaded;
    }

    private FrameworkElement BuildHeading()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = DisplayLineName(_line),
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#0F172A")
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Task {_line.TaskNumber}  •  Cost code {_line.ProjectCode}  •  Drag bars to position the work across periods",
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = BrushFactory.Frozen("#64748B")
        });
        return panel;
    }

    private FrameworkElement BuildPhaseControls()
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = BrushFactory.Frozen("#D8E2EE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10)
        };
        var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new TextBlock
        {
            Text = "Activities",
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#334155"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var newPhaseName = new TextBox
        {
            Width = 170,
            Height = 30,
            Text = "New activity",
            Tag = "New activity",
            Padding = new Thickness(8, 4, 8, 4),
            ToolTip = "Enter an activity name, then add it to the mini-Gantt"
        };
        row.Children.Add(newPhaseName);
        var add = new Button
        {
            Content = "Add activity",
            Height = 30,
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(6, 0, 6, 0)
        };
        add.Click += (_, _) => AddPhase(newPhaseName);
        row.Children.Add(add);

        var remove = new Button
        {
            Content = "Remove selected",
            Height = 30,
            Padding = new Thickness(10, 2, 10, 2),
            IsEnabled = false,
            Tag = "Remove phase"
        };
        _removePhaseButton = remove;
        remove.Click += (_, _) => RemoveSelectedPhase(remove);
        row.Children.Add(remove);

        var showAllResources = new Button
        {
            Content = "Show all resources",
            Height = 30,
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Show the other resource forecast bars for this task"
        };
        _showAllResourcesButton = showAllResources;
        showAllResources.Click += (_, _) => ToggleAllResources();
        row.Children.Add(showAllResources);

        var hint = new TextBlock
        {
            Text = "Click a bar to select it. Drag the body to move it; drag the right edge to extend it.",
            Foreground = BrushFactory.Frozen("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        row.Children.Add(hint);
        border.Child = row;
        return border;
    }

    private FrameworkElement BuildCostLinesPanel(DataGrid grid)
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = BrushFactory.Frozen("#D8E2EE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 8, 0, 0)
        };
        var panel = new DockPanel();
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 7)
        };
        toolbar.Children.Add(new TextBlock
        {
            Text = "Cost lines",
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#334155"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });

        var add = new Button
        {
            Content = "Add cost line",
            Height = 28,
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Add a future variation or other planned cost line"
        };
        add.Click += (_, _) => AddCostLine();
        toolbar.Children.Add(add);

        var remove = new Button
        {
            Content = "Remove cost line",
            Height = 28,
            Padding = new Thickness(10, 2, 10, 2),
            IsEnabled = false,
            Tag = "Remove cost line",
            ToolTip = "Remove the selected variation"
        };
        remove.Click += (_, _) => RemoveSelectedCostLine(remove);
        toolbar.Children.Add(remove);
        toolbar.Children.Add(new TextBlock
        {
            Text = "Actual cost to date is locked to the months incurred. Forecast costs and added variations can be dragged across the cost timeline below.",
            Foreground = BrushFactory.Frozen("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0, 0, 0)
        });
        DockPanel.SetDock(toolbar, Dock.Top);
        panel.Children.Add(toolbar);

        grid.Tag = remove;
        panel.Children.Add(grid);
        border.Child = panel;
        return border;
    }

    private DataGrid BuildCostLinesGrid()
    {
        var grid = new DataGrid
        {
            ItemsSource = _costLineView,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            IsReadOnly = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Height = CostLinesGridHeight,
            RowHeight = 28,
            ColumnHeaderHeight = 30,
            Background = Brushes.White,
            BorderBrush = BrushFactory.Frozen("#D8E2EE"),
            BorderThickness = new Thickness(1),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = BrushFactory.Frozen("#E5EAF1"),
            VerticalGridLinesBrush = BrushFactory.Frozen("#E5EAF1"),
            ColumnHeaderStyle = CreateCostLinesColumnHeaderStyle(),
            CellStyle = CreateCostLinesCellStyle(),
            ToolTip = "Edit a variation in the table. Drag its bar in the activity period planner to move it."
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Item",
            Binding = new Binding(nameof(ForecastTaskCostLine.Name))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 170
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Cost",
            Binding = new Binding(nameof(ForecastTaskCostLine.Amount))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                StringFormat = "{0:N2}"
            },
            Width = 110,
            MinWidth = 90
        });

        var periodLabels = _periods.Select(period => period.PeriodLabel).ToList();
        grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "From",
            ItemsSource = periodLabels,
            SelectedItemBinding = new Binding(nameof(ForecastTaskCostLine.StartPeriodLabel))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = 90,
            MinWidth = 76
        });
        grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "To",
            ItemsSource = periodLabels,
            SelectedItemBinding = new Binding(nameof(ForecastTaskCostLine.EndPeriodLabel))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = 90,
            MinWidth = 76
        });
        grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Awarded",
            Binding = new Binding(nameof(ForecastTaskCostLine.IsAwarded))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            },
            Width = 76,
            MinWidth = 68
        });
        grid.BeginningEdit += CostLinesGrid_BeginningEdit;
        grid.CellEditEnding += CostLinesGrid_CellEditEnding;
        grid.SelectionChanged += CostLinesGrid_SelectionChanged;
        return grid;
    }

    private static Style CreateCostLinesColumnHeaderStyle()
    {
        var style = new Style(typeof(DataGridColumnHeader));
        style.Setters.Add(new Setter(Control.BackgroundProperty, BrushFactory.Frozen("#EAF0F8")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, BrushFactory.Frozen("#0F172A")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12d));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFactory.Frozen("#E2EAF4")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style CreateCostLinesCellStyle()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        style.Setters.Add(new Setter(Control.ForegroundProperty, BrushFactory.Frozen("#0F172A")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFactory.Frozen("#E5EAF1")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 2, 6, 2)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private FrameworkElement BuildCurveControls()
    {
        var border = new Border
        {
            Background = BrushFactory.Frozen("#F1F5F9"),
            BorderBrush = BrushFactory.Frozen("#D8E2EE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 8, 0, 0)
        };
        var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new TextBlock
        {
            Text = "Forecast curve",
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#334155"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        var profile = new ComboBox
        {
            Width = 220,
            Height = 30,
            Tag = "Curve profile",
            ToolTip = "Choose an existing curve profile"
        };
        foreach (var curve in ForecastCurveService.Profiles)
        {
            profile.Items.Add(new ComboBoxItem
            {
                Content = ForecastCurveService.DescribeProfile(curve),
                Tag = curve
            });
        }
        row.Children.Add(profile);

        var total = new TextBox
        {
            Width = 100,
            Height = 30,
            Tag = "Curve total",
            Margin = new Thickness(8, 0, 8, 0),
            Padding = new Thickness(8, 4, 8, 4),
            ToolTip = "Total to distribute over the selected periods"
        };
        row.Children.Add(total);

        row.Children.Add(CreateLabeledCombo("From period", out var from));
        row.Children.Add(CreateLabeledCombo("To period", out var to));

        var apply = new Button
        {
            Content = "Apply curve",
            Height = 30,
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(8, 0, 6, 0)
        };
        apply.Click += (_, _) => ApplyCurve();
        row.Children.Add(apply);

        var fullEditor = new Button
        {
            Content = "Open curve editor",
            Height = 30,
            Padding = new Thickness(10, 2, 10, 2)
        };
        fullEditor.Click += (_, _) => OpenFullCurveEditor();
        row.Children.Add(fullEditor);

        border.Child = row;
        return border;
    }

    private FrameworkElement CreateLabeledCombo(string label, out ComboBox combo)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = BrushFactory.Frozen("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });
        combo = new ComboBox
        {
            Width = 76,
            Height = 30,
            Tag = label
        };
        panel.Children.Add(combo);
        return panel;
    }

    private void SetInitialCurveControls()
    {
        _curveProfileBox.SelectedIndex = 0;
        var labels = _periods.Select(period => period.PeriodLabel).ToList();
        _curveFromBox.ItemsSource = labels;
        _curveToBox.ItemsSource = labels;
        var (start, end) = GetCurrentTaskRange();
        _curveFromBox.SelectedIndex = labels.Count == 0 ? -1 : start;
        _curveToBox.SelectedIndex = labels.Count == 0 ? -1 : end;
        _curveTotalBox.Text = GetRangeTotal(start, end).ToString("0.##", CultureInfo.CurrentCulture);
    }

    private void EnsureDefaultPhases()
    {
        if (_periods.Count == 0)
        {
            return;
        }

        foreach (var phase in _line.TaskPhases)
        {
            var start = GetPeriodIndex(phase.StartPeriodLabel, 0);
            var end = GetPeriodIndex(phase.EndPeriodLabel, start);
            if (end < start)
            {
                (start, end) = (end, start);
            }

            phase.StartPeriodLabel = _periods[start].PeriodLabel;
            phase.EndPeriodLabel = _periods[end].PeriodLabel;
        }

        if (_line.TaskPhases.Count > 0)
        {
            MigrateLegacyDefaultPhaseRangeToCurrentPeriod();
            return;
        }

        var relatedPhaseSource = GetProjectTaskLines()
            .Where(candidate => !ReferenceEquals(candidate, _line))
            .FirstOrDefault(candidate => candidate.TaskPhases is { Count: > 0 });
        if (relatedPhaseSource is not null)
        {
            _line.TaskPhases = relatedPhaseSource.TaskPhases
                .Select(phase => new ForecastTaskPhase
                {
                    Name = phase.Name,
                    StartPeriodLabel = phase.StartPeriodLabel,
                    EndPeriodLabel = phase.EndPeriodLabel
                })
                .ToList();
            MigrateLegacyDefaultPhaseRangeToCurrentPeriod();
            return;
        }

        var first = GetCurrentPeriodIndex();
        var last = _periods.Count - 1;
        AddDefaultPhase("Design", first, Math.Min(first + 1, last));
        AddDefaultPhase("Construction", Math.Min(first + 1, last), Math.Max(Math.Min(first + 1, last), last - 1));
        AddDefaultPhase("Close out", Math.Max(first, last - 1), last);
        _viewModel.IsDirty = true;
    }

    private void MigrateLegacyDefaultPhaseRangeToCurrentPeriod()
    {
        if (_periods.Count == 0
            || _line.TaskPhases.Count != 3
            || !string.Equals(_line.TaskPhases[0].Name, "Design", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(_line.TaskPhases[1].Name, "Construction", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(_line.TaskPhases[2].Name, "Close out", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var last = _periods.Count - 1;
        var legacyDesignEnd = Math.Min(1, last);
        var legacyConstructionStart = Math.Min(1, last);
        var legacyConstructionEnd = Math.Max(legacyConstructionStart, last - 1);
        var legacyCloseOutStart = Math.Max(0, last - 1);
        int FindPhasePeriod(string label) => _periods.FindIndex(period =>
            string.Equals(period.PeriodLabel, label, StringComparison.OrdinalIgnoreCase));
        if (FindPhasePeriod(_line.TaskPhases[0].StartPeriodLabel) != 0
            || FindPhasePeriod(_line.TaskPhases[0].EndPeriodLabel) != legacyDesignEnd
            || FindPhasePeriod(_line.TaskPhases[1].StartPeriodLabel) != legacyConstructionStart
            || FindPhasePeriod(_line.TaskPhases[1].EndPeriodLabel) != legacyConstructionEnd
            || FindPhasePeriod(_line.TaskPhases[2].StartPeriodLabel) != legacyCloseOutStart
            || FindPhasePeriod(_line.TaskPhases[2].EndPeriodLabel) != last)
        {
            return;
        }

        var first = GetCurrentPeriodIndex();
        _line.TaskPhases[0].StartPeriodLabel = _periods[first].PeriodLabel;
        _line.TaskPhases[0].EndPeriodLabel = _periods[Math.Min(first + 1, last)].PeriodLabel;
        _line.TaskPhases[1].StartPeriodLabel = _periods[Math.Min(first + 1, last)].PeriodLabel;
        _line.TaskPhases[1].EndPeriodLabel = _periods[Math.Max(Math.Min(first + 1, last), last - 1)].PeriodLabel;
        _line.TaskPhases[2].StartPeriodLabel = _periods[Math.Max(first, last - 1)].PeriodLabel;
        _line.TaskPhases[2].EndPeriodLabel = _periods[last].PeriodLabel;
        _viewModel.IsDirty = true;
    }

    private void AddDefaultPhase(string name, int start, int end)
    {
        _line.TaskPhases.Add(new ForecastTaskPhase
        {
            Name = name,
            StartPeriodLabel = _periods[Math.Clamp(start, 0, _periods.Count - 1)].PeriodLabel,
            EndPeriodLabel = _periods[Math.Clamp(end, 0, _periods.Count - 1)].PeriodLabel
        });
    }

    private void EnsureDefaultCostLines()
    {
        foreach (var costLine in _line.TaskCostLines)
        {
            NormalizeCostLine(costLine);
        }
    }

    private void RefreshCostLineView()
    {
        var (actualStart, actualEnd) = GetActualCostRange();
        _actualCostLine.StartPeriodLabel = actualStart is null ? string.Empty : actualStart.PeriodLabel;
        _actualCostLine.EndPeriodLabel = actualEnd is null ? string.Empty : actualEnd.PeriodLabel;
        _actualCostLine.Amount = _periods.Sum(period => period.ActualCostAmount);

        var (forecastStart, forecastEnd) = GetCurrentTaskRange();
        _baseCostLine.StartPeriodLabel = _periods.Count == 0 ? string.Empty : _periods[forecastStart].PeriodLabel;
        _baseCostLine.EndPeriodLabel = _periods.Count == 0 ? string.Empty : _periods[forecastEnd].PeriodLabel;
        _baseCostLine.Amount = _periods
            .Skip(GetCurrentPeriodIndex())
            .Sum(period => period.Amount);

        _costLineView.Clear();
        _costLineView.Add(_actualCostLine);
        _costLineView.Add(_baseCostLine);
        foreach (var costLine in _line.TaskCostLines)
        {
            NormalizeCostLine(costLine);
            _costLineView.Add(costLine);
        }
    }

    private List<ForecastLine> GetTaskResourceLines()
    {
        return _viewModel.ForecastLines
            .Where(candidate => string.Equals(candidate.TaskNumber, _line.TaskNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.ProjectCode, _line.ProjectCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.ResourceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<ForecastLine> GetProjectTaskLines()
    {
        return _viewModel.ForecastLines
            .Where(candidate => string.Equals(candidate.ProjectCode, _line.ProjectCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void NormalizeCostLine(ForecastTaskCostLine costLine)
    {
        if (_periods.Count == 0)
        {
            costLine.StartPeriodLabel = string.Empty;
            costLine.EndPeriodLabel = string.Empty;
            return;
        }

        var start = GetPeriodIndex(costLine.StartPeriodLabel, GetCurrentPeriodIndex());
        var end = GetPeriodIndex(costLine.EndPeriodLabel, start);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        costLine.StartPeriodLabel = _periods[start].PeriodLabel;
        costLine.EndPeriodLabel = _periods[end].PeriodLabel;
    }

    private void CostLinesGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is ForecastTaskCostLine { IsAwarded: true } costLine
            && (ReferenceEquals(costLine, _actualCostLine) || ReferenceEquals(costLine, _baseCostLine)))
        {
            e.Cancel = true;
        }
    }

    private void CostLinesGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not ForecastTaskCostLine costLine)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            NormalizeCostLine(costLine);
            if (!ReferenceEquals(costLine, _baseCostLine) && !ReferenceEquals(costLine, _actualCostLine))
            {
                _viewModel.IsDirty = true;
            }

            RenderTimeline();
            _statusText.Text = $"Updated {DisplayCostLineName(costLine)}.";
        }));
    }

    private void CostLinesGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.SelectedItem is not ForecastTaskCostLine costLine)
        {
            return;
        }

        _selectedCostLine = ReferenceEquals(costLine, _baseCostLine) || ReferenceEquals(costLine, _actualCostLine)
            ? null
            : costLine;
        SetCostLineSelectedVisuals();
        if (grid.Tag is Button removeButton)
        {
            removeButton.IsEnabled = _selectedCostLine is not null;
        }
    }

    private void RenderTimeline()
    {
        _rendering = true;
        try
        {
            _phaseBars.Clear();
            _costBars.Clear();
            _resourceBars.Clear();
            _frozenTimelineElements.Clear();
            _timelineCanvas.Children.Clear();
            _currentTaskBar = null;
            RefreshCostLineView();

            if (_periods.Count == 0)
            {
                _timelineCanvas.Width = 640;
                _timelineCanvas.Height = 180;
                AddCanvasText("No forecast periods are available for this task.", 20, 22, 16, FontWeights.SemiBold, "#475569");
                _statusText.Text = "Add forecast periods before using task planning.";
                return;
            }

            var resourceRows = _showAllResources ? _taskResourceLines.Count : 0;
            var activityHeaderY = SectionHeaderHeight;
            var activityRowsY = activityHeaderY + HeaderHeight;
            var costSectionY = activityRowsY + (_phaseView.Count * RowHeight) + TableGap;
            var costHeaderY = costSectionY + SectionHeaderHeight;
            var costRowsY = costHeaderY + HeaderHeight;
            var totalCostRows = _costLineView.Count + resourceRows;
            _timelineCanvas.Width = TimelineLabelWidth + (_periods.Count * PeriodWidth);

            _timelineCanvas.Height = costRowsY + (totalCostRows * RowHeight) + 8;
            RenderTimelineSectionHeader(0, "Activity periods", "Shared timing replicated across tasks");
            RenderTimelineHeader(activityHeaderY, "Activity / period");

            for (var index = 0; index < _phaseView.Count; index++)
            {
                var phase = _phaseView[index];
                var y = activityRowsY + (index * RowHeight);
                RenderPhaseRow(phase, index, y);
            }

            RenderTimelineSectionHeader(costSectionY, "Cost forecast", "Actual cost is locked; forecast and variations can be moved");
            RenderTimelineHeader(costHeaderY, "Cost line / period");
            for (var index = 0; index < _costLineView.Count; index++)
            {
                var costLine = _costLineView[index];
                var y = costRowsY + (index * RowHeight);
                RenderCostLineRow(costLine, index, y);
            }

            if (_showAllResources)
            {
                var resourceRow = _costLineView.Count;
                for (var index = 0; index < _taskResourceLines.Count; index++)
                {
                    var resourceLine = _taskResourceLines[index];
                    var y = costRowsY + ((resourceRow + index) * RowHeight);
                    RenderResourceRow(resourceLine, index, y);
                }
            }

            var currentPeriodIndex = GetPeriodIndex(_viewModel.Header.CurrentPeriod, 0);
            var currentPeriodX = TimelineLabelWidth + currentPeriodIndex * PeriodWidth;
            var currentPeriodLine = new Line
            {
                X1 = currentPeriodX,
                X2 = currentPeriodX,
                Y1 = 0,
                Y2 = _timelineCanvas.Height,
                Stroke = BrushFactory.Frozen("#DC2626"),
                StrokeThickness = 2,
                ToolTip = $"Current period: {_viewModel.Header.CurrentPeriod}"
            };
            Panel.SetZIndex(currentPeriodLine, 100);
            _timelineCanvas.Children.Add(currentPeriodLine);
            AddCanvasText("Current period", Math.Min(currentPeriodX + 4, _timelineCanvas.Width - 115), 7, 10, FontWeights.SemiBold, "#B91C1C");
            UpdateFrozenTimelineElements();
        }
        finally
        {
            _rendering = false;
        }
    }

    private void RenderTimelineSectionHeader(double y, string title, string hint)
    {
        AddTimelineCell(0, y, TimelineLabelWidth, SectionHeaderHeight, "#DBEAFE", title, FontWeights.SemiBold, "#075985");
        for (var index = 0; index < _periods.Count; index++)
        {
            AddTimelineCell(
                TimelineLabelWidth + index * PeriodWidth,
                y,
                PeriodWidth,
                SectionHeaderHeight,
                index % 2 == 0 ? "#EFF6FF" : "#E0F2FE",
                string.Empty,
                FontWeights.Normal,
                "#BFDBFE");
        }

        var hintBlock = new TextBlock
        {
            Text = hint,
            FontSize = 11,
            Foreground = BrushFactory.Frozen("#475569"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0, 8, 0),
            ToolTip = hint
        };
        AddTimelineElement(
            hintBlock,
            TimelineLabelWidth + 4,
            y + 5,
            Math.Max(20, (_periods.Count * PeriodWidth) - 8),
            SectionHeaderHeight - 10);
    }

    private void RenderTimelineHeader(double y, string label)
    {
        AddTimelineCell(0, y, TimelineLabelWidth, HeaderHeight, "#E2E8F0", label, FontWeights.SemiBold, "#334155");
        for (var index = 0; index < _periods.Count; index++)
        {
            var period = _periods[index];
            var (primary, secondary) = GetTimelineHeaderText(index);
            var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = primary,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFactory.Frozen("#334155")
            });
            panel.Children.Add(new TextBlock
            {
                Text = secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 11,
                Foreground = BrushFactory.Frozen("#64748B")
            });
            AddTimelineElement(panel, TimelineLabelWidth + index * PeriodWidth, y, PeriodWidth, HeaderHeight,
                index % 2 == 0 ? "#F8FAFC" : "#EEF4FF", "#D8E2EE");
            if (_timelineCanvas.Children[^1] is FrameworkElement headerCell)
            {
                headerCell.ToolTip = period.PeriodLabel;
            }
        }
    }

    private (string Primary, string Secondary) GetTimelineHeaderText(int index)
    {
        var period = _periods[index];
        if (period.PeriodStartDate is not DateOnly date)
        {
            return (period.PeriodLabel, string.Empty);
        }

        if (_timelineZoom <= 0.58)
        {
            var yearKey = date.Year;
            var previousYear = index > 0 ? _periods[index - 1].PeriodStartDate?.Year : null;
            return (previousYear == yearKey ? string.Empty : yearKey.ToString(CultureInfo.CurrentCulture), string.Empty);
        }

        if (_timelineZoom <= 0.82)
        {
            var quarterKey = (date.Year, (date.Month - 1) / 3);
            var previousQuarter = index > 0 && _periods[index - 1].PeriodStartDate is DateOnly previousDate
                ? (previousDate.Year, (previousDate.Month - 1) / 3)
                : ((int Year, int Quarter)?)null;
            return (previousQuarter == quarterKey ? string.Empty : $"Q{quarterKey.Item2 + 1}", previousQuarter == quarterKey ? string.Empty : date.Year.ToString(CultureInfo.CurrentCulture));
        }

        return (
            date.ToString("MMM", CultureInfo.CurrentCulture),
            period.PeriodLabel);
    }

    private void TimelineScrollViewer_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        _timelinePanStart = e.GetPosition(viewer);
        _timelinePanStartHorizontalOffset = viewer.HorizontalOffset;
        _timelinePanStartVerticalOffset = viewer.VerticalOffset;
        viewer.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void ForecastTaskPlanningWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialTimelinePositionApplied)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PositionCurrentPeriodAtOneThird));
    }

    private void PositionCurrentPeriodAtOneThird()
    {
        if (_initialTimelinePositionApplied || _periods.Count == 0)
        {
            return;
        }

        _timelineScrollViewer.UpdateLayout();
        var viewportWidth = _timelineScrollViewer.ViewportWidth;
        if (!double.IsFinite(viewportWidth) || viewportWidth <= TimelineLabelWidth)
        {
            viewportWidth = _timelineScrollViewer.ActualWidth;
        }

        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(PositionCurrentPeriodAtOneThird));
            return;
        }

        var currentPeriodCenter = TimelineLabelWidth + ((GetCurrentPeriodIndex() + 0.5) * PeriodWidth);
        var targetViewportX = Math.Max(
            TimelineLabelWidth + (PeriodWidth / 2d),
            viewportWidth / 3d);
        var targetOffset = Math.Clamp(
            currentPeriodCenter - targetViewportX,
            0,
            Math.Max(0, _timelineScrollViewer.ScrollableWidth));
        _timelineScrollViewer.ScrollToHorizontalOffset(targetOffset);
        _timelineScrollViewer.ScrollToVerticalOffset(0);
        _initialTimelinePositionApplied = true;
        UpdateFrozenTimelineElements();
    }

    private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.HorizontalChange) > 0.01 || Math.Abs(e.ExtentWidthChange) > 0.01)
        {
            UpdateFrozenTimelineElements();
        }
    }

    private void TimelineScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ScrollViewer viewer || _timelinePanStart is not Point start)
        {
            return;
        }

        if (e.RightButton != MouseButtonState.Pressed)
        {
            EndTimelinePan(viewer);
            return;
        }

        var current = e.GetPosition(viewer);
        viewer.ScrollToHorizontalOffset(_timelinePanStartHorizontalOffset - (current.X - start.X));
        viewer.ScrollToVerticalOffset(_timelinePanStartVerticalOffset - (current.Y - start.Y));
        e.Handled = true;
    }

    private void TimelineScrollViewer_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            EndTimelinePan(viewer);
            e.Handled = true;
        }
    }

    private void EndTimelinePan(ScrollViewer viewer)
    {
        _timelinePanStart = null;
        if (ReferenceEquals(Mouse.Captured, viewer))
        {
            viewer.ReleaseMouseCapture();
        }

        Mouse.OverrideCursor = null;
    }

    private void TimelineScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer
            || _periods.Count == 0)
        {
            return;
        }

        var oldPeriodWidth = PeriodWidth;
        var pointer = e.GetPosition(viewer);
        var timelineContentX = Math.Max(
            0,
            viewer.HorizontalOffset + pointer.X - TimelineLabelWidth);
        var nextZoom = Math.Clamp(
            _timelineZoom + (e.Delta > 0 ? 0.1 : -0.1),
            MinimumTimelineZoom,
            MaximumTimelineZoom);
        if (Math.Abs(nextZoom - _timelineZoom) < 0.001)
        {
            e.Handled = true;
            return;
        }

        _timelineZoom = nextZoom;
        RenderTimeline();
        viewer.UpdateLayout();
        var scale = PeriodWidth / oldPeriodWidth;
        viewer.ScrollToHorizontalOffset(Math.Max(
            0,
            TimelineLabelWidth + (timelineContentX * scale) - pointer.X));
        UpdateFrozenTimelineElements();
        e.Handled = true;
    }

    private void RenderPhaseRow(ForecastTaskPhase phase, int index, double y)
    {
        AddTimelineCell(0, y, TimelineLabelWidth, RowHeight, index % 2 == 0 ? "#FFFFFF" : "#F8FAFC", "", FontWeights.Normal, "#334155");
        var nameBox = new TextBox
        {
            Text = phase.Name,
            Width = TimelineLabelWidth - 12,
            Height = 30,
            Padding = new Thickness(7, 4, 7, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Tag = phase,
            ToolTip = "Edit activity name"
        };
        nameBox.TextChanged += (_, _) =>
        {
            if (!_rendering)
            {
                phase.Name = nameBox.Text;
                SynchronizeRelatedTaskPhases();
                _viewModel.IsDirty = true;
            }
        };
        Canvas.SetLeft(nameBox, 6);
        Canvas.SetTop(nameBox, y + 11);
        _timelineCanvas.Children.Add(nameBox);
        TrackFrozenTimelineElement(nameBox, 6);

        for (var periodIndex = 0; periodIndex < _periods.Count; periodIndex++)
        {
            AddTimelineCell(
                TimelineLabelWidth + periodIndex * PeriodWidth,
                y,
                PeriodWidth,
                RowHeight,
                periodIndex % 2 == 0 ? "#FFFFFF" : "#F8FAFC",
                "",
                FontWeights.Normal,
                "#D8E2EE");
        }

        var start = GetPeriodIndex(phase.StartPeriodLabel, 0);
        var end = GetPeriodIndex(phase.EndPeriodLabel, start);
        if (end < start)
        {
            (start, end) = (end, start);
        }
        phase.StartPeriodLabel = _periods[start].PeriodLabel;
        phase.EndPeriodLabel = _periods[end].PeriodLabel;

        var bar = new Border
        {
            Height = 30,
            Background = GetPhaseBrush(index),
            BorderBrush = BrushFactory.Frozen("#FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.SizeAll,
            Tag = phase,
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(phase.Name) ? "Activity" : phase.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            }
        };
        _phaseBars[phase] = bar;
        UpdatePhaseBarVisual(phase, bar, start, end, y + 11);
        bar.MouseLeftButtonDown += PhaseBar_MouseLeftButtonDown;
        bar.MouseMove += PhaseBar_MouseMove;
        bar.MouseLeftButtonUp += PhaseBar_MouseLeftButtonUp;
        _timelineCanvas.Children.Add(bar);
        SetPhaseSelectedVisual(phase, bar);
    }

    private void RenderCostLineRow(ForecastTaskCostLine costLine, int index, double y)
    {
        if (ReferenceEquals(costLine, _actualCostLine))
        {
            RenderActualCostRow(y);
            return;
        }

        if (ReferenceEquals(costLine, _baseCostLine))
        {
            RenderCurrentTaskRow(y);
            return;
        }

        var rowBackground = index % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
        AddTimelineCell(0, y, TimelineLabelWidth, RowHeight, rowBackground, string.Empty, FontWeights.Normal, "#334155");
        AddTimelinePeriodCells(y, rowBackground, "#D8E2EE");

        AddCanvasText(
            string.IsNullOrWhiteSpace(costLine.Name) ? "Future variation" : costLine.Name,
            8,
            y + 17,
            12,
            FontWeights.SemiBold,
            "#334155");

        var start = GetPeriodIndex(costLine.StartPeriodLabel, GetCurrentPeriodIndex());
        var end = GetPeriodIndex(costLine.EndPeriodLabel, start);
        if (end < start)
        {
            (start, end) = (end, start);
        }
        costLine.StartPeriodLabel = _periods[start].PeriodLabel;
        costLine.EndPeriodLabel = _periods[end].PeriodLabel;

        var bar = new Border
        {
            Height = 30,
            Background = GetCostLineBrush(index),
            BorderBrush = BrushFactory.Frozen("#FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.SizeAll,
            Tag = costLine,
            ToolTip = $"{DisplayCostLineName(costLine)} — drag to move; drag the right edge to extend it",
            Child = new TextBlock
            {
                Text = GetCostBarLabel(costLine),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            }
        };
        _costBars[costLine] = bar;
        UpdateCostLineBarVisual(costLine, bar, start, end, y + 11);
        bar.MouseLeftButtonDown += CostLineBar_MouseLeftButtonDown;
        bar.MouseMove += CostLineBar_MouseMove;
        bar.MouseLeftButtonUp += CostLineBar_MouseLeftButtonUp;
        bar.MouseLeave += CostLineBar_MouseLeave;
        _timelineCanvas.Children.Add(bar);
        SetCostLineSelectedVisual(costLine, bar);
    }

    private void RenderActualCostRow(double y)
    {
        AddTimelineCell(0, y, TimelineLabelWidth, RowHeight, "#F1F5F9", string.Empty, FontWeights.SemiBold, "#475569");
        AddTimelinePeriodCells(y, "#F8FAFC", "#CBD5E1");
        AddCanvasText("Actual cost to date", 8, y + 17, 12, FontWeights.SemiBold, "#475569");

        var (start, end) = GetActualCostRange();
        if (start is null || end is null)
        {
            return;
        }

        var startIndex = _periods.IndexOf(start);
        var endIndex = _periods.IndexOf(end);
        if (startIndex < 0 || endIndex < startIndex)
        {
            return;
        }

        var bar = new Border
        {
            Height = 30,
            Background = BrushFactory.Frozen("#64748B"),
            BorderBrush = BrushFactory.Frozen("#334155"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Arrow,
            ToolTip = $"{GetCostBarLabel(_actualCostLine)} — locked to actual cost months",
            Child = new TextBlock
            {
                Text = GetCostBarLabel(_actualCostLine),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            }
        };
        Canvas.SetLeft(bar, TimelineLabelWidth + startIndex * PeriodWidth + 4);
        Canvas.SetTop(bar, y + 11);
        bar.Width = Math.Max(PeriodWidth - 8, (endIndex - startIndex + 1) * PeriodWidth - 8);
        _timelineCanvas.Children.Add(bar);
    }

    private void RenderResourceRow(ForecastLine resourceLine, int index, double y)
    {
        var rowBackground = index % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
        AddTimelineCell(0, y, TimelineLabelWidth, RowHeight, rowBackground, string.Empty, FontWeights.Normal, "#475569");
        AddTimelinePeriodCells(y, rowBackground, "#E2E8F0");
        AddCanvasText(
            $"Resource: {DisplayLineName(resourceLine)}",
            8,
            y + 17,
            11,
            FontWeights.Normal,
            "#64748B");

        var (start, end) = GetForecastRange(resourceLine);
        var bar = new Border
        {
            Height = 22,
            Background = BrushFactory.Frozen("#94A3B8"),
            BorderBrush = BrushFactory.Frozen("#64748B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            ToolTip = $"{DisplayLineName(resourceLine)} forecast — read-only overview",
            Child = new TextBlock
            {
                Text = resourceLine.MonthlyForecasts.Sum(period => period.Amount).ToString("N0", CultureInfo.CurrentCulture),
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        _resourceBars[resourceLine] = bar;
        Canvas.SetLeft(bar, TimelineLabelWidth + start * PeriodWidth + 4);
        Canvas.SetTop(bar, y + 15);
        bar.Width = Math.Max(PeriodWidth - 8, (end - start + 1) * PeriodWidth - 8);
        _timelineCanvas.Children.Add(bar);
    }

    private void AddTimelinePeriodCells(double y, string background, string border)
    {
        for (var periodIndex = 0; periodIndex < _periods.Count; periodIndex++)
        {
            AddTimelineCell(
                TimelineLabelWidth + periodIndex * PeriodWidth,
                y,
                PeriodWidth,
                RowHeight,
                background,
                string.Empty,
                FontWeights.Normal,
                border);
        }
    }

    private void RenderCurrentTaskRow(double y)
    {
        AddTimelineCell(0, y, TimelineLabelWidth, RowHeight, "#E0F2FE", "Forecast costs", FontWeights.SemiBold, "#075985");
        for (var periodIndex = 0; periodIndex < _periods.Count; periodIndex++)
        {
            AddTimelineCell(
                TimelineLabelWidth + periodIndex * PeriodWidth,
                y,
                PeriodWidth,
                RowHeight,
                periodIndex % 2 == 0 ? "#F0F9FF" : "#E0F2FE",
                "",
                FontWeights.Normal,
                "#BAE6FD");
        }

        (_currentTaskStart, _currentTaskEnd) = GetCurrentTaskRange();
        _currentTaskBar = new Border
        {
            Height = 30,
            Background = BrushFactory.Frozen("#0284C7"),
            BorderBrush = BrushFactory.Frozen("#075985"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.SizeAll,
            ToolTip = $"Drag to move the base cost forecast window ({_baseCostLine.Amount:N0})",
            Child = new TextBlock
            {
                Text = GetCostBarLabel(_baseCostLine),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        UpdateCurrentTaskBarVisual(y + 11);
        _currentTaskBar.MouseLeftButtonDown += CurrentTaskBar_MouseLeftButtonDown;
        _currentTaskBar.MouseMove += CurrentTaskBar_MouseMove;
        _currentTaskBar.MouseLeftButtonUp += CurrentTaskBar_MouseLeftButtonUp;
        _timelineCanvas.Children.Add(_currentTaskBar);
    }

    private void AddPhase(TextBox input)
    {
        var name = input.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || _periods.Count == 0)
        {
            _statusText.Text = "Enter an activity name before adding it.";
            return;
        }

        var start = GetPeriodIndex(_viewModel.Header.CurrentPeriod, 0);
        var phase = new ForecastTaskPhase
        {
            Name = name,
            StartPeriodLabel = _periods[start].PeriodLabel,
            EndPeriodLabel = _periods[Math.Min(_periods.Count - 1, start + 1)].PeriodLabel
        };
        _line.TaskPhases.Add(phase);
        _phaseView.Add(phase);
        SynchronizeRelatedTaskPhases();
        _viewModel.IsDirty = true;
        input.Text = "New activity";
        _statusText.Text = $"Added activity {name}.";
        RenderTimeline();
    }

    private void RemoveSelectedPhase(Button button)
    {
        if (_selectedPhase is null)
        {
            return;
        }

        var removedName = _selectedPhase.Name;
        _line.TaskPhases.Remove(_selectedPhase);
        _phaseView.Remove(_selectedPhase);
        _selectedPhase = null;
        button.IsEnabled = false;
        SynchronizeRelatedTaskPhases();
        _viewModel.IsDirty = true;
        _statusText.Text = $"Removed activity {removedName}.";
        RenderTimeline();
    }

    private void AddCostLine()
    {
        if (_periods.Count == 0)
        {
            _statusText.Text = "Add forecast periods before adding a cost line.";
            return;
        }

        var start = GetCurrentPeriodIndex();
        var variation = new ForecastTaskCostLine
        {
            Name = "Future variation",
            Amount = 0,
            StartPeriodLabel = _periods[start].PeriodLabel,
            EndPeriodLabel = _periods[Math.Min(_periods.Count - 1, start + 1)].PeriodLabel,
            IsAwarded = false
        };
        _line.TaskCostLines.Add(variation);
        _costLineView.Add(variation);
        _costLinesGrid.SelectedItem = variation;
        _viewModel.IsDirty = true;
        _statusText.Text = "Added a future variation cost line. Enter its cost in the table.";
        RenderTimeline();
    }

    private void RemoveSelectedCostLine(Button button)
    {
        if (_selectedCostLine is null)
        {
            return;
        }

        var name = DisplayCostLineName(_selectedCostLine);
        _line.TaskCostLines.Remove(_selectedCostLine);
        _costLineView.Remove(_selectedCostLine);
        _selectedCostLine = null;
        button.IsEnabled = false;
        _viewModel.IsDirty = true;
        _statusText.Text = $"Removed {name}.";
        RenderTimeline();
    }

    private void ToggleAllResources()
    {
        _showAllResources = !_showAllResources;
        if (_showAllResourcesButton is not null)
        {
            _showAllResourcesButton.Content = _showAllResources ? "Hide other resources" : "Show all resources";
            _showAllResourcesButton.ToolTip = _showAllResources
                ? "Hide the other resource forecast bars for this task"
                : "Show the other resource forecast bars for this task";
        }

        _statusText.Text = _showAllResources
            ? $"Showing {_taskResourceLines.Count:N0} resource forecast bar(s) for task {_line.TaskNumber}."
            : "Showing the selected resource forecast.";
        RenderTimeline();
    }

    private void SynchronizeRelatedTaskPhases()
    {
        var relatedLines = GetProjectTaskLines();
        var phases = _phaseView
            .Select(phase => new ForecastTaskPhase
            {
                Name = phase.Name,
                StartPeriodLabel = phase.StartPeriodLabel,
                EndPeriodLabel = phase.EndPeriodLabel
            })
            .ToList();

        foreach (var relatedLine in relatedLines.Where(candidate => !ReferenceEquals(candidate, _line)))
        {
            if (relatedLine.TaskPhases.Count == phases.Count
                && relatedLine.TaskPhases.Zip(phases).All(pair =>
                    string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal)
                    && string.Equals(pair.First.StartPeriodLabel, pair.Second.StartPeriodLabel, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pair.First.EndPeriodLabel, pair.Second.EndPeriodLabel, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            relatedLine.TaskPhases = phases
                .Select(phase => new ForecastTaskPhase
                {
                    Name = phase.Name,
                    StartPeriodLabel = phase.StartPeriodLabel,
                    EndPeriodLabel = phase.EndPeriodLabel
                })
                .ToList();
            _viewModel.IsDirty = true;
        }
    }

    private void PhaseBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: ForecastTaskPhase phase } bar)
        {
            return;
        }

        SelectPhase(phase);
        var point = e.GetPosition(bar);
        var start = GetPeriodIndex(phase.StartPeriodLabel, 0);
        var end = GetPeriodIndex(phase.EndPeriodLabel, start);
        _phaseDrag = new PhaseDragState(
            phase,
            start,
            end,
            e.GetPosition(_timelineCanvas),
            point.X >= Math.Max(0, bar.ActualWidth - BarResizeHitThickness));
        SetPlannerBarCursor(bar, _phaseDrag.ResizeEnd);
        bar.CaptureMouse();
        e.Handled = true;
    }

    private void PhaseBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Border bar)
        {
            return;
        }

        if (_phaseDrag is null)
        {
            UpdatePlannerBarCursor(bar, e.GetPosition(bar));
            return;
        }

        SetPlannerBarCursor(bar, _phaseDrag.ResizeEnd);
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = GetPeriodDelta(_phaseDrag.StartPoint, e.GetPosition(_timelineCanvas));
        var span = _phaseDrag.EndIndex - _phaseDrag.StartIndex;
        int start;
        int end;
        if (_phaseDrag.ResizeEnd)
        {
            start = _phaseDrag.StartIndex;
            end = Math.Clamp(_phaseDrag.EndIndex + delta, start, _periods.Count - 1);
        }
        else
        {
            start = Math.Clamp(_phaseDrag.StartIndex + delta, 0, Math.Max(0, _periods.Count - 1 - span));
            end = start + span;
        }

        _phaseDrag.Phase.StartPeriodLabel = _periods[start].PeriodLabel;
        _phaseDrag.Phase.EndPeriodLabel = _periods[end].PeriodLabel;
        UpdatePhaseBarVisual(_phaseDrag.Phase, bar, start, end, Canvas.GetTop(bar));
        e.Handled = true;
    }

    private void PhaseBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_phaseDrag is null || sender is not Border bar)
        {
            return;
        }

        SynchronizeRelatedTaskPhases();
        bar.ReleaseMouseCapture();
        _phaseDrag = null;
        ClearPlannerBarCursor();
        bar.Cursor = Cursors.SizeAll;
        _viewModel.IsDirty = true;
        _statusText.Text = "Activity timing updated.";
        e.Handled = true;
    }

    private void CostLineBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: ForecastTaskCostLine costLine } bar)
        {
            return;
        }

        SelectCostLine(costLine);
        var point = e.GetPosition(bar);
        var start = GetPeriodIndex(costLine.StartPeriodLabel, GetCurrentPeriodIndex());
        var end = GetPeriodIndex(costLine.EndPeriodLabel, start);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        _costLineDrag = new CostLineDragState(
            costLine,
            start,
            end,
            e.GetPosition(_timelineCanvas),
            point.X >= Math.Max(0, bar.ActualWidth - BarResizeHitThickness));
        SetPlannerBarCursor(bar, _costLineDrag.ResizeEnd);
        bar.CaptureMouse();
        e.Handled = true;
    }

    private void CostLineBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Border { Tag: ForecastTaskCostLine costLine } bar)
        {
            return;
        }

        if (_costLineDrag is null)
        {
            UpdatePlannerBarCursor(bar, e.GetPosition(bar));
            return;
        }

        SetPlannerBarCursor(bar, _costLineDrag.ResizeEnd);
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = GetPeriodDelta(_costLineDrag.StartPoint, e.GetPosition(_timelineCanvas));
        var span = _costLineDrag.EndIndex - _costLineDrag.StartIndex;
        var start = _costLineDrag.ResizeEnd
            ? _costLineDrag.StartIndex
            : Math.Clamp(_costLineDrag.StartIndex + delta, 0, Math.Max(0, _periods.Count - 1 - span));
        var end = _costLineDrag.ResizeEnd
            ? Math.Clamp(_costLineDrag.EndIndex + delta, start, _periods.Count - 1)
            : start + span;
        costLine.StartPeriodLabel = _periods[start].PeriodLabel;
        costLine.EndPeriodLabel = _periods[end].PeriodLabel;
        UpdateCostLineBarVisual(costLine, bar, start, end, Canvas.GetTop(bar));
        e.Handled = true;
    }

    private void CostLineBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_costLineDrag is null || sender is not Border { Tag: ForecastTaskCostLine costLine } bar)
        {
            return;
        }

        bar.ReleaseMouseCapture();
        _costLineDrag = null;
        ClearPlannerBarCursor();
        bar.Cursor = Cursors.SizeAll;
        NormalizeCostLine(costLine);
        _viewModel.IsDirty = true;
        _statusText.Text = $"Moved {DisplayCostLineName(costLine)} to {costLine.StartPeriodLabel}–{costLine.EndPeriodLabel}.";
        RenderTimeline();
        e.Handled = true;
    }

    private void CostLineBar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_costLineDrag is null && sender is Border bar)
        {
            bar.Cursor = Cursors.SizeAll;
        }
    }

    private void CurrentTaskBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentTaskBar is null)
        {
            return;
        }

        _currentTaskDrag = new CurrentTaskDragState(_currentTaskStart, _currentTaskEnd, e.GetPosition(_timelineCanvas));
        _currentTaskBar.CaptureMouse();
        e.Handled = true;
    }

    private void CurrentTaskBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_currentTaskDrag is null || _currentTaskBar is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = GetPeriodDelta(_currentTaskDrag.StartPoint, e.GetPosition(_timelineCanvas));
        var span = _currentTaskDrag.EndIndex - _currentTaskDrag.StartIndex;
        _currentTaskStart = Math.Clamp(_currentTaskDrag.StartIndex + delta, 0, Math.Max(0, _periods.Count - 1 - span));
        _currentTaskEnd = _currentTaskStart + span;
        UpdateCurrentTaskBarVisual(Canvas.GetTop(_currentTaskBar));
        e.Handled = true;
    }

    private void CurrentTaskBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentTaskDrag is null || _currentTaskBar is null)
        {
            return;
        }

        _currentTaskBar.ReleaseMouseCapture();
        var drag = _currentTaskDrag;
        _currentTaskDrag = null;
        if (_currentTaskStart != drag.StartIndex)
        {
            ApplyCurrentTaskMove(drag.StartIndex, drag.EndIndex, _currentTaskStart, _currentTaskEnd);
        }
        else
        {
            RenderTimeline();
        }

        e.Handled = true;
    }

    private void ApplyCurrentTaskMove(int sourceStart, int sourceEnd, int targetStart, int targetEnd)
    {
        if (_viewModel.IsViewingSavedMonth)
        {
            _statusText.Text = "Saved months are read-only. Return to the working period before moving the current task.";
            RenderTimeline();
            return;
        }

        var source = _periods.Skip(sourceStart).Take(sourceEnd - sourceStart + 1).ToList();
        var target = _periods.Skip(targetStart).Take(targetEnd - targetStart + 1).ToList();
        if (source.Count != target.Count || source.Any(month => month.IsLocked) || target.Any(month => month.IsLocked))
        {
            _statusText.Text = "The current task can only move across unlocked forecast periods.";
            RenderTimeline();
            return;
        }

        var values = source.Select(month => month.Amount).ToList();
        _viewModel.BeginSpreadsheetEditBatch();
        for (var index = 0; index < source.Count; index++)
        {
            source[index].Amount = 0;
        }

        for (var index = 0; index < target.Count; index++)
        {
            target[index].Amount = values[index];
        }

        _line.NotifyMonthForecastValuesChanged();
        _viewModel.RecalculateForecastLinesForSpreadsheetEdit([_line]);
        _viewModel.EndSpreadsheetEditBatch("Moved current task forecast window", changed: true, rebuildFilterLists: false);
        _statusText.Text = "Current task moved and the forecast values were shifted with it.";
        SetInitialCurveControls();
        RenderTimeline();
    }

    private void ApplyCurve()
    {
        if (_curveProfileBox.SelectedItem is not ComboBoxItem { Tag: ForecastCurveProfile profile }
            || !decimal.TryParse(_curveTotalBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var total))
        {
            _statusText.Text = "Enter a valid curve total and select a curve profile.";
            return;
        }

        var from = _curveFromBox.SelectedItem as string;
        var to = _curveToBox.SelectedItem as string;
        if (_viewModel.ApplyForecastCurve(_line, profile, total, from, to))
        {
            _statusText.Text = $"Applied {ForecastCurveService.DescribeProfile(profile)}.";
            SetInitialCurveControls();
            RenderTimeline();
        }
        else
        {
            _statusText.Text = _viewModel.StatusText;
        }
    }

    private void OpenFullCurveEditor()
    {
        var points = _periods.Select(period => new ForecastCurvePoint(
            period.PeriodLabel,
            period.PeriodStartDate?.ToString("MMM", CultureInfo.CurrentCulture) ?? period.PeriodLabel,
            FiscalPeriod.FiscalYearFromPeriodLabel(period.PeriodLabel),
            period.Amount,
            period.IsLocked)).ToList();
        var window = new ForecastCurveWindow(_line.ResourceName, _line.ProjectCode, points, _viewModel)
        {
            Owner = this
        };
        if (window.ShowDialog() != true)
        {
            return;
        }

        _viewModel.BeginSpreadsheetEditBatch();
        for (var index = 0; index < _periods.Count && index < window.Values.Count; index++)
        {
            if (!_periods[index].IsLocked)
            {
                _periods[index].Amount = window.Values[index];
            }
        }

        _line.NotifyMonthForecastValuesChanged();
        _viewModel.RecalculateForecastLinesForSpreadsheetEdit([_line]);
        _viewModel.EndSpreadsheetEditBatch("Updated forecast from task planning curve editor", changed: true, rebuildFilterLists: false);
        _statusText.Text = "Forecast curve updated.";
        SetInitialCurveControls();
        RenderTimeline();
    }

    private void SelectPhase(ForecastTaskPhase phase)
    {
        _selectedPhase = phase;
        foreach (var pair in _phaseBars)
        {
            SetPhaseSelectedVisual(pair.Key, pair.Value);
        }

        var removeButton = FindButtonByTag(Content as DependencyObject, "Remove phase");
        if (removeButton is not null)
        {
            removeButton.IsEnabled = true;
        }
    }

    private void SetPhaseSelectedVisual(ForecastTaskPhase phase, Border bar)
    {
        bar.BorderThickness = new Thickness(ReferenceEquals(phase, _selectedPhase) ? 2.5 : 1);
        bar.BorderBrush = ReferenceEquals(phase, _selectedPhase)
            ? BrushFactory.Frozen("#0F172A")
            : Brushes.White;
    }

    private void SelectCostLine(ForecastTaskCostLine costLine)
    {
        _selectedCostLine = costLine;
        if (!_costLinesGrid.SelectedItems.Contains(costLine))
        {
            _costLinesGrid.SelectedItem = costLine;
        }

        SetCostLineSelectedVisuals();
        if (_costLinesGrid.Tag is Button removeButton)
        {
            removeButton.IsEnabled = true;
        }
    }

    private void SetCostLineSelectedVisuals()
    {
        foreach (var pair in _costBars)
        {
            SetCostLineSelectedVisual(pair.Key, pair.Value);
        }
    }

    private void SetCostLineSelectedVisual(ForecastTaskCostLine costLine, Border bar)
    {
        bar.BorderThickness = new Thickness(ReferenceEquals(costLine, _selectedCostLine) ? 2.5 : 1);
        bar.BorderBrush = ReferenceEquals(costLine, _selectedCostLine)
            ? BrushFactory.Frozen("#0F172A")
            : Brushes.White;
    }

    private void UpdatePhaseBarVisual(ForecastTaskPhase phase, Border bar, int start, int end, double barTop)
    {
        Canvas.SetLeft(bar, TimelineLabelWidth + start * PeriodWidth + 4);
        Canvas.SetTop(bar, barTop);
        bar.Width = Math.Max(PeriodWidth - 8, (end - start + 1) * PeriodWidth - 8);
        if (bar.Child is TextBlock label)
        {
            label.Text = string.IsNullOrWhiteSpace(phase.Name) ? "Activity" : phase.Name;
        }
    }

    private void UpdateCostLineBarVisual(
        ForecastTaskCostLine costLine,
        Border bar,
        int start,
        int end,
        double barTop)
    {
        Canvas.SetLeft(bar, TimelineLabelWidth + start * PeriodWidth + 4);
        Canvas.SetTop(bar, barTop);
        bar.Width = Math.Max(PeriodWidth - 8, (end - start + 1) * PeriodWidth - 8);
        if (bar.Child is TextBlock label)
        {
            label.Text = GetCostBarLabel(costLine);
        }
    }

    private void UpdatePlannerBarCursor(Border bar, Point point)
    {
        var resizeEnd = point.X >= Math.Max(0, bar.ActualWidth - BarResizeHitThickness);
        bar.Cursor = resizeEnd ? Cursors.SizeWE : Cursors.SizeAll;
    }

    private static void SetPlannerBarCursor(Border bar, bool resizeEnd)
    {
        bar.Cursor = resizeEnd ? Cursors.SizeWE : Cursors.SizeAll;
        Mouse.OverrideCursor = bar.Cursor;
    }

    private static void ClearPlannerBarCursor()
    {
        Mouse.OverrideCursor = null;
    }

    private void UpdateCurrentTaskBarVisual(double barTop)
    {
        if (_currentTaskBar is null)
        {
            return;
        }

        Canvas.SetLeft(_currentTaskBar, TimelineLabelWidth + _currentTaskStart * PeriodWidth + 4);
        Canvas.SetTop(_currentTaskBar, barTop);
        _currentTaskBar.Width = Math.Max(PeriodWidth - 8, (_currentTaskEnd - _currentTaskStart + 1) * PeriodWidth - 8);
    }

    private (int Start, int End) GetCurrentTaskRange()
    {
        if (_periods.Count == 0)
        {
            return (0, 0);
        }

        var currentPeriodIndex = GetCurrentPeriodIndex();
        var active = _periods
            .Select((period, index) => (period, index))
            .Where(item => item.index >= currentPeriodIndex && Math.Abs(item.period.Amount) > 0.005m)
            .Select(item => item.index)
            .ToList();
        var start = currentPeriodIndex;
        var end = active.Count > 0 ? Math.Max(start, active[^1]) : start;
        return (Math.Clamp(start, 0, _periods.Count - 1), Math.Clamp(end, start, _periods.Count - 1));
    }

    private int GetCurrentPeriodIndex()
        => GetPeriodIndex(_viewModel.Header.CurrentPeriod, 0);

    private (MonthlyForecast? Start, MonthlyForecast? End) GetActualCostRange()
    {
        var active = _periods
            .Where(period => Math.Abs(period.ActualCostAmount) > 0.005m)
            .ToList();
        return active.Count == 0 ? (null, null) : (active[0], active[^1]);
    }

    private int GetTaskTimelineStartIndex()
    {
        if (_periods.Count == 0)
        {
            return 0;
        }

        var firstActivity = _phaseView
            .Select(phase => _periods.FindIndex(period => string.Equals(period.PeriodLabel, phase.StartPeriodLabel, StringComparison.OrdinalIgnoreCase)))
            .Where(index => index >= 0)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        var firstCost = _periods
            .Select((period, index) => (period, index))
            .Where(item => Math.Abs(item.period.Amount) > 0.005m || Math.Abs(item.period.ActualCostAmount) > 0.005m)
            .Select(item => item.index)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        if (firstActivity == int.MaxValue && firstCost == int.MaxValue)
        {
            return GetPeriodIndex(_viewModel.Header.CurrentPeriod, 0);
        }

        return Math.Clamp(Math.Min(firstActivity, firstCost), 0, _periods.Count - 1);
    }

    private (int Start, int End) GetForecastRange(ForecastLine line)
    {
        var active = line.MonthlyForecasts
            .Where(period => Math.Abs(period.Amount) > 0.005m || Math.Abs(period.ActualCostAmount) > 0.005m)
            .Select(period => GetPeriodIndex(period.PeriodLabel, -1))
            .Where(index => index >= 0)
            .ToList();
        var taskStart = GetTaskTimelineStartIndex();
        if (active.Count == 0)
        {
            return (taskStart, taskStart);
        }

        var start = Math.Min(taskStart, active[0]);
        return (start, Math.Max(start, active[^1]));
    }

    private decimal GetRangeTotal(int start, int end)
        => _periods.Skip(start).Take(Math.Max(0, end - start + 1)).Where(period => !period.IsLocked).Sum(period => period.Amount);

    private int GetPeriodIndex(string? label, int fallback)
    {
        var index = _periods.FindIndex(period => string.Equals(period.PeriodLabel, label, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : Math.Clamp(fallback, 0, Math.Max(0, _periods.Count - 1));
    }

    private int GetPeriodDelta(Point start, Point current)
        => (int)Math.Round((current.X - start.X) / PeriodWidth, MidpointRounding.AwayFromZero);

    private void AddTimelineCell(
        double x,
        double y,
        double width,
        double height,
        string background,
        string text,
        FontWeight weight,
        string foreground)
    {
        var panel = new Border
        {
            Background = BrushFactory.Frozen(background),
            BorderBrush = BrushFactory.Frozen("#D8E2EE"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Width = width,
            Height = height,
            Child = string.IsNullOrWhiteSpace(text)
                ? null
                : new TextBlock
                {
                    Text = text,
                    FontWeight = weight,
                    Foreground = BrushFactory.Frozen(foreground),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(9, 0, 6, 0)
                }
        };
        AddTimelineElement(panel, x, y, width, height);
    }

    private void AddTimelineElement(FrameworkElement element, double x, double y, double width, double height, string? background = null, string? border = null)
    {
        if (background is not null && element is not Border)
        {
            element = new Border
            {
                Background = BrushFactory.Frozen(background),
                BorderBrush = BrushFactory.Frozen(border ?? "#D8E2EE"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = element
            };
        }
        else if (element is Border elementBorder && background is not null)
        {
            elementBorder.Background = BrushFactory.Frozen(background);
            elementBorder.BorderBrush = BrushFactory.Frozen(border ?? "#D8E2EE");
        }

        element.Width = width;
        element.Height = height;
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        _timelineCanvas.Children.Add(element);
        if (x < TimelineLabelWidth)
        {
            TrackFrozenTimelineElement(element, x);
        }
    }

    private void AddCanvasText(string text, double x, double y, double fontSize, FontWeight weight, string foreground)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = BrushFactory.Frozen(foreground)
        };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        _timelineCanvas.Children.Add(block);
        if (x < TimelineLabelWidth)
        {
            TrackFrozenTimelineElement(block, x);
        }
    }

    private void TrackFrozenTimelineElement(FrameworkElement element, double baseLeft)
    {
        _frozenTimelineElements.Add(new FrozenTimelineElement(element, baseLeft));
        Panel.SetZIndex(element, 300);
        Canvas.SetLeft(element, _timelineScrollViewer.HorizontalOffset + baseLeft);
    }

    private void UpdateFrozenTimelineElements()
    {
        var horizontalOffset = _timelineScrollViewer.HorizontalOffset;
        foreach (var frozen in _frozenTimelineElements)
        {
            Canvas.SetLeft(frozen.Element, horizontalOffset + frozen.BaseLeft);
        }
    }

    private static Brush GetPhaseBrush(int index)
        => BrushFactory.Frozen((index % 4) switch
        {
            1 => "#0F766E",
            2 => "#7C3AED",
            3 => "#B45309",
            _ => "#2563EB"
        });

    private static Brush GetCostLineBrush(int index)
        => BrushFactory.Frozen((index % 4) switch
        {
            1 => "#C2410C",
            2 => "#7C3AED",
            3 => "#15803D",
            _ => "#0F766E"
        });

    private static string DisplayLineName(ForecastLine line)
        => string.IsNullOrWhiteSpace(line.ResourceName) ? "Unnamed task" : line.ResourceName;

    private static string DisplayCostLineName(ForecastTaskCostLine costLine)
        => string.IsNullOrWhiteSpace(costLine.Name) ? "Future variation" : costLine.Name;

    private static string GetCostBarLabel(ForecastTaskCostLine costLine)
    {
        var name = DisplayCostLineName(costLine);
        return Math.Abs(costLine.Amount) > 0.005m
            ? $"{name}  {costLine.Amount:N0}"
            : name;
    }

    private static TextBox? FindNamedTextBox(DependencyObject? root, string tag)
    {
        if (root is TextBox textBox && string.Equals(textBox.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
        {
            return textBox;
        }

        if (root is not null)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var match = FindNamedTextBox(VisualTreeHelper.GetChild(root, index), tag);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    private static ComboBox? FindNamedComboBox(DependencyObject? root, string tag)
    {
        if (root is ComboBox combo && string.Equals(combo.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
        {
            return combo;
        }

        if (root is not null)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var match = FindNamedComboBox(VisualTreeHelper.GetChild(root, index), tag);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    private static Button? FindButtonByTag(DependencyObject? root, string tag)
    {
        if (root is Button button && string.Equals(button.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
        {
            return button;
        }

        if (root is not null)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var match = FindButtonByTag(VisualTreeHelper.GetChild(root, index), tag);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    private sealed record PhaseDragState(
        ForecastTaskPhase Phase,
        int StartIndex,
        int EndIndex,
        Point StartPoint,
        bool ResizeEnd);

    private sealed record CostLineDragState(
        ForecastTaskCostLine CostLine,
        int StartIndex,
        int EndIndex,
        Point StartPoint,
        bool ResizeEnd);

    private sealed record CurrentTaskDragState(int StartIndex, int EndIndex, Point StartPoint);

    private sealed record FrozenTimelineElement(FrameworkElement Element, double BaseLeft);
}
