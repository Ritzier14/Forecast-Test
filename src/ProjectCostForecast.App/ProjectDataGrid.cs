using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
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

public sealed class ProjectDataGridRowHeightChangedEventArgs : EventArgs
{
    public ProjectDataGridRowHeightChangedEventArgs(object item, double? oldHeight, double? newHeight)
    {
        Item = item;
        OldHeight = oldHeight;
        NewHeight = newHeight;
    }

    public object Item { get; }

    public double? OldHeight { get; }

    public double? NewHeight { get; }
}

public sealed class ProjectDataGridModifierSelectionEventArgs : EventArgs
{
    public ProjectDataGridModifierSelectionEventArgs(object currentItem)
    {
        CurrentItem = currentItem;
    }

    public object CurrentItem { get; }
}

/// <summary>
/// The single table control used by the application. Profiles provide the
/// table-specific defaults while resize and selection fundamentals live here.
/// </summary>
public sealed class ProjectDataGrid : DataGrid
{
    private const double DefaultResizeHitThickness = 8d;
    private const double DefaultMinimumResizableRowHeight = 24d;
    private const double DefaultMaximumResizableRowHeight = 600d;
    private const double SpreadsheetFillHandleSize = 10d;

    private readonly Dictionary<object, double> _rowHeightOverrides = new(ReferenceEqualityComparer.Instance);
    private RowResizeSession? _rowResize;
    private object? _rowSelectionAnchor;

    private sealed record RowResizeSession(
        DataGridRow Row,
        object Item,
        Point StartPointer,
        double StartHeight,
        double CurrentHeight,
        double? PreviousOverride,
        bool HasChanged);

    public static readonly DependencyProperty ProfileProperty =
        ProjectDataGridProfiles.ProfileProperty.AddOwner(
            typeof(ProjectDataGrid),
            new FrameworkPropertyMetadata(ProjectDataGridProfile.Default, OnProfileChanged));

    public static readonly DependencyProperty RowResizeHitThicknessProperty = DependencyProperty.Register(
        nameof(RowResizeHitThickness),
        typeof(double),
        typeof(ProjectDataGrid),
        new FrameworkPropertyMetadata(DefaultResizeHitThickness, null, CoercePositiveFiniteDouble));

    public static readonly DependencyProperty MinimumResizableRowHeightProperty = DependencyProperty.Register(
        nameof(MinimumResizableRowHeight),
        typeof(double),
        typeof(ProjectDataGrid),
        new FrameworkPropertyMetadata(DefaultMinimumResizableRowHeight, null, CoercePositiveFiniteDouble));

    public static readonly DependencyProperty MaximumResizableRowHeightProperty = DependencyProperty.Register(
        nameof(MaximumResizableRowHeight),
        typeof(double),
        typeof(ProjectDataGrid),
        new FrameworkPropertyMetadata(DefaultMaximumResizableRowHeight, null, CoercePositiveFiniteDouble));

    public static readonly DependencyProperty ModifierClickSelectsWholeRowsProperty = DependencyProperty.Register(
        nameof(ModifierClickSelectsWholeRows),
        typeof(bool),
        typeof(ProjectDataGrid),
        new FrameworkPropertyMetadata(true));

