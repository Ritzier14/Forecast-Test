using System.Windows;

namespace ProjectCostForecast.App.Models;

public static class GridColumnHighlightState
{
    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.RegisterAttached(
            "IsHighlighted",
            typeof(bool),
            typeof(GridColumnHighlightState),
            new FrameworkPropertyMetadata(false));

    public static bool GetIsHighlighted(DependencyObject element)
    {
        return (bool)element.GetValue(IsHighlightedProperty);
    }

    public static void SetIsHighlighted(DependencyObject element, bool value)
    {
        element.SetValue(IsHighlightedProperty, value);
    }
}
