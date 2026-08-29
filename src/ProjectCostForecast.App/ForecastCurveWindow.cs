using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public sealed class ForecastCurveWindow : Window
{
    private const double PlotLeft = 58;
    private const double PlotTop = 24;
    private const double PlotRight = 24;
    private const double PlotBottom = 58;
    private readonly Canvas _canvas;
    private readonly ObservableCollection<ForecastCurveValueRow> _rows;
    private readonly ObservableCollection<ForecastCurveSummaryRow> _summaryRows;
    private readonly MainWindowViewModel? _viewModel;
    private readonly string _resourceName;
    private readonly Grid _valuesGrid;
    private readonly List<FrameworkElement> _points = [];
    private readonly List<Rectangle> _monthlyBars = [];
    private readonly List<Button> _lockButtons = [];
    private readonly ComboBox _presetBox;
    private Path? _newPath;
    private decimal[] _newCumulative;
    private IReadOnlyList<decimal>? _presetPreviewValues;
    private bool _showMonthlyBars = true;
    private int _effectRadius = 2;
    private int _dragIndex = -1;
    private decimal _axisMax;

    public ForecastCurveWindow(string resourceName, string costCode, IReadOnlyList<ForecastCurvePoint> points, MainWindowViewModel? viewModel = null)
    {
        _viewModel = viewModel;
        _resourceName = resourceName;
        _rows = new ObservableCollection<ForecastCurveValueRow>(points.Select(point => new ForecastCurveValueRow
        {
            PeriodKey = point.PeriodKey,
            Month = point.Month,
            FiscalPeriod = point.FiscalPeriod,
            ExistingValue = point.Value,
            NewValue = point.Value,
            IsLocked = point.IsLocked
        }));
        _summaryRows =
        [
            new ForecastCurveSummaryRow("Current", _rows.Select(row => row.ExistingValue).ToList()),
            new ForecastCurveSummaryRow("New", _rows.Select(row => row.NewValue).ToList())
        ];
        _newCumulative = ForecastCurveMath.BuildCumulative(_rows.Select(row => row.NewValue));

        Title = $"Adjust curve - {resourceName}";
        Width = Math.Max(760, 150 + points.Count * 72);
        Height = 650;
        MinWidth = 700;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(138) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        heading.Children.Add(new TextBlock
        {
            Text = resourceName,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#0F172A")
        });
        heading.Children.Add(new TextBlock
        {
            Text = $"Cost code: {costCode}",
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = BrushFactory.Frozen("#64748B")
        });
        root.Children.Add(heading);

        var presetPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12)
        };
        var presetRow = new WrapPanel
        {
            ItemHeight = 38,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var actionRow = new WrapPanel
        {
            ItemHeight = 38
        };
        presetPanel.Children.Add(presetRow);
        presetPanel.Children.Add(actionRow);
        presetRow.Children.Add(new TextBlock
        {
            Text = "Preset curve",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 8, 0)
        });
        _presetBox = new ComboBox
        {
            Width = 185
        };
        foreach (var preset in ForecastCurvePresets.All)
        {
            var item = new ComboBoxItem { Content = BuildPresetLabel(preset, "Built-in"), Tag = preset };
            item.MouseEnter += (_, _) => PreviewPreset(preset);
            item.MouseLeave += (_, _) => ClearPresetPreview();
            _presetBox.Items.Add(item);
        }

        if (_viewModel?.UserForecastCurvePresets.Count > 0)
        {
            _presetBox.Items.Add(new ComboBoxItem
            {
                Content = "User Presets",
                IsEnabled = false,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFactory.Frozen("#94A3B8")
            });
            foreach (var preset in _viewModel.UserForecastCurvePresets)
            {
                var item = new ComboBoxItem
                {
                    Content = BuildPresetLabel(preset.Name, $"{preset.MonthCount} months"),
                    Tag = preset
                };
                item.MouseEnter += (_, _) => PreviewUserPreset(preset);
                item.MouseLeave += (_, _) => ClearPresetPreview();
                _presetBox.Items.Add(item);
            }
        }

        _presetBox.SelectedIndex = 0;
        presetRow.Children.Add(_presetBox);
        presetRow.Children.Add(new TextBlock
        {
            Text = "Select to apply; hover to preview.",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 6, 0),
            Foreground = BrushFactory.Frozen("#64748B")
        });
        presetRow.Children.Add(CreateLegend("Existing", "#94A3B8", new Thickness(18, 0, 0, 0), isDashed: true));
        presetRow.Children.Add(CreateLegend("New cumulative", "#176B8C", new Thickness(12, 0, 0, 0)));
        presetRow.Children.Add(CreateLegend("Current monthly", "#94A3B8", new Thickness(12, 0, 0, 0)));
        presetRow.Children.Add(CreateLegend("Monthly forecast", "#93C5FD", new Thickness(12, 0, 0, 0)));
        var toggleBars = new Button
        {
            Content = "Hide monthly bars",
            MinWidth = 132,
            Height = 32,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 10, 0)
        };
        toggleBars.Click += (_, _) =>
        {
            _showMonthlyBars = !_showMonthlyBars;
            toggleBars.Content = _showMonthlyBars ? "Hide monthly bars" : "Show monthly bars";
            DrawChart();
        };
        actionRow.Children.Add(toggleBars);
        var unlockAll = new Button
        {
            Content = "Unlock all",
            MinWidth = 88,
            Height = 32,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 24, 0)
        };
        unlockAll.Click += (_, _) =>
        {
            foreach (var row in _rows)
            {
                row.IsLocked = false;
            }

            DrawChart();
        };
        actionRow.Children.Add(unlockAll);
        actionRow.Children.Add(new TextBlock
        {
            Text = "Adjustment range",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 8, 0)
        });
        AddEffectRangeButton(actionRow, "Tight", 1);
        AddEffectRangeButton(actionRow, "Nearby", 2, isSelected: true);
        AddEffectRangeButton(actionRow, "Wide", 4);
        AddEffectRangeButton(actionRow, "Full", int.MaxValue);
        if (_viewModel is not null)
        {
            var savePreset = new Button
            {
                Content = "Save preset",
                MinWidth = 92,
                Height = 32,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(18, 0, 6, 0)
            };
            savePreset.Click += (_, _) => SaveCurrentPreset();
            actionRow.Children.Add(savePreset);
            var managePresets = new Button
            {
                Content = "Manage",
                MinWidth = 76,
                Height = 32,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0)
            };
            managePresets.Click += (_, _) => ManagePresets();
            actionRow.Children.Add(managePresets);
        }
        Grid.SetRow(presetPanel, 1);
        root.Children.Add(presetPanel);

        _canvas = new Canvas { Background = Brushes.White, ClipToBounds = true };
        _canvas.SizeChanged += (_, _) => DrawChart();
        _presetBox.SelectionChanged += (_, _) => ApplySelectedPreset();
        var chartBorder = new Border
        {
            BorderBrush = BrushFactory.Frozen("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Child = _canvas
        };
        Grid.SetRow(chartBorder, 2);
        root.Children.Add(chartBorder);

        _valuesGrid = new Grid();
        var valuesScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _valuesGrid
        };
        var valuesBorder = new Border
        {
            BorderBrush = BrushFactory.Frozen("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Height = 125,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
            Child = valuesScroll
        };
        BuildValuesGrid();
        Grid.SetRow(valuesBorder, 3);
        root.Children.Add(valuesBorder);

        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = "Drag a point up or down to fine-tune a month.",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = BrushFactory.Frozen("#64748B")
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(cancel);
        var apply = new Button { Content = "Apply curve", MinWidth = 105 };
        apply.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(apply);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
    }

    private static FrameworkElement CreateLegend(string text, string colour, Thickness margin, bool isDashed = false)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = margin };
        if (isDashed)
        {
            panel.Children.Add(new Canvas
            {
                Width = 22,
                Height = 10,
                Margin = new Thickness(0, 0, 5, 0),
                Children =
                {
                    new Line
                    {
                        X1 = 0,
                        X2 = 22,
                        Y1 = 5,
                        Y2 = 5,
                        Stroke = BrushFactory.Frozen(colour),
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection([3, 2])
                    }
                }
            });
        }
        else
        {
            panel.Children.Add(new Border
            {
                Width = 18,
                Height = 3,
                Background = BrushFactory.Frozen(colour),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
        }

        panel.Children.Add(new TextBlock { Text = text, Foreground = BrushFactory.Frozen("#475569"), VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private void BuildValuesGrid()
    {
        _valuesGrid.Children.Clear();
        _valuesGrid.ColumnDefinitions.Clear();
        _valuesGrid.RowDefinitions.Clear();

        _valuesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        _valuesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        _valuesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        _valuesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        _valuesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        for (var index = 0; index < _rows.Count; index++)
        {
            _valuesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        }

        AddValueGridCell(0, 0, string.Empty, "#DCEBF8", fontWeight: FontWeights.SemiBold);
        AddValueGridCell(1, 0, string.Empty, "#DCEBF8", fontWeight: FontWeights.SemiBold);
        AddValueGridCell(2, 0, "Current", "#F8FAFC", foreground: "#64748B", fontWeight: FontWeights.SemiBold, horizontalAlignment: HorizontalAlignment.Left);
        AddValueGridCell(3, 0, "New", "#FFFFFF", fontWeight: FontWeights.SemiBold, horizontalAlignment: HorizontalAlignment.Left);

        for (var index = 0; index < _rows.Count; index++)
        {
            var column = index + 1;
            AddValueGridCell(0, column, _rows[index].FiscalPeriod, "#D9EBC7", fontWeight: FontWeights.SemiBold);
            AddValueGridCell(1, column, _rows[index].Month, "#CFE4F5", fontWeight: FontWeights.SemiBold, fontStyle: FontStyles.Italic);
            AddValueGridCell(2, column, _rows[index].ExistingValue.ToString("C0", CultureInfo.CurrentCulture), "#F8FAFC", foreground: "#64748B", horizontalAlignment: HorizontalAlignment.Right);

            var editor = new TextBox
            {
                Text = _rows[index].NewValue.ToString("C0", CultureInfo.CurrentCulture),
                Tag = index,
                BorderThickness = new Thickness(0),
                Background = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0)
            };
            editor.LostFocus += NewValueEditor_Commit;
            editor.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    NewValueEditor_Commit(editor, e);
                    e.Handled = true;
                    MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            };
            AddValueGridBorder(3, column, "#FFFFFF");
            Grid.SetRow(editor, 3);
            Grid.SetColumn(editor, column);
            _valuesGrid.Children.Add(editor);
        }
    }

    private void AddValueGridCell(
        int row,
        int column,
        string text,
        string background,
        string foreground = "#0F172A",
        FontWeight? fontWeight = null,
        FontStyle? fontStyle = null,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center)
    {
        AddValueGridBorder(row, column, background);
        var block = new TextBlock
        {
            Text = text,
            Foreground = BrushFactory.Frozen(foreground),
            FontWeight = fontWeight ?? FontWeights.Normal,
            FontStyle = fontStyle ?? FontStyles.Normal,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = horizontalAlignment == HorizontalAlignment.Right
                ? new Thickness(6, 0, 8, 0)
                : new Thickness(8, 0, 8, 0)
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        _valuesGrid.Children.Add(block);
    }

    private void AddValueGridBorder(int row, int column, string background)
    {
        var border = new Border
        {
            Background = BrushFactory.Frozen(background),
            BorderBrush = BrushFactory.Frozen("#D8E2EE"),
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        _valuesGrid.Children.Add(border);
    }

    private void NewValueEditor_Commit(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { Tag: int index } editor || index < 0 || index >= _rows.Count)
        {
            return;
        }

        var text = editor.Text.Trim();
        if (!decimal.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out var value)
            && !decimal.TryParse(text, NumberStyles.Currency, CultureInfo.InvariantCulture, out value))
        {
            editor.Text = _rows[index].NewValue.ToString("C0", CultureInfo.CurrentCulture);
            return;
        }

        _rows[index].NewValue = Math.Round(value, 0);
        _rows[index].IsLocked = true;
        _newCumulative = ForecastCurveMath.BuildCumulative(_rows.Select(item => item.NewValue));
        RefreshSummaryRows();
        DrawChart();
        BuildValuesGrid();
    }

    private void AddEffectRangeButton(Panel panel, string label, int radius, bool isSelected = false)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 58,
            Height = 32,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 0),
            Tag = radius,
            FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal
        };
        button.Click += (_, _) =>
        {
            _effectRadius = radius;
            foreach (var sibling in panel.Children.OfType<Button>().Where(item => item.Tag is int))
            {
                sibling.FontWeight = ReferenceEquals(sibling, button) ? FontWeights.Bold : FontWeights.Normal;
            }
        };
        panel.Children.Add(button);
    }

    public IReadOnlyList<decimal> Values => _rows.Select(row => row.NewValue).ToList();

    private void ApplySelectedPreset()
    {
        if (_presetBox.SelectedItem is ComboBoxItem { Tag: UserForecastCurvePreset userPreset })
        {
            ApplyUserPreset(userPreset);
            return;
        }

        var preset = GetSelectedPreset();
        if (string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        var values = BuildPresetValues(preset);
        _presetPreviewValues = null;
        for (var index = 0; index < _rows.Count; index++)
        {
            if (!_rows[index].IsLocked)
            {
                _rows[index].NewValue = values[index];
            }
        }

        _newCumulative = ForecastCurveMath.BuildCumulative(_rows.Select(row => row.NewValue));
        RefreshSummaryRows();
        BuildValuesGrid();
        DrawChart();
    }

    private string GetSelectedPreset()
    {
        return _presetBox.SelectedItem is ComboBoxItem { Tag: string preset }
            ? preset
            : string.Empty;
    }

    private void PreviewPreset(string preset)
    {
        _presetPreviewValues = BuildPresetValues(preset);
        DrawChart();
    }

    private void PreviewUserPreset(UserForecastCurvePreset preset)
    {
        _presetPreviewValues = BuildUserPresetValues(preset);
        DrawChart();
    }

    private List<decimal> BuildPresetValues(string preset)
    {
        var unlockedIndexes = Enumerable.Range(0, _rows.Count).Where(index => !_rows[index].IsLocked).ToList();
        var result = _rows.Select(row => row.NewValue).ToList();
        if (unlockedIndexes.Count == 0)
        {
            return result;
        }

        var sourceValues = unlockedIndexes.Select(index => _rows[index].ExistingValue).ToList();
        var presetValues = ForecastCurvePresets.Apply(preset, sourceValues);
        for (var index = 0; index < unlockedIndexes.Count; index++)
        {
            result[unlockedIndexes[index]] = presetValues[index];
        }

        return result;
    }

    private List<decimal> BuildUserPresetValues(UserForecastCurvePreset preset)
    {
        var unlockedIndexes = Enumerable.Range(0, _rows.Count).Where(index => !_rows[index].IsLocked).ToList();
        var result = _rows.Select(row => row.NewValue).ToList();
        if (unlockedIndexes.Count == 0)
        {
            return result;
        }

        var sourceValues = unlockedIndexes.Select(index => _rows[index].ExistingValue).ToList();
        var presetValues = ForecastCurvePresets.ApplyUserPreset(preset, sourceValues);
        for (var index = 0; index < unlockedIndexes.Count; index++)
        {
            result[unlockedIndexes[index]] = presetValues[index];
        }

        return result;
    }

    private void ApplyUserPreset(UserForecastCurvePreset preset)
    {
        var values = BuildUserPresetValues(preset);
        _presetPreviewValues = null;
        for (var index = 0; index < _rows.Count; index++)
        {
            if (!_rows[index].IsLocked)
            {
                _rows[index].NewValue = values[index];
            }
        }

        _newCumulative = ForecastCurveMath.BuildCumulative(_rows.Select(row => row.NewValue));
        RefreshSummaryRows();
        BuildValuesGrid();
        DrawChart();
    }

    private void SaveCurrentPreset()
    {
        if (_viewModel is null)
        {
            return;
        }

        var window = new SaveForecastCurvePresetWindow(_viewModel, _resourceName, _rows.Select(row => row.NewValue).ToList())
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            RebuildPresetSelector();
        }
    }

    private void ManagePresets()
    {
        if (_viewModel is null)
        {
            return;
        }

        var window = new ManageForecastCurvePresetsWindow(_viewModel) { Owner = this };
        if (window.ShowDialog() == true)
        {
            RebuildPresetSelector();
        }
    }

    private void RebuildPresetSelector()
    {
        _presetBox.Items.Clear();
        foreach (var preset in ForecastCurvePresets.All)
        {
            var item = new ComboBoxItem { Content = BuildPresetLabel(preset, "Built-in"), Tag = preset };
            item.MouseEnter += (_, _) => PreviewPreset(preset);
            item.MouseLeave += (_, _) => ClearPresetPreview();
            _presetBox.Items.Add(item);
        }

        if (_viewModel?.UserForecastCurvePresets.Count > 0)
        {
            _presetBox.Items.Add(new ComboBoxItem
            {
                Content = "User Presets",
                IsEnabled = false,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFactory.Frozen("#94A3B8")
            });
            foreach (var preset in _viewModel.UserForecastCurvePresets)
            {
                var item = new ComboBoxItem { Content = BuildPresetLabel(preset.Name, $"{preset.MonthCount} months"), Tag = preset };
                item.MouseEnter += (_, _) => PreviewUserPreset(preset);
                item.MouseLeave += (_, _) => ClearPresetPreview();
                _presetBox.Items.Add(item);
            }
        }

        _presetBox.SelectedIndex = 0;
    }

    private static FrameworkElement BuildPresetLabel(string name, string detail)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Canvas
        {
            Width = 28,
            Height = 16,
            Margin = new Thickness(0, 0, 7, 0),
            Children =
            {
                new Polyline
                {
                    Points = new PointCollection([new Point(1, 13), new Point(8, 10), new Point(15, 5), new Point(27, 2)]),
                    Stroke = BrushFactory.Frozen("#2563EB"),
                    StrokeThickness = 2
                }
            }
        });
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = detail, FontSize = 10, Foreground = BrushFactory.Frozen("#64748B") });
        panel.Children.Add(text);
        return panel;
    }

    private void ClearPresetPreview()
    {
        if (_presetPreviewValues is null)
        {
            return;
        }

        _presetPreviewValues = null;
        DrawChart();
    }

    private void DrawChart()
    {
        if (_canvas.ActualWidth <= PlotLeft + PlotRight || _canvas.ActualHeight <= PlotTop + PlotBottom || _rows.Count == 0)
        {
            return;
        }

        _canvas.Children.Clear();
        _points.Clear();
        _monthlyBars.Clear();
        _lockButtons.Clear();
        var existingCumulative = ForecastCurveMath.BuildCumulative(_rows.Select(row => row.ExistingValue));
        var existingMonthlyValues = _rows.Select(row => row.ExistingValue).ToList();
        var displayedMonthlyValues = _presetPreviewValues ?? _rows.Select(row => row.NewValue).ToList();
        var displayedCumulative = ForecastCurveMath.BuildCumulative(displayedMonthlyValues);
        _newCumulative = ForecastCurveMath.BuildCumulative(_rows.Select(row => row.NewValue));
        var selectedCumulativeTotal = Math.Max(existingCumulative.Max(), displayedCumulative.Max());
        _axisMax = CalculateAxisMaximum(selectedCumulativeTotal);
        var plotBottom = _canvas.ActualHeight - PlotBottom;
        DrawGrid(plotBottom);
        if (_showMonthlyBars)
        {
            DrawMonthlyBars(displayedMonthlyValues, existingMonthlyValues, plotBottom);
        }

        var existingPath = new Path
        {
            Stroke = BrushFactory.Frozen("#94A3B8"),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([4, 3]),
            Opacity = 0.85
        };
        _canvas.Children.Add(existingPath);
        _newPath = new Path { Stroke = BrushFactory.Frozen("#176B8C"), StrokeThickness = 3 };
        _canvas.Children.Add(_newPath);
        var existingPoints = new List<Point>(_rows.Count);
        var newPoints = new List<Point>(_rows.Count);

        for (var index = 0; index < _rows.Count; index++)
        {
            var x = GetX(index);
            existingPoints.Add(new Point(x, GetY(existingCumulative[index])));
            var y = GetY(displayedCumulative[index]);
            newPoints.Add(new Point(x, y));
            var label = new TextBlock
            {
                Text = $"{_rows[index].Month}\n{_rows[index].FiscalPeriod}",
                TextAlignment = TextAlignment.Center,
                FontSize = 10,
                Foreground = BrushFactory.Frozen("#475569")
            };
            Canvas.SetLeft(label, x - 30);
            Canvas.SetTop(label, plotBottom + 7);
            _canvas.Children.Add(label);

            var point = new Polygon
            {
                Width = 15,
                Height = 15,
                Points = new PointCollection([new Point(7.5, 0), new Point(15, 7.5), new Point(7.5, 15), new Point(0, 7.5)]),
                Fill = BrushFactory.Frozen(_rows[index].IsLocked ? "#94A3B8" : "#176B8C"),
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                Cursor = _rows[index].IsLocked ? Cursors.Arrow : Cursors.SizeNS,
                Tag = index,
                ToolTip = _rows[index].IsLocked
                    ? $"{_rows[index].Month}: locked"
                    : $"{_rows[index].Month}: cumulative {displayedCumulative[index]:C0}"
            };
            point.PreviewMouseLeftButtonDown += Point_MouseDown;
            point.PreviewMouseMove += Point_MouseMove;
            point.PreviewMouseLeftButtonUp += Point_MouseUp;
            Canvas.SetLeft(point, x - 7.5);
            Canvas.SetTop(point, y - 7.5);
            _points.Add(point);
            _canvas.Children.Add(point);

            var lockButton = new Button
            {
                Content = _rows[index].IsLocked ? "Locked" : "Open",
                FontSize = 10,
                MinWidth = 42,
                Padding = new Thickness(4, 1, 4, 1),
                Tag = index
            };
            lockButton.Click += LockButton_Click;
            Canvas.SetLeft(lockButton, x - 21);
            Canvas.SetTop(lockButton, plotBottom + 34);
            _lockButtons.Add(lockButton);
            _canvas.Children.Add(lockButton);
        }


        existingPath.Data = CreateSmoothGeometry(existingPoints);
        _newPath.Data = CreateSmoothGeometry(newPoints);
    }

    private void DrawMonthlyBars(IReadOnlyList<decimal> monthlyValues, IReadOnlyList<decimal> existingMonthlyValues, double plotBottom)
    {
        var spacing = _rows.Count <= 1
            ? Math.Max(40, _canvas.ActualWidth - PlotLeft - PlotRight)
            : (_canvas.ActualWidth - PlotLeft - PlotRight) / (_rows.Count - 1);
        var barWidth = Math.Clamp(spacing * 0.48, 14, 52);
        for (var index = 0; index < monthlyValues.Count; index++)
        {
            if (index < existingMonthlyValues.Count)
            {
                var existingValue = Math.Max(0, existingMonthlyValues[index]);
                var existingTop = GetY(existingValue);
                var previousBar = new Rectangle
                {
                    Width = Math.Max(8, barWidth * 0.32),
                    Height = Math.Max(0, plotBottom - existingTop),
                    Fill = BrushFactory.Frozen("#94A3B8"),
                    Opacity = 0.45,
                    RadiusX = 2,
                    RadiusY = 2,
                    ToolTip = $"{_rows[index].Month}: current monthly forecast {existingValue:C0}"
                };
                Canvas.SetLeft(previousBar, GetX(index) - (barWidth / 2) - 3);
                Canvas.SetTop(previousBar, existingTop);
                _canvas.Children.Add(previousBar);
            }

            var value = Math.Max(0, monthlyValues[index]);
            var top = GetY(value);
            var bar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(0, plotBottom - top),
                Fill = BrushFactory.Frozen(_rows[index].IsLocked ? "#CBD5E1" : "#93C5FD"),
                Opacity = 0.55,
                RadiusX = 2,
                RadiusY = 2,
                ToolTip = _rows[index].IsLocked
                    ? $"{_rows[index].Month}: locked monthly forecast {value:C0}"
                    : $"{_rows[index].Month}: monthly forecast {value:C0}"
            };
            Canvas.SetLeft(bar, GetX(index) - (barWidth / 2));
            Canvas.SetTop(bar, top);
            _monthlyBars.Add(bar);
            _canvas.Children.Add(bar);
        }
    }

    private static decimal CalculateAxisMaximum(decimal selectedCumulativeTotal)
    {
        if (selectedCumulativeTotal <= 0)
        {
            return 5;
        }

        var rawStep = selectedCumulativeTotal / 5m;
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)rawStep)));
        var roundingUnit = Math.Max(1m, magnitude / 10m);
        var cleanStep = Math.Ceiling(rawStep / roundingUnit) * roundingUnit;
        return Math.Max(5m, cleanStep * 5m);
    }

    private static Geometry CreateSmoothGeometry(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return Geometry.Empty;
        }

        if (points.Count == 1)
        {
            return new PathGeometry([new PathFigure { StartPoint = points[0] }]);
        }

        var figure = new PathFigure { StartPoint = points[0] };
        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var current = points[index];
            var previousPrevious = index > 1 ? points[index - 2] : previous;
            var next = index + 1 < points.Count ? points[index + 1] : current;
            var tangentX = (current.X - previousPrevious.X) / 6d;
            var tangentY = (current.Y - previousPrevious.Y) / 6d;
            var nextTangentX = (next.X - previous.X) / 6d;
            var nextTangentY = (next.Y - previous.Y) / 6d;

            var control1 = new Point(previous.X + tangentX, previous.Y + tangentY);
            var control2 = new Point(current.X - nextTangentX, current.Y - nextTangentY);

            if (Math.Abs(current.Y - previous.Y) < 0.001d)
            {
                control1 = new Point(previous.X + ((current.X - previous.X) * 0.35d), previous.Y);
                control2 = new Point(current.X - ((current.X - previous.X) * 0.35d), current.Y);
            }

            figure.Segments.Add(new BezierSegment(
                control1,
                control2,
                current,
                isStroked: true));
        }

        return new PathGeometry([figure]);
    }

    private void DrawGrid(double plotBottom)
    {
        var plotRight = _canvas.ActualWidth - PlotRight;
        var plotHeight = plotBottom - PlotTop;
        for (var step = 0; step <= 5; step++)
        {
            var y = PlotTop + plotHeight * step / 5d;
            _canvas.Children.Add(new Line
            {
                X1 = PlotLeft,
                X2 = plotRight,
                Y1 = y,
                Y2 = y,
                Stroke = BrushFactory.Frozen(step == 5 ? "#94A3B8" : "#E2E8F0"),
                StrokeThickness = 1
            });
            var value = _axisMax * (decimal)(1 - step / 5d);
            var label = new TextBlock { Text = value.ToString("N0"), FontSize = 11, Foreground = BrushFactory.Frozen("#64748B") };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, y - 8);
            _canvas.Children.Add(label);
        }

        for (var index = 0; index < _rows.Count; index++)
        {
            var x = GetX(index);
            _canvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = PlotTop,
                Y2 = plotBottom,
                Stroke = BrushFactory.Frozen("#CBD5E1"),
                StrokeThickness = 1
            });
        }
    }

    private double GetX(int index) => _rows.Count == 1
        ? PlotLeft
        : PlotLeft + index * ((_canvas.ActualWidth - PlotLeft - PlotRight) / (_rows.Count - 1));

    private double GetY(decimal value)
    {
        var height = _canvas.ActualHeight - PlotTop - PlotBottom;
        var ratio = _axisMax <= 0 ? 0d : (double)(Math.Max(0, value) / _axisMax);
        return PlotTop + height * (1 - Math.Clamp(ratio, 0d, 1d));
    }

    private void Point_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragIndex = (int)((FrameworkElement)sender).Tag;
        if (_rows[_dragIndex].IsLocked)
        {
            _dragIndex = -1;
            return;
        }

        ((FrameworkElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Point_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragIndex < 0 || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(_canvas);
        var plotHeight = _canvas.ActualHeight - PlotTop - PlotBottom;
        var y = Math.Clamp(position.Y, PlotTop, PlotTop + plotHeight);
        var requestedCumulative = Math.Round(_axisMax * (decimal)(1 - ((y - PlotTop) / plotHeight)), 0);
        var currentValues = _rows.Select(row => row.NewValue).ToArray();
        var monthlyValues = ForecastCurveMath.AdjustMonthlyCurve(
            currentValues,
            _dragIndex,
            requestedCumulative,
            _effectRadius,
            _rows.Select(row => row.IsLocked).ToArray());
        for (var index = 0; index < _rows.Count; index++)
        {
            _rows[index].NewValue = monthlyValues[index];
        }
        _newCumulative = ForecastCurveMath.BuildCumulative(_rows.Select(row => row.NewValue));
        RefreshSummaryRows();
        BuildValuesGrid();
        UpdatePoints();
    }

    private void Point_MouseUp(object sender, MouseButtonEventArgs e)
    {
        ((FrameworkElement)sender).ReleaseMouseCapture();
        _dragIndex = -1;
    }

    private void UpdatePoints()
    {
        if (_newPath is null)
        {
            return;
        }

        var points = new List<Point>(_rows.Count);
        for (var index = 0; index < _rows.Count; index++)
        {
            var x = GetX(index);
            var y = GetY(_newCumulative[index]);
            points.Add(new Point(x, y));
            Canvas.SetTop(_points[index], y - 7.5);
            _points[index].ToolTip = _rows[index].IsLocked
                ? $"{_rows[index].Month}: locked"
                : $"{_rows[index].Month}: cumulative {_newCumulative[index]:C0}";
        }

        if (_showMonthlyBars)
        {
            var plotBottom = _canvas.ActualHeight - PlotBottom;
            for (var index = 0; index < _monthlyBars.Count && index < _rows.Count; index++)
            {
                var top = GetY(_rows[index].NewValue);
                _monthlyBars[index].Height = Math.Max(0, plotBottom - top);
                Canvas.SetTop(_monthlyBars[index], top);
                _monthlyBars[index].Fill = BrushFactory.Frozen(_rows[index].IsLocked ? "#CBD5E1" : "#93C5FD");
                _monthlyBars[index].ToolTip = _rows[index].IsLocked
                    ? $"{_rows[index].Month}: locked monthly forecast {_rows[index].NewValue:C0}"
                    : $"{_rows[index].Month}: monthly forecast {_rows[index].NewValue:C0}";
            }
        }

        _newPath.Data = CreateSmoothGeometry(points);
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int index } || index < 0 || index >= _rows.Count)
        {
            return;
        }

        _rows[index].IsLocked = !_rows[index].IsLocked;
        DrawChart();
    }

    private void RefreshSummaryRows()
    {
        if (_summaryRows.Count < 2)
        {
            return;
        }

        _summaryRows[0].Update(_rows.Select(row => row.ExistingValue).ToList());
        _summaryRows[1].Update(_rows.Select(row => row.NewValue).ToList());
    }
}

