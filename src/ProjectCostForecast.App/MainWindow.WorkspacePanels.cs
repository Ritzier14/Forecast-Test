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
    private void WorkspaceViewName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is TextBox textBox && textBox.DataContext is WorkspaceViewTab view)
        {
            SetWorkspaceViewEditingWidthLock(textBox, isEditing: false);
            viewModel.EndRenameWorkspaceView(view);
        }
    }

    private void WorkspaceViewName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel viewModel && sender is TextBox textBox && textBox.DataContext is WorkspaceViewTab view)
        {
            SetWorkspaceViewEditingWidthLock(textBox, isEditing: false);
            viewModel.EndRenameWorkspaceView(view);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void WorkspaceViewName_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateWorkspaceViewEditorWidth(textBox);
        }
    }

    private void WorkspaceViewName_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox || e.NewValue is not bool isVisible || !isVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            SetWorkspaceViewEditingWidthLock(textBox, isEditing: true);
            UpdateWorkspaceViewEditorWidth(textBox);
            textBox.Focus();
            textBox.SelectAll();
        }));
    }

    private void WorkspaceViewName_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateWorkspaceViewEditorWidth(textBox);
            textBox.SelectAll();
        }
    }

    private static void UpdateWorkspaceViewEditorWidth(TextBox textBox)
    {
        var text = string.IsNullOrWhiteSpace(textBox.Text)
            ? " "
            : textBox.Text;
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            textBox.FlowDirection,
            new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
            textBox.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(textBox).PixelsPerDip);
        var chromeWidth = textBox.Padding.Left + textBox.Padding.Right + WorkspaceViewEditorExtraWidth;
        textBox.Width = Math.Max(WorkspaceViewEditorMinimumWidth, Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace + chromeWidth));
    }

    private static void SetWorkspaceViewEditingWidthLock(TextBox textBox, bool isEditing)
    {
        if (FindParent<ListBoxItem>(textBox) is not ListBoxItem item)
        {
            return;
        }

        if (isEditing)
        {
            item.MinWidth = Math.Max(item.ActualWidth, item.MinWidth);
        }
        else
        {
            item.ClearValue(FrameworkElement.MinWidthProperty);
        }
    }

    private void QueueAttachInteractiveGridHandlers()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            AttachColumnMenus(this);
            ApplyDefaultColumnPresentation(this);
            AttachGridPanHandlers(this);
            AttachSpreadsheetGridHandlers(this);
        }));
    }

    private void HideDetailWorkspacePanel_Click(object sender, RoutedEventArgs e)
    {
        CollapseDetailWorkspacePanel();
    }

    private void DetailWorkspaceCollapsedTab_Click(object sender, RoutedEventArgs e)
    {
        ExpandDetailWorkspacePanel();
    }

    private void CollapseDetailWorkspacePanel()
    {
        if (DetailWorkspaceColumn.Width.Value > 0)
        {
            _detailWorkspaceExpandedWidth = DetailWorkspaceColumn.Width;
        }

        DetailWorkspacePanel.Visibility = Visibility.Collapsed;
        WorkspaceGridSplitter.Visibility = Visibility.Collapsed;
        WorkspaceSplitterColumn.Width = new GridLength(0);
        DetailWorkspaceContentColumn.Width = new GridLength(0);
        DetailWorkspaceColumn.Width = new GridLength(68);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetDetailPanelCollapsed(true);
        }
    }

    private void ExpandDetailWorkspacePanel()
    {
        DetailWorkspacePanel.Visibility = Visibility.Visible;
        WorkspaceGridSplitter.Visibility = Visibility.Visible;
        DetailWorkspaceContentColumn.Width = new GridLength(1, GridUnitType.Star);
        WorkspaceSplitterColumn.Width = new GridLength(12);
        DetailWorkspaceColumn.Width = _detailWorkspaceExpandedWidth.Value > 0
            ? _detailWorkspaceExpandedWidth
            : new GridLength(1.25, GridUnitType.Star);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetDetailPanelCollapsed(false);
        }
    }
}
