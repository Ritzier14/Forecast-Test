using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectCostForecast.App;

internal static class SpreadsheetReadOnlyGridBehavior
{
    public static DataGrid Attach(DataGrid grid)
    {
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
        grid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
        grid.PreviewMouseRightButtonDown += (_, e) => OpenMenu(grid, e);
        return grid;
    }

    private static void OpenMenu(DataGrid grid, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindParent<DataGridColumnHeader>(source) is not null)
        {
            return;
        }

        var cell = FindParent<DataGridCell>(source);
        if (cell is null)
        {
            return;
        }

        var info = new DataGridCellInfo(cell.DataContext, cell.Column);
        if (!grid.SelectedCells.Contains(info))
        {
            grid.SelectedCells.Clear();
            grid.SelectedCells.Add(info);
        }

        grid.CurrentCell = info;
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Cut", InputGestureText = "Ctrl+X", IsEnabled = false });
        var copy = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
        copy.Click += (_, _) => ApplicationCommands.Copy.Execute(null, grid);
        menu.Items.Add(copy);
        menu.Items.Add(new MenuItem { Header = "Paste", InputGestureText = "Ctrl+V", IsEnabled = false });
        cell.ContextMenu = menu;
        menu.PlacementTarget = cell;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
