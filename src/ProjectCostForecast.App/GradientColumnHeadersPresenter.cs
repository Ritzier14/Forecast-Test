using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class GradientColumnHeadersPresenter : DataGridColumnHeadersPresenter
{
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var grid = FindAncestor<DataGrid>(this);
        if (grid is null)
        {
            return;
        }

        var left = 0d;
        var activeSpec = string.Empty;
        var activeLeft = 0d;
        var activeWidth = 0d;
        foreach (var column in grid.Columns
                     .Where(column => column.Visibility == Visibility.Visible)
                     .OrderBy(column => column.DisplayIndex))
        {
            var width = column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue;
            var spec = GridColumnPresentationState.GetHeaderColorSpec(column);
            var usesSharedGradient = !string.IsNullOrWhiteSpace(spec)
                && BrushFactory.TryParseAdvancedHeaderGradientSpec(spec, out _);

            if (usesSharedGradient && string.Equals(spec, activeSpec, StringComparison.OrdinalIgnoreCase))
            {
                activeWidth += width;
            }
            else
            {
                DrawSharedHeaderBackground(drawingContext, activeSpec, activeLeft, activeWidth);
                activeSpec = usesSharedGradient ? spec : string.Empty;
                activeLeft = left;
                activeWidth = usesSharedGradient ? width : 0;
            }

            left += width;
        }

        DrawSharedHeaderBackground(drawingContext, activeSpec, activeLeft, activeWidth);
    }

    private void DrawSharedHeaderBackground(DrawingContext drawingContext, string spec, double left, double width)
    {
        if (string.IsNullOrWhiteSpace(spec) || width <= 0)
        {
            return;
        }

        drawingContext.DrawRectangle(
            BrushFactory.FrozenHeaderGradient(spec),
            null,
            new Rect(left, 0, width, ActualHeight));
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