    public ProjectDataGridProfile Profile
    {
        get => (ProjectDataGridProfile)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    public double RowResizeHitThickness
    {
        get => (double)GetValue(RowResizeHitThicknessProperty);
        set => SetValue(RowResizeHitThicknessProperty, value);
    }

    public double MinimumResizableRowHeight
    {
        get => (double)GetValue(MinimumResizableRowHeightProperty);
        set => SetValue(MinimumResizableRowHeightProperty, value);
    }

    public double MaximumResizableRowHeight
    {
        get => (double)GetValue(MaximumResizableRowHeightProperty);
        set => SetValue(MaximumResizableRowHeightProperty, value);
    }

    public bool ModifierClickSelectsWholeRows
    {
        get => (bool)GetValue(ModifierClickSelectsWholeRowsProperty);
        set => SetValue(ModifierClickSelectsWholeRowsProperty, value);
    }

    public bool IsRowResizeInProgress => _rowResize is not null;

    public bool IsHandlingRowResizeInput { get; private set; }

    public event EventHandler<ProjectDataGridRowHeightChangedEventArgs>? RowHeightChanged;

    public event EventHandler<ProjectDataGridModifierSelectionEventArgs>? ModifierRowSelectionCompleted;

    public ProjectDataGrid()
    {
        LoadingRow += ProjectDataGrid_LoadingRow;
        UnloadingRow += ProjectDataGrid_UnloadingRow;
        Loaded += ProjectDataGrid_Loaded;
        Unloaded += ProjectDataGrid_Unloaded;
    }

    /// <summary>
    /// Applies a custom height to an item and its realized row, if present.
    /// This is also the commit path used by the pointer resize gesture.
    /// </summary>
    public void SetRowHeight(object item, double height)
    {
        ArgumentNullException.ThrowIfNull(item);
        var row = FindRealizedRow(item);
        var clampedHeight = ClampRowHeight(height, row);
        var oldHeight = GetRowHeight(item);
        _rowHeightOverrides[item] = clampedHeight;

        if (row is not null)
        {
            ApplyRowHeight(row, clampedHeight);
        }

        if (oldHeight is null || Math.Abs(oldHeight.Value - clampedHeight) >= 0.1d)
        {
            RowHeightChanged?.Invoke(
                this,
                new ProjectDataGridRowHeightChangedEventArgs(item, oldHeight, clampedHeight));
        }
    }

    public double? GetRowHeight(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _rowHeightOverrides.TryGetValue(item, out var height)
            ? ClampRowHeight(height)
            : null;
    }

    public void ResetRowHeight(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var oldHeight = GetRowHeight(item);
        _rowHeightOverrides.Remove(item);

        if (FindRealizedRow(item) is { } row)
        {
            ClearRealizedRowHeight(row);
        }


        if (oldHeight is not null)
        {
            RowHeightChanged?.Invoke(
                this,
                new ProjectDataGridRowHeightChangedEventArgs(item, oldHeight, null));
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        IsHandlingRowResizeInput = false;
        if (e.ChangedButton == MouseButton.Left
            && e.OriginalSource is DependencyObject source
            && TryGetResizeTarget(source, e.MouseDevice, out var row))
        {
            IsHandlingRowResizeInput = true;
            if (e.ClickCount >= 2)
            {
                ResetRowHeight(row.Item);
            }
            else
            {
                BeginRowResize(row, e.GetPosition(this));
            }

            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left
            && e.OriginalSource is DependencyObject selectionSource
            && ItemsControl.ContainerFromElement(this, selectionSource) is DataGridRow selectionRow
            && selectionRow.Item is { } selectionItem
            && selectionItem != CollectionView.NewItemPlaceholder)
        {
            var modifiers = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift);
            if (ModifierClickSelectsWholeRows && modifiers != ModifierKeys.None)
            {
                SelectWholeRowsFromModifierClick(selectionItem, modifiers);
                e.Handled = true;
                return;
            }

            if (modifiers == ModifierKeys.None)
            {
                _rowSelectionAnchor = selectionItem;
            }
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseDoubleClick(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left
            && e.OriginalSource is DependencyObject source
            && TryGetResizeTarget(source, e.MouseDevice, out var row))
        {
            IsHandlingRowResizeInput = true;
            ResetRowHeight(row.Item);
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseDoubleClick(e);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        if (_rowResize is not null)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateRowResize(e.GetPosition(this));
            }
            else
            {
                CompleteRowResize(commit: true);
            }

            e.Handled = true;
            return;
        }

        base.OnPreviewMouseMove(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_rowResize is not null)
        {
            IsHandlingRowResizeInput = true;
            UpdateRowResize(e.GetPosition(this));
            CompleteRowResize(commit: true);
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonUp(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_rowResize is not null && e.Key == Key.Escape)
        {
            CompleteRowResize(commit: false);
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_rowResize is not null && !ReferenceEquals(Mouse.Captured, this))
        {
            CompleteRowResize(commit: true);
        }
    }

    protected override void OnQueryCursor(QueryCursorEventArgs e)
    {
        if (_rowResize is not null
            || (e.OriginalSource is DependencyObject source
                && TryGetResizeTarget(source, e.MouseDevice, out _)))
        {
            e.Cursor = Cursors.SizeNS;
            e.Handled = true;
            return;
        }

        base.OnQueryCursor(e);
    }

    private static void OnProfileChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ProjectDataGrid grid)
        {
            ProjectDataGridProfiles.Apply(grid, (ProjectDataGridProfile)e.NewValue);
        }
    }

    private static object CoercePositiveFiniteDouble(DependencyObject dependencyObject, object baseValue)
    {
        return baseValue is double value && double.IsFinite(value) && value > 0
            ? value
            : 1d;
    }

