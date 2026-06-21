using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectCostForecast.App;

public enum ProjectDataGridProfile
{
    Default,
    Forecast,
    ReadOnlyLedger,
    Pivot,
    ManagementResource,
    Schedule
}

public sealed class ProjectDataGrid : DataGrid
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile),
        typeof(ProjectDataGridProfile),
        typeof(ProjectDataGrid),
        new FrameworkPropertyMetadata(ProjectDataGridProfile.Default, OnProfileChanged));

    public ProjectDataGridProfile Profile
    {
        get => (ProjectDataGridProfile)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    public ProjectDataGrid()
    {
        Loaded += (_, _) => ApplyProfile();
    }

    private static void OnProfileChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ProjectDataGrid grid)
        {
            grid.ApplyProfile();
        }
    }

    private void ApplyProfile()
    {
        ProjectDataGridProfiles.Apply(this, Profile);
    }
}

public static class ProjectDataGridProfiles
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.RegisterAttached(
        "Profile",
        typeof(ProjectDataGridProfile),
        typeof(ProjectDataGridProfiles),
        new FrameworkPropertyMetadata(ProjectDataGridProfile.Default, OnAttachedProfileChanged));

    public static ProjectDataGridProfile GetProfile(DataGrid grid)
    {
        return (ProjectDataGridProfile)grid.GetValue(ProfileProperty);
    }

    public static void SetProfile(DataGrid grid, ProjectDataGridProfile value)
    {
        grid.SetValue(ProfileProperty, value);
    }

    private static void OnAttachedProfileChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not DataGrid grid)
        {
            return;
        }

        void ApplyWhenReady(object? sender, RoutedEventArgs args)
        {
            grid.Loaded -= ApplyWhenReady;
            Apply(grid, (ProjectDataGridProfile)e.NewValue);
        }

        if (grid.IsLoaded)
        {
            Apply(grid, (ProjectDataGridProfile)e.NewValue);
        }
        else
        {
            grid.Loaded -= ApplyWhenReady;
            grid.Loaded += ApplyWhenReady;
        }
    }

    public static void Apply(DataGrid grid, ProjectDataGridProfile profile)
    {
        ApplySharedDefaults(grid);

        switch (profile)
        {
            case ProjectDataGridProfile.Forecast:
                ApplyForecast(grid);
                break;
            case ProjectDataGridProfile.ReadOnlyLedger:
                ApplyReadOnlyLedger(grid);
                break;
            case ProjectDataGridProfile.Pivot:
                ApplyPivot(grid);
                break;
            case ProjectDataGridProfile.ManagementResource:
                ApplyManagementResource(grid);
                break;
            case ProjectDataGridProfile.Schedule:
                ApplySchedule(grid);
                break;
        }
    }

    public static bool UsesSpreadsheetInteractions(DataGrid grid)
    {
        return GetProfile(grid) is ProjectDataGridProfile.Forecast
            or ProjectDataGridProfile.ReadOnlyLedger
            or ProjectDataGridProfile.Pivot
            or ProjectDataGridProfile.ManagementResource;
    }

    public static bool SupportsTypeOverwrite(DataGrid grid)
    {
        return GetProfile(grid) is ProjectDataGridProfile.Forecast
            or ProjectDataGridProfile.ManagementResource;
    }

    public static bool IsReadOnlyProfile(DataGrid grid)
    {
        return GetProfile(grid) is ProjectDataGridProfile.ReadOnlyLedger
            or ProjectDataGridProfile.Pivot;
    }

    private static void ApplySharedDefaults(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.CanUserAddRowsProperty, false);
        SetIfUnset(grid, DataGrid.AutoGenerateColumnsProperty, false);
        SetIfUnset(grid, DataGrid.HeadersVisibilityProperty, DataGridHeadersVisibility.All);
        SetIfUnset(grid, DataGrid.GridLinesVisibilityProperty, DataGridGridLinesVisibility.All);
        SetIfUnset(grid, DataGrid.HorizontalGridLinesBrushProperty, BrushFactory.Frozen("#DDE6F0"));
        SetIfUnset(grid, DataGrid.VerticalGridLinesBrushProperty, BrushFactory.Frozen("#DDE6F0"));
        SetIfUnset(grid, DataGrid.EnableRowVirtualizationProperty, true);
        SetIfUnset(grid, DataGrid.EnableColumnVirtualizationProperty, true);
        SetIfUnset(grid, DataGrid.ClipboardCopyModeProperty, DataGridClipboardCopyMode.ExcludeHeader);
        ScrollViewer.SetCanContentScroll(grid, true);
    }

    private static void ApplyForecast(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.SelectionModeProperty, DataGridSelectionMode.Extended);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplyReadOnlyLedger(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.IsReadOnlyProperty, true);
        SetIfUnset(grid, DataGrid.SelectionModeProperty, DataGridSelectionMode.Extended);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplyPivot(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.IsReadOnlyProperty, true);
        SetIfUnset(grid, DataGrid.SelectionModeProperty, DataGridSelectionMode.Extended);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplyManagementResource(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.SelectionModeProperty, DataGridSelectionMode.Extended);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplySchedule(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.SelectionModeProperty, DataGridSelectionMode.Extended);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.FullRow);
        SetIfUnset(grid, DataGrid.ClipboardCopyModeProperty, DataGridClipboardCopyMode.IncludeHeader);
    }

    private static void SetIfUnset<T>(DependencyObject target, DependencyProperty property, T value)
    {
        if (target.ReadLocalValue(property) == DependencyProperty.UnsetValue)
        {
            target.SetValue(property, value);
        }
    }
}
