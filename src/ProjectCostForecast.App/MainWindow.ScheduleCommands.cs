using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private ScheduleComparisonWindow? _scheduleComparisonWindow;

    private void ScheduleActivityPanel_Click(object sender, RoutedEventArgs e)
    {
        var opening = ScheduleActivityPanel.Visibility != Visibility.Visible;
        SetScheduleActivityPanelOpen(opening);
    }

    private void ScheduleActivityPanelClose_Click(object sender, RoutedEventArgs e)
    {
        SetScheduleActivityPanelOpen(false);
    }

    private void SetScheduleActivityPanelOpen(bool open)
    {
        ScheduleActivityPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ScheduleActivityPanelSplitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ScheduleActivityPanelSplitterColumn.Width = open ? new GridLength(8) : new GridLength(0);
        ScheduleActivityPanelColumn.Width = open ? new GridLength(360) : new GridLength(0);
    }

    private void ScheduleInsertAbove_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            BeginEditingScheduleActivityNameCell(viewModel.AddScheduleActivityRelative(ScheduleActivityKind.Task, above: true));
        }
    }

    private void ScheduleInsertBelow_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            BeginEditingScheduleActivityNameCell(viewModel.AddScheduleActivityRelative(ScheduleActivityKind.Task, above: false));
        }
    }

    private void ScheduleBreakLinks_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            foreach (var activity in GetSelectedScheduleActivities())
            {
                viewModel.BreakAllScheduleLinks(activity);
            }
        }
    }

    private void ScheduleCopyLinkSource_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            foreach (var activity in GetSelectedScheduleActivities())
            {
                viewModel.CopyScheduleLinkSource(activity);
            }
        }
    }

    private void SchedulePasteLink_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            var linkedAny = false;
            foreach (var activity in GetSelectedScheduleActivities())
            {
                linkedAny |= viewModel.PasteScheduleLinkTo(activity);
            }

            if (!linkedAny && viewModel.ScheduleLinkClipboardActivities.Count > 0)
            {
                MessageBox.Show(this, viewModel.StatusText, "Paste schedule link", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void SchedulePopoutComparison_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel is { } viewModel)
        {
            ShowScheduleComparisonWindow(viewModel);
        }
    }

    private void ScheduleProgress0_Click(object sender, RoutedEventArgs e) => GanttViewModel?.SetSelectedScheduleProgress(0);
    private void ScheduleProgress25_Click(object sender, RoutedEventArgs e) => GanttViewModel?.SetSelectedScheduleProgress(25);
    private void ScheduleProgress50_Click(object sender, RoutedEventArgs e) => GanttViewModel?.SetSelectedScheduleProgress(50);
    private void ScheduleProgress75_Click(object sender, RoutedEventArgs e) => GanttViewModel?.SetSelectedScheduleProgress(75);
    private void ScheduleProgress100_Click(object sender, RoutedEventArgs e) => GanttViewModel?.SetSelectedScheduleProgress(100);

    private void ScheduleGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (GanttViewModel is not { } viewModel)
        {
            return;
        }

        var header = e.Column.Header?.ToString() ?? string.Empty;
        var isBaselineColumn = string.Equals(header, "BL start", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "BL finish", StringComparison.OrdinalIgnoreCase);
        if (viewModel.IsEditingScheduleBaseline != isBaselineColumn)
        {
            e.Cancel = true;
            viewModel.StatusText = viewModel.IsEditingScheduleBaseline
                ? "Baseline edit mode only permits BL start and BL finish changes."
                : "Switch to SelectedBaseline edit mode to change baseline dates.";
        }
    }

    private void ScheduleGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || FindParent<DataGridRow>(source)?.Item is not ScheduleActivity activity
            || GanttViewModel is not { } viewModel)
        {
            return;
        }

        SelectScheduleActivity(activity, preserveSelection: (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control, scrollIntoView: false);
        var menu = BuildScheduleActivityContextMenu(viewModel, activity);
        menu.PlacementTarget = FindParent<DataGridCell>(source) is { } cell ? cell : ScheduleGrid;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildScheduleActivityContextMenu(MainWindowViewModel viewModel, ScheduleActivity activity)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateScheduleMenuItem("Insert activity above", () => BeginEditingScheduleActivityNameCell(viewModel.AddScheduleActivityRelative(ScheduleActivityKind.Task, true))));
        menu.Items.Add(CreateScheduleMenuItem("Insert activity below", () => BeginEditingScheduleActivityNameCell(viewModel.AddScheduleActivityRelative(ScheduleActivityKind.Task, false))));
        menu.Items.Add(CreateScheduleMenuItem("Make milestone", viewModel.ConvertSelectedScheduleActivityToMilestone,
            activity.Kind != ScheduleActivityKind.Milestone));
        menu.Items.Add(new Separator());

        var progress = new MenuItem { Header = "Set progress" };
        foreach (var percentage in new[] { 0d, 25d, 50d, 75d, 100d })
        {
            progress.Items.Add(CreateScheduleMenuItem($"{percentage:0}%", () => viewModel.SetSelectedScheduleProgress(percentage)));
        }
        menu.Items.Add(progress);

        var link = new MenuItem { Header = "Relationships" };
        link.Items.Add(CreateScheduleMenuItem("Copy as link source", () => viewModel.CopyScheduleLinkSource(activity)));
        var paste = new MenuItem { Header = "Paste link from clipboard" };
        foreach (var linkType in MainWindowViewModel.ScheduleLinkTypeOptions)
        {
            var capturedType = linkType;
            paste.Items.Add(CreateScheduleMenuItem(linkType.ToString(), () =>
            {
                if (!viewModel.PasteScheduleLinkTo(activity, capturedType) && viewModel.ScheduleLinkClipboardActivities.Count > 0)
                {
                    MessageBox.Show(this, viewModel.StatusText, "Paste schedule link", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }));
        }
        link.Items.Add(paste);

        var predecessors = SchedulingService.ParsePredecessors(activity.PredecessorText, out _);
        var breakOne = new MenuItem { Header = "Break predecessor link", IsEnabled = predecessors.Count > 0 };
        foreach (var predecessor in predecessors)
        {
            var predecessorId = predecessor.PredecessorId;
            var predecessorMenu = new MenuItem
            {
                Header = $"{predecessorId} {new ActivityLink { Type = predecessor.Type }.TypeLabel} {predecessor.LagDays:+0;-0;}"
            };
            foreach (var linkType in MainWindowViewModel.ScheduleLinkTypeOptions)
            {
                var capturedType = linkType;
                predecessorMenu.Items.Add(CreateScheduleMenuItem(
                    $"Change to {new ActivityLink { Type = capturedType }.TypeLabel}",
                    () => viewModel.UpdateScheduleLink(activity, predecessorId, capturedType, predecessor.LagDays)));
            }
            predecessorMenu.Items.Add(new Separator());
            predecessorMenu.Items.Add(CreateScheduleMenuItem("Lag -1 day", () => viewModel.UpdateScheduleLink(activity, predecessorId, predecessor.Type, predecessor.LagDays - 1)));
            predecessorMenu.Items.Add(CreateScheduleMenuItem("Lag +1 day", () => viewModel.UpdateScheduleLink(activity, predecessorId, predecessor.Type, predecessor.LagDays + 1)));
            predecessorMenu.Items.Add(CreateScheduleMenuItem("Clear lag", () => viewModel.UpdateScheduleLink(activity, predecessorId, predecessor.Type, 0)));
            predecessorMenu.Items.Add(new Separator());
            predecessorMenu.Items.Add(CreateScheduleMenuItem("Remove relationship", () => viewModel.BreakScheduleLink(activity, predecessorId)));
            breakOne.Items.Add(predecessorMenu);
        }
        link.Items.Add(breakOne);
        link.Items.Add(CreateScheduleMenuItem("Break all incoming and outgoing links", () => viewModel.BreakAllScheduleLinks(activity)));
        link.Items.Add(CreateScheduleMenuItem("Clear link clipboard", viewModel.ClearScheduleLinkClipboard));
        menu.Items.Add(link);

        var constraints = new MenuItem { Header = "Imposed dates" };
        var imposedDate = activity.EarlyStart ?? viewModel.ScheduleDataRef.ProjectStart ?? DateOnly.FromDateTime(DateTime.Today);
        constraints.Items.Add(CreateScheduleMenuItem("Start on current date", () => viewModel.ImposeSelectedScheduleDate(ScheduleConstraintType.StartOn, imposedDate)));
        constraints.Items.Add(CreateScheduleMenuItem("Start on or after current date", () => viewModel.ImposeSelectedScheduleDate(ScheduleConstraintType.StartOnOrAfter, imposedDate)));
        var finishDate = activity.EarlyFinish ?? imposedDate;
        constraints.Items.Add(CreateScheduleMenuItem("Finish on current date", () => viewModel.ImposeSelectedScheduleDate(ScheduleConstraintType.FinishOn, finishDate)));
        constraints.Items.Add(CreateScheduleMenuItem("Clear imposed date", () =>
        {
            activity.ConstraintType = ScheduleConstraintType.None;
            activity.ConstraintDate = null;
        }));
        menu.Items.Add(constraints);

        menu.Items.Add(new Separator());
        menu.Items.Add(CreateScheduleMenuItem("Open baseline comparison", () => ShowScheduleComparisonWindow(viewModel)));
        menu.Items.Add(CreateScheduleMenuItem("Delete selected activity row(s)", () =>
        {
            viewModel.DeleteScheduleActivities(GetSelectedScheduleActivities());
            RestoreScheduleGridSelection();
        }));
        return menu;
    }

    private void ShowScheduleComparisonWindow(MainWindowViewModel viewModel)
    {
        if (_scheduleComparisonWindow is { IsLoaded: true } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        _scheduleComparisonWindow = new ScheduleComparisonWindow(viewModel) { Owner = this };
        _scheduleComparisonWindow.Closed += (_, _) => _scheduleComparisonWindow = null;
        _scheduleComparisonWindow.Show();
    }

    private void ScheduleActivityAddPredecessor_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel?.SelectedScheduleActivity is not { } activity)
        {
            return;
        }

        if (!GanttViewModel.PasteScheduleLinkTo(activity) && GanttViewModel.ScheduleLinkClipboardActivities.Count > 0)
        {
            MessageBox.Show(this, GanttViewModel.StatusText, "Add predecessor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ScheduleActivityDeletePredecessor_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel?.SelectedScheduleActivity is not { } activity
            || ScheduleActivityPredecessorsGrid.SelectedItem is not ActivityLink link)
        {
            return;
        }

        GanttViewModel.BreakScheduleLink(activity, link.PredecessorId);
    }

    private void ScheduleActivityCopyAsSource_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel?.SelectedScheduleActivity is { } activity)
        {
            GanttViewModel.CopyScheduleLinkSource(activity);
        }
    }

    private void ScheduleActivityDeleteSuccessor_Click(object sender, RoutedEventArgs e)
    {
        if (GanttViewModel?.SelectedScheduleActivity is not { } activity
            || ScheduleActivitySuccessorsGrid.SelectedItem is not ActivityLink link)
        {
            return;
        }

        var successor = GanttViewModel.ScheduleActivities.FirstOrDefault(item =>
            string.Equals(item.Id, link.SuccessorId, StringComparison.OrdinalIgnoreCase));
        if (successor is not null)
        {
            GanttViewModel.BreakScheduleLink(successor, activity.Id);
        }
    }

    private void BeginEditingScheduleActivityNameCell(ScheduleActivity activity)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ScheduleGrid.SelectedItem = activity;
            if (GanttViewModel is { } viewModel)
            {
                viewModel.SelectedScheduleActivity = activity;
            }

            ScheduleGrid.ScrollIntoView(activity);
            var nameColumn = ScheduleGrid.Columns.FirstOrDefault(column =>
                string.Equals(column.Header?.ToString(), "Activity name", StringComparison.OrdinalIgnoreCase))
                ?? ScheduleGrid.Columns.ElementAtOrDefault(1);
            if (nameColumn is null)
            {
                return;
            }

            ScheduleGrid.ScrollIntoView(activity, nameColumn);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ScheduleGrid.UpdateLayout();
                ScheduleGrid.CurrentCell = new DataGridCellInfo(activity, nameColumn);
                if (ScheduleGrid.ItemContainerGenerator.ContainerFromItem(activity) is not DataGridRow row)
                {
                    return;
                }

                var presenter = FindChild<DataGridCellsPresenter>(row);
                var cell = presenter?.ItemContainerGenerator.ContainerFromIndex(nameColumn.DisplayIndex) as DataGridCell;
                cell?.Focus();
                if (ScheduleGrid.BeginEdit())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var editor = cell is null ? null : FindChild<TextBox>(cell);
                        editor?.Focus();
                        editor?.SelectAll();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private static MenuItem CreateScheduleMenuItem(string header, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }
}
