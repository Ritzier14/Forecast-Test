using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

internal interface IReportCanvasObjectHost
{
    ReportCanvasObjectLayout Layout { get; }

    void SetSelected(bool selected);

    void SyncPositionFromCanvas();
}

internal static class ReportCanvasObjectPositioning
{
    public static Point ClampToCanvas(Point position, Size elementSize, double canvasWidth, double canvasHeight)
    {
        return new Point(
            ClampCoordinate(position.X, elementSize.Width, canvasWidth),
            ClampCoordinate(position.Y, elementSize.Height, canvasHeight));
    }

    private static double ClampCoordinate(double position, double elementSize, double canvasSize)
    {
        var safePosition = double.IsFinite(position) ? position : 0;
        var safeElementSize = double.IsFinite(elementSize) ? Math.Max(0, elementSize) : 0;
        var safeCanvasSize = double.IsFinite(canvasSize) ? Math.Max(0, canvasSize) : 0;
        return Math.Clamp(safePosition, 0, Math.Max(0, safeCanvasSize - safeElementSize));
    }
}

/// <summary>
/// Owns the shared header-to-canvas drag interaction for report objects.
/// Cards provide their layout and persistence callback; the controller owns
/// coordinate space, bounds, capture, and z-order behavior.
/// </summary>
internal sealed class ReportCanvasDragController
{
    private readonly FrameworkElement _target;
    private readonly FrameworkElement _handle;
    private readonly IReportCanvasObjectHost _host;
    private readonly Action _changed;
    private Point _dragOrigin;
    private double _leftOrigin;
    private double _topOrigin;
    private int _originalZIndex;
    private bool _isDragging;
    private bool _isEnding;

    public ReportCanvasDragController(
        FrameworkElement target,
        FrameworkElement handle,
        IReportCanvasObjectHost host,
        Action changed)
    {
        _target = target;
        _handle = handle;
        _host = host;
        _changed = changed;

        _handle.PreviewMouseLeftButtonDown += HandleMouseLeftButtonDown;
        _handle.PreviewMouseMove += HandleMouseMove;
        _handle.PreviewMouseLeftButtonUp += HandleMouseLeftButtonUp;
        _handle.LostMouseCapture += HandleLostMouseCapture;
        _target.Unloaded += TargetUnloaded;
    }

    public bool IsDragging => _isDragging;

    private void HandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || IsButtonDescendant(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (FindCanvas(_target) is not { } canvas)
        {
            return;
        }

        _dragOrigin = e.GetPosition(canvas);
        _leftOrigin = GetCanvasOffset(Canvas.GetLeft(_target), _host.Layout.X);
        _topOrigin = GetCanvasOffset(Canvas.GetTop(_target), _host.Layout.Y);
        _originalZIndex = Panel.GetZIndex(_target);
        _isDragging = true;
        _handle.CaptureMouse();
        Panel.SetZIndex(_target, 100);
        e.Handled = true;
    }

    private void HandleMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        if (FindCanvas(_target) is not { } canvas)
        {
            EndDrag();
            return;
        }

        var point = e.GetPosition(canvas);
        var position = ReportCanvasObjectPositioning.ClampToCanvas(
            new Point(
                _leftOrigin + point.X - _dragOrigin.X,
                _topOrigin + point.Y - _dragOrigin.Y),
            new Size(_target.Width, _target.Height),
            canvas.Width,
            canvas.Height);
        Canvas.SetLeft(_target, position.X);
        Canvas.SetTop(_target, position.Y);
        _host.SyncPositionFromCanvas();
        e.Handled = true;
    }

    private void HandleMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        EndDrag();
        e.Handled = true;
    }

    private void HandleLostMouseCapture(object sender, MouseEventArgs e)
    {
        EndDrag();
    }

    private void TargetUnloaded(object sender, RoutedEventArgs e)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_isDragging || _isEnding)
        {
            return;
        }

        _isEnding = true;
        try
        {
            _isDragging = false;
            if (_handle.IsMouseCaptured)
            {
                _handle.ReleaseMouseCapture();
            }

            Panel.SetZIndex(_target, _originalZIndex);
            _changed();
        }
        finally
        {
            _isEnding = false;
        }
    }

    private static bool IsButtonDescendant(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static Canvas? FindCanvas(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Canvas canvas)
            {
                return canvas;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static double GetCanvasOffset(double canvasOffset, double layoutOffset)
    {
        return double.IsFinite(canvasOffset)
            ? canvasOffset
            : double.IsFinite(layoutOffset) ? layoutOffset : 0;
    }
}