public sealed record ForecastCurvePoint(string PeriodKey, string Month, string FiscalPeriod, decimal Value, bool IsLocked = false);

public sealed class ForecastCurveValueRow : ObservableModel
{
    private decimal _newValue;
    private bool _isLocked;

    public string PeriodKey { get; init; } = string.Empty;
    public string Month { get; init; } = string.Empty;
    public string FiscalPeriod { get; init; } = string.Empty;
    public decimal ExistingValue { get; init; }
    public decimal NewValue { get => _newValue; set => SetProperty(ref _newValue, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }
}

public sealed class ForecastCurveSummaryRow : ObservableModel
{
    private List<decimal> _values;

    public ForecastCurveSummaryRow(string label, List<decimal> values)
    {
        Label = label;
        _values = values;
    }

    public string Label { get; }
    public List<decimal> Values => _values;
    public decimal Total => _values.Sum();

    public void Update(List<decimal> values)
    {
        _values = values;
        OnPropertyChanged(nameof(Values));
        OnPropertyChanged(nameof(Total));
    }
}

public static class ForecastCurvePresets
{
    public const string Existing = "Existing curve";
    public const string SCurve = "S curve";
    public const string LazyCurve = "Lazy curve";
    public const string FrontHeavy = "Front heavy";
    public const string BackHeavy = "Back heavy";
    public static IReadOnlyList<string> All { get; } = [Existing, SCurve, LazyCurve, FrontHeavy, BackHeavy];