    private void ProjectDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        ProjectDataGridProfiles.Apply(this, Profile);
    }

    private void ProjectDataGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        CompleteRowResize(commit: true);
    }

    private void ProjectDataGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        // WPF's native row gripper resizes the cells presenter. Reuse that
        // mechanism and clear it whenever a recycled row receives a new item.
        // Individual DataGridCell instances deliberately never own Height.
        e.Row.Loaded -= ProjectDataGridRow_Loaded;
        e.Row.Loaded += ProjectDataGridRow_Loaded;
        PrepareRealizedRow(e.Row);
    }

    private void ProjectDataGrid_UnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (_rowResize is { Row: var activeRow } && ReferenceEquals(activeRow, e.Row))
        {
            CompleteRowResize(commit: true);
        }

        e.Row.Loaded -= ProjectDataGridRow_Loaded;
        ClearRealizedRowHeight(e.Row);
    }

    private void ProjectDataGridRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGridRow row)
        {
            PrepareRealizedRow(row);
        }
    }

    private void PrepareRealizedRow(DataGridRow row)
    {
        ClearRealizedRowHeight(row);
        if (row.Item is { } item
            && item != CollectionView.NewItemPlaceholder
            && _rowHeightOverrides.TryGetValue(item, out var storedHeight))
        {
            // An item can receive an override while virtualized, before its
            // row-specific MinHeight is known. Normalize it as soon as the
            // container is realized so the presenter, borders, and row can
            // never end up with different heights.
            var height = ClampRowHeight(storedHeight, row);
            _rowHeightOverrides[item] = height;
            ApplyRowHeight(row, height);

            if (Math.Abs(storedHeight - height) >= 0.1d)
            {
                RowHeightChanged?.Invoke(
                    this,
                    new ProjectDataGridRowHeightChangedEventArgs(item, storedHeight, height));
            }
        }
    }

    private bool TryGetResizeTarget(
        DependencyObject source,
        MouseDevice mouseDevice,
        out DataGridRow row)
    {
        row = null!;
        if (!CanUserResizeRows
            || ItemsControl.ContainerFromElement(this, source) is not DataGridRow candidate
            || candidate.Item is null
            || candidate.Item == CollectionView.NewItemPlaceholder
            || candidate.ActualHeight <= 0)
        {
            return false;
        }

        var point = mouseDevice.GetPosition(candidate);
        var hitThickness = Math.Min(RowResizeHitThickness, candidate.ActualHeight);
        if (point.Y < candidate.ActualHeight - hitThickness
            || point.Y > candidate.ActualHeight + 1d
            || IsInteractiveResizeExclusion(source)
            || IsSpreadsheetFillHandleArea(source, candidate, mouseDevice))
        {
            return false;
        }

        row = candidate;
        return true;
    }

    private static bool IsInteractiveResizeExclusion(DependencyObject source)
    {
        return FindVisualAncestor<ButtonBase>(source) is { } button && button is not DataGridRowHeader
            || FindVisualAncestor<TextBoxBase>(source) is not null
            || FindVisualAncestor<ComboBox>(source) is not null
            || FindVisualAncestor<PasswordBox>(source) is not null
            || FindVisualAncestor<Slider>(source) is not null;
    }

    private bool IsSpreadsheetFillHandleArea(
        DependencyObject source,
        DataGridRow row,
        MouseDevice mouseDevice)
    {
        if (FindVisualAncestor<DataGridCell>(source) is not { } cell
            || !CurrentCell.IsValid
            || !ReferenceEquals(CurrentCell.Item, row.Item)
            || !ReferenceEquals(CurrentCell.Column, cell.Column))
        {
            return false;
        }

        var point = mouseDevice.GetPosition(cell);
        return point.X >= Math.Max(0, cell.ActualWidth - SpreadsheetFillHandleSize)
            && point.Y >= Math.Max(0, cell.ActualHeight - SpreadsheetFillHandleSize);
    }

    private void BeginRowResize(DataGridRow row, Point pointer)
    {
        if (_rowResize is not null || row.Item is null)
        {
            return;
        }

        row.ApplyTemplate();
        if (FindCellsPresenter(row) is not { } cellsPresenter)
        {
            return;
        }

        var startHeight = ClampRowHeight(
            cellsPresenter.ActualHeight > 0 && double.IsFinite(cellsPresenter.ActualHeight)
                ? cellsPresenter.ActualHeight
                : ResolveDefaultRowHeight(),
            row);
        var previousOverride = GetRowHeight(row.Item);
        ApplyRowHeight(row, startHeight);
        _rowResize = new RowResizeSession(
            row,
            row.Item,
            pointer,
            startHeight,
            startHeight,
            previousOverride,
            HasChanged: false);
        if (!CaptureMouse())
        {
            _rowResize = null;
            RestoreRowHeight(row, previousOverride);
        }
    }

    private void UpdateRowResize(Point pointer)
    {
        if (_rowResize is not { } session)
        {
            return;
        }

        var height = ClampRowHeight(
            session.StartHeight + pointer.Y - session.StartPointer.Y,
            session.Row);
        if (Math.Abs(height - session.CurrentHeight) < 0.1d)
        {
            return;
        }

        // Store and announce the live value so dependent row-aligned views
        // (notably the Gantt canvas) remain synchronized during the drag.
        SetRowHeight(session.Item, height);
        _rowResize = session with { CurrentHeight = height, HasChanged = true };
    }

    private void CompleteRowResize(bool commit)
    {
        var session = _rowResize;
        _rowResize = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (session is null)
        {
            return;
        }

        if (commit && session.HasChanged)
        {
            // UpdateRowResize already committed the live value.
            return;
        }

        if (commit)
        {
            // A click without movement is not a resize and must not create a
            // redundant per-item override.
            RestoreRowHeight(session.Row, session.PreviousOverride);
        }
        else
        {
            if (session.PreviousOverride is { } previousHeight)
            {
                SetRowHeight(session.Item, previousHeight);
            }
            else
            {
                ResetRowHeight(session.Item);
            }
        }
    }

    private double ClampRowHeight(double height, DataGridRow? row = null)
    {
        var minimum = Math.Max(MinimumResizableRowHeight, MinRowHeight);
        if (row is not null)
        {
            minimum = Math.Max(minimum, row.MinHeight);
        }

        var maximum = Math.Max(minimum, MaximumResizableRowHeight);
        return Math.Clamp(
            IsUsableHeight(height) ? height : ResolveDefaultRowHeight(),
            minimum,
            maximum);
    }

    private double ResolveDefaultRowHeight()
    {
        return IsUsableHeight(RowHeight)
            ? RowHeight
            : Math.Max(DefaultMinimumResizableRowHeight, MinRowHeight);
    }

    private static bool IsUsableHeight(double height)
    {
        return double.IsFinite(height) && height > 0;
    }

    private static void ApplyRowHeight(DataGridRow row, double height)
    {
        // This mirrors DataGridRow.OnRowResize: the presenter is the height
        // owner. Setting DataGridRow.Height creates unused space around the
        // old presenter; setting cell Height breaks recycling and borders.
        row.ClearValue(FrameworkElement.HeightProperty);
        row.ApplyTemplate();
        if (FindCellsPresenter(row) is { } cellsPresenter)
        {
            cellsPresenter.Height = height;
            cellsPresenter.InvalidateMeasure();
            cellsPresenter.InvalidateArrange();
        }

        row.InvalidateMeasure();
        row.InvalidateArrange();
    }

    private static void RestoreRowHeight(DataGridRow row, double? height)
    {
        if (height is { } previousHeight)
        {
            ApplyRowHeight(row, previousHeight);
            return;
        }

        ClearRealizedRowHeight(row);
    }

    private static void ClearRealizedRowHeight(DataGridRow row)
    {
        row.ClearValue(FrameworkElement.HeightProperty);
        if (FindCellsPresenter(row) is { } cellsPresenter)
        {
            cellsPresenter.ClearValue(FrameworkElement.HeightProperty);
            cellsPresenter.InvalidateMeasure();
            cellsPresenter.InvalidateArrange();
        }

        row.InvalidateMeasure();
        row.InvalidateArrange();
    }

    private static DataGridCellsPresenter? FindCellsPresenter(DataGridRow row)
    {
        return FindVisualDescendants<DataGridCellsPresenter>(row).FirstOrDefault();
    }

    private void SelectWholeRowsFromModifierClick(object endItem, ModifierKeys modifiers)
    {
        var visibleItems = GetSelectableItemsInViewOrder();
        var visibleColumns = Columns
            .Where(column => column.Visibility == Visibility.Visible)
            .OrderBy(column => column.DisplayIndex)
            .ToList();
        if (visibleItems.Count == 0 || visibleColumns.Count == 0)
        {
            return;
        }

        var endIndex = visibleItems.FindIndex(item => ReferenceEquals(item, endItem));
        if (endIndex < 0)
        {
            return;
        }

        var shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var control = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var anchor = _rowSelectionAnchor
            ?? (CurrentCell.IsValid ? CurrentCell.Item : null)
            ?? SelectedItem
            ?? endItem;
        var anchorIndex = visibleItems.FindIndex(item => ReferenceEquals(item, anchor));
        if (anchorIndex < 0)
        {
            anchorIndex = endIndex;
        }

        var targetItems = shift
            ? visibleItems
                .Skip(Math.Min(anchorIndex, endIndex))
                .Take(Math.Abs(endIndex - anchorIndex) + 1)
                .ToList()
            : [endItem];

        CommitEdit(DataGridEditingUnit.Cell, true);
        CommitEdit(DataGridEditingUnit.Row, true);

        if (control && !shift)
        {
            ToggleWholeRow(endItem, visibleColumns);
            _rowSelectionAnchor = endItem;
            var currentItem = IsWholeRowSelected(endItem, visibleColumns)
                ? endItem
                : FindFirstSelectedRow(visibleItems) ?? endItem;
            SetCurrentRow(currentItem, visibleColumns[0]);
            ModifierRowSelectionCompleted?.Invoke(
                this,
                new ProjectDataGridModifierSelectionEventArgs(currentItem));
            return;
        }

        if (!control)
        {
            ClearRowSelection();
        }

        if (SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            foreach (var item in targetItems)
            {
                if (!SelectedItems.Contains(item))
                {
                    SelectedItems.Add(item);
                }
            }
        }
        else
        {
            foreach (var item in targetItems)
            {
                foreach (var column in visibleColumns)
                {
                    var cell = new DataGridCellInfo(item, column);
                    if (!SelectedCells.Contains(cell))
                    {
                        SelectedCells.Add(cell);
                    }
                }
            }
        }

        SetCurrentRow(endItem, visibleColumns[0]);
        ModifierRowSelectionCompleted?.Invoke(
            this,
            new ProjectDataGridModifierSelectionEventArgs(endItem));
    }

    private List<object> GetSelectableItemsInViewOrder()
    {
        var result = new List<object>();
        foreach (var item in Items.Cast<object>())
        {
            AddSelectableViewItem(item, result);
        }

        return result;
    }

    private static void AddSelectableViewItem(object? item, ICollection<object> result)
    {
        if (item is null || item == CollectionView.NewItemPlaceholder)
        {
            return;
        }

        if (item is CollectionViewGroup group)
        {
            foreach (var child in group.Items)
            {
                AddSelectableViewItem(child, result);
            }

            return;
        }

        result.Add(item);
    }

    private void ToggleWholeRow(object item, IReadOnlyList<DataGridColumn> visibleColumns)
    {
        if (SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            if (SelectedItems.Contains(item))
            {
                SelectedItems.Remove(item);
            }
            else
            {
                SelectedItems.Add(item);
            }

            return;
        }

        var selectedCells = SelectedCells
            .Where(cell => ReferenceEquals(cell.Item, item))
            .ToList();
        var selectedColumns = selectedCells.Select(cell => cell.Column).ToHashSet();
        if (visibleColumns.All(selectedColumns.Contains))
        {
            foreach (var cell in selectedCells)
            {
                SelectedCells.Remove(cell);
            }

            return;
        }

        foreach (var column in visibleColumns)
        {
            var cell = new DataGridCellInfo(item, column);
            if (!SelectedCells.Contains(cell))
            {
                SelectedCells.Add(cell);
            }
        }

    }

    private bool IsWholeRowSelected(object item, IReadOnlyList<DataGridColumn> visibleColumns)
    {
        if (SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            return SelectedItems.Contains(item);
        }

        var selectedColumns = SelectedCells
            .Where(cell => ReferenceEquals(cell.Item, item))
            .Select(cell => cell.Column)
            .ToHashSet();
        return visibleColumns.All(selectedColumns.Contains);
    }

    private object? FindFirstSelectedRow(IEnumerable<object> visibleItems)
    {
        if (SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            return visibleItems.FirstOrDefault(SelectedItems.Contains);
        }

        var selectedItems = SelectedCells
            .Where(cell => cell.Item is not null)
            .Select(cell => cell.Item)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        return visibleItems.FirstOrDefault(selectedItems.Contains);
    }

    private void SetCurrentRow(object item, DataGridColumn column)
    {
        CurrentItem = item;
        CurrentCell = new DataGridCellInfo(item, column);
    }

    private void ClearRowSelection()
    {
        if (SelectionUnit == DataGridSelectionUnit.FullRow)
        {
            SelectedItems.Clear();
        }
        else
        {
            SelectedCells.Clear();
        }
    }

    private DataGridRow? FindRealizedRow(object item)
    {
        if (ItemContainerGenerator.ContainerFromItem(item) is DataGridRow directRow)
        {
            return directRow;
        }

        return FindVisualDescendants<DataGridRow>(this)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Item, item));
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}

