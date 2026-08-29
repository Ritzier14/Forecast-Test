using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ProjectCostForecast.App;

public static class RightClickGridPanBehavior
{
    public const double DragThreshold = 6d;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(RightClickGridPanBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty IsDraggingProperty =
        DependencyProperty.RegisterAttached(
            "IsDragging",
            typeof(bool),
            typeof(RightClickGridPanBehavior),
            new FrameworkPropertyMetadata(false));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(GridPanState),
            typeof(RightClickGridPanBehavior),
            new PropertyMetadata(null));

    private static readonly MouseButtonEventHandler PreviewMouseDownHandler = OnPreviewMouseDown;
    private static readonly MouseEventHandler PreviewMouseMoveHandler = OnPreviewMouseMove;
    private static readonly MouseButtonEventHandler PreviewMouseUpHandler = OnPreviewMouseUp;
    private static readonly MouseButtonEventHandler PreviewMouseRightButtonUpHandler = OnPreviewMouseRightButtonUp;
    private static readonly KeyEventHandler PreviewKeyDownHandler = OnPreviewKeyDown;

    public static bool GetIsEnabled(DependencyObject target) =>
        (bool)target.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject target, bool value) =>
        target.SetValue(IsEnabledProperty, value);

    public static bool GetIsDragging(DependencyObject target) =>
        (bool)target.GetValue(IsDraggingProperty);

    public static bool IsDraggingFrom(DependencyObject? source)
    {
        return FindAncestor<DataGrid>(source) is { } grid && GetIsDragging(grid);
    }

    public static void Cancel(DataGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (GetState(grid) is { } state)
        {
            Cancel(grid, state);
        }
        else
        {
            SetIsDragging(grid, false);
        }
    }

    internal static bool IsPastDragThreshold(double deltaX, double deltaY)
    {
        return Math.Abs(deltaX) >= DragThreshold || Math.Abs(deltaY) >= DragThreshold;
    }

    internal static double ClampScrollOffset(double requestedOffset, double maximumOffset)
    {
        if (double.IsNaN(requestedOffset)
            || double.IsNaN(maximumOffset)
            || maximumOffset <= 0)
        {
            return 0;
        }

        return Math.Clamp(requestedOffset, 0, maximumOffset);
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not DataGrid grid)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Attach(grid);
        }
        else
        {
            Detach(grid);
        }
    }

    private static void Attach(DataGrid grid)
    {
        if (GetState(grid) is not null)
        {
            return;
        }

        grid.SetValue(StateProperty, new GridPanState());
        grid.AddHandler(UIElement.PreviewMouseDownEvent, PreviewMouseDownHandler, handledEventsToo: true);
        grid.AddHandler(UIElement.PreviewMouseMoveEvent, PreviewMouseMoveHandler, handledEventsToo: true);
        grid.AddHandler(UIElement.PreviewMouseUpEvent, PreviewMouseUpHandler, handledEventsToo: true);
        grid.AddHandler(UIElement.PreviewMouseRightButtonUpEvent, PreviewMouseRightButtonUpHandler, handledEventsToo: true);
        grid.AddHandler(UIElement.PreviewKeyDownEvent, PreviewKeyDownHandler, handledEventsToo: true);
        grid.LostMouseCapture += Grid_LostMouseCapture;
        grid.Unloaded += Grid_Unloaded;
        SetIsDragging(grid, false);
    }

    private static void Detach(DataGrid grid)
    {
        if (GetState(grid) is not { } state)
        {
            SetIsDragging(grid, false);
            return;
        }

        grid.RemoveHandler(UIElement.PreviewMouseDownEvent, PreviewMouseDownHandler);
        grid.RemoveHandler(UIElement.PreviewMouseMoveEvent, PreviewMouseMoveHandler);
        grid.RemoveHandler(UIElement.PreviewMouseUpEvent, PreviewMouseUpHandler);
        grid.RemoveHandler(UIElement.PreviewMouseRightButtonUpEvent, PreviewMouseRightButtonUpHandler);
        grid.RemoveHandler(UIElement.PreviewKeyDownEvent, PreviewKeyDownHandler);
        grid.LostMouseCapture -= Grid_LostMouseCapture;
        grid.Unloaded -= Grid_Unloaded;
        Cancel(grid, state);
        grid.ClearValue(IsDraggingProperty);
        grid.ClearValue(StateProperty);
    }

    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || GetState(grid) is not { } state)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            Cancel(grid, state);
            return;
        }

        if (IsScrollBarInteractionSource(source))
        {
            Cancel(grid, state);
            return;
        }

        if (e.ChangedButton != MouseButton.Right)
        {
            if (state.Session.IsActive || GetIsDragging(grid))
            {
                Cancel(grid, state);
            }

            return;
        }

        if (FindAncestor<DataGridColumnHeader>(source) is not null)
        {
            Cancel(grid, state);
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(grid);
        if (scrollViewer is null)
        {
            Cancel(grid, state);
            return;
        }

        state.ResetVersion++;
        state.Session.Begin(
            e.GetPosition(scrollViewer),
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset);
        state.ScrollViewer = scrollViewer;
        state.CapturedMouse = false;
        SetIsDragging(grid, false);
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not DataGrid grid
            || GetState(grid) is not { } state
            || state.ScrollViewer is not { } scrollViewer)
        {
            return;
        }

        var move = state.Session.TryMove(
            e.GetPosition(scrollViewer),
            e.RightButton == MouseButtonState.Pressed,
            scrollViewer.ScrollableWidth,
            scrollViewer.ScrollableHeight);
        if (move is not { } panMove)
        {
            return;
        }

        if (!grid.IsMouseCaptured)
        {
            state.CapturedMouse = grid.CaptureMouse();
        }

        if (scrollViewer.ScrollableWidth > 0)
        {
            scrollViewer.ScrollToHorizontalOffset(panMove.HorizontalOffset);
        }

        if (scrollViewer.ScrollableHeight > 0)
        {
            scrollViewer.ScrollToVerticalOffset(panMove.VerticalOffset);
        }

        SetIsDragging(grid, true);
        e.Handled = true;
    }

    private static void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right
            || sender is not DataGrid grid
            || GetState(grid) is not { } state)
        {
            return;
        }

        var wasDragging = state.Session.IsDragging;
        ReleaseCapturedMouse(grid, state);
        state.Session.End();
        state.ScrollViewer = null;

        if (wasDragging)
        {
            SetIsDragging(grid, true);
            e.Handled = true;
            QueueDraggingReset(grid, state);
        }
        else
        {
            state.ResetVersion++;
            SetIsDragging(grid, false);
        }
    }

    private static void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && GetIsDragging(grid))
        {
            e.Handled = true;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape
            || sender is not DataGrid grid
            || GetState(grid) is not { } state
            || (!state.Session.IsActive && !GetIsDragging(grid)))
        {
            return;
        }

        Cancel(grid, state);
        e.Handled = true;
    }

    private static void Grid_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (sender is DataGrid grid
            && GetState(grid) is { } state
            && !state.ReleasingMouseCapture)
        {
            Cancel(grid, state);
        }
    }

    private static void Grid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            Cancel(grid);
        }
    }

    private static void Cancel(DataGrid grid, GridPanState state)
    {
        state.ResetVersion++;
        ReleaseCapturedMouse(grid, state);
        state.Session.Cancel();
        state.ScrollViewer = null;
        SetIsDragging(grid, false);
    }

    private static void ReleaseCapturedMouse(DataGrid grid, GridPanState state)
    {
        if (state.CapturedMouse && grid.IsMouseCaptured)
        {
            state.ReleasingMouseCapture = true;
            grid.ReleaseMouseCapture();
            state.ReleasingMouseCapture = false;
        }

        state.CapturedMouse = false;
    }

    private static void QueueDraggingReset(DataGrid grid, GridPanState state)
    {
        var resetVersion = ++state.ResetVersion;
        if (grid.Dispatcher.HasShutdownStarted || grid.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            grid.Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    if (GetState(grid) == state && state.ResetVersion == resetVersion)
                    {
                        SetIsDragging(grid, false);
                    }
                }));
        }
        catch (InvalidOperationException)
        {
            // The owning window can shut down while the final mouse route is unwinding.
        }
    }

    private static GridPanState? GetState(DataGrid grid) =>
        grid.GetValue(StateProperty) as GridPanState;

    private static void SetIsDragging(DataGrid grid, bool value) =>
        grid.SetValue(IsDraggingProperty, value);

    private static bool IsScrollBarInteractionSource(DependencyObject source)
    {
        return FindAncestor<ScrollBar>(source) is not null
            || FindAncestor<RepeatButton>(source) is not null
            || FindAncestor<Thumb>(source) is not null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = source switch
            {
                Visual or Visual3D => VisualTreeHelper.GetParent(source),
                FrameworkContentElement contentElement => contentElement.Parent,
                _ => LogicalTreeHelper.GetParent(source)
            };
        }

        return null;
    }

    private sealed class GridPanState
    {
        public RightClickGridPanSession Session { get; } = new();
        public ScrollViewer? ScrollViewer { get; set; }
        public bool CapturedMouse { get; set; }
        public bool ReleasingMouseCapture { get; set; }
        public int ResetVersion { get; set; }
    }
}
