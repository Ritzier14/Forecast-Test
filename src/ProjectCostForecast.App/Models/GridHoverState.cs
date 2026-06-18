using System.Windows;

namespace ProjectCostForecast.App.Models;

public static class GridHoverState
{
    public static readonly DependencyProperty IsRowHoveredProperty =
        DependencyProperty.RegisterAttached(
            "IsRowHovered",
            typeof(bool),
            typeof(GridHoverState),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsRowHovered(DependencyObject element)
    {
        return (bool)element.GetValue(IsRowHoveredProperty);
    }

    public static void SetIsRowHovered(DependencyObject element, bool value)
    {
        element.SetValue(IsRowHoveredProperty, value);
    }
}