    public static IReadOnlyList<decimal> Apply(string preset, IReadOnlyList<decimal> existingValues)
    {
        if (existingValues.Count == 0 || string.Equals(preset, Existing, StringComparison.OrdinalIgnoreCase))
        {
            return existingValues.ToList();
        }

        return ForecastCurveMath.Distribute(
            existingValues.Sum(),
            existingValues.Count,
            ToProfile(preset));
    }

    public static IReadOnlyList<decimal> CaptureShape(IReadOnlyList<decimal> values) =>
        ForecastCurveMath.CaptureNormalizedShape(values);

    public static IReadOnlyList<decimal> ApplyUserPreset(UserForecastCurvePreset preset, IReadOnlyList<decimal> existingValues) =>
        ForecastCurveMath.ApplyNormalizedShape(existingValues, preset.Weights);

    private static ForecastCurveProfile ToProfile(string preset)
    {
        return preset switch
        {
            SCurve => ForecastCurveProfile.SCurve,
            LazyCurve => ForecastCurveProfile.Bell,
            FrontHeavy => ForecastCurveProfile.FrontLoaded,
            BackHeavy => ForecastCurveProfile.BackLoaded,
            _ => ForecastCurveProfile.Linear
        };
    }
}

public static class ForecastCurveMath
{
    /// <summary>
    /// Creates the canonical financial allocation used by both the committed
    /// forecast command and the curve editor preview. Values are rounded to
    /// cents and any residual is assigned to the largest allocated period.
    /// </summary>
    public static decimal[] Distribute(decimal total, int periodCount, ForecastCurveProfile profile)
    {
        if (periodCount <= 0)
        {
            return [];
        }

        if (periodCount == 1)
        {
            return [decimal.Round(total, 2, MidpointRounding.AwayFromZero)];
        }

        var weights = new decimal[periodCount];
        for (var index = 0; index < periodCount; index++)
        {
            weights[index] = (decimal)GetWeight(profile, index, periodCount);
        }

        return Allocate(total, weights);
    }

