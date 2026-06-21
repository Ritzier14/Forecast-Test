using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private const string PivotFieldDragFormat = "ProjectCostForecast.PivotField";
    private const string PivotAreaFieldDragFormat = "ProjectCostForecast.PivotAreaField";
    private Point _pivotDragStartPoint;
    private object? _pivotDragItem;

    private void PivotDragList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pivotDragStartPoint = e.GetPosition(this);
        _pivotDragItem = null;
        if (e.OriginalSource is not DependencyObject source || FindParent<ListBoxItem>(source) is not { } item)
        {
            return;
        }

        if (item.DataContext is PivotFieldDefinition or PivotAreaField)
        {
            _pivotDragItem = item.DataContext;
        }
    }

    private void PivotFieldList_PreviewMouseMove(object sender, MouseEventArgs e)
        => StartPivotDragIfReady(sender, e, PivotFieldDragFormat, typeof(PivotFieldDefinition));

    private void PivotAreaList_PreviewMouseMove(object sender, MouseEventArgs e)
        => StartPivotDragIfReady(sender, e, PivotAreaFieldDragFormat, typeof(PivotAreaField));

    private void StartPivotDragIfReady(object sender, MouseEventArgs e, string format, Type expectedType)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _pivotDragItem is null
            || !expectedType.IsInstanceOfType(_pivotDragItem))
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _pivotDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _pivotDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(format, _pivotDragItem);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        _pivotDragItem = null;
    }

    private void PivotDropArea_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetPivotDropAreaKey(sender) is not null && HasPivotDragData(e)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void PivotDropArea_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || GetPivotDropAreaKey(sender) is not { } areaKey)
        {
            return;
        }

        if (e.Data.GetData(PivotFieldDragFormat) is PivotFieldDefinition field)
        {
            viewModel.TryAddPivotFieldToArea(areaKey, field);
        }
        else if (e.Data.GetData(PivotAreaFieldDragFormat) is PivotAreaField areaField)
        {
            viewModel.TryMovePivotFieldToArea(areaKey, areaField);
        }

        e.Handled = true;
    }

    private static bool HasPivotDragData(DragEventArgs e)
        => e.Data.GetDataPresent(PivotFieldDragFormat) || e.Data.GetDataPresent(PivotAreaFieldDragFormat);

    private static string? GetPivotDropAreaKey(object sender)
        => sender is FrameworkElement { Tag: string areaKey } ? areaKey : null;
}
