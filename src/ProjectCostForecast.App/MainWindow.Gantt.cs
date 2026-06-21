using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private const double GanttRowHeight = 26;
    private const double GanttHeaderHeight = 48;

    private static readonly Brush GanttTaskBrush = BrushFactory.Frozen(0x3B, 0x82, 0xF6);
    private static readonly Brush GanttTaskProgressBrush = BrushFactory.Frozen(0x1D, 0x4E, 0xD8);
    private static readonly Brush GanttCriticalBrush = BrushFactory.Frozen(0xEF, 0x44, 0x44);
    private static readonly Brush GanttCriticalProgressBrush = BrushFactory.Frozen(0xB9, 0x1C, 0x1C);
    private static readonly Brush GanttGhostBrush = BrushFactory.Frozen(0x93, 0xC5, 0xFD);
    private static readonly Brush GanttHeadingBrush = BrushFactory.Frozen(0x11, 0x18, 0x27);
    private static readonly Brush GanttHammockBrush = BrushFactory.Frozen(0x0F, 0x76, 0x6E);
    private static readonly Brush GanttBaselineBrush = BrushFactory.Frozen(0x94, 0xA3, 0xB8);
    private static readonly Brush GanttFloatBrush = BrushFactory.Frozen(0x94, 0xA3, 0xB8);
    private static readonly Brush GanttLinkBrush = BrushFactory.Frozen(0x64, 0x74, 0x8B);
    private static readonly Brush GanttSlipBrush = BrushFactory.Frozen(0xDC, 0x26, 0x26);
    private static readonly Brush GanttWeekendBrush = BrushFactory.Frozen(0xF1, 0xF5, 0xF9);
    private static readonly Brush GanttGridLineBrush = BrushFactory.Frozen(0xE2, 0xE8, 0xF0);
    private static readonly Brush GanttRowLineBrush = BrushFactory.Frozen(0xF8, 0xFA, 0xFC);
    private static readonly Brush GanttHeaderTextBrush = BrushFactory.Frozen(0x33, 0x41, 0x55);
    private static readonly Brush GanttSelectionBrush = BrushFactory.Frozen(0xF9, 0x73, 0x16);

    private enum GanttDragMode
    {
        None,
        Move,
        Resize,
        Link,
        Create
    }

    private double _ganttDayWidth = 18;
    private bool _ganttWired;
    private bool _ganttSyncingScroll;
    private ScrollViewer? _scheduleGridScrollViewer;
    private GanttDragMode _ganttDragMode;
    private ScheduleActivity? _ganttDragActivity;
    private Point _ganttDragStart;
    private double _ganttDragBarLeft;
    private double _ganttDragBarWidth;
    private double _ganttDragBarTop;
    private DateOnly _ganttDragRangeStart;
    private Rectangle? _ganttGhostRect;
    private Line? _ganttLinkLine;
    private bool _ganttDragMoved;
    private Point _ganttCreateStart;
    private ScheduleActivity? _schedulePrimarySelection;
    private Popup? _scheduleLinkClipboardPopup;
    private Border? _scheduleLinkClipboardSurface;
    private Ellipse? _scheduleLinkClipboardHandle;
    private StackPanel? _scheduleLinkClipboardList;
    private bool _scheduleLinkClipboardMoving;
    private Point _scheduleLinkClipboardMoveStart;
    private double _scheduleLinkClipboardStartX;
    private double _scheduleLinkClipboardStartY;

    private void InitializeGanttChart()
    {
        Loaded += (_, _) => WireGanttSubscriptions();
        DataContextChanged += (_, _) => WireGanttSubscriptions();
    }

    private void WireGanttSubscriptions()
    {
        if (_ganttWired || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _ganttWired = true;
        viewModel.ScheduleRecalculated += (_, _) => QueueRedrawGantt();
        QueueRedrawGantt();
        AutoSizeScheduleGridColumns();
    }

    private void AutoSizeScheduleGridColumns()
    {
        ConfigureScheduleGridPerformance();
        if (_scheduleGridColumnsAutoSized)
        {
            return;
        }

        _scheduleGridColumnsAutoSized = true;
        // Auto measures the wider of the header and realised cell content. Do this once;
        // repeating it after every schedule calculation makes manual resizing very costly.
        foreach (var column in ScheduleGrid.Columns)
        {
            if (column.Width.UnitType != DataGridLengthUnitType.Star)
            {
                column.Width = DataGridLength.Auto;
            }
        }
    }

    private void ConfigureScheduleGridPerformance()
    {
        ScheduleGrid.EnableRowVirtualization = true;
        ScheduleGrid.EnableColumnVirtualization = false;
        ScheduleGrid.CanUserResizeColumns = true;
        VirtualizingPanel.SetIsVirtualizing(ScheduleGrid, true);
        VirtualizingPanel.SetVirtualizationMode(ScheduleGrid, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(ScheduleGrid, true);
    }

    private MainWindowViewModel? GanttViewModel => DataContext as MainWindowViewModel;

    private void AddScheduleActivity_Click(object sender, RoutedEventArgs e) => GanttViewModel?.AddScheduleActivity(ScheduleActivityKind.Task);

    private void AddScheduleMilestone_Click(object sender, RoutedEventArgs e) => GanttViewModel?.AddScheduleActivity(ScheduleActivityKind.Milestone);

    private void AddScheduleHeading_Click(object sender, RoutedEventArgs e) => GanttViewModel?.AddScheduleActivity(ScheduleActivityKind.Heading);

    private void AddScheduleHammock_Click(object sender, RoutedEventArgs e) => GanttViewModel?.AddScheduleActivity(ScheduleActivityKind.Hammock);

    private void DeleteScheduleActivity_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            viewModel.DeleteScheduleActivities(GetSelectedScheduleActivities());
            RestoreScheduleGridSelection();
        }
    }

    private void ScheduleIndent_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            viewModel.IndentScheduleActivities(GetSelectedScheduleActivities(), 1);
        }
    }

    private void ScheduleOutdent_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            viewModel.IndentScheduleActivities(GetSelectedScheduleActivities(), -1);
        }
    }

    private void ScheduleMoveUp_Click(object sender, RoutedEventArgs e) => GanttViewModel?.MoveSelectedScheduleActivity(-1);

    private void ScheduleMoveDown_Click(object sender, RoutedEventArgs e) => GanttViewModel?.MoveSelectedScheduleActivity(1);

    private void ScheduleLink_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            var selection = ScheduleGrid.SelectedItems.OfType<ScheduleActivity>().ToList();
            viewModel.LinkScheduleActivities(selection);
        }
    }

    private void ScheduleCalendars_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            var window = new ScheduleCalendarWindow(viewModel) { Owner = this };
            window.ShowDialog();
        }
    }

    private void ScheduleCaptureBaseline_Click(object sender, RoutedEventArgs e)
    {
        GanttViewModel?.CaptureScheduleBaseline(BaselineNameTextBox.Text);
        BaselineNameTextBox.Clear();
    }

    private void ScheduleDeleteBaseline_Click(object sender, RoutedEventArgs e) => GanttViewModel?.DeleteActiveScheduleBaseline();

    private void GanttZoomDay_Click(object sender, RoutedEventArgs e) => SetGanttZoom(24);

    private void GanttZoomWeek_Click(object sender, RoutedEventArgs e) => SetGanttZoom(8);

    private void GanttZoomMonth_Click(object sender, RoutedEventArgs e) => SetGanttZoom(3);

    private void SetGanttZoom(double dayWidth)
    {
        _ganttDayWidth = dayWidth;
        QueueRedrawGantt();
    }

    private void ScheduleGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var latest = e.AddedItems.OfType<ScheduleActivity>().LastOrDefault()
                     ?? ScheduleGrid.SelectedItems.OfType<ScheduleActivity>().LastOrDefault();
        _schedulePrimarySelection = latest;
        if (latest is not null && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedScheduleActivity = latest;
        }

        QueueRedrawGantt();
    }

    private void SelectScheduleActivity(ScheduleActivity activity, bool preserveSelection, bool scrollIntoView)
    {
        if (preserveSelection)
        {
            if (ScheduleGrid.SelectedItems.Contains(activity))
            {
                ScheduleGrid.SelectedItems.Remove(activity);
            }
            else
            {
                ScheduleGrid.SelectedItems.Add(activity);
            }
        }
        else
        {
            ScheduleGrid.SelectedItems.Clear();
            ScheduleGrid.SelectedItem = activity;
        }

        ScheduleGrid.CurrentItem = activity;
        _schedulePrimarySelection = activity;
        if (GanttViewModel is { } viewModel)
        {
            viewModel.SelectedScheduleActivity = activity;
        }

        if (scrollIntoView)
        {
            ScheduleGrid.ScrollIntoView(activity);
        }

        QueueRedrawGantt();
    }

    private IReadOnlyList<ScheduleActivity> GetSelectedScheduleActivities()
    {
        var selected = ScheduleGrid.SelectedItems.OfType<ScheduleActivity>().ToList();
        if (selected.Count > 0)
        {
            return selected;
        }

        return GanttViewModel?.SelectedScheduleActivity is { } active
            ? [active]
            : [];
    }

    private void RestoreScheduleGridSelection()
    {
        if (GanttViewModel?.SelectedScheduleActivity is not { } active)
        {
            return;
        }

        ScheduleGrid.SelectedItem = active;
        ScheduleGrid.CurrentItem = active;
    }

    private void EditScheduleRelationship(MainWindowViewModel viewModel, ScheduleActivity successor, ActivityLink predecessor)
    {
        var editor = new ScheduleLinkEditWindow(predecessor.Type, predecessor.LagDays) { Owner = this };
        if (editor.ShowDialog() != true)
        {
            return;
        }

        if (editor.DeleteRequested)
        {
            viewModel.BreakScheduleLink(successor, predecessor.PredecessorId);
            return;
        }

        viewModel.UpdateScheduleLink(successor, predecessor.PredecessorId, editor.LinkType, editor.LagDays);
    }

    private void ScheduleGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var clickedActivity = FindParent<DataGridRow>(source)?.Item as ScheduleActivity;
        if (clickedActivity is null)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None)
        {
            _schedulePrimarySelection = clickedActivity;
            return;
        }

        _schedulePrimarySelection ??= ScheduleGrid.SelectedItems.OfType<ScheduleActivity>().FirstOrDefault() ?? clickedActivity;
    }

    private void ScheduleGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _scheduleGridScrollViewer ??= FindDescendantScrollViewer(ScheduleGrid);
        if (_scheduleGridScrollViewer is not null)
        {
            _scheduleGridScrollViewer.ScrollChanged -= ScheduleGridScrollViewer_ScrollChanged;
            _scheduleGridScrollViewer.ScrollChanged += ScheduleGridScrollViewer_ScrollChanged;
        }

        QueueRedrawGantt();
    }

    private void ScheduleGridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_ganttSyncingScroll)
        {
            return;
        }

        _ganttSyncingScroll = true;
        GanttBodyScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _ganttSyncingScroll = false;
    }

    private void GanttBodyScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        GanttHeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        if (_ganttSyncingScroll)
        {
            return;
        }

        _ganttSyncingScroll = true;
        _scheduleGridScrollViewer?.ScrollToVerticalOffset(e.VerticalOffset);
        _ganttSyncingScroll = false;
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            if (FindDescendantScrollViewer(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private void QueueRedrawGantt()
    {
        if (_ganttRedrawQueued)
        {
            return;
        }

        _ganttRedrawQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _ganttRedrawQueued = false;
            RedrawGantt();
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void RedrawGantt()
    {
        if (GanttViewModel is not { } viewModel || GanttBodyCanvas is null || _ganttDragMode != GanttDragMode.None)
        {
            return;
        }

        using var redrawMeasure = GridPerformanceDiagnostics.Measure(
            "schedule-gantt-redraw",
            minimumMillisecondsToLog: 16,
            sampleRate: 25);

        GanttBodyCanvas.Children.Clear();
        GanttHeaderCanvas.Children.Clear();

        var activities = viewModel.ScheduleActivities;
        if (activities.Count == 0)
        {
            GanttBodyCanvas.Width = 0;
            GanttBodyCanvas.Height = 0;
            GanttHeaderCanvas.Width = 0;
            return;
        }

        var (rangeStart, rangeEnd) = GetGanttDateRange(viewModel);
        var totalDays = rangeEnd.DayNumber - rangeStart.DayNumber + 1;
        var width = totalDays * _ganttDayWidth;
        var height = activities.Count * GanttRowHeight;

        GanttBodyCanvas.Width = width;
        GanttBodyCanvas.Height = height;
        GanttHeaderCanvas.Width = width;

        DrawGanttCalendarShading(viewModel, rangeStart, rangeEnd, height);
        DrawGanttTimescale(rangeStart, rangeEnd, height);
        DrawGanttRowLines(activities.Count, width);
        DrawGanttMarkerLines(viewModel, rangeStart, height);

        var rowIndexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < activities.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(activities[i].Id))
            {
                rowIndexById[activities[i].Id] = i;
            }
        }

        DrawGanttLinks(viewModel, rowIndexById, rangeStart);

        for (var i = 0; i < activities.Count; i++)
        {
            DrawGanttRow(activities[i], i, rangeStart);
        }
    }

    private (DateOnly Start, DateOnly End) GetGanttDateRange(MainWindowViewModel viewModel)
    {
        var dates = new List<DateOnly>();
        foreach (var activity in viewModel.ScheduleActivities)
        {
            if (activity.EarlyStart is { } earlyStart)
            {
                dates.Add(earlyStart);
            }

            if (activity.EarlyFinish is { } earlyFinish)
            {
                dates.Add(earlyFinish);
            }

            if (activity.LateFinish is { } lateFinish)
            {
                dates.Add(lateFinish);
            }

            if (viewModel.ShowScheduleBaselineComparison && activity.BaselineStart is { } baselineStart)
            {
                dates.Add(baselineStart);
            }

            if (viewModel.ShowScheduleBaselineComparison && activity.BaselineFinish is { } baselineFinish)
            {
                dates.Add(baselineFinish);
            }
        }

        if (viewModel.ScheduleDataRef.ProjectStart is { } projectStart)
        {
            dates.Add(projectStart);
        }

        if (viewModel.ScheduleDataRef.MustFinishBy is { } mustFinish)
        {
            dates.Add(mustFinish);
        }

        if (dates.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return (today.AddDays(-7), today.AddDays(60));
        }

        return (dates.Min().AddDays(-7), dates.Max().AddDays(14));
    }

    private double GanttDateToX(DateOnly date, DateOnly rangeStart)
    {
        return (date.DayNumber - rangeStart.DayNumber) * _ganttDayWidth;
    }

    private void DrawGanttCalendarShading(MainWindowViewModel viewModel, DateOnly rangeStart, DateOnly rangeEnd, double height)
    {
        if (_ganttDayWidth < 4)
        {
            return;
        }

        var visibleCalendars = viewModel.ScheduleCalendars.Where(calendar => calendar.IsVisibleOnGantt).ToList();
        if (visibleCalendars.Count == 0)
        {
            return;
        }

        foreach (var calendar in visibleCalendars)
        {
            var calendarBrush = CreateCalendarOverlayBrush(calendar.ColorHex);
            for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
            {
                if (calendar.IsWorkingDay(date))
                {
                    continue;
                }

                var shade = new Rectangle
                {
                    Width = _ganttDayWidth,
                    Height = height,
                    Fill = calendarBrush,
                    IsHitTestVisible = false,
                    ToolTip = $"{calendar.Name}: non-working day"
                };
                Canvas.SetLeft(shade, GanttDateToX(date, rangeStart));
                Canvas.SetTop(shade, 0);
                GanttBodyCanvas.Children.Add(shade);
            }

            var legend = new Border
            {
                Background = CreateCalendarSolidBrush(calendar.ColorHex),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Child = new TextBlock
                {
                    Text = calendar.Name,
                    FontSize = 9,
                    Foreground = Brushes.White
                }
            };
            Canvas.SetLeft(legend, 6 + (visibleCalendars.IndexOf(calendar) * 100));
            Canvas.SetTop(legend, 27);
            GanttHeaderCanvas.Children.Add(legend);
        }
    }

    private static Brush CreateCalendarOverlayBrush(string? colorHex)
    {
        var solid = CreateCalendarSolidBrush(colorHex);
        return new SolidColorBrush(solid.Color) { Opacity = 0.12 };
    }

    private static SolidColorBrush CreateCalendarSolidBrush(string? colorHex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(colorHex) ? "#94A3B8" : colorHex)!;
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        }
    }

    private void DrawGanttTimescale(DateOnly rangeStart, DateOnly rangeEnd, double bodyHeight)
    {
        // Month band
        var monthCursor = new DateOnly(rangeStart.Year, rangeStart.Month, 1);
        while (monthCursor <= rangeEnd)
        {
            var monthEnd = monthCursor.AddMonths(1).AddDays(-1);
            var x1 = Math.Max(0, GanttDateToX(monthCursor < rangeStart ? rangeStart : monthCursor, rangeStart));
            var x2 = GanttDateToX(monthEnd > rangeEnd ? rangeEnd : monthEnd, rangeStart) + _ganttDayWidth;

            var label = new TextBlock
            {
                Text = monthCursor.ToString("MMM yyyy"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GanttHeaderTextBrush
            };
            Canvas.SetLeft(label, x1 + 4);
            Canvas.SetTop(label, 4);
            GanttHeaderCanvas.Children.Add(label);

            var divider = new Line
            {
                X1 = x2,
                X2 = x2,
                Y1 = 0,
                Y2 = GanttHeaderHeight,
                Stroke = GanttGridLineBrush,
                StrokeThickness = 1
            };
            GanttHeaderCanvas.Children.Add(divider);

            var bodyDivider = new Line
            {
                X1 = x2,
                X2 = x2,
                Y1 = 0,
                Y2 = bodyHeight,
                Stroke = GanttGridLineBrush,
                StrokeThickness = 1
            };
            GanttBodyCanvas.Children.Add(bodyDivider);

            monthCursor = monthCursor.AddMonths(1);
        }

        // Day / week band
        if (_ganttDayWidth >= 14)
        {
            for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
            {
                var dayLabel = new TextBlock
                {
                    Text = date.Day.ToString(),
                    FontSize = 9,
                    Foreground = GanttHeaderTextBrush
                };
                Canvas.SetLeft(dayLabel, GanttDateToX(date, rangeStart) + 2);
                Canvas.SetTop(dayLabel, 28);
                GanttHeaderCanvas.Children.Add(dayLabel);
            }
        }
        else if (_ganttDayWidth >= 4)
        {
            for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Monday)
                {
                    continue;
                }

                var weekLabel = new TextBlock
                {
                    Text = $"{date.Day}/{date.Month}",
                    FontSize = 9,
                    Foreground = GanttHeaderTextBrush
                };
                Canvas.SetLeft(weekLabel, GanttDateToX(date, rangeStart) + 1);
                Canvas.SetTop(weekLabel, 28);
                GanttHeaderCanvas.Children.Add(weekLabel);

                var tick = new Line
                {
                    X1 = GanttDateToX(date, rangeStart),
                    X2 = GanttDateToX(date, rangeStart),
                    Y1 = 26,
                    Y2 = GanttHeaderHeight,
                    Stroke = GanttGridLineBrush,
                    StrokeThickness = 1
                };
                GanttHeaderCanvas.Children.Add(tick);
            }
        }
    }

    private void DrawGanttRowLines(int rowCount, double width)
    {
        for (var i = 1; i <= rowCount; i++)
        {
            var line = new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = i * GanttRowHeight,
                Y2 = i * GanttRowHeight,
                Stroke = GanttRowLineBrush,
                StrokeThickness = 1
            };
            GanttBodyCanvas.Children.Add(line);
        }
    }

    private void DrawGanttMarkerLines(MainWindowViewModel viewModel, DateOnly rangeStart, double height)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        AddGanttMarkerLine(GanttDateToX(today, rangeStart) + (_ganttDayWidth / 2), height, GanttSlipBrush, "Today");
        if (viewModel.ScheduleDataRef.ProjectStart is { } projectStart)
        {
            AddGanttMarkerLine(GanttDateToX(projectStart, rangeStart), height, GanttTaskBrush, "Project start");
        }

        if (viewModel.ScheduleDataRef.MustFinishBy is { } mustFinish)
        {
            AddGanttMarkerLine(GanttDateToX(mustFinish, rangeStart) + _ganttDayWidth, height, GanttHeadingBrush, "Must finish by");
        }
    }

    private void AddGanttMarkerLine(double x, double height, Brush brush, string toolTip)
    {
        var line = new Line
        {
            X1 = x,
            X2 = x,
            Y1 = 0,
            Y2 = height,
            Stroke = brush,
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
            ToolTip = toolTip,
            Opacity = 0.7
        };
        GanttBodyCanvas.Children.Add(line);
    }

    private void DrawGanttLinks(MainWindowViewModel viewModel, Dictionary<string, int> rowIndexById, DateOnly rangeStart)
    {
        foreach (var link in viewModel.ScheduleDataRef.Links)
        {
            if (!rowIndexById.TryGetValue(link.PredecessorId, out var predRow)
                || !rowIndexById.TryGetValue(link.SuccessorId, out var succRow))
            {
                continue;
            }

            var predecessor = viewModel.ScheduleActivities[predRow];
            var successor = viewModel.ScheduleActivities[succRow];
            if (predecessor.EarlyStart is null || predecessor.EarlyFinish is null
                || successor.EarlyStart is null || successor.EarlyFinish is null)
            {
                continue;
            }

            var predUsesStart = link.Type is ActivityLinkType.StartToStart or ActivityLinkType.StartToFinish;
            var succUsesFinish = link.Type is ActivityLinkType.FinishToFinish or ActivityLinkType.StartToFinish;

            var fromX = predUsesStart
                ? GanttDateToX(predecessor.EarlyStart.Value, rangeStart)
                : GanttDateToX(predecessor.EarlyFinish.Value, rangeStart) + _ganttDayWidth;
            var toX = succUsesFinish
                ? GanttDateToX(successor.EarlyFinish.Value, rangeStart) + _ganttDayWidth
                : GanttDateToX(successor.EarlyStart.Value, rangeStart);
            var fromY = (predRow * GanttRowHeight) + (GanttRowHeight / 2);
            var toY = (succRow * GanttRowHeight) + (succRow >= predRow ? 4 : GanttRowHeight - 4);

            var elbowX = predUsesStart ? fromX - 8 : fromX + 8;
            var approachX = succUsesFinish ? toX + 8 : toX - 8;

            var polyline = new Polyline
            {
                Stroke = GanttLinkBrush,
                StrokeThickness = 5,
                Points =
                [
                    new Point(fromX, fromY),
                    new Point(elbowX, fromY),
                    new Point(elbowX, toY),
                    new Point(approachX, toY),
                    new Point(approachX, toY),
                    new Point(toX, toY)
                ],
                ToolTip = $"{link.PredecessorId} {link.TypeLabel}{(link.LagDays != 0 ? (link.LagDays > 0 ? "+" : "") + link.LagDays : string.Empty)} → {link.SuccessorId}"
            };
            polyline.Cursor = Cursors.Hand;
            polyline.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                if (e.ClickCount != 2)
                {
                    return;
                }

                EditScheduleRelationship(viewModel, successor, link);
            };
            polyline.MouseRightButtonUp += (_, e) =>
            {
                var menu = new ContextMenu();
                menu.Items.Add(CreateScheduleMenuItem("Edit relationship...", () => EditScheduleRelationship(viewModel, successor, link)));
                menu.Items.Add(new Separator());
                foreach (var linkType in MainWindowViewModel.ScheduleLinkTypeOptions)
                {
                    var capturedType = linkType;
                    menu.Items.Add(CreateScheduleMenuItem(
                        $"Change to {new ActivityLink { Type = capturedType }.TypeLabel}",
                        () => viewModel.UpdateScheduleLink(successor, predecessor.Id, capturedType, link.LagDays)));
                }
                menu.Items.Add(new Separator());
                menu.Items.Add(CreateScheduleMenuItem("Lag -1 day", () => viewModel.UpdateScheduleLink(successor, predecessor.Id, link.Type, link.LagDays - 1)));
                menu.Items.Add(CreateScheduleMenuItem("Lag +1 day", () => viewModel.UpdateScheduleLink(successor, predecessor.Id, link.Type, link.LagDays + 1)));
                menu.Items.Add(CreateScheduleMenuItem("Remove relationship", () => viewModel.BreakScheduleLink(successor, predecessor.Id)));
                menu.PlacementTarget = polyline;
                menu.Placement = PlacementMode.MousePoint;
                menu.IsOpen = true;
                e.Handled = true;
            };
            GanttBodyCanvas.Children.Add(polyline);

            var arrowTipY = succRow >= predRow ? toY + 4 : toY - 4;
            var arrow = new Polygon
            {
                Fill = GanttLinkBrush,
                IsHitTestVisible = false,
                Points =
                [
                    new Point(toX, arrowTipY),
                    new Point(toX - 3.5, toY),
                    new Point(toX + 3.5, toY)
                ]
            };
            GanttBodyCanvas.Children.Add(arrow);
        }
    }

    private void DrawGanttRow(ScheduleActivity activity, int rowIndex, DateOnly rangeStart)
    {
        var rowTop = rowIndex * GanttRowHeight;
        var isSelected = ScheduleGrid.SelectedItems.Contains(activity);
        if (isSelected)
        {
            var selectionBand = new Rectangle
            {
                Width = Math.Max(GanttBodyCanvas.Width, GanttBodyScroll.ViewportWidth),
                Height = GanttRowHeight,
                Fill = BrushFactory.Frozen("#DBEAFE"),
                Opacity = 0.45,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(selectionBand, 0);
            Canvas.SetTop(selectionBand, rowTop);
            GanttBodyCanvas.Children.Add(selectionBand);
        }

        // Baseline bar beneath the current bar
        if (GanttViewModel?.ShowScheduleBaselineComparison == true
            && activity.BaselineStart is { } baselineStart
            && activity.BaselineFinish is { } baselineFinish
            && baselineFinish >= baselineStart)
        {
            var baselineBar = new Rectangle
            {
                Width = Math.Max(2, ((baselineFinish.DayNumber - baselineStart.DayNumber + 1) * _ganttDayWidth)),
                Height = 4,
                Fill = GanttBaselineBrush,
                Opacity = 0.85,
                RadiusX = 2,
                RadiusY = 2,
                ToolTip = $"{activity.Id} baseline: {baselineStart:d/MM/yyyy} → {baselineFinish:d/MM/yyyy}"
            };
            Canvas.SetLeft(baselineBar, GanttDateToX(baselineStart, rangeStart));
            Canvas.SetTop(baselineBar, rowTop + GanttRowHeight - 6);
            GanttBodyCanvas.Children.Add(baselineBar);
        }

        if (activity.EarlyStart is not { } earlyStart || activity.EarlyFinish is not { } earlyFinish || earlyFinish < earlyStart)
        {
            return;
        }

        var barLeft = GanttDateToX(earlyStart, rangeStart);
        var barWidth = Math.Max(2, (earlyFinish.DayNumber - earlyStart.DayNumber + 1) * _ganttDayWidth);
        var toolTip = BuildGanttToolTip(activity);

        // Total float line from early finish to late finish
        if (activity.LateFinish is { } lateFinish && lateFinish > earlyFinish && activity.Kind is ScheduleActivityKind.Task or ScheduleActivityKind.Milestone)
        {
            var floatLine = new Line
            {
                X1 = barLeft + barWidth,
                X2 = GanttDateToX(lateFinish, rangeStart) + _ganttDayWidth,
                Y1 = rowTop + (GanttRowHeight / 2),
                Y2 = rowTop + (GanttRowHeight / 2),
                Stroke = GanttFloatBrush,
                StrokeThickness = 2,
                ToolTip = $"Float to late finish {lateFinish:d/MM/yyyy}"
            };
            GanttBodyCanvas.Children.Add(floatLine);
        }

        // Slip marker from baseline finish to current finish
        if (activity.SlipDays is > 0 && activity.BaselineFinish is { } slipBaselineFinish)
        {
            var slipLine = new Line
            {
                X1 = GanttDateToX(slipBaselineFinish, rangeStart) + _ganttDayWidth,
                X2 = barLeft + barWidth,
                Y1 = rowTop + GanttRowHeight - 4.5,
                Y2 = rowTop + GanttRowHeight - 4.5,
                Stroke = GanttSlipBrush,
                StrokeThickness = 1.6,
                StrokeDashArray = [2, 2],
                ToolTip = $"Slip {activity.SlipDays} working days against baseline"
            };
            GanttBodyCanvas.Children.Add(slipLine);
        }

        FrameworkElement bar;
        switch (activity.Kind)
        {
            case ScheduleActivityKind.Heading:
            {
                bar = BuildGanttSummaryBar(barWidth);
                Canvas.SetTop(bar, rowTop + 6);
                break;
            }
            case ScheduleActivityKind.Milestone:
            {
                var size = 12.0;
                bar = new Polygon
                {
                    Points =
                    [
                        new Point(size / 2, 0),
                        new Point(size, size / 2),
                        new Point(size / 2, size),
                        new Point(0, size / 2)
                    ],
                    Fill = activity.IsCritical ? GanttCriticalBrush : GanttHeadingBrush,
                    Stroke = isSelected ? GanttSelectionBrush : null,
                    StrokeThickness = isSelected ? 2 : 0
                };
                barLeft = GanttDateToX(earlyStart, rangeStart) + (_ganttDayWidth / 2) - (size / 2);
                Canvas.SetTop(bar, rowTop + (GanttRowHeight / 2) - (size / 2));
                break;
            }
            case ScheduleActivityKind.Hammock:
            {
                bar = BuildGanttHammockBar(barWidth);
                Canvas.SetTop(bar, rowTop + 8);
                break;
            }
            default:
            {
                var fill = activity.IsCritical ? GanttCriticalBrush : GanttTaskBrush;
                var container = new Grid { Width = barWidth, Height = 12 };
                container.Children.Add(new Rectangle
                {
                    Fill = fill,
                    RadiusX = 2,
                    RadiusY = 2,
                    Stroke = isSelected ? GanttSelectionBrush : null,
                    StrokeThickness = isSelected ? 1.6 : 0
                });
                if (activity.PercentComplete > 0)
                {
                    container.Children.Add(new Rectangle
                    {
                        Fill = activity.IsCritical ? GanttCriticalProgressBrush : GanttTaskProgressBrush,
                        Height = 5,
                        Margin = new Thickness(1, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = Math.Max(0, (barWidth - 2) * Math.Clamp(activity.PercentComplete / 100.0, 0, 1))
                    });
                }

                bar = container;
                Canvas.SetTop(bar, rowTop + (GanttRowHeight / 2) - 6);
                break;
            }
        }

        bar.ToolTip = toolTip;
        bar.Tag = activity;
        Canvas.SetLeft(bar, barLeft);
        GanttBodyCanvas.Children.Add(bar);

        if (activity.Kind is ScheduleActivityKind.Task or ScheduleActivityKind.Milestone)
        {
            AttachGanttBarInteractions(bar, activity, rangeStart);
            AddGanttLinkHandle(activity, activity.IsMilestone ? barLeft + 13 : barLeft + barWidth, rowTop);
        }
        else
        {
            bar.MouseLeftButtonDown += GanttBar_SelectOnly;
        }

        // Bar label to the right
        var label = new TextBlock
        {
            Text = activity.Name,
            FontSize = 10,
            Foreground = GanttHeaderTextBrush,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, barLeft + barWidth + 18);
        Canvas.SetTop(label, rowTop + (GanttRowHeight / 2) - 7);
        GanttBodyCanvas.Children.Add(label);
    }

    private static Grid BuildGanttSummaryBar(double width)
    {
        var grid = new Grid { Width = width, Height = 9 };
        grid.Children.Add(new Rectangle
        {
            Fill = GanttHeadingBrush,
            Height = 5,
            VerticalAlignment = VerticalAlignment.Top
        });
        grid.Children.Add(new Polygon
        {
            Points = [new Point(0, 0), new Point(8, 0), new Point(0, 9)],
            Fill = GanttHeadingBrush,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        grid.Children.Add(new Polygon
        {
            Points = [new Point(8, 0), new Point(0, 0), new Point(8, 9)],
            Fill = GanttHeadingBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        return grid;
    }

    private static Grid BuildGanttHammockBar(double width)
    {
        var grid = new Grid { Width = width, Height = 10 };
        grid.Children.Add(new Rectangle
        {
            Fill = GanttHammockBrush,
            Height = 4,
            VerticalAlignment = VerticalAlignment.Top
        });
        grid.Children.Add(new Rectangle
        {
            Fill = GanttHammockBrush,
            Width = 2.5,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        grid.Children.Add(new Rectangle
        {
            Fill = GanttHammockBrush,
            Width = 2.5,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        return grid;
    }

    private static string BuildGanttToolTip(ScheduleActivity activity)
    {
        var lines = new List<string>
        {
            $"{activity.Id}  {activity.Name}",
            $"{activity.Kind}, {activity.DurationDays}d",
            $"Start {activity.EarlyStart:d/MM/yyyy}   Finish {activity.EarlyFinish:d/MM/yyyy}"
        };
        if (activity.LateStart.HasValue)
        {
            lines.Add($"Late start {activity.LateStart:d/MM/yyyy}   Late finish {activity.LateFinish:d/MM/yyyy}");
        }

        if (activity.TotalFloatDays.HasValue)
        {
            lines.Add($"Total float {activity.TotalFloatDays}d{(activity.IsCritical ? "  (critical)" : string.Empty)}");
        }

        if (activity.BaselineStart.HasValue || activity.BaselineFinish.HasValue)
        {
            lines.Add($"Baseline {activity.BaselineStart:d/MM/yyyy} → {activity.BaselineFinish:d/MM/yyyy}");
        }

        if (activity.SlipDays is { } slip && slip != 0)
        {
            lines.Add(slip > 0 ? $"Slipping {slip} working days" : $"Ahead {-slip} working days");
        }

        if (!string.IsNullOrWhiteSpace(activity.ScheduleNote))
        {
            lines.Add(activity.ScheduleNote);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void GanttBar_SelectOnly(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ScheduleActivity activity })
        {
            SelectScheduleActivity(activity, preserveSelection: (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control, scrollIntoView: true);
            e.Handled = true;
        }
    }

    private void AttachGanttBarInteractions(FrameworkElement bar, ScheduleActivity activity, DateOnly rangeStart)
    {
        bar.MouseLeftButtonDown += (sender, e) =>
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            SelectScheduleActivity(activity, preserveSelection: (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control, scrollIntoView: true);

            var barWidth = double.IsNaN(element.Width) ? element.ActualWidth : element.Width;
            var positionInBar = e.GetPosition(element);
            _ganttDragMode = activity.Kind == ScheduleActivityKind.Task && barWidth > 16 && positionInBar.X >= barWidth - 8
                ? GanttDragMode.Resize
                : GanttDragMode.Move;
            _ganttDragActivity = activity;
            _ganttDragStart = e.GetPosition(GanttBodyCanvas);
            _ganttDragBarLeft = Canvas.GetLeft(element);
            _ganttDragBarTop = Canvas.GetTop(element);
            _ganttDragBarWidth = barWidth;
            _ganttDragRangeStart = rangeStart;
            _ganttDragMoved = false;

            _ganttGhostRect = new Rectangle
            {
                Width = Math.Max(4, barWidth),
                Height = 12,
                Fill = GanttGhostBrush,
                Opacity = 0.65,
                RadiusX = 2,
                RadiusY = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_ganttGhostRect, _ganttDragBarLeft);
            Canvas.SetTop(_ganttGhostRect, _ganttDragBarTop - 1);
            GanttBodyCanvas.Children.Add(_ganttGhostRect);

            element.CaptureMouse();
            e.Handled = true;
        };

        bar.MouseMove += (sender, e) =>
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            if (_ganttDragMode == GanttDragMode.None)
            {
                var barWidth = double.IsNaN(element.Width) ? element.ActualWidth : element.Width;
                element.Cursor = activity.Kind == ScheduleActivityKind.Task && barWidth > 16 && e.GetPosition(element).X >= barWidth - 8
                    ? Cursors.SizeWE
                    : Cursors.SizeAll;
                return;
            }

            if (!ReferenceEquals(_ganttDragActivity, activity) || _ganttGhostRect is null)
            {
                return;
            }

            var position = e.GetPosition(GanttBodyCanvas);
            var deltaX = position.X - _ganttDragStart.X;
            var deltaY = position.Y - _ganttDragStart.Y;
            if (Math.Abs(deltaX) > 2 || Math.Abs(deltaY) > 2)
            {
                _ganttDragMoved = true;
            }

            if (_ganttDragMode == GanttDragMode.Move
                && Math.Abs(deltaY) >= GanttRowHeight / 2)
            {
                var targetRow = Math.Clamp((int)Math.Floor(position.Y / GanttRowHeight), 0, GanttViewModel?.ScheduleActivities.Count - 1 ?? 0);
                Canvas.SetTop(_ganttGhostRect, targetRow * GanttRowHeight + 3);
            }

            var deltaDays = Math.Round(deltaX / _ganttDayWidth);
            if (_ganttDragMode == GanttDragMode.Resize)
            {
                _ganttGhostRect.Width = Math.Max(_ganttDayWidth, _ganttDragBarWidth + (deltaDays * _ganttDayWidth));
            }
            else
            {
                Canvas.SetLeft(_ganttGhostRect, _ganttDragBarLeft + (deltaDays * _ganttDayWidth));
            }
        };

        bar.MouseLeftButtonUp += (sender, e) =>
        {
            if (sender is not FrameworkElement element || _ganttDragMode is GanttDragMode.None or GanttDragMode.Link)
            {
                return;
            }

            element.ReleaseMouseCapture();
            RemoveGanttDragVisuals();

            var mode = _ganttDragMode;
            _ganttDragMode = GanttDragMode.None;
            _ganttDragActivity = null;

            var position = e.GetPosition(GanttBodyCanvas);
            var deltaDays = (int)Math.Round((position.X - _ganttDragStart.X) / _ganttDayWidth);
            var deltaY = position.Y - _ganttDragStart.Y;
            e.Handled = true;

            if (!_ganttDragMoved || GanttViewModel is not { } viewModel)
            {
                RedrawGantt();
                return;
            }

            if (mode == GanttDragMode.Move)
            {
                if (deltaDays != 0)
                {
                    viewModel.ShiftScheduleActivity(activity, deltaDays);
                }

                if (Math.Abs(deltaY) >= GanttRowHeight / 2)
                {
                    var targetRow = Math.Clamp((int)Math.Floor(position.Y / GanttRowHeight), 0, viewModel.ScheduleActivities.Count - 1);
                    viewModel.MoveScheduleActivity(activity, targetRow);
                    RedrawGantt();
                    return;
                }
            }

            if (deltaDays == 0)
            {
                RedrawGantt();
                return;
            }

            if (mode == GanttDragMode.Resize)
            {
                viewModel.ResizeScheduleActivity(activity, deltaDays);
            }
        };
    }

    private void AddGanttLinkHandle(ScheduleActivity activity, double x, double rowTop)
    {
        var handle = new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = Brushes.White,
            Stroke = GanttLinkBrush,
            StrokeThickness = 1.4,
            Cursor = Cursors.Cross,
            ToolTip = "Drag onto another bar to link (finish-to-start)",
            Tag = activity
        };
        var centerX = x + 6.5;
        var centerY = rowTop + (GanttRowHeight / 2);
        Canvas.SetLeft(handle, x + 2);
        Canvas.SetTop(handle, centerY - 4.5);

        handle.MouseLeftButtonDown += (sender, e) =>
        {
            _ganttDragMode = GanttDragMode.Link;
            _ganttDragActivity = activity;
            _ganttDragMoved = false;
            _ganttLinkLine = new Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = centerX,
                Y2 = centerY,
                Stroke = GanttTaskBrush,
                StrokeThickness = 1.6,
                StrokeDashArray = [3, 2],
                IsHitTestVisible = false
            };
            GanttBodyCanvas.Children.Add(_ganttLinkLine);
            handle.CaptureMouse();
            e.Handled = true;
        };

        handle.MouseMove += (sender, e) =>
        {
            if (_ganttDragMode != GanttDragMode.Link || _ganttLinkLine is null || !ReferenceEquals(_ganttDragActivity, activity))
            {
                return;
            }

            var position = e.GetPosition(GanttBodyCanvas);
            _ganttLinkLine.X2 = position.X;
            _ganttLinkLine.Y2 = position.Y;
            _ganttDragMoved = true;
        };

        handle.MouseLeftButtonUp += (sender, e) =>
        {
            if (_ganttDragMode != GanttDragMode.Link)
            {
                return;
            }

            handle.ReleaseMouseCapture();
            RemoveGanttDragVisuals();
            _ganttDragMode = GanttDragMode.None;
            _ganttDragActivity = null;
            e.Handled = true;

            if (!_ganttDragMoved || GanttViewModel is not { } viewModel)
            {
                RedrawGantt();
                return;
            }

            var position = e.GetPosition(GanttBodyCanvas);
            var screenPoint = handle.PointToScreen(e.GetPosition(handle));
            if (IsPointOverScheduleLinkClipboard(screenPoint))
            {
                viewModel.CopyScheduleLinkSource(activity);
                RefreshScheduleLinkClipboardPopup();
                RedrawGantt();
                return;
            }

            var targetRow = (int)Math.Floor(position.Y / GanttRowHeight);
            if (targetRow >= 0 && targetRow < viewModel.ScheduleActivities.Count)
            {
                if (!viewModel.TryCreateScheduleLink(activity, viewModel.ScheduleActivities[targetRow]))
                {
                    MessageBox.Show(this, viewModel.StatusText, "Create schedule link", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                viewModel.CopyScheduleLinkSource(activity);
            }

            RedrawGantt();
        };

        GanttBodyCanvas.Children.Add(handle);
    }

    private void GanttBodyCanvas_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Polyline)
        {
            return;
        }

        if (GanttViewModel is not { } viewModel)
        {
            return;
        }

        var position = e.GetPosition(GanttBodyCanvas);
        var row = (int)Math.Floor(position.Y / GanttRowHeight);
        var target = row >= 0 && row < viewModel.ScheduleActivities.Count
            ? viewModel.ScheduleActivities[row]
            : null;
        var menu = new ContextMenu();
        menu.Items.Add(CreateScheduleMenuItem("Open link clipboard", ShowScheduleLinkClipboardPopup));
        menu.Items.Add(CreateScheduleMenuItem(
            "Copy selected activity as link source",
            () =>
            {
                if (viewModel.SelectedScheduleActivity is { } selected)
                {
                    viewModel.CopyScheduleLinkSource(selected);
                    ShowScheduleLinkClipboardPopup();
                }
            },
            viewModel.SelectedScheduleActivity is not null));
        menu.Items.Add(CreateScheduleMenuItem(
            "Paste clipboard link to this row",
            () =>
            {
                if (!viewModel.PasteScheduleLinkTo(target!) && viewModel.ScheduleLinkClipboardActivities.Count > 0)
                {
                    MessageBox.Show(this, viewModel.StatusText, "Paste schedule link", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            },
            target is not null && viewModel.ScheduleLinkClipboardActivities.Count > 0));
        menu.Items.Add(CreateScheduleMenuItem("Clear link clipboard", () =>
        {
            viewModel.ClearScheduleLinkClipboard();
            RefreshScheduleLinkClipboardPopup();
        }));
        menu.PlacementTarget = GanttBodyCanvas;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ShowScheduleLinkClipboardPopup()
    {
        EnsureScheduleLinkClipboardPopup();
        RefreshScheduleLinkClipboardPopup();
        if (_scheduleLinkClipboardPopup is not null)
        {
            _scheduleLinkClipboardPopup.IsOpen = true;
        }
    }

    private void EnsureScheduleLinkClipboardPopup()
    {
        if (_scheduleLinkClipboardPopup is not null)
        {
            return;
        }

        _scheduleLinkClipboardHandle = new Ellipse
        {
            Width = 18,
            Height = 18,
            Fill = GanttTaskBrush,
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Cursor = Cursors.Cross,
            ToolTip = "Drag this handle onto a target schedule row"
        };
        _scheduleLinkClipboardHandle.MouseLeftButtonDown += ScheduleLinkClipboardHandle_MouseLeftButtonDown;
        _scheduleLinkClipboardHandle.MouseMove += ScheduleLinkClipboardHandle_MouseMove;
        _scheduleLinkClipboardHandle.MouseLeftButtonUp += ScheduleLinkClipboardHandle_MouseLeftButtonUp;

        var close = new Button
        {
            Content = "×",
            Width = 24,
            Height = 24,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Close link clipboard"
        };
        close.Click += (_, _) => _scheduleLinkClipboardPopup!.IsOpen = false;
        var header = new DockPanel { Cursor = Cursors.SizeAll, Background = Brushes.Transparent };
        header.MouseLeftButtonDown += ScheduleLinkClipboardHeader_MouseLeftButtonDown;
        header.MouseMove += ScheduleLinkClipboardHeader_MouseMove;
        header.MouseLeftButtonUp += ScheduleLinkClipboardHeader_MouseLeftButtonUp;
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = "Link clipboard", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(new TextBlock { Tag = "ClipboardText", Margin = new Thickness(0, 7, 0, 4), Foreground = GanttHeaderTextBrush, TextWrapping = TextWrapping.Wrap });
        _scheduleLinkClipboardList = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        content.Children.Add(_scheduleLinkClipboardList);
        content.Children.Add(_scheduleLinkClipboardHandle);
        _scheduleLinkClipboardSurface = new Border
        {
            Background = Brushes.White,
            BorderBrush = GanttTaskBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10),
            Width = 250,
            Child = content
        };
        _scheduleLinkClipboardPopup = new Popup
        {
            PlacementTarget = GanttBodyScroll,
            Placement = PlacementMode.Relative,
            HorizontalOffset = 12,
            VerticalOffset = 12,
            AllowsTransparency = true,
            StaysOpen = true,
            Child = _scheduleLinkClipboardSurface
        };
    }

    private void ScheduleLinkClipboardHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DockPanel header || _scheduleLinkClipboardPopup is null)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) is not null)
        {
            return;
        }

        _scheduleLinkClipboardMoving = true;
        _scheduleLinkClipboardMoveStart = e.GetPosition(this);
        _scheduleLinkClipboardStartX = _scheduleLinkClipboardPopup.HorizontalOffset;
        _scheduleLinkClipboardStartY = _scheduleLinkClipboardPopup.VerticalOffset;
        header.CaptureMouse();
        e.Handled = true;
    }

    private void ScheduleLinkClipboardHeader_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_scheduleLinkClipboardMoving || _scheduleLinkClipboardPopup is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        _scheduleLinkClipboardPopup.HorizontalOffset = _scheduleLinkClipboardStartX + point.X - _scheduleLinkClipboardMoveStart.X;
        _scheduleLinkClipboardPopup.VerticalOffset = _scheduleLinkClipboardStartY + point.Y - _scheduleLinkClipboardMoveStart.Y;
    }

    private void ScheduleLinkClipboardHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement header)
        {
            header.ReleaseMouseCapture();
        }

        _scheduleLinkClipboardMoving = false;
        e.Handled = true;
    }

    private void RefreshScheduleLinkClipboardPopup()
    {
        if (_scheduleLinkClipboardSurface?.Child is not StackPanel content || GanttViewModel is not { } viewModel)
        {
            return;
        }

        var text = content.Children.OfType<TextBlock>().FirstOrDefault(item => Equals(item.Tag, "ClipboardText"));
        if (text is not null)
        {
            text.Text = viewModel.ScheduleLinkClipboardActivities.Count > 0
                ? "Stored link sources:"
                : "Drag a bar link handle here, or copy selected activities.";
        }

        if (_scheduleLinkClipboardList is not null)
        {
            _scheduleLinkClipboardList.Children.Clear();
            foreach (var activity in viewModel.ScheduleLinkClipboardActivities)
            {
                _scheduleLinkClipboardList.Children.Add(new TextBlock
                {
                    Text = $"{activity.Id}  {activity.Name}",
                    Foreground = GanttHeaderTextBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }
        }

        if (_scheduleLinkClipboardHandle is not null)
        {
            _scheduleLinkClipboardHandle.IsEnabled = viewModel.ScheduleLinkClipboardActivities.Count > 0;
            _scheduleLinkClipboardHandle.Opacity = _scheduleLinkClipboardHandle.IsEnabled ? 1 : 0.35;
        }
    }

    private bool IsPointOverScheduleLinkClipboard(Point screenPoint)
    {
        if (_scheduleLinkClipboardPopup?.IsOpen != true || _scheduleLinkClipboardSurface is null)
        {
            return false;
        }

        var topLeft = _scheduleLinkClipboardSurface.PointToScreen(new Point(0, 0));
        return new Rect(topLeft, new Size(_scheduleLinkClipboardSurface.ActualWidth, _scheduleLinkClipboardSurface.ActualHeight)).Contains(screenPoint);
    }

    private void ScheduleLinkClipboardHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse handle || GanttViewModel is not { } viewModel || viewModel.ScheduleLinkClipboardActivities.Count == 0)
        {
            return;
        }

        _ganttDragMode = GanttDragMode.Link;
        _ganttDragActivity = viewModel.ScheduleLinkClipboardActivities[0];
        var position = e.GetPosition(GanttBodyCanvas);
        _ganttLinkLine = new Line
        {
            X1 = position.X,
            Y1 = position.Y,
            X2 = position.X,
            Y2 = position.Y,
            Stroke = GanttTaskBrush,
            StrokeThickness = 2,
            StrokeDashArray = [3, 2],
            IsHitTestVisible = false
        };
        GanttBodyCanvas.Children.Add(_ganttLinkLine);
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void ScheduleLinkClipboardHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_ganttDragMode != GanttDragMode.Link || _ganttLinkLine is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(GanttBodyCanvas);
        _ganttLinkLine.X2 = position.X;
        _ganttLinkLine.Y2 = position.Y;
    }

    private void ScheduleLinkClipboardHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse handle || _ganttDragMode != GanttDragMode.Link || GanttViewModel is not { } viewModel)
        {
            return;
        }

        handle.ReleaseMouseCapture();
        var position = e.GetPosition(GanttBodyCanvas);
        var targetRow = (int)Math.Floor(position.Y / GanttRowHeight);
        RemoveGanttDragVisuals();
        _ganttDragMode = GanttDragMode.None;
        _ganttDragActivity = null;
        if (targetRow >= 0 && targetRow < viewModel.ScheduleActivities.Count)
        {
            if (!viewModel.PasteScheduleLinkTo(viewModel.ScheduleActivities[targetRow])
                && viewModel.ScheduleLinkClipboardActivities.Count > 0)
            {
                MessageBox.Show(this, viewModel.StatusText, "Paste schedule link", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            RefreshScheduleLinkClipboardPopup();
        }

        RedrawGantt();
        e.Handled = true;
    }

    private void GanttBodyCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_ganttDragMode != GanttDragMode.None || GanttViewModel is not { } viewModel)
        {
            return;
        }

        _ganttDragMode = GanttDragMode.Create;
        _ganttCreateStart = e.GetPosition(GanttBodyCanvas);
        _ganttDragStart = _ganttCreateStart;
        _ganttDragRangeStart = GetGanttDateRange(viewModel).Start;
        _ganttDragMoved = false;
        _ganttGhostRect = new Rectangle
        {
            Width = 1,
            Height = GanttRowHeight - 7,
            Fill = GanttGhostBrush,
            Stroke = GanttTaskBrush,
            StrokeThickness = 1,
            StrokeDashArray = [3, 2],
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_ganttGhostRect, _ganttCreateStart.X);
        Canvas.SetTop(_ganttGhostRect, Math.Floor(_ganttCreateStart.Y / GanttRowHeight) * GanttRowHeight + 3);
        GanttBodyCanvas.Children.Add(_ganttGhostRect);
        GanttBodyCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void GanttBodyCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_ganttDragMode != GanttDragMode.Create || _ganttGhostRect is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(GanttBodyCanvas);
        var left = Math.Min(position.X, _ganttCreateStart.X);
        _ganttGhostRect.Width = Math.Max(1, Math.Abs(position.X - _ganttCreateStart.X));
        Canvas.SetLeft(_ganttGhostRect, left);
        _ganttDragMoved = Math.Abs(position.X - _ganttCreateStart.X) >= Math.Max(8, _ganttDayWidth / 2);
        e.Handled = true;
    }

    private void GanttBodyCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_ganttDragMode != GanttDragMode.Create)
        {
            return;
        }

        GanttBodyCanvas.ReleaseMouseCapture();
        var position = e.GetPosition(GanttBodyCanvas);
        var moved = _ganttDragMoved;
        RemoveGanttDragVisuals();
        _ganttDragMode = GanttDragMode.None;
        e.Handled = true;

        if (!moved || GanttViewModel is not { } viewModel)
        {
            RedrawGantt();
            return;
        }

        var startX = Math.Max(0, Math.Min(position.X, _ganttCreateStart.X));
        var finishX = Math.Max(startX, Math.Max(position.X, _ganttCreateStart.X));
        var startDate = _ganttDragRangeStart.AddDays((int)Math.Floor(startX / _ganttDayWidth));
        var finishDate = _ganttDragRangeStart.AddDays((int)Math.Floor(finishX / _ganttDayWidth));
        var calendar = viewModel.ScheduleDataRef.EnsureDefaultCalendar();
        startDate = SchedulingService.RollForward(calendar, startDate);
        var duration = Math.Max(1, SchedulingService.CountWorkingDaysSigned(calendar, startDate, finishDate) + 1);
        var row = Math.Clamp((int)Math.Floor(_ganttCreateStart.Y / GanttRowHeight), 0, viewModel.ScheduleActivities.Count);
        var activity = row < viewModel.ScheduleActivities.Count && viewModel.ScheduleActivities[row].IsUnscheduled
            ? viewModel.ScheduleActivities[row]
            : viewModel.AddScheduleActivityAt(ScheduleActivityKind.Task, row);
        viewModel.ScheduleActivityFromBarDrag(activity, startDate, duration);
        BeginEditingScheduleActivityNameCell(activity);
        RedrawGantt();
    }

    private void RemoveGanttDragVisuals()
    {
        if (_ganttGhostRect is not null)
        {
            GanttBodyCanvas.Children.Remove(_ganttGhostRect);
            _ganttGhostRect = null;
        }

        if (_ganttLinkLine is not null)
        {
            GanttBodyCanvas.Children.Remove(_ganttLinkLine);
            _ganttLinkLine = null;
        }
    }
}