    public static IReadOnlyList<decimal> CaptureNormalizedShape(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var total = values.Sum();
        if (total == 0)
        {
            var even = Math.Round(1m / values.Count, 8);
            var weights = Enumerable.Repeat(even, values.Count).ToList();
            weights[^1] += 1m - weights.Sum();
            return weights;
        }

        var result = values.Select(value => Math.Round(value / total, 8)).ToList();
        result[^1] += 1m - result.Sum();
        return result;
    }

    public static IReadOnlyList<decimal> ApplyNormalizedShape(
        IReadOnlyList<decimal> existingValues,
        IReadOnlyList<decimal> normalizedShape)
    {
        if (existingValues.Count == 0)
        {
            return [];
        }

        if (normalizedShape.Count == 0)
        {
            return existingValues.ToList();
        }

        var targetTotal = existingValues.Sum();
        if (targetTotal == 0)
        {
            return Enumerable.Repeat(0m, existingValues.Count).ToList();
        }

        return Allocate(targetTotal, ResampleWeights(normalizedShape, existingValues.Count));
    }

    public static decimal[] BuildCumulative(IEnumerable<decimal> monthlyValues)
    {
        var total = 0m;
        return monthlyValues.Select(value => total += value).ToArray();
    }

    public static decimal[] ToMonthlyValues(IReadOnlyList<decimal> cumulativeValues)
    {
        var result = new decimal[cumulativeValues.Count];
        for (var index = 0; index < cumulativeValues.Count; index++)
        {
            result[index] = cumulativeValues[index] - (index == 0 ? 0 : cumulativeValues[index - 1]);
        }

        return result;
    }

