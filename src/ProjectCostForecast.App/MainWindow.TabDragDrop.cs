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
    private void WorkspaceTabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _workspaceTabDragStart = null;
        _workspaceDraggedTabItem = null;

        if (sender is not TabControl tabControl || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var tabItem = FindParent<TabItem>(source);
        // The drill-down tabs are the headings above the detail grids. Do
        // not start a tab reorder when the pointer began inside a grid or
        // another tab's content; that made normal cell selection feel like a
        // tab drag. Tab headers are hosted by the TabPanel in the shared tab
        // template, so this also keeps the gesture consistent with the view
        // pills, which start only from their item surface.
        if (FindParent<TabPanel>(source) is null
            || tabItem is null
            || !tabControl.Items.Contains(tabItem))
        {
            return;
        }

        _workspaceTabDragStart = e.GetPosition(tabControl);
        _workspaceDraggedTabItem = tabItem;
    }

    private void WorkspaceTabs_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_workspaceTabDragStart is null
            || _workspaceDraggedTabItem is null
            || sender is not TabControl tabControl
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(tabControl);
        if (Math.Abs(current.X - _workspaceTabDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _workspaceTabDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DimDraggedElement(_workspaceDraggedTabItem);
        tabControl.CaptureMouse();
        if (tabControl.InputHitTest(current) is DependencyObject source)
        {
            var targetTab = FindParent<TabItem>(source);
            var targetPosition = targetTab is null ? e.GetPosition(tabControl) : e.GetPosition(targetTab);
            var targetIndex = GetTabDropIndex(tabControl, source, targetPosition);
            if (MoveTabItem(tabControl, _workspaceDraggedTabItem, targetIndex) && DataContext is MainWindowViewModel viewModel)
            {
                PersistWorkspaceTabOrder(tabControl, viewModel);

                var insertionX = GetTabInsertionLineX(tabControl, targetTab, targetPosition);
                EnsureWorkspaceTabReorderAdorner(tabControl);
                _workspaceTabReorderAdorner?.SetPosition(insertionX, GetTabHeaderHeight(tabControl, targetTab));
            }
            else
            {
                RemoveWorkspaceTabReorderAdorner();
            }
        }
        else
        {
            RemoveWorkspaceTabReorderAdorner();
        }

        _workspaceTabDragStart = current;
        e.Handled = true;
    }

    private void WorkspaceTabs_DragOver(object sender, DragEventArgs e)
    {
        if (sender is TabControl tabControl
            && DataContext is MainWindowViewModel viewModel
            && e.Data.GetData(typeof(TabItem)) is TabItem draggedTab
            && tabControl.Items.Contains(draggedTab)
            && e.OriginalSource is DependencyObject source)
        {
            e.Effects = DragDropEffects.Move;
            var targetIndex = GetTabDropIndex(tabControl, source, e);
            if (MoveTabItem(tabControl, draggedTab, targetIndex))
            {
                PersistWorkspaceTabOrder(tabControl, viewModel);
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void WorkspaceTabs_Drop(object sender, DragEventArgs e)
    {
        if (sender is not TabControl tabControl
            || e.Data.GetData(typeof(TabItem)) is not TabItem draggedTab
            || !tabControl.Items.Contains(draggedTab)
            || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var targetIndex = GetTabDropIndex(tabControl, source, e);

        if (MoveTabItem(tabControl, draggedTab, targetIndex) && DataContext is MainWindowViewModel viewModel)
        {
            PersistWorkspaceTabOrder(tabControl, viewModel);
        }

        e.Handled = true;
    }

    private void WorkspaceViewTabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _workspaceViewDragStart = null;
        _workspaceDraggedView = null;

        if (sender is not ListBox listBox || e.OriginalSource is not DependencyObject source || FindParent<TextBox>(source) is not null)
        {
            return;
        }

        var listBoxItem = FindParent<ListBoxItem>(source);
        if (listBoxItem?.DataContext is not WorkspaceViewTab view || !listBox.Items.Contains(view))
        {
            return;
        }

        _workspaceViewDragStart = e.GetPosition(listBox);
        _workspaceDraggedView = view;
    }

    private void WorkspaceViewTabs_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_workspaceViewDragStart is null
            || _workspaceDraggedView is null
            || sender is not ListBox listBox
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(listBox);
        if (Math.Abs(current.X - _workspaceViewDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _workspaceViewDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DimDraggedElement(listBox.ItemContainerGenerator.ContainerFromItem(_workspaceDraggedView) as UIElement);
        listBox.CaptureMouse();
        if (listBox.InputHitTest(current) is DependencyObject source && DataContext is MainWindowViewModel viewModel)
        {
            var targetItem = FindParent<ListBoxItem>(source);
            var targetPosition = targetItem is null ? e.GetPosition(listBox) : e.GetPosition(targetItem);
            viewModel.ReorderWorkspaceView(
                _workspaceDraggedView,
                GetWorkspaceViewDropIndex(listBox, source, targetPosition),
                IsDetailWorkspaceViewListBox(listBox, viewModel));
        }

        _workspaceViewDragStart = current;
        e.Handled = true;
    }

    private void WorkspaceTabs_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        RestoreDimmedDragElement();
        _workspaceTabDragStart = null;
        _workspaceDraggedTabItem = null;
        RemoveWorkspaceTabReorderAdorner();
    }

    private void WorkspaceViewTabs_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        RestoreDimmedDragElement();
        _workspaceViewDragStart = null;
        _workspaceDraggedView = null;
    }

    private void WorkspaceViewTabs_DragOver(object sender, DragEventArgs e)
    {
        if (sender is ListBox listBox
            && DataContext is MainWindowViewModel viewModel
            && e.Data.GetData(typeof(WorkspaceViewTab)) is WorkspaceViewTab view
            && listBox.Items.Contains(view)
            && e.OriginalSource is DependencyObject source)
        {
            e.Effects = DragDropEffects.Move;
            viewModel.ReorderWorkspaceView(view, GetWorkspaceViewDropIndex(listBox, source, e), IsDetailWorkspaceViewListBox(listBox, viewModel));
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void WorkspaceViewTabs_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox
            || DataContext is not MainWindowViewModel viewModel
            || e.Data.GetData(typeof(WorkspaceViewTab)) is not WorkspaceViewTab draggedView
            || !listBox.Items.Contains(draggedView)
            || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        viewModel.ReorderWorkspaceView(draggedView, GetWorkspaceViewDropIndex(listBox, source, e), IsDetailWorkspaceViewListBox(listBox, viewModel));
        e.Handled = true;
    }

    private static int GetTabDropIndex(TabControl tabControl, DependencyObject source, DragEventArgs e)
    {
        var targetTab = FindParent<TabItem>(source);
        if (targetTab is null || !tabControl.Items.Contains(targetTab))
        {
            return tabControl.Items.Count;
        }

        var targetIndex = tabControl.Items.IndexOf(targetTab);
        return e.GetPosition(targetTab).X > targetTab.ActualWidth / 2
            ? targetIndex + 1
            : targetIndex;
    }

    private static int GetTabDropIndex(TabControl tabControl, DependencyObject source, Point position)
    {
        var targetTab = FindParent<TabItem>(source);
        if (targetTab is null || !tabControl.Items.Contains(targetTab))
        {
            return tabControl.Items.Count;
        }

        var targetIndex = tabControl.Items.IndexOf(targetTab);
        return position.X > targetTab.ActualWidth / 2 ? targetIndex + 1 : targetIndex;
    }

    private static double GetTabInsertionLineX(TabControl tabControl, TabItem? targetTab, Point targetPosition)
    {
        if (targetTab is not null && tabControl.Items.Contains(targetTab))
        {
            var insertAfter = targetPosition.X > targetTab.ActualWidth / 2;
            var edge = targetTab.TranslatePoint(
                new Point(insertAfter ? targetTab.ActualWidth : 0, 0),
                tabControl);
            return edge.X;
        }

        var lastTab = tabControl.Items.OfType<TabItem>().LastOrDefault();
        if (lastTab is null)
        {
            return 0;
        }

        return lastTab.TranslatePoint(new Point(lastTab.ActualWidth, 0), tabControl).X;
    }

    private static double GetTabHeaderHeight(TabControl tabControl, TabItem? targetTab)
    {
        var tabPanel = targetTab is not null
            ? FindParent<TabPanel>(targetTab)
            : FindChildren<TabPanel>(tabControl).FirstOrDefault();
        return Math.Max(1, tabPanel?.ActualHeight ?? targetTab?.ActualHeight ?? 34);
    }

    private void EnsureWorkspaceTabReorderAdorner(TabControl tabControl)
    {
        if (_workspaceTabReorderAdorner is not null)
        {
            return;
        }

        if (AdornerLayer.GetAdornerLayer(tabControl) is { } layer)
        {
            _workspaceTabReorderAdorner = new TabReorderAdorner(tabControl);
            layer.Add(_workspaceTabReorderAdorner);
        }
    }

    private void RemoveWorkspaceTabReorderAdorner()
    {
        if (_workspaceTabReorderAdorner is null)
        {
            return;
        }

        if (AdornerLayer.GetAdornerLayer(_workspaceTabReorderAdorner.AdornedElement) is { } layer)
        {
            layer.Remove(_workspaceTabReorderAdorner);
        }

        _workspaceTabReorderAdorner = null;
    }

    private static int GetWorkspaceViewDropIndex(ListBox listBox, DependencyObject source, DragEventArgs e)
    {
        var targetItem = FindParent<ListBoxItem>(source);
        if (targetItem?.DataContext is not WorkspaceViewTab targetView || !listBox.Items.Contains(targetView))
        {
            return listBox.Items.Count;
        }

        var targetIndex = listBox.Items.IndexOf(targetView);
        return e.GetPosition(targetItem).X > targetItem.ActualWidth / 2
            ? targetIndex + 1
            : targetIndex;
    }

    private static int GetWorkspaceViewDropIndex(ListBox listBox, DependencyObject source, Point position)
    {
        var targetItem = FindParent<ListBoxItem>(source);
        if (targetItem?.DataContext is not WorkspaceViewTab targetView || !listBox.Items.Contains(targetView))
        {
            return listBox.Items.Count;
        }

        var targetIndex = listBox.Items.IndexOf(targetView);
        return position.X > targetItem.ActualWidth / 2 ? targetIndex + 1 : targetIndex;
    }

    private void DimDraggedElement(UIElement? element)
    {
        _dimmedDragElement = element;
        if (_dimmedDragElement is not null)
        {
            _dimmedDragElement.Opacity = 0.6;
        }
    }

    private void RestoreDimmedDragElement()
    {
        if (_dimmedDragElement is not null)
        {
            _dimmedDragElement.Opacity = 1;
            _dimmedDragElement = null;
        }
    }

    private void ApplySavedWorkspaceTabOrders()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        ApplySavedTabOrder(WorkspaceTabControl, viewModel.WorkspaceTabOrder);
        ApplySavedTabOrder(LedgerWorkspaceTabControl, viewModel.DetailWorkspaceTabOrder);
    }

    private void ApplySavedTabOrder(TabControl tabControl, IReadOnlyList<string> orderedKeys)
    {
        if (orderedKeys.Count == 0)
        {
            return;
        }

        var originalSelection = tabControl.SelectedItem;
        var orderedItems = tabControl.Items
            .OfType<TabItem>()
            .OrderBy(tab =>
            {
                var index = orderedKeys
                    .Select((key, i) => new { key, i })
                    .FirstOrDefault(item => string.Equals(item.key, GetWorkspaceTabPersistenceKey(tabControl, tab), StringComparison.OrdinalIgnoreCase))
                    ?.i;
                return index ?? int.MaxValue;
            })
            .ToList();

        if (ReferenceEquals(tabControl, WorkspaceTabControl)
            && !orderedKeys.Contains("Budget", StringComparer.OrdinalIgnoreCase)
            && orderedItems.FirstOrDefault(tab => string.Equals(GetWorkspaceTabPersistenceKey(tabControl, tab), "Budget", StringComparison.OrdinalIgnoreCase)) is { } budgetTab)
        {
            orderedItems.Remove(budgetTab);
            var auditIndex = orderedItems.FindIndex(tab => string.Equals(GetWorkspaceTabPersistenceKey(tabControl, tab), "Audit", StringComparison.OrdinalIgnoreCase));
            orderedItems.Insert(auditIndex >= 0 ? auditIndex + 1 : orderedItems.Count, budgetTab);
        }

        if (orderedItems.Count == 0 || orderedItems.SequenceEqual(tabControl.Items.OfType<TabItem>()))
        {
            return;
        }

        tabControl.Items.Clear();
        foreach (var tabItem in orderedItems)
        {
            tabControl.Items.Add(tabItem);
        }

        tabControl.SelectedItem = originalSelection is not null && tabControl.Items.Contains(originalSelection)
            ? originalSelection
            : tabControl.Items.OfType<TabItem>().FirstOrDefault();
    }

    private static bool MoveTabItem(TabControl tabControl, TabItem tabItem, int targetIndex)
    {
        var oldIndex = tabControl.Items.IndexOf(tabItem);
        if (oldIndex < 0)
        {
            return false;
        }

        targetIndex = Math.Clamp(targetIndex, 0, tabControl.Items.Count);
        if (oldIndex < targetIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Clamp(targetIndex, 0, tabControl.Items.Count - 1);
        if (oldIndex == targetIndex)
        {
            return false;
        }

        var selectedItem = tabControl.SelectedItem;
        tabControl.Items.RemoveAt(oldIndex);
        tabControl.Items.Insert(targetIndex, tabItem);
        tabControl.SelectedItem = selectedItem is not null && tabControl.Items.Contains(selectedItem)
            ? selectedItem
            : tabItem;
        return true;
    }

    private void PersistWorkspaceTabOrder(TabControl tabControl, MainWindowViewModel viewModel)
    {
        viewModel.SetWorkspaceTabOrder(
            tabControl.Items.OfType<TabItem>().Select(tab => GetWorkspaceTabPersistenceKey(tabControl, tab)),
            ReferenceEquals(tabControl, LedgerWorkspaceTabControl));
    }

    private string GetWorkspaceTabPersistenceKey(TabControl tabControl, TabItem tabItem)
    {
        var header = tabItem.Tag?.ToString() ?? tabItem.Header?.ToString() ?? string.Empty;
        return ReferenceEquals(tabControl, LedgerWorkspaceTabControl)
            ? GetDetailWorkspaceKeyFromTabHeader(header)
            : header;
    }

    private static bool IsDetailWorkspaceViewListBox(ListBox listBox, MainWindowViewModel viewModel)
    {
        return ReferenceEquals(listBox.ItemsSource, viewModel.CurrentDetailWorkspaceViews);
    }

    private static string GetDetailWorkspaceKeyFromTabHeader(string? header)
    {
        return header switch
        {
            "Monthly forecast" or "Monthly Forecast" => "Ledger Monthly Forecast",
            "Spend curve" or "Spend Curve" => "Ledger Spend Curve",
            _ => "Ledger Costs"
        };
    }

    private sealed class TabReorderAdorner : Adorner
    {
        private double _x;
        private double _height;

        public TabReorderAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public void SetPosition(double x, double height)
        {
            _x = Math.Max(0, x);
            _height = Math.Max(1, height);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var fill = new SolidColorBrush(Color.FromArgb(45, 37, 99, 235));
            fill.Freeze();
            var pen = new Pen(BrushFactory.Frozen("#2563EB"), 2);
            pen.Freeze();
            drawingContext.DrawRectangle(fill, null, new Rect(_x - 3, 0, 6, _height));
            drawingContext.DrawLine(pen, new Point(_x, 0), new Point(_x, _height));
        }
    }
}
