using System.Windows;

namespace ProjectCostForecast.App;

internal readonly record struct RightClickGridPanMove(double HorizontalOffset, double VerticalOffset);

internal sealed class RightClickGridPanSession
{
    private Point? _startPoint;
    private double _horizontalStartOffset;
    private double _verticalStartOffset;

    public bool IsActive => _startPoint.HasValue;

    public bool IsDragging { get; private set; }

    public void Begin(Point startPoint, double horizontalOffset, double verticalOffset)
    {
        _startPoint = startPoint;
        _horizontalStartOffset = horizontalOffset;
        _verticalStartOffset = verticalOffset;
        IsDragging = false;
    }

    public RightClickGridPanMove? TryMove(
        Point currentPoint,
        bool rightButtonPressed,
        double maximumHorizontalOffset,
        double maximumVerticalOffset)
    {
        if (!IsActive || !rightButtonPressed)
        {
            return null;
        }

        var deltaX = currentPoint.X - _startPoint!.Value.X;
        var deltaY = currentPoint.Y - _startPoint.Value.Y;
        if (!IsDragging && !RightClickGridPanBehavior.IsPastDragThreshold(deltaX, deltaY))
        {
            return null;
        }

        IsDragging = true;
        return new RightClickGridPanMove(
            RightClickGridPanBehavior.ClampScrollOffset(
                _horizontalStartOffset - deltaX,
                maximumHorizontalOffset),
            RightClickGridPanBehavior.ClampScrollOffset(
                _verticalStartOffset - deltaY,
                maximumVerticalOffset));
    }

    public bool End()
    {
        var wasDragging = IsDragging;
        Reset();
        return wasDragging;
    }

    public void Cancel()
    {
        Reset();
    }

    private void Reset()
    {
        _startPoint = null;
        _horizontalStartOffset = 0;
        _verticalStartOffset = 0;
        IsDragging = false;
    }
}