    public static decimal[] MoveCumulativePoint(IReadOnlyList<decimal> cumulativeValues, int index, decimal requestedValue)
    {
        var result = cumulativeValues.ToArray();
        if (index < 0 || index >= result.Length)
        {
            return result;
        }

        var minimum = index == 0 ? 0 : result[index - 1];
        var maximum = index + 1 < result.Length ? result[index + 1] : decimal.MaxValue;
        result[index] = Math.Clamp(requestedValue, minimum, maximum);
        return result;
    }

    public static decimal[] AdjustMonthlyCurve(IReadOnlyList<decimal> monthlyValues, int markerIndex, decimal requestedCumulative, int effectRadius)
    {
        var result = monthlyValues.ToArray();
        if (markerIndex < 0 || markerIndex >= result.Length)
        {
            return result;
        }

        var cumulative = BuildCumulative(result);
        var delta = requestedCumulative - cumulative[markerIndex];
        if (delta == 0)
        {
            return result;
        }

        var radius = effectRadius == int.MaxValue ? result.Length : Math.Max(1, effectRadius);
        var leftIndexes = BuildLocalIndexes(markerIndex, radius, result.Length, includeCurrent: true, direction: -1);
        if (markerIndex == result.Length - 1)
        {
            if (delta < 0)
            {
                delta = -Math.Min(-delta, leftIndexes.Sum(index => result[index]));
            }

            ApplyWeightedChange(result, leftIndexes, delta, towardEnd: true);
            return result;
        }

        var rightIndexes = BuildLocalIndexes(markerIndex, radius, result.Length, includeCurrent: false, direction: 1);
        if (delta > 0)
        {
            rightIndexes = ExpandIndexesForCapacity(rightIndexes, markerIndex + 1, result.Length - 1, delta, result);
            var transfer = Math.Min(delta, rightIndexes.Sum(index => result[index]));
            ApplyWeightedChange(result, leftIndexes, transfer, towardEnd: true);
            ApplyWeightedChange(result, rightIndexes, -transfer, towardEnd: false);
        }
        else
        {
            leftIndexes = ExpandIndexesForCapacity(leftIndexes, markerIndex, 0, -delta, result);
            var transfer = Math.Min(-delta, leftIndexes.Sum(index => result[index]));
            ApplyWeightedChange(result, leftIndexes, -transfer, towardEnd: true);
            ApplyWeightedChange(result, rightIndexes, transfer, towardEnd: false);
        }

        return result;
    }