public static class ProjectDataGridProfiles
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.RegisterAttached(
        "Profile",
        typeof(ProjectDataGridProfile),
        typeof(ProjectDataGridProfiles),
        new FrameworkPropertyMetadata(ProjectDataGridProfile.Default, OnAttachedProfileChanged));

    public static readonly DependencyProperty ShowsFooterRowProperty = DependencyProperty.RegisterAttached(
        "ShowsFooterRow",
        typeof(bool),
        typeof(ProjectDataGridProfiles),
        new FrameworkPropertyMetadata(false));

    public static ProjectDataGridProfile GetProfile(DataGrid grid)
    {
        return (ProjectDataGridProfile)grid.GetValue(ProfileProperty);
    }

    public static void SetProfile(DataGrid grid, ProjectDataGridProfile value)
    {
        grid.SetValue(ProfileProperty, value);
    }

    public static bool GetShowsFooterRow(DataGrid grid)
    {
        return (bool)grid.GetValue(ShowsFooterRowProperty);
    }

    public static void SetShowsFooterRow(DataGrid grid, bool value)
    {
        grid.SetValue(ShowsFooterRowProperty, value);
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
        SetIfUnset(grid, DataGrid.GridLinesVisibilityProperty, DataGridGridLinesVisibility.None);
        SetIfUnset(grid, DataGrid.HorizontalGridLinesBrushProperty, BrushFactory.Frozen("#EEF3F8"));
        SetIfUnset(grid, DataGrid.VerticalGridLinesBrushProperty, BrushFactory.Frozen("#EEF3F8"));
        SetIfUnset(grid, DataGrid.EnableRowVirtualizationProperty, true);
        SetIfUnset(grid, DataGrid.EnableColumnVirtualizationProperty, true);
        SetIfUnset(grid, DataGrid.ClipboardCopyModeProperty, DataGridClipboardCopyMode.ExcludeHeader);

        // Extended selection and the shared row-resize seam are application
        // contracts. Apply them even to legacy declarations that previously
        // hard-coded Single/false; profiles still control cell-vs-row units.
        grid.SetCurrentValue(DataGrid.SelectionModeProperty, DataGridSelectionMode.Extended);
        grid.SetCurrentValue(DataGrid.CanUserResizeRowsProperty, true);
        SetIfUnset(grid, ScrollViewer.CanContentScrollProperty, true);
        SetWholeRowModifierSelection(grid, true);
    }

    private static void ApplyForecast(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.HeadersVisibilityProperty, DataGridHeadersVisibility.Column);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplyReadOnlyLedger(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.HeadersVisibilityProperty, DataGridHeadersVisibility.Column);
        SetIfUnset(grid, DataGrid.IsReadOnlyProperty, true);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplyPivot(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.HeadersVisibilityProperty, DataGridHeadersVisibility.Column);
        SetIfUnset(grid, DataGrid.IsReadOnlyProperty, true);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplyManagementResource(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.HeadersVisibilityProperty, DataGridHeadersVisibility.Column);
        SetIfUnset(grid, DataGrid.SelectionUnitProperty, DataGridSelectionUnit.CellOrRowHeader);
    }

    private static void ApplySchedule(DataGrid grid)
    {
        SetIfUnset(grid, DataGrid.HeadersVisibilityProperty, DataGridHeadersVisibility.Column);
        grid.SetCurrentValue(DataGrid.SelectionUnitProperty, DataGridSelectionUnit.FullRow);
        SetIfUnset(grid, DataGrid.ClipboardCopyModeProperty, DataGridClipboardCopyMode.IncludeHeader);
    }

    private static void SetWholeRowModifierSelection(DataGrid grid, bool enabled)
    {
        if (grid is ProjectDataGrid projectGrid)
        {
            projectGrid.SetCurrentValue(ProjectDataGrid.ModifierClickSelectsWholeRowsProperty, enabled);
        }
    }

    private static void SetIfUnset<T>(DependencyObject target, DependencyProperty property, T value)
    {
        if (target.ReadLocalValue(property) == DependencyProperty.UnsetValue)
        {
            target.SetCurrentValue(property, value);
        }
    }
}
