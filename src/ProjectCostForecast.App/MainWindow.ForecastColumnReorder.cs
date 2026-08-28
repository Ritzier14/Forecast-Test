using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private sealed class ForecastColumnReorderSession
    {
        public ForecastColumnReorderSession(DataGridColumn column, Point startPoint)
        {
            Column = column;
            StartPoint = startPoint;
        }

        public DataGridColumn Column { get; }
        public Point StartPoint { get; }
        public bool IsDragging { get; set; }
    }

    private sealed class ColumnReorderAdorner : Adorner
    {
        private double _x;

        public ColumnReorderAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public void SetX(double x)
        {
            _x = Math.Max(0, x);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var fill = new SolidColorBrush(Color.FromArgb(40, 37, 99, 235));
            fill.Freeze();
            var pen = new Pen(BrushFactory.Frozen("#2563EB"), 2);
            pen.Freeze();
            drawingContext.DrawRectangle(fill, null, new Rect(_x - 3, 0, 6, ActualHeight));
            drawingContext.DrawLine(pen, new Point(_x, 0), new Point(_x, ActualHeight));
        }
    }

    private void ForecastLinesGrid_ColumnReorderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not DataGrid grid
            || e.OriginalSource is not DependencyObject source
            // The shared column-header template exposes resize thumbs at
            // both edges. Let those thumbs keep their native width-resize
            // gesture instead of interpreting it as a column drag.
            || FindParent<Thumb>(source) is not null
            || FindParent<DataGridColumnHeader>(source) is not { Column: { } column })
        {
            return;
        }

        _forecastColumnReorder = new ForecastColumnReorderSession(column, e.GetPosition(grid));
    }

    private void ForecastLinesGrid_ColumnReorderMouseMove(object sender, MouseEventArgs e)
    {
        if (_forecastColumnReorder is not { } session
            || sender is not DataGrid grid
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(grid);
        if (!session.IsDragging
            && (Math.Abs(current.X - session.StartPoint.X) < DragThreshold
                && Math.Abs(current.Y - session.StartPoint.Y) < DragThreshold))
        {
            return;
        }

        if (!session.IsDragging)
        {
            session.IsDragging = true;
            grid.CaptureMouse();
        }

        var target = FindColumnDropTarget(grid, current, out var insertAfter);
        if (target is null)
        {
            RemoveForecastColumnReorderAdorner();
            e.Handled = true;
            return;
        }

        var targetIndex = target.DisplayIndex + (insertAfter ? 1 : 0);
        if (session.Column.DisplayIndex < targetIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Clamp(targetIndex, 0, grid.Columns.Count - 1);
        if (targetIndex == session.Column.DisplayIndex)
        {
            // Do not paint an insertion line over the source column or while
            // the pointer is in a position that would leave the layout
            // unchanged. The line is feedback for an actionable move only.
            RemoveForecastColumnReorderAdorner();
            e.Handled = true;
            return;
        }

        session.Column.DisplayIndex = targetIndex;
        QueueCaptureGridColumnState(grid);

        var header = FindColumnHeader(grid, target);
        if (header is not null)
        {
            EnsureForecastColumnReorderAdorner(grid);
            var point = header.TranslatePoint(new Point(insertAfter ? header.ActualWidth : 0, 0), grid);
            _forecastColumnReorderAdorner?.SetX(point.X);
        }

        e.Handled = true;
    }

    private void ForecastLinesGrid_ColumnReorderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_forecastColumnReorder is not { } session)
        {
            return;
        }

        if (sender is DataGrid grid && session.IsDragging)
        {
            QueueCaptureGridColumnState(grid);
            e.Handled = true;
        }

        EndForecastColumnReorder();
    }

    private void ForecastLinesGrid_ColumnReorderLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_forecastColumnReorder?.IsDragging == true)
        {
            EndForecastColumnReorder();
        }
    }

    private void EndForecastColumnReorder()
    {
        _forecastColumnReorder = null;
        if (ForecastLinesGrid.IsMouseCaptured)
        {
            ForecastLinesGrid.ReleaseMouseCapture();
        }

        RemoveForecastColumnReorderAdorner();
    }

    private void EnsureForecastColumnReorderAdorner(DataGrid grid)
    {
        if (_forecastColumnReorderAdorner is not null)
        {
            return;
        }

        if (AdornerLayer.GetAdornerLayer(grid) is { } layer)
        {
            _forecastColumnReorderAdorner = new ColumnReorderAdorner(grid);
            layer.Add(_forecastColumnReorderAdorner);
        }
    }

    private void RemoveForecastColumnReorderAdorner()
    {
        var adorner = _forecastColumnReorderAdorner;
        if (adorner is null)
        {
            return;
        }

        if (AdornerLayer.GetAdornerLayer(ForecastLinesGrid) is { } layer)
        {
            layer.Remove(adorner);
        }

        _forecastColumnReorderAdorner = null;
    }

    private static DataGridColumn? FindColumnDropTarget(DataGrid grid, Point point, out bool insertAfter)
    {
        insertAfter = false;
        var headers = FindChildren<DataGridColumnHeader>(grid)
            .Where(header => header.Column is not null && header.Visibility == Visibility.Visible)
            .OrderBy(header => header.TranslatePoint(new Point(0, 0), grid).X)
            .ToList();
        if (headers.Count == 0)
        {
            return null;
        }

        var target = headers.FirstOrDefault(header =>
        {
            var origin = header.TranslatePoint(new Point(0, 0), grid);
            return point.X >= origin.X && point.X <= origin.X + header.ActualWidth;
        });
        target ??= point.X < headers[0].TranslatePoint(new Point(0, 0), grid).X ? headers[0] : headers[^1];
        var targetOrigin = target.TranslatePoint(new Point(0, 0), grid);
        insertAfter = point.X > targetOrigin.X + target.ActualWidth / 2;
        return target.Column;
    }

    private static DataGridColumnHeader? FindColumnHeader(DataGrid grid, DataGridColumn column)
    {
        return FindChildren<DataGridColumnHeader>(grid)
            .FirstOrDefault(header => ReferenceEquals(header.Column, column));
    }
}
