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
            if (viewModel.EndRenameWorkspaceView(view))
            {
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    UpdateWorkspaceViewEditorWidth(textBox);
                    textBox.Focus();
                    textBox.SelectAll();
                }));
            }
        }
    }

    private void WorkspaceViewName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel viewModel && sender is TextBox textBox && textBox.DataContext is WorkspaceViewTab view)
        {
            if (viewModel.EndRenameWorkspaceView(view))
            {
                Keyboard.ClearFocus();
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    UpdateWorkspaceViewEditorWidth(textBox);
                    textBox.Focus();
                    textBox.SelectAll();
                }));
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape && DataContext is MainWindowViewModel escapeViewModel && sender is TextBox escapeTextBox && escapeTextBox.DataContext is WorkspaceViewTab escapeView)
        {
            escapeViewModel.CancelRenameWorkspaceView(escapeView);
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
            UpdateWorkspaceViewEditorWidth(textBox);
            textBox.HorizontalAlignment = HorizontalAlignment.Left;

            if (_pendingWorkspaceEditorFocusView is not null
                && !ReferenceEquals(textBox.DataContext, _pendingWorkspaceEditorFocusView))
            {
                return;
            }

            textBox.Focus();
            textBox.SelectAll();
            if (ReferenceEquals(textBox.DataContext, _pendingWorkspaceEditorFocusView))
            {
                _pendingWorkspaceEditorFocusView = null;
            }
        }));
    }

    private void WorkspaceViewName_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateWorkspaceViewEditorWidth(textBox);
            textBox.HorizontalAlignment = HorizontalAlignment.Left;
            textBox.SelectAll();
        }
    }

    private void WorkspaceViewName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.IsVisible)
        {
            UpdateWorkspaceViewEditorWidth(textBox);
        }
    }

    private void AddWorkspaceViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.AddWorkspaceViewCommand.CanExecute(null))
        {
            viewModel.AddWorkspaceViewCommand.Execute(null);
            _pendingWorkspaceEditorFocusView = viewModel.SelectedWorkspaceView;
            QueueFocusPendingWorkspaceViewEditor();
        }
    }

    private void AddDetailWorkspaceViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.AddDetailWorkspaceViewCommand.CanExecute(null))
        {
            viewModel.AddDetailWorkspaceViewCommand.Execute(null);
            _pendingWorkspaceEditorFocusView = viewModel.SelectedDetailWorkspaceView;
            QueueFocusPendingWorkspaceViewEditor();
        }
    }

    private void QueueFocusPendingWorkspaceViewEditor()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_pendingWorkspaceEditorFocusView is null)
            {
                return;
            }

            var editor = FindChildren<TextBox>(this)
                .FirstOrDefault(textBox => textBox.IsVisible && ReferenceEquals(textBox.DataContext, _pendingWorkspaceEditorFocusView));
            if (editor is null)
            {
                return;
            }

            UpdateWorkspaceViewEditorWidth(editor);
            editor.HorizontalAlignment = HorizontalAlignment.Left;
            editor.Focus();
            editor.SelectAll();
            _pendingWorkspaceEditorFocusView = null;
        }));
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

    private void CollapsedDetailWorkspaceHost_MouseEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is MainWindowViewModel { IsDetailPanelCollapsed: true }
            && !IsDetailWorkspaceSuppressed())
        {
            ShowTransientDetailWorkspacePanel();
        }
    }

    private void CollapsedDetailWorkspaceHost_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is MainWindowViewModel { IsDetailPanelCollapsed: true }
            && !IsMouseOverDetailWorkspace())
        {
            CollapseDetailWorkspacePanel();
        }
    }

    private bool IsMouseOverDetailWorkspace()
        => DetailWorkspaceShell.IsMouseOver || CollapsedDetailWorkspaceHost.IsMouseOver;

    private void CollapseDetailWorkspacePanel()
    {
        if (DetailWorkspaceColumn.Width.Value > 0)
        {
            _detailWorkspaceExpandedWidth = DetailWorkspaceColumn.Width;
        }

        DetailWorkspaceShell.Visibility = Visibility.Collapsed;
        CollapsedDetailWorkspaceHost.Visibility = Visibility.Visible;
        DetailWorkspacePanel.Visibility = Visibility.Collapsed;
        DetailWorkspaceShell.Background = Brushes.Transparent;
        DetailWorkspaceShell.BorderBrush = Brushes.Transparent;
        DetailWorkspaceShell.BorderThickness = new Thickness(0);
        DetailWorkspaceShell.Padding = new Thickness(0);
        DetailWorkspaceShell.CornerRadius = new CornerRadius(0);
        DetailWorkspaceRail.Margin = new Thickness(0);
        DetailWorkspaceRail.Background = Brushes.Transparent;
        DetailWorkspaceRail.BorderBrush = Brushes.Transparent;
        DetailWorkspaceRail.BorderThickness = new Thickness(0);
        DetailWorkspaceRail.CornerRadius = new CornerRadius(0);
        DetailWorkspaceRail.Padding = new Thickness(0);
        DetailWorkspaceRail.Width = 32;
        DetailWorkspaceCollapsedTab.Background = BrushFactory.Frozen("#F8FAFC");
        DetailWorkspaceCollapsedTab.BorderBrush = BrushFactory.Frozen("#D7E0EA");
        DetailWorkspaceCollapsedTab.BorderThickness = new Thickness(1);
        WorkspaceGridSplitter.Visibility = Visibility.Collapsed;
        WorkspaceSplitterColumn.Width = new GridLength(0);
        DetailWorkspaceContentColumn.Width = new GridLength(0);
        DetailWorkspaceColumn.Width = new GridLength(40);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetDetailPanelCollapsed(true);
        }
    }

    private void ShowTransientDetailWorkspacePanel()
    {
        DetailWorkspaceShell.Visibility = Visibility.Visible;
        CollapsedDetailWorkspaceHost.Visibility = Visibility.Visible;
        DetailWorkspacePanel.Visibility = Visibility.Visible;
        DetailWorkspaceShell.Background = BrushFactory.Frozen("#F8FAFD");
        DetailWorkspaceShell.BorderBrush = BrushFactory.Frozen("#DCE4EE");
        DetailWorkspaceShell.BorderThickness = new Thickness(1);
        DetailWorkspaceShell.Padding = new Thickness(4);
        DetailWorkspaceShell.CornerRadius = new CornerRadius(14);
        DetailWorkspaceRail.Margin = new Thickness(8, 0, 0, 0);
        DetailWorkspaceRail.Background = BrushFactory.Frozen("#FCFCFD");
        DetailWorkspaceRail.BorderBrush = BrushFactory.Frozen("#DCE4EE");
        DetailWorkspaceRail.BorderThickness = new Thickness(1);
        DetailWorkspaceRail.CornerRadius = new CornerRadius(12);
        DetailWorkspaceRail.Padding = new Thickness(5);
        DetailWorkspaceRail.Width = 52;
        DetailWorkspaceContentColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailWorkspaceColumn.Width = _detailWorkspaceExpandedWidth.Value > 0
            ? _detailWorkspaceExpandedWidth
            : new GridLength(1.25, GridUnitType.Star);
    }

    private void ExpandDetailWorkspacePanel()
    {
        DetailWorkspaceShell.Visibility = Visibility.Visible;
        CollapsedDetailWorkspaceHost.Visibility = Visibility.Collapsed;
        DetailWorkspacePanel.Visibility = Visibility.Visible;
        DetailWorkspaceShell.Background = BrushFactory.Frozen("#F8FAFD");
        DetailWorkspaceShell.BorderBrush = BrushFactory.Frozen("#DCE4EE");
        DetailWorkspaceShell.BorderThickness = new Thickness(1);
        DetailWorkspaceShell.Padding = new Thickness(4);
        DetailWorkspaceShell.CornerRadius = new CornerRadius(14);
        DetailWorkspaceRail.Margin = new Thickness(8, 0, 0, 0);
        DetailWorkspaceRail.Background = BrushFactory.Frozen("#FCFCFD");
        DetailWorkspaceRail.BorderBrush = BrushFactory.Frozen("#DCE4EE");
        DetailWorkspaceRail.BorderThickness = new Thickness(1);
        DetailWorkspaceRail.CornerRadius = new CornerRadius(12);
        DetailWorkspaceRail.Padding = new Thickness(5);
        DetailWorkspaceRail.Width = 52;
        DetailWorkspaceCollapsedTab.Background = BrushFactory.Frozen("#F8FAFC");
        DetailWorkspaceCollapsedTab.BorderBrush = BrushFactory.Frozen("#D7E0EA");
        DetailWorkspaceCollapsedTab.BorderThickness = new Thickness(1);
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

    private void SuppressDetailWorkspacePanel()
    {
        DetailWorkspaceShell.Visibility = Visibility.Collapsed;
        CollapsedDetailWorkspaceHost.Visibility = Visibility.Collapsed;
        DetailWorkspacePanel.Visibility = Visibility.Collapsed;
        WorkspaceGridSplitter.Visibility = Visibility.Collapsed;
        WorkspaceSplitterColumn.Width = new GridLength(0);
        DetailWorkspaceContentColumn.Width = new GridLength(0);
        DetailWorkspaceColumn.Width = new GridLength(0);
    }

    private bool IsDetailWorkspaceSuppressed()
        => DataContext is MainWindowViewModel viewModel
        && string.Equals(viewModel.ActiveWorkspaceKey, "Schedule", StringComparison.OrdinalIgnoreCase);

    private void ApplyDetailWorkspaceAvailability(MainWindowViewModel viewModel)
    {
        if (string.Equals(viewModel.ActiveWorkspaceKey, "Schedule", StringComparison.OrdinalIgnoreCase))
        {
            SuppressDetailWorkspacePanel();
            return;
        }

        if (string.Equals(viewModel.ActiveWorkspaceKey, "Pivot Builder", StringComparison.OrdinalIgnoreCase))
        {
            CollapseDetailWorkspacePanel();
            return;
        }

        if (viewModel.IsDetailPanelCollapsed)
        {
            CollapseDetailWorkspacePanel();
        }
        else
        {
            ExpandDetailWorkspacePanel();
        }
    }
}