    public static decimal[] AdjustMonthlyCurve(
        IReadOnlyList<decimal> monthlyValues,
        int markerIndex,
        decimal requestedCumulative,
        int effectRadius,
        IReadOnlyList<bool> lockedMonths)
    {
        var result = AdjustMonthlyCurve(monthlyValues, markerIndex, requestedCumulative, effectRadius);
        if (lockedMonths.Count != monthlyValues.Count || !lockedMonths.Any(isLocked => isLocked))
        {
            return result;
        }

        var redistribution = 0m;
        for (var index = 0; index < result.Length; index++)
        {
            if (!lockedMonths[index])
            {
                continue;
            }

            redistribution += result[index] - monthlyValues[index];
            result[index] = monthlyValues[index];
        }

        if (redistribution == 0)
        {
            return result;
        }

        var unlockedIndexes = Enumerable.Range(0, result.Length)
            .Where(index => !lockedMonths[index])
            .ToList();
        if (unlockedIndexes.Count == 0)
        {
            return monthlyValues.ToArray();
        }

        ApplyWeightedChange(result, unlockedIndexes, redistribution, towardEnd: true);
        return result;
    }

    private static List<int> BuildLocalIndexes(int markerIndex, int radius, int length, bool includeCurrent, int direction)
    {
        var indexes = new List<int>();
        if (direction < 0)
        {
            var start = includeCurrent ? markerIndex : markerIndex - 1;
            for (var index = start; index >= 0 && indexes.Count < radius; index--)
            {
                indexes.Insert(0, index);
            }
        }
        else
        {
            var start = includeCurrent ? markerIndex : markerIndex + 1;
            for (var index = start; index < length && indexes.Count < radius; index++)
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    private static List<int> ExpandIndexesForCapacity(List<int> indexes, int start, int boundary, decimal requiredCapacity, IReadOnlyList<decimal> values)
    {
        if (requiredCapacity <= 0)
        {
            return indexes;
        }

        var capacity = indexes.Sum(index => values[index]);
        if (capacity >= requiredCapacity)
        {
            return indexes;
        }

        var step = boundary >= start ? 1 : -1;
        var index = indexes.Count > 0 ? indexes[^1] + step : start;
        while ((step > 0 && index <= boundary) || (step < 0 && index >= boundary))
        {
            if (!indexes.Contains(index))
            {
                indexes.Add(index);
                capacity += values[index];
                if (capacity >= requiredCapacity)
                {
                    break;
                }
            }

            index += step;
        }

        if (step < 0)
        {
            indexes.Sort();
        }

        return indexes;
    }

    private static void ApplyWeightedChange(decimal[] values, IReadOnlyList<int> indexes, decimal change, bool towardEnd)
    {
        if (indexes.Count == 0 || change == 0)
        {
            return;
        }

        var weights = Enumerable.Range(0, indexes.Count)
            .Select(index =>
            {
                var position = towardEnd ? index + 1d : indexes.Count - index;
                var normalised = position / indexes.Count;
                return normalised * normalised * (3d - 2d * normalised);
            })
            .ToArray();
        var remaining = change;
        var remainingWeight = weights.Sum();
        for (var index = 0; index < indexes.Count; index++)
        {
            var valueIndex = indexes[index];
            var amount = index == indexes.Count - 1
                ? remaining
                : Math.Round(remaining * (decimal)(weights[index] / remainingWeight), 0);
            if (amount < 0 && -amount > values[valueIndex])
            {
                amount = -values[valueIndex];
            }

            values[valueIndex] += amount;
            remaining -= amount;
            remainingWeight -= weights[index];
        }

        if (remaining != 0)
        {
            var target = indexes.FirstOrDefault(index => remaining >= 0 || values[index] >= -remaining, -1);
            if (target >= 0)
            {
                values[target] += remaining;
            }
        }
    }

    private static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        if (weights.Count == 0)
        {
            return [];
        }

        var weightTotal = weights.Sum();
        if (weightTotal <= 0)
        {
            weightTotal = weights.Count;
            weights = Enumerable.Repeat(1m, weights.Count).ToArray();
        }

        var amounts = new decimal[weights.Count];
        decimal allocated = 0;
        var largestIndex = 0;
        for (var index = 0; index < weights.Count; index++)
        {
            amounts[index] = decimal.Round(
                total * (weights[index] / weightTotal),
                2,
                MidpointRounding.AwayFromZero);
            allocated += amounts[index];
            if (Math.Abs(amounts[index]) > Math.Abs(amounts[largestIndex]))
            {
                largestIndex = index;
            }
        }

        amounts[largestIndex] += total - allocated;
        return amounts;
    }

    private static List<decimal> ResampleWeights(IReadOnlyList<decimal> weights, int targetCount)
    {
        if (targetCount <= 0)
        {
            return [];
        }

        if (weights.Count == targetCount)
        {
            return weights.ToList();
        }

        if (targetCount == 1)
        {
            return [1m];
        }

        if (weights.Count == 1)
        {
            return Enumerable.Repeat(Math.Round(1m / targetCount, 8), targetCount).ToList();
        }

        var result = new List<decimal>(targetCount);
        for (var index = 0; index < targetCount; index++)
        {
            var position = (double)index * (weights.Count - 1) / (targetCount - 1);
            var left = (int)Math.Floor(position);
            var right = Math.Min(weights.Count - 1, left + 1);
            var blend = (decimal)(position - left);
            result.Add(Math.Round(weights[left] + ((weights[right] - weights[left]) * blend), 8));
        }

        var total = result.Sum();
        if (total == 0)
        {
            return Enumerable.Repeat(Math.Round(1m / targetCount, 8), targetCount).ToList();
        }

        result = result.Select(weight => Math.Round(weight / total, 8)).ToList();
        result[^1] += 1m - result.Sum();
        return result;
    }

    private static double GetWeight(ForecastCurveProfile profile, int index, int periodCount)
    {
        var t = (index + 0.5) / periodCount;
        return profile switch
        {
            ForecastCurveProfile.FrontLoaded => 2.0 * (1.0 - t),
            ForecastCurveProfile.BackLoaded => 2.0 * t,
            ForecastCurveProfile.Bell => Math.Sin(Math.PI * t),
            ForecastCurveProfile.SCurve => SCurveWeight(index, periodCount),
            _ => 1.0
        };
    }

    private static double SCurveWeight(int index, int periodCount)
    {
        const double steepness = 8.0;
        var start = Logistic((double)index / periodCount, steepness);
        var end = Logistic((double)(index + 1) / periodCount, steepness);
        return end - start;
    }

    private static double Logistic(double x, double steepness)
    {
        return 1.0 / (1.0 + Math.Exp(-steepness * (x - 0.5)));
    }
}
