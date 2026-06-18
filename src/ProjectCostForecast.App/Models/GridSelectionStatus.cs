using System.Windows;

namespace ProjectCostForecast.App.Models;

public static class GridSelectionStatus
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(GridSelectionStatus),
        new FrameworkPropertyMetadata("Count: 0"));

    public static string GetText(DependencyObject target) => (string)target.GetValue(TextProperty);

    public static void SetText(DependencyObject target, string value) => target.SetValue(TextProperty, value);
}
