using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectCostForecast.App;

public partial class HeaderColorPickerPopup : UserControl
{
    private static readonly IReadOnlyList<(string Label, string? Spec, string Hex)> DefaultPresets =
    [
        ("Default", null, "#EAF0F8"),
        ("Gray", BrushFactory.SerializeHeaderGradientSpec("#E2E8F0", "Balanced"), "#E2E8F0"),
        ("Brown", BrushFactory.SerializeHeaderGradientSpec("#E7D5C4", "Balanced"), "#E7D5C4"),
        ("Orange", BrushFactory.SerializeHeaderGradientSpec("#FDBA74", "Balanced"), "#FDBA74"),
        ("Yellow", BrushFactory.SerializeHeaderGradientSpec("#FDE68A", "Balanced"), "#FDE68A"),
        ("Green", BrushFactory.SerializeHeaderGradientSpec("#BBF7D0", "Balanced"), "#BBF7D0"),
        ("Blue", BrushFactory.SerializeHeaderGradientSpec("#BFDBFE", "Balanced"), "#BFDBFE"),
        ("Purple", BrushFactory.SerializeHeaderGradientSpec("#DDD6FE", "Balanced"), "#DDD6FE"),
        ("Pink", BrushFactory.SerializeHeaderGradientSpec("#FBCFE8", "Balanced"), "#FBCFE8"),
        ("Red", BrushFactory.SerializeHeaderGradientSpec("#FECACA", "Balanced"), "#FECACA")
    ];

    private static readonly IReadOnlyList<string> ColourWellHexes =
    [
        "#E2E8F0", "#E7D5C4", "#FDBA74", "#FDE68A", "#BBF7D0", "#BFDBFE",
        "#DDD6FE", "#FBCFE8", "#FECACA", "#93C5FD", "#86EFAC", "#F9A8D4"
    ];

    private static readonly IReadOnlyList<string> GradientVariants = ["Soft", "Balanced", "Strong"];

    private readonly Action<string?> _apply;
    private string? _selectedSpec;
    private string _customBaseHex;
    private string _selectedVariant;

    public HeaderColorPickerPopup(string title, string? selectedSpec, Action<string?> apply)
    {
        InitializeComponent();
        _apply = apply;
        _selectedSpec = selectedSpec;
        var parsed = BrushFactory.ParseHeaderGradientSpec(selectedSpec);
        _customBaseHex = parsed.BaseHex;
        _selectedVariant = parsed.VariantKey;
        TitleTextBlock.Text = title;
        BuildDefaultPresets();
        BuildColourWell();
        BuildGradientVariants();
        CustomHexTextBox.Text = _customBaseHex;
        RefreshPreview();
        Loaded += (_, _) => CustomHexTextBox.Focus();
    }

    public event EventHandler? CloseRequested;

    private void BuildDefaultPresets()
    {
        DefaultSwatchPanel.Children.Clear();
        foreach (var preset in DefaultPresets)
        {
            var button = new Button
            {
                Width = 144,
                Height = 58,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = preset.Spec
            };
            button.Click += (_, _) =>
            {
                _apply((string?)button.Tag);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            };

            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderBrush = BrushFactory.Frozen("#DCE4EE"),
                BorderThickness = new Thickness(1),
                Background = string.IsNullOrWhiteSpace(preset.Spec)
                    ? CreateDefaultHeaderGradient()
                    : BrushFactory.FrozenHeaderGradient(preset.Spec)
            };
            border.Child = new TextBlock
            {
                Text = preset.Label,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFactory.Frozen("#1F2937"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            button.Content = border;
            DefaultSwatchPanel.Children.Add(button);
        }
    }

    private void BuildColourWell()
    {
        CustomColourWellPanel.Children.Clear();
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
                Tag = hex
            };
            swatch.Click += (_, _) =>
            {
                _customBaseHex = (string)swatch.Tag;
                CustomHexTextBox.Text = _customBaseHex;
                RefreshPreview();
            };
            swatch.Content = new Border
            {
                Background = BrushFactory.Frozen(hex),
                BorderBrush = BrushFactory.Frozen("#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7)
            };
            CustomColourWellPanel.Children.Add(swatch);
        }
    }

