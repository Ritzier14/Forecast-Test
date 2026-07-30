using System.Windows;
using System.Windows.Controls;

namespace ProjectCostForecast.App.Models;

public static class GridColumnRoleState
{
    public const string ForecastRowSelector = nameof(ForecastRowSelector);
    public const string ForecastComments = nameof(ForecastComments);

    public static readonly DependencyProperty RoleProperty = DependencyProperty.RegisterAttached(
        "Role",
        typeof(string),
        typeof(GridColumnRoleState),
        new PropertyMetadata(string.Empty));

    public static string GetRole(DataGridColumn column)
    {
        return (string?)column.GetValue(RoleProperty) ?? string.Empty;
    }

    public static void SetRole(DataGridColumn column, string value)
    {
        column.SetValue(RoleProperty, value);
    }
}
