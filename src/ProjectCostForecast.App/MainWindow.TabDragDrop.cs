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
        if (tabItem is null || !tabControl.Items.Contains(tabItem))
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
            }
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
}
