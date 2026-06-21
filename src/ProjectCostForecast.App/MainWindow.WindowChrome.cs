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

public partial class MainWindow
{
    private void ApplyWindowPreferences()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        WindowState = viewModel.StartInFullScreen ? WindowState.Maximized : WindowState.Normal;

        ApplyDetailWorkspaceAvailability(viewModel);
    }

    private void KpiCard_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_kpiRightDragging || DataContext is not MainWindowViewModel viewModel || sender is not FrameworkElement element || !int.TryParse(element.Tag?.ToString(), out var pillId))
        {
            return;
        }

        OpenKpiContextMenu(element, viewModel, pillId);
        e.Handled = true;
    }

    private void KpiStrip_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_kpiRightDragging || DataContext is not MainWindowViewModel viewModel || sender is not FrameworkElement element)
        {
            return;
        }

        OpenKpiContextMenu(element, viewModel, pillId: null);
        e.Handled = true;
    }

    private void OpenKpiContextMenu(FrameworkElement element, MainWindowViewModel viewModel, int? pillId)
    {
        var menu = BuildKpiContextMenu(viewModel, pillId);
        menu.PlacementTarget = element;
        menu.Placement = PlacementMode.MousePoint;
        element.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private ContextMenu BuildKpiContextMenu(MainWindowViewModel viewModel, int? pillId)
    {
        var menu = CreateColumnContextMenu();
        foreach (var option in viewModel.KpiOptions)
        {
            var isActive = viewModel.IsKpiPillActive(option.Key);
            var item = new MenuItem
            {
                Header = option.Name,
                IsCheckable = true,
                IsChecked = isActive
            };
            item.Click += (_, _) => viewModel.SetKpiPillActive(option.Key, !isActive);
            menu.Items.Add(item);
        }

        if (pillId.HasValue)
        {
            var selectedOption = viewModel.GetSelectedKpi(pillId.Value);
            if (selectedOption is not null)
            {
                menu.Items.Add(new Separator());
                var changeIcon = new MenuItem
                {
                    Header = "Change icon"
                };
                changeIcon.Click += (_, _) => ExecuteAfterClosingMenu(changeIcon, () => OpenKpiIconPicker(pillId.Value));
                menu.Items.Add(changeIcon);
            }

            menu.Items.Add(new Separator());
            var removeItem = new MenuItem
            {
                Header = "Remove pill",
                IsEnabled = viewModel.KpiPills.Count > 1
            };
            removeItem.Click += (_, _) => viewModel.RemoveKpiPill(pillId.Value);
            menu.Items.Add(removeItem);
        }

        ApplyMenuIcons(menu);
        return menu;
    }

    private void KpiScrollViewer_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        _kpiRightDragStart = e.GetPosition(scrollViewer);
        _kpiScrollStartOffset = scrollViewer.HorizontalOffset;
        _kpiRightDragging = false;
    }

    private void KpiScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_kpiRightDragStart is null || sender is not ScrollViewer scrollViewer || e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(scrollViewer);
        var delta = current.X - _kpiRightDragStart.Value.X;
        if (!_kpiRightDragging && Math.Abs(delta) < 6)
        {
            return;
        }

        _kpiRightDragging = true;
        scrollViewer.ScrollToHorizontalOffset(Math.Max(0, _kpiScrollStartOffset - delta));
        e.Handled = true;
    }

    private void KpiScrollViewer_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _kpiRightDragStart = null;
        Dispatcher.BeginInvoke(() => _kpiRightDragging = false);
    }

    private void KpiCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_kpiRightDragging || sender is not FrameworkElement element || !int.TryParse(element.Tag?.ToString(), out var pillId))
        {
            return;
        }

        _kpiLeftDragStart = e.GetPosition(KpiScrollViewer);
        _kpiDragPillId = pillId;
    }

    private void KpiCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_kpiRightDragging
            || _kpiLeftDragStart is null
            || _kpiDragPillId is null
            || e.LeftButton != MouseButtonState.Pressed
            || sender is not FrameworkElement element)
        {
            return;
        }

        var current = e.GetPosition(KpiScrollViewer);
        if (Math.Abs(current.X - _kpiLeftDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _kpiLeftDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(typeof(int), _kpiDragPillId.Value);
        try
        {
            DimDraggedElement(element);
            DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
        }
        finally
        {
            RestoreDimmedDragElement();
            _kpiLeftDragStart = null;
            _kpiDragPillId = null;
        }

        e.Handled = true;
    }

    private void KpiCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _kpiLeftDragStart = null;
        _kpiDragPillId = null;
    }

    private void KpiCard_DragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !e.Data.GetDataPresent(typeof(int)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(typeof(int)) is int sourcePillId)
        {
            MoveKpiPillByPointer(viewModel, sourcePillId, e.GetPosition(KpiScrollViewer).X);
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void KpiCard_DragLeave(object sender, DragEventArgs e)
    {
    }

    private void KpiCard_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !e.Data.GetDataPresent(typeof(int)))
        {
            return;
        }

        if (e.Data.GetData(typeof(int)) is int sourcePillId)
        {
            MoveKpiPillByPointer(viewModel, sourcePillId, e.GetPosition(KpiScrollViewer).X);
        }

        e.Handled = true;
    }

    private void KpiStrip_DragOver(object sender, DragEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel
            && e.Data.GetDataPresent(typeof(int))
            && e.Data.GetData(typeof(int)) is int sourcePillId)
        {
            MoveKpiPillByPointer(viewModel, sourcePillId, e.GetPosition(KpiScrollViewer).X);
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void KpiStrip_DragLeave(object sender, DragEventArgs e)
    {
    }

    private void KpiStrip_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !e.Data.GetDataPresent(typeof(int)))
        {
            return;
        }

        if (e.Data.GetData(typeof(int)) is int sourcePillId)
        {
            MoveKpiPillByPointer(viewModel, sourcePillId, e.GetPosition(KpiScrollViewer).X);
        }

        e.Handled = true;
    }

    private void MoveKpiPillByPointer(MainWindowViewModel viewModel, int sourcePillId, double pointerX)
    {
        var targetIndex = 0;
        foreach (var pill in viewModel.KpiPills)
        {
            if (pill.Id == sourcePillId)
            {
                continue;
            }

            if (KpiItemsControl.ItemContainerGenerator.ContainerFromItem(pill) is not FrameworkElement container)
            {
                continue;
            }

            var centerX = container.TransformToAncestor(KpiScrollViewer).Transform(new Point(container.ActualWidth / 2, 0)).X;
            if (pointerX > centerX)
            {
                targetIndex++;
            }
        }

        viewModel.MoveKpiPillToIndex(sourcePillId, targetIndex);
    }

    private void LedgerChartScrollViewer_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        _ledgerChartRightDragStart = e.GetPosition(scrollViewer);
        _ledgerChartScrollStartOffset = scrollViewer.HorizontalOffset;
        _ledgerChartRightDragging = false;
        scrollViewer.CaptureMouse();
    }

    private void LedgerChartScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_ledgerChartRightDragStart is null || sender is not ScrollViewer scrollViewer || e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(scrollViewer);
        var delta = current.X - _ledgerChartRightDragStart.Value.X;
        if (!_ledgerChartRightDragging && Math.Abs(delta) < 6)
        {
            return;
        }

        _ledgerChartRightDragging = true;
        scrollViewer.ScrollToHorizontalOffset(Math.Max(0, _ledgerChartScrollStartOffset - delta));
        e.Handled = true;
    }

    private void LedgerChartScrollViewer_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && _ledgerChartRightDragStart is not null)
        {
            scrollViewer.ReleaseMouseCapture();
        }

        _ledgerChartRightDragStart = null;
        Dispatcher.BeginInvoke(() => _ledgerChartRightDragging = false);
    }

    private void Grid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid sourceGrid
            && e.OriginalSource is DependencyObject interactionSource
            && IsScrollBarInteractionSource(interactionSource))
        {
            CancelGridPanCapture(sourceGrid);
            return;
        }

        if (e.ChangedButton != MouseButton.Right
            || sender is not DataGrid grid
            || e.OriginalSource is not DependencyObject source
            || FindParent<DataGridColumnHeader>(source) is not null)
        {
            return;
        }

        var scrollViewer = FindChild<ScrollViewer>(grid);
        if (scrollViewer is null)
        {
            return;
        }

        _activeGridScrollViewer = scrollViewer;
        _gridRightDragStart = e.GetPosition(grid);
        _gridHorizontalScrollStartOffset = scrollViewer.HorizontalOffset;
        _gridVerticalScrollStartOffset = scrollViewer.VerticalOffset;
        _gridRightDragging = false;
    }

    private void CancelGridPanCapture(DataGrid? grid)
    {
        if (grid?.IsMouseCaptured == true)
        {
            grid.ReleaseMouseCapture();
        }

        _gridRightDragStart = null;
        _activeGridScrollViewer = null;
        _gridRightDragging = false;
    }

    private static bool IsScrollBarInteractionSource(DependencyObject source)
    {
        return FindParent<ScrollBar>(source) is not null;
    }

    private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_gridRightDragStart is null || _activeGridScrollViewer is null || sender is not DataGrid grid || e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(grid);
        var deltaX = current.X - _gridRightDragStart.Value.X;
        var deltaY = current.Y - _gridRightDragStart.Value.Y;
        if (!_gridRightDragging && Math.Abs(deltaX) < 6 && Math.Abs(deltaY) < 6)
        {
            return;
        }

        _gridRightDragging = true;
        if (!grid.IsMouseCaptured)
        {
            grid.CaptureMouse();
        }
        _activeGridScrollViewer.ScrollToHorizontalOffset(Math.Max(0, _gridHorizontalScrollStartOffset - deltaX));
        _activeGridScrollViewer.ScrollToVerticalOffset(Math.Max(0, _gridVerticalScrollStartOffset - deltaY));
        e.Handled = true;
    }

    private void Grid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right)
        {
            return;
        }

        if (sender is DataGrid grid && grid.IsMouseCaptured)
        {
            grid.ReleaseMouseCapture();
        }

        var wasDragging = _gridRightDragging;
        _gridRightDragStart = null;
        _activeGridScrollViewer = null;
        if (wasDragging)
        {
            e.Handled = true;
        }
        Dispatcher.BeginInvoke(() => _gridRightDragging = false);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ForecastLine)))
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.Any(IsSupportedImportFile))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ForecastLine)))
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel
            || !e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        var importFile = files.FirstOrDefault(IsSupportedImportFile);
        if (string.IsNullOrWhiteSpace(importFile))
        {
            MessageBox.Show(this, "Drop a supported import file: .csv, .xlsx, or .xlsm.", "Import file", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        viewModel.ImportTransactionFile(importFile);
        e.Handled = true;
    }
}
