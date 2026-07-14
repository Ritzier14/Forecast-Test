using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace ProjectCostForecast.App;

public partial class HeaderColorPickerPopup : UserControl
{
    private static readonly IReadOnlyList<GradientPreset> Presets =
    [
        new("Default header", null, ["#F8FAFC", "#ECF1F6", "#E1E8F0"], [0, 0.5, 1]),
        new("Linear blue", "Right", ["#F8FBFF", "#3B82F6", "#1E40AF"], [0, 0.5, 1]),
        new("Linear green", "Right", ["#F0FDF4", "#22C55E", "#15803D"], [0, 0.5, 1]),
        new("Linear red", "Right", ["#FEF2F2", "#EF4444", "#B91C1C"], [0, 0.5, 1]),
        new("Linear grey", "Right", ["#FFFFFF", "#CBD5E1", "#475569"], [0, 0.55, 1]),
        new("Sky blue", "DownRight", ["#E0F2FE", "#7DD3FC", "#2563EB"], [0, 0.52, 1]),
        new("Sunset", "Right", ["#EC4899", "#F97316", "#FDE047"], [0, 0.55, 1]),
        new("Gold", "Right", ["#FEF3C7", "#FBBF24", "#D97706"], [0, 0.5, 1]),
        new("Rainbow", "Right", ["#EF4444", "#F59E0B", "#22C55E", "#06B6D4", "#4F46E5"], [0, 0.25, 0.5, 0.75, 1]),
        new("Heat", "Right", ["#FFEDD5", "#FB923C", "#DC2626"], [0, 0.45, 1]),
        new("Ocean", "Right", ["#CFFAFE", "#06B6D4", "#1E3A8A"], [0, 0.5, 1]),
        new("Forest", "Right", ["#DCFCE7", "#16A34A", "#064E3B"], [0, 0.52, 1])
    ];

    private static readonly IReadOnlyList<string> ColourWellHexes =
    [
        "#FFFFFF", "#F8FAFC", "#E2E8F0", "#94A3B8", "#334155", "#0F172A",
        "#FECACA", "#FCA5A5", "#EF4444", "#FDBA74", "#F97316", "#FDE68A",
        "#FACC15", "#BBF7D0", "#22C55E", "#86EFAC", "#06B6D4", "#BAE6FD",
        "#3B82F6", "#1E40AF", "#C4B5FD", "#8B5CF6", "#F9A8D4", "#EC4899"
    ];

    private readonly Action<string?> _apply;
    private readonly string? _initialSpec;
    private readonly List<EditableGradientStop> _stops = [];
    private readonly List<Button> _presetButtons = [];
    private readonly Dictionary<string, Button> _directionButtons = new(StringComparer.OrdinalIgnoreCase);

    private Border _previewBorder = null!;
    private Canvas _stopMarkerCanvas = null!;
    private ComboBox _gradientTypeBox = null!;
    private TextBox _positionTextBox = null!;
    private TextBox _hexTextBox = null!;
    private TextBox _opacityTextBox = null!;
    private Border _selectedColourPreview = null!;
    private TextBlock _statusText = null!;
    private Button _removeStopButton = null!;
    private Button _duplicateStopButton = null!;

    private string _gradientType = "Linear";
    private string _direction = "Right";
    private int _selectedStopIndex = 1;
    private bool _isDefaultSelection;
    private bool _isRefreshing;
    private bool _isDraggingStop;
    private EditableGradientStop? _draggedStop;

    public HeaderColorPickerPopup(string title, string? selectedSpec, Action<string?> apply)
    {
        InitializeComponent();
        _apply = apply;
        _initialSpec = selectedSpec;
        LoadSpec(selectedSpec);
        BuildEditor(title);
        RefreshAll();
    }

    public event EventHandler? CloseRequested;

    private void BuildEditor(string title)
    {
        Root.Children.Clear();
        Root.Children.Add(new Border
        {
            Background = BrushFactory.Frozen("#FFFFFF"),
            BorderBrush = BrushFactory.Frozen("#DCE4EE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(24),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 5,
                Opacity = 0.16,
                Color = Color.FromRgb(0x33, 0x41, 0x55)
            },
            Child = CreateLayout(title)
        });
    }

    private Grid CreateLayout(string title)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(CreateHeader(title));