    private void BuildGradientVariants()
    {
        GradientVariantPanel.Children.Clear();
        foreach (var variant in GradientVariants)
        {
            var card = new Button
            {
                Width = 138,
                Height = 64,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = variant
            };
            card.Click += (_, _) =>
            {
                _selectedVariant = (string)card.Tag;
                RefreshGradientSelection();
                RefreshPreview();
            };

            var outer = new Border
            {
                BorderBrush = BrushFactory.Frozen("#DCE4EE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8)
            };
            var stack = new StackPanel();
            var preview = new Border
            {
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Tag = $"preview:{variant}"
            };
            var text = new TextBlock
            {
                Text = variant,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = BrushFactory.Frozen("#334155")
            };
            stack.Children.Add(preview);
            stack.Children.Add(text);
            outer.Child = stack;
            card.Content = outer;
            GradientVariantPanel.Children.Add(card);
        }

        RefreshGradientSelection();
    }

    private void RefreshGradientSelection()
    {
        foreach (var button in GradientVariantPanel.Children.OfType<Button>())
        {
            var selected = string.Equals((string?)button.Tag, _selectedVariant, StringComparison.OrdinalIgnoreCase);
            if (button.Content is Border border)
            {
                border.BorderBrush = selected ? BrushFactory.Frozen("#2563EB") : BrushFactory.Frozen("#DCE4EE");
                border.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            }

            if (FindDescendantByTag<Border>(button, $"preview:{button.Tag}") is { } previewBorder)
            {
                var spec = BrushFactory.SerializeHeaderGradientSpec(_customBaseHex, (string)button.Tag);
                previewBorder.Background = BrushFactory.FrozenHeaderGradient(spec);
            }
        }
    }

    private void RefreshPreview()
    {
        if (BrushFactory.TryParseHexColor(_customBaseHex, out var color))
        {
            CustomHexTextBox.ClearValue(BorderBrushProperty);
            CustomColourPreview.Background = new SolidColorBrush(color);
            PreviewBorder.Background = BrushFactory.FrozenHeaderGradient(BrushFactory.SerializeHeaderGradientSpec(_customBaseHex, _selectedVariant));
        }
        else
        {
            CustomHexTextBox.BorderBrush = Brushes.IndianRed;
        }

        RefreshGradientSelection();
    }

    private void DefaultTabButton_Click(object sender, RoutedEventArgs e)
    {
        DefaultTabContent.Visibility = Visibility.Visible;
        CustomTabContent.Visibility = Visibility.Collapsed;
        DefaultTabButton.Foreground = BrushFactory.Frozen("#2563EB");
        DefaultTabButton.BorderBrush = BrushFactory.Frozen("#2563EB");
        DefaultTabButton.FontWeight = FontWeights.SemiBold;
        CustomTabButton.Foreground = BrushFactory.Frozen("#475569");
        CustomTabButton.BorderBrush = Brushes.Transparent;
        CustomTabButton.FontWeight = FontWeights.Normal;
    }

    private void CustomTabButton_Click(object sender, RoutedEventArgs e)
    {
        DefaultTabContent.Visibility = Visibility.Collapsed;
        CustomTabContent.Visibility = Visibility.Visible;
        CustomTabButton.Foreground = BrushFactory.Frozen("#2563EB");
        CustomTabButton.BorderBrush = BrushFactory.Frozen("#2563EB");
        CustomTabButton.FontWeight = FontWeights.SemiBold;
        DefaultTabButton.Foreground = BrushFactory.Frozen("#475569");
        DefaultTabButton.BorderBrush = Brushes.Transparent;
        DefaultTabButton.FontWeight = FontWeights.Normal;
        CustomHexTextBox.Focus();
        CustomHexTextBox.SelectAll();
    }

    private void CustomHexTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _customBaseHex = CustomHexTextBox.Text.Trim();
        RefreshPreview();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _apply(null);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!BrushFactory.TryParseHexColor(_customBaseHex, out _))
        {
            return;
        }

        _selectedSpec = BrushFactory.SerializeHeaderGradientSpec(_customBaseHex, _selectedVariant);
        _apply(_selectedSpec);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
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
}