        var content = new Grid { Margin = new Thickness(0, 22, 0, 22) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(content, 1);
        content.Children.Add(CreatePresetPanel());
        var editor = CreateEditorPanel();
        Grid.SetColumn(editor, 2);
        content.Children.Add(editor);
        root.Children.Add(content);

        var footer = CreateFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private Grid CreateHeader(string title)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Advanced Colour Settings",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#0F172A")
        });
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(title)
                ? "Create and customise reusable gradients."
                : $"Editing {title}. Create, import, and fine tune gradient colours.",
            FontSize = 13,
            Foreground = BrushFactory.Frozen("#64748B"),
            Margin = new Thickness(0, 6, 0, 0)
        });
        header.Children.Add(stack);

        var close = CreateQuietButton("X", 38, 38);
        close.FontSize = 20;
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        return header;
    }

    private Border CreatePresetPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(CreateSectionLabel("Presets"));

        var addCustom = CreateOutlinedButton("+  New custom gradient", 220, 40);
        addCustom.Margin = new Thickness(0, 12, 0, 12);
        addCustom.Click += (_, _) =>
        {
            _isDefaultSelection = false;
            LoadSpec(BrushFactory.SerializeAdvancedHeaderGradientSpec(CreateCurrentSpec() with
            {
                Stops =
                [
                    new(0, "#FFFFFF", 1),
                    new(0.5, "#3B82F6", 1),
                    new(1, "#1E40AF", 1)
                ]
            }));
            RefreshAll();
        };
        panel.Children.Add(addCustom);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var list = new StackPanel();
        scroll.Content = list;

        foreach (var preset in Presets)
        {
            var button = CreatePresetButton(preset);
            _presetButtons.Add(button);
            list.Children.Add(button);
        }

        panel.Children.Add(scroll);

        return new Border
        {
            Background = BrushFactory.Frozen("#FFFFFF"),
            BorderBrush = BrushFactory.Frozen("#DCE4EE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = panel
        };
    }

    private Button CreatePresetButton(GradientPreset preset)
    {
        var button = new Button
        {
            Height = 48,
            Width = 220,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            BorderBrush = BrushFactory.Frozen("#E2E8F0"),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = Cursors.Hand,
            Tag = preset,
            Template = CreateStretchButtonTemplate(new CornerRadius(12))
        };

        var grid = new Grid { Width = 202 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var preview = new Border
        {
            Width = 70,
            Height = 28,
            CornerRadius = new CornerRadius(5),
            Background = preset.Spec is null ? CreateDefaultHeaderGradient() : BrushFactory.FrozenHeaderGradient(preset.Spec),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        grid.Children.Add(preview);

        var text = new TextBlock
        {
            Text = preset.Name,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = BrushFactory.Frozen("#334155"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 0, 0, 0)
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var check = new TextBlock
        {
            Text = "OK",
            Visibility = Visibility.Collapsed,
            Foreground = BrushFactory.Frozen("#2563EB"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        check.Tag = "check";
        Grid.SetColumn(check, 2);
        grid.Children.Add(check);

        button.Content = grid;
        button.Click += (_, _) =>
        {
            LoadSpec(preset.Spec);
            RefreshAll();
        };
        return button;
    }

    private Border CreateEditorPanel()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var top = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.Children.Add(CreateGradientTypeControl());
        var directions = CreateDirectionControls();
        Grid.SetColumn(directions, 1);
        top.Children.Add(directions);
        root.Children.Add(top);

        var previewPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        previewPanel.Children.Add(CreateSectionLabel("Gradient preview"));
        _previewBorder = new Border
        {
            Height = 92,
            CornerRadius = new CornerRadius(5),
            BorderBrush = BrushFactory.Frozen("#DCE4EE"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 10, 0, 0)
        };
        previewPanel.Children.Add(_previewBorder);
        Grid.SetRow(previewPanel, 1);
        root.Children.Add(previewPanel);

        var stopPanel = CreateStopPanel();
        Grid.SetRow(stopPanel, 2);
        root.Children.Add(stopPanel);

        var details = CreateStopDetailsPanel();
        Grid.SetRow(details, 3);
        root.Children.Add(details);

        return new Border
        {
            Background = BrushFactory.Frozen("#FFFFFF"),
            BorderBrush = BrushFactory.Frozen("#DCE4EE"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20),
            Child = root
        };
    }

    private StackPanel CreateGradientTypeControl()
    {
        var stack = new StackPanel();
        stack.Children.Add(CreateSectionLabel("Gradient type"));
        _gradientTypeBox = new ComboBox
        {
            Height = 40,
            Width = 250,
            Margin = new Thickness(0, 10, 0, 0),
            ItemsSource = new[] { "Linear", "Radial" },
            SelectedItem = _gradientType
        };
        _gradientTypeBox.SelectionChanged += (_, _) =>
        {
            if (_isRefreshing)
            {
                return;
            }

            _gradientType = _gradientTypeBox.SelectedItem?.ToString() ?? "Linear";
            _isDefaultSelection = false;
            RefreshPreviewOnly();
        };
        stack.Children.Add(_gradientTypeBox);
        return stack;
    }

    private StackPanel CreateDirectionControls()
    {
        var stack = new StackPanel();
        stack.Children.Add(CreateSectionLabel("Direction"));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var (key, label) in new[]
                 {
                     ("Right", "Right"),
                     ("Down", "Down"),
                     ("DownLeft", "Down-left"),
                     ("UpRight", "Up-right"),
                     ("DownRight", "Down-right"),
                     ("Left", "Left")
                 })
        {
            var button = CreateOutlinedButton(label, 84, 40);
            button.Margin = new Thickness(0, 0, 10, 0);
            button.Tag = key;
            button.Click += (_, _) =>
            {
                _direction = key;
                _isDefaultSelection = false;
                RefreshDirectionButtons();
                RefreshPreviewOnly();
            };
            _directionButtons[key] = button;
            buttons.Children.Add(button);
        }

        stack.Children.Add(buttons);
        return stack;
    }

    private StackPanel CreateStopPanel()
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        stack.Children.Add(CreateSectionLabel("Gradient stops"));

        var frame = new Border
        {
            Height = 92,
            BorderBrush = BrushFactory.Frozen("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(18, 14, 18, 10)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _stopMarkerCanvas = new Canvas { Height = 42 };
        _stopMarkerCanvas.SizeChanged += (_, _) => PositionStopMarkers();
        grid.Children.Add(_stopMarkerCanvas);

        var labels = new UniformGrid { Columns = 5, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var label in new[] { "0%", "25%", "50%", "75%", "100%" })
        {
            labels.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = BrushFactory.Frozen("#64748B"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        Grid.SetRow(labels, 1);
        grid.Children.Add(labels);
        frame.Child = grid;
        stack.Children.Add(frame);
        return stack;
    }

    private Grid CreateStopDetailsPanel()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

        var details = new Grid { Margin = new Thickness(0, 0, 24, 0) };
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        details.Children.Add(CreateSectionLabel("Stop details"));

        _positionTextBox = CreateTextBox();
        AddDetailRow(details, 1, "Position", _positionTextBox, "%");
        _positionTextBox.LostFocus += (_, _) => CommitStopDetails();
        _positionTextBox.PreviewKeyDown += DetailTextBox_PreviewKeyDown;

        var colourGrid = new Grid();
        colourGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colourGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        _hexTextBox = CreateTextBox();
        _hexTextBox.CharacterCasing = CharacterCasing.Upper;
        _hexTextBox.LostFocus += (_, _) => CommitStopDetails();
        _hexTextBox.PreviewKeyDown += DetailTextBox_PreviewKeyDown;
        colourGrid.Children.Add(_hexTextBox);
        _selectedColourPreview = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(8),
            BorderBrush = BrushFactory.Frozen("#CBD5E1"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            Cursor = Cursors.Hand,
            ToolTip = "Choose colour"
        };
        _selectedColourPreview.MouseLeftButtonUp += (_, _) => ShowStopColourMenu(_selectedColourPreview);
        Grid.SetColumn(_selectedColourPreview, 1);
        colourGrid.Children.Add(_selectedColourPreview);
        AddDetailRow(details, 2, "Colour", colourGrid, string.Empty);

        _opacityTextBox = CreateTextBox();
        AddDetailRow(details, 3, "Opacity", _opacityTextBox, "%");
        _opacityTextBox.LostFocus += (_, _) => CommitStopDetails();
        _opacityTextBox.PreviewKeyDown += DetailTextBox_PreviewKeyDown;

        var colourWell = new WrapPanel { Margin = new Thickness(110, 12, 0, 0) };
        foreach (var hex in ColourWellHexes)
        {
            var swatch = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 7, 7),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = hex,
                Content = new Border
                {
                    Background = BrushFactory.Frozen(hex),
                    BorderBrush = BrushFactory.Frozen("#CBD5E1"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6)
                }
            };
            swatch.Click += (_, _) =>
            {
                _hexTextBox.Text = (string)swatch.Tag;
                CommitStopDetails();
            };
            colourWell.Children.Add(swatch);
        }

        Grid.SetRow(colourWell, 4);
        Grid.SetColumnSpan(colourWell, 3);
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.Children.Add(colourWell);
        grid.Children.Add(details);

        var actions = new StackPanel();
        actions.Children.Add(CreateSectionLabel("Stop actions"));
        var add = CreateOutlinedButton("+  Add stop", 210, 40);
        add.Margin = new Thickness(0, 12, 0, 10);
        add.Click += (_, _) => AddStop();
        actions.Children.Add(add);
        _removeStopButton = CreateOutlinedButton("-  Remove stop", 210, 40);
        _removeStopButton.Margin = new Thickness(0, 0, 0, 10);
        _removeStopButton.Click += (_, _) => RemoveSelectedStop();
        actions.Children.Add(_removeStopButton);
        _duplicateStopButton = CreateOutlinedButton("Duplicate stop", 210, 40);
        _duplicateStopButton.Click += (_, _) => DuplicateSelectedStop();
        actions.Children.Add(_duplicateStopButton);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        return grid;
    }

    private Grid CreateFooter()
    {
        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var reset = CreateOutlinedButton("Reset", 130, 42);
        reset.Click += (_, _) =>
        {
            LoadSpec(null);
            RefreshAll();
            _statusText.Text = "Default header gradient selected.";
        };
        footer.Children.Add(reset);

        var import = CreateOutlinedButton("Import", 130, 42);
        import.Margin = new Thickness(14, 0, 0, 0);
        import.Click += (_, _) => ImportGradient();
        Grid.SetColumn(import, 1);
        footer.Children.Add(import);

        var export = CreateOutlinedButton("Export", 130, 42);
        export.Margin = new Thickness(14, 0, 0, 0);
        export.Click += (_, _) => ExportGradient();
        Grid.SetColumn(export, 2);
        footer.Children.Add(export);

        _statusText = new TextBlock
        {
            Foreground = BrushFactory.Frozen("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18, 0, 0, 0)
        };
        Grid.SetColumn(_statusText, 3);
        footer.Children.Add(_statusText);

        var cancel = CreateOutlinedButton("Cancel", 130, 42);
        cancel.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(cancel, 4);
        footer.Children.Add(cancel);

        var save = CreatePrimaryButton("Save", 130, 42);
        save.Margin = new Thickness(14, 0, 0, 0);
        save.Click += (_, _) =>
        {
            CommitStopDetails();
            _apply(_isDefaultSelection ? null : BrushFactory.SerializeAdvancedHeaderGradientSpec(CreateCurrentSpec()));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        };
        Grid.SetColumn(save, 5);
        footer.Children.Add(save);
        return footer;
    }

    private void LoadSpec(string? spec)
    {
        _stops.Clear();
        _isDefaultSelection = string.IsNullOrWhiteSpace(spec);
        var advanced = string.IsNullOrWhiteSpace(spec)
            ? CreateDefaultAdvancedSpec()
            : BrushFactory.ToAdvancedHeaderGradientSpec(spec);
        _gradientType = advanced.GradientType;
        _direction = advanced.Direction;
        foreach (var stop in advanced.Stops.OrderBy(stop => stop.Offset))
        {
            _stops.Add(new EditableGradientStop(stop.Offset, stop.Hex, stop.Opacity));
        }

        _selectedStopIndex = Math.Clamp(_stops.FindIndex(stop => stop.Offset >= 0.48), 0, Math.Max(0, _stops.Count - 1));
    }

    private void RefreshAll()
    {
        _isRefreshing = true;
        _gradientTypeBox.SelectedItem = _gradientType;
        _isRefreshing = false;
        RefreshPresetSelection();
        RefreshDirectionButtons();
        RefreshStopMarkers();
        RefreshStopDetails();
        RefreshPreviewOnly();
    }

    private void RefreshPreviewOnly()
    {
        _previewBorder.Background = _isDefaultSelection
            ? CreateDefaultHeaderGradient()
            : BrushFactory.FrozenHeaderGradient(BrushFactory.SerializeAdvancedHeaderGradientSpec(CreateCurrentSpec()));
    }

    private void RefreshPresetSelection()
    {
        var current = _isDefaultSelection ? null : BrushFactory.SerializeAdvancedHeaderGradientSpec(CreateCurrentSpec());
        foreach (var button in _presetButtons)
        {
            var preset = (GradientPreset)button.Tag;
            var selected = string.IsNullOrWhiteSpace(current)
                ? preset.Spec is null
                : string.Equals(current, preset.Spec, StringComparison.OrdinalIgnoreCase);
            button.BorderBrush = selected ? BrushFactory.Frozen("#2563EB") : BrushFactory.Frozen("#E2E8F0");
            button.BorderThickness = new Thickness(1);
            if (FindDescendantByTag<TextBlock>(button, "check") is { } check)
            {
                check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void RefreshDirectionButtons()
    {
        foreach (var (key, button) in _directionButtons)
        {
            var selected = string.Equals(key, _direction, StringComparison.OrdinalIgnoreCase);
            button.BorderBrush = selected ? BrushFactory.Frozen("#2563EB") : BrushFactory.Frozen("#DCE4EE");
            button.Foreground = selected ? BrushFactory.Frozen("#1D4ED8") : BrushFactory.Frozen("#334155");
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void RefreshStopMarkers()
    {
        _stopMarkerCanvas.Children.Clear();
        for (var i = 0; i < _stops.Count; i++)
        {
            var stop = _stops[i];
            var marker = new Button
            {
                Width = 24,
                Height = 34,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = i,
                Content = CreateStopMarker(stop, i == _selectedStopIndex),
                Template = CreateStretchButtonTemplate(new CornerRadius(0))
            };
            marker.PreviewMouseLeftButtonDown += StopMarker_PreviewMouseLeftButtonDown;
            marker.PreviewMouseMove += StopMarker_PreviewMouseMove;
            marker.PreviewMouseLeftButtonUp += StopMarker_PreviewMouseLeftButtonUp;
            _stopMarkerCanvas.Children.Add(marker);
        }

        PositionStopMarkers();
    }

    private FrameworkElement CreateStopMarker(EditableGradientStop stop, bool selected)
    {
        var grid = new Grid { Width = 24, Height = 34 };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        var marker = new Border
        {
            Width = 22,
            Height = 24,
            CornerRadius = new CornerRadius(5, 5, 9, 9),
            Background = BrushFactory.Frozen(stop.Hex),
            BorderBrush = selected ? BrushFactory.Frozen("#2563EB") : BrushFactory.Frozen("#475569"),
            BorderThickness = selected ? new Thickness(2) : new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        grid.Children.Add(marker);
        var stem = new Border
        {
            Width = 2,
            Background = selected ? BrushFactory.Frozen("#2563EB") : BrushFactory.Frozen("#94A3B8"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(stem, 1);
        grid.Children.Add(stem);
        return grid;
    }

    private void PositionStopMarkers()
    {
        var width = Math.Max(1, _stopMarkerCanvas.ActualWidth - 24);
        foreach (Button marker in _stopMarkerCanvas.Children.OfType<Button>())
        {
            var index = (int)marker.Tag;
            Canvas.SetLeft(marker, _stops[index].Offset * width);
            Canvas.SetTop(marker, 0);
        }
    }

    private void StopMarker_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button marker || marker.Tag is not int index || index < 0 || index >= _stops.Count)
        {
            return;
        }

        _selectedStopIndex = index;
        _draggedStop = _stops[index];
        _isDraggingStop = true;
        marker.CaptureMouse();
        marker.Focus();
        UpdateDraggedStop(e.GetPosition(_stopMarkerCanvas));
        marker.Content = CreateStopMarker(_draggedStop, true);
        RefreshStopDetails();
        e.Handled = true;
    }

    private void StopMarker_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingStop || _draggedStop is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateDraggedStop(e.GetPosition(_stopMarkerCanvas));
        PositionStopMarkers();
        RefreshStopDetails();
        RefreshPreviewOnly();
        RefreshPresetSelection();
        e.Handled = true;
    }

    private void StopMarker_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button marker)
        {
            marker.ReleaseMouseCapture();
        }

        if (_isDraggingStop && _draggedStop is not null)
        {
            SortStopsKeepingSelection(_draggedStop);
            RefreshStopMarkers();
            RefreshStopDetails();
            RefreshPreviewOnly();
            RefreshPresetSelection();
        }

        _isDraggingStop = false;
        _draggedStop = null;
        e.Handled = true;
    }

    private void UpdateDraggedStop(Point point)
    {
        if (_draggedStop is null)
        {
            return;
        }

        var width = Math.Max(1, _stopMarkerCanvas.ActualWidth - 24);
        _draggedStop.Offset = Math.Clamp((point.X - 12) / width, 0, 1);
        _selectedStopIndex = _stops.IndexOf(_draggedStop);
        _isDefaultSelection = false;
    }

    private void RefreshStopDetails()
    {
        _selectedStopIndex = Math.Clamp(_selectedStopIndex, 0, Math.Max(0, _stops.Count - 1));
        if (_stops.Count == 0)
        {
            return;
        }

        var stop = _stops[_selectedStopIndex];
        _isRefreshing = true;
        _positionTextBox.Text = Math.Round(stop.Offset * 100).ToString(CultureInfo.InvariantCulture);
        _hexTextBox.Text = stop.Hex;
        _opacityTextBox.Text = Math.Round(stop.Opacity * 100).ToString(CultureInfo.InvariantCulture);
        _selectedColourPreview.Background = BrushFactory.Frozen(stop.Hex);
        _isRefreshing = false;
        _removeStopButton.IsEnabled = _stops.Count > 2;
        _duplicateStopButton.IsEnabled = _stops.Count < 12;
    }

    private void CommitStopDetails()
    {
        if (_isRefreshing || _stops.Count == 0)
        {
            return;
        }

        var stop = _stops[_selectedStopIndex];
        if (double.TryParse(_positionTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var position))
        {
            stop.Offset = Math.Clamp(position / 100, 0, 1);
        }

        if (BrushFactory.TryParseHexColor(_hexTextBox.Text, out var color))
        {
            stop.Hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            _hexTextBox.ClearValue(BorderBrushProperty);
        }
        else
        {
            _hexTextBox.BorderBrush = Brushes.IndianRed;
            return;
        }

        if (double.TryParse(_opacityTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
        {
            stop.Opacity = Math.Clamp(opacity / 100, 0, 1);
        }

        _isDefaultSelection = false;
        SortStopsKeepingSelection(stop);
        RefreshStopMarkers();
        RefreshStopDetails();
        RefreshPreviewOnly();
        RefreshPresetSelection();
    }

    private void DetailTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitStopDetails();
        e.Handled = true;
    }

    private void AddStop()
    {
        var offset = 0.5;
        if (_stops.Count >= 2)
        {
            var ordered = _stops.OrderBy(stop => stop.Offset).ToList();
            var widest = ordered.Zip(ordered.Skip(1), (left, right) => new { Left = left, Right = right, Gap = right.Offset - left.Offset })
                .OrderByDescending(item => item.Gap)
                .First();
            offset = widest.Left.Offset + (widest.Gap / 2);
        }

        var color = SampleColorAt(offset);
        var stop = new EditableGradientStop(offset, color, 1);
        _stops.Add(stop);
        SortStopsKeepingSelection(stop);
        _isDefaultSelection = false;
        RefreshAll();
    }

    private void RemoveSelectedStop()
    {
        if (_stops.Count <= 2)
        {
            return;
        }

        _stops.RemoveAt(_selectedStopIndex);
        _selectedStopIndex = Math.Clamp(_selectedStopIndex, 0, _stops.Count - 1);
        _isDefaultSelection = false;
        RefreshAll();
    }

    private void DuplicateSelectedStop()
    {
        if (_stops.Count == 0 || _stops.Count >= 12)
        {
            return;
        }

        var selected = _stops[_selectedStopIndex];
        var copy = new EditableGradientStop(Math.Clamp(selected.Offset + 0.05, 0, 1), selected.Hex, selected.Opacity);
        _stops.Add(copy);
        SortStopsKeepingSelection(copy);
        _isDefaultSelection = false;
        RefreshAll();
    }

    private void SortStopsKeepingSelection(EditableGradientStop selected)
    {
        _stops.Sort((left, right) => left.Offset.CompareTo(right.Offset));
        _selectedStopIndex = _stops.IndexOf(selected);
    }

    private string SampleColorAt(double offset)
    {
        var ordered = _stops.OrderBy(stop => stop.Offset).ToList();
        var left = ordered.LastOrDefault(stop => stop.Offset <= offset) ?? ordered.First();
        var right = ordered.FirstOrDefault(stop => stop.Offset >= offset) ?? ordered.Last();
        if (ReferenceEquals(left, right) || Math.Abs(right.Offset - left.Offset) < 0.001)
        {
            return left.Hex;
        }

        var t = (offset - left.Offset) / (right.Offset - left.Offset);
        _ = BrushFactory.TryParseHexColor(left.Hex, out var leftColor);
        _ = BrushFactory.TryParseHexColor(right.Hex, out var rightColor);
        return $"#{BlendByte(leftColor.R, rightColor.R, t):X2}{BlendByte(leftColor.G, rightColor.G, t):X2}{BlendByte(leftColor.B, rightColor.B, t):X2}";
    }

    private void ImportGradient()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import colour gradient",
            Filter = "Gradient files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<GradientFile>(File.ReadAllText(dialog.FileName));
            if (file is null || !BrushFactory.IsValidHeaderGradientSpec(file.GradientSpec))
            {
                _statusText.Text = "That file does not contain a valid gradient.";
                return;
            }

            LoadSpec(file.GradientSpec);
            RefreshAll();
            _statusText.Text = $"Imported {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Import failed: {ex.Message}";
        }
    }

    private void ExportGradient()
    {
        CommitStopDetails();
        var dialog = new SaveFileDialog
        {
            Title = "Export colour gradient",
            FileName = "project-cost-gradient.json",
            Filter = "Gradient files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var file = new GradientFile
        {
            Name = "Project Cost Forecast gradient",
            GradientSpec = BrushFactory.SerializeAdvancedHeaderGradientSpec(CreateCurrentSpec())
        };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
        _statusText.Text = $"Exported {Path.GetFileName(dialog.FileName)}.";
    }

    private BrushFactory.AdvancedHeaderGradientSpec CreateCurrentSpec()
    {
        return new BrushFactory.AdvancedHeaderGradientSpec(
            _gradientType,
            _direction,
            _stops
                .OrderBy(stop => stop.Offset)
                .Select(stop => new BrushFactory.HeaderGradientStop(stop.Offset, stop.Hex, stop.Opacity))
                .ToList());
    }

    private void ShowStopColourMenu(FrameworkElement placementTarget)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Bottom,
            StaysOpen = false
        };

        var panel = new WrapPanel
        {
            Width = 216,
            Margin = new Thickness(8)
        };

        foreach (var hex in ColourWellHexes)
        {
            var swatch = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = hex,
                Template = CreateStretchButtonTemplate(new CornerRadius(6)),
                Content = new Border
                {
                    Background = BrushFactory.Frozen(hex),
                    BorderBrush = BrushFactory.Frozen("#CBD5E1"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6)
                }
            };
            swatch.Click += (_, _) =>
            {
                _hexTextBox.Text = (string)swatch.Tag;
                CommitStopDetails();
                menu.IsOpen = false;
            };
            panel.Children.Add(swatch);
        }

        var item = new MenuItem
        {
            Header = panel,
            StaysOpenOnClick = true,
            Padding = new Thickness(0)
        };
        menu.Items.Add(item);
        menu.IsOpen = true;
    }

    private static BrushFactory.AdvancedHeaderGradientSpec CreateDefaultAdvancedSpec()
    {
        return new BrushFactory.AdvancedHeaderGradientSpec(
            "Linear",
            "Down",
            [
                new BrushFactory.HeaderGradientStop(0, "#F8FAFC", 1),
                new BrushFactory.HeaderGradientStop(0.5, "#ECF1F6", 1),
                new BrushFactory.HeaderGradientStop(1, "#E1E8F0", 1)
            ]);
    }

    private static void AddDetailRow(Grid grid, int row, string label, UIElement editor, string suffix)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = BrushFactory.Frozen("#334155"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 10, 12, 0)
        };
        Grid.SetRow(labelBlock, row);
        grid.Children.Add(labelBlock);

        if (editor is FrameworkElement element)
        {
            element.Margin = new Thickness(0, 10, 0, 0);
        }

        Grid.SetRow(editor, row);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            var suffixText = new TextBlock
            {
                Text = suffix,
                Foreground = BrushFactory.Frozen("#334155"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 10, 0, 0)
            };
            Grid.SetRow(suffixText, row);
            Grid.SetColumn(suffixText, 2);
            grid.Children.Add(suffixText);
        }
    }

    private static TextBox CreateTextBox()
    {
        return new TextBox
        {
            Height = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 0, 10, 0),
            BorderBrush = BrushFactory.Frozen("#DCE4EE"),
            BorderThickness = new Thickness(1),
            Foreground = BrushFactory.Frozen("#0F172A")
        };
    }

    private static ControlTemplate CreateStretchButtonTemplate(CornerRadius cornerRadius)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "ButtonBorder";
        border.SetValue(Border.CornerRadiusProperty, cornerRadius);
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
        border.AppendChild(presenter);
        template.VisualTree = border;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, BrushFactory.Frozen("#9DBDE4"), "ButtonBorder"));
        template.Triggers.Add(hover);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55));
        template.Triggers.Add(disabled);
        return template;
    }

    private static TextBlock CreateSectionLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#1F2937")
        };
    }

    private static Button CreateOutlinedButton(string text, double width, double height)
    {
        return new Button
        {
            Content = text,
            Width = width,
            Height = height,
            Padding = new Thickness(10, 0, 10, 0),
            BorderBrush = BrushFactory.Frozen("#DCE4EE"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Foreground = BrushFactory.Frozen("#334155"),
            Cursor = Cursors.Hand,
            Template = CreateStretchButtonTemplate(new CornerRadius(15))
        };
    }

    private static Button CreateQuietButton(string text, double width, double height)
    {
        return new Button
        {
            Content = text,
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = BrushFactory.Frozen("#334155"),
            Cursor = Cursors.Hand,
            Template = CreateStretchButtonTemplate(new CornerRadius(15))
        };
    }

    private static Button CreatePrimaryButton(string text, double width, double height)
    {
        return new Button
        {
            Content = text,
            Width = width,
            Height = height,
            Padding = new Thickness(10, 0, 10, 0),
            BorderBrush = BrushFactory.Frozen("#2563EB"),
            BorderThickness = new Thickness(1),
            Background = BrushFactory.Frozen("#2563EB"),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            Template = CreateStretchButtonTemplate(new CornerRadius(15))
        };
    }

    private static Brush CreateDefaultHeaderGradient()
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xF8, 0xFA, 0xFC), 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xEC, 0xF1, 0xF6), 0.5));
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xE1, 0xE8, 0xF0), 1));
        gradient.Freeze();
        return gradient;
    }

    private static byte BlendByte(byte left, byte right, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return (byte)Math.Round(left + ((right - left) * amount));
    }

    private static T? FindDescendantByTag<T>(DependencyObject root, object tag) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && Equals(typed.Tag, tag))
            {
                return typed;
            }

            var descendant = FindDescendantByTag<T>(child, tag);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private sealed record GradientPreset(string Name, string? Direction, IReadOnlyList<string> Hexes, IReadOnlyList<double> Offsets)
    {
        public string? Spec => Direction is null
            ? null
            : BrushFactory.SerializeAdvancedHeaderGradientSpec(new BrushFactory.AdvancedHeaderGradientSpec(
                "Linear",
                Direction,
                Hexes.Select((hex, index) => new BrushFactory.HeaderGradientStop(Offsets[index], hex, 1)).ToList()));
    }

    private sealed class EditableGradientStop(double offset, string hex, double opacity)
    {
        public double Offset { get; set; } = Math.Clamp(offset, 0, 1);
        public string Hex { get; set; } = hex;
        public double Opacity { get; set; } = Math.Clamp(opacity, 0, 1);
    }

    private sealed class GradientFile
    {
        public string Format { get; set; } = "ProjectCostForecast.AdvancedColourGradient.v1";
        public string? Name { get; set; }
        public string GradientSpec { get; set; } = string.Empty;
    }
}
