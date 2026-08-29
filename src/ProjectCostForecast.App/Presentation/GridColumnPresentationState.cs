using System.Windows;
using System.Windows.Media;

namespace ProjectCostForecast.App;

public static class GridColumnPresentationState
{
    private static readonly Brush DefaultHeaderGradient = BrushFactory.FrozenDefaultHeaderGradient();
    private static readonly Brush DefaultColumnBorderBrush = CreateFrozenBrush(0xEE, 0xF3, 0xF8);
    private static readonly Brush DefaultHeaderBorderBrush = CreateFrozenBrush(0xE2, 0xEA, 0xF4);

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.RegisterAttached(
            "IconGlyph",
            typeof(string),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty ColumnBackgroundProperty =
        DependencyProperty.RegisterAttached(
            "ColumnBackground",
            typeof(Brush),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(Brushes.White));

    public static readonly DependencyProperty BaseColumnBackgroundProperty =
        DependencyProperty.RegisterAttached(
            "BaseColumnBackground",
            typeof(Brush),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.RegisterAttached(
            "HeaderBackground",
            typeof(Brush),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(DefaultHeaderGradient));

    public static readonly DependencyProperty BaseHeaderBackgroundProperty =
        DependencyProperty.RegisterAttached(
            "BaseHeaderBackground",
            typeof(Brush),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty ColumnBorderBrushProperty =
        DependencyProperty.RegisterAttached(
            "ColumnBorderBrush",
            typeof(Brush),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(DefaultColumnBorderBrush));

    public static readonly DependencyProperty HeaderBorderBrushProperty =
        DependencyProperty.RegisterAttached(
            "HeaderBorderBrush",
            typeof(Brush),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(DefaultHeaderBorderBrush));

    public static readonly DependencyProperty HeaderColorSpecProperty =
        DependencyProperty.RegisterAttached(
            "HeaderColorSpec",
            typeof(string),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(string.Empty));

    public static string GetIconGlyph(DependencyObject element) => (string)element.GetValue(IconGlyphProperty);

    public static void SetIconGlyph(DependencyObject element, string value) => element.SetValue(IconGlyphProperty, value);

    public static Brush GetColumnBackground(DependencyObject element) => (Brush)element.GetValue(ColumnBackgroundProperty);

    public static void SetColumnBackground(DependencyObject element, Brush value) => element.SetValue(ColumnBackgroundProperty, value);

    public static Brush? GetBaseColumnBackground(DependencyObject element) => element.GetValue(BaseColumnBackgroundProperty) as Brush;

    public static void SetBaseColumnBackground(DependencyObject element, Brush value) => element.SetValue(BaseColumnBackgroundProperty, value);

    public static Brush GetHeaderBackground(DependencyObject element) => (Brush)element.GetValue(HeaderBackgroundProperty);

    public static void SetHeaderBackground(DependencyObject element, Brush value) => element.SetValue(HeaderBackgroundProperty, value);

    public static Brush? GetBaseHeaderBackground(DependencyObject element) => element.GetValue(BaseHeaderBackgroundProperty) as Brush;

    public static void SetBaseHeaderBackground(DependencyObject element, Brush value) => element.SetValue(BaseHeaderBackgroundProperty, value);

    public static Brush GetColumnBorderBrush(DependencyObject element) => (Brush)element.GetValue(ColumnBorderBrushProperty);

    public static void SetColumnBorderBrush(DependencyObject element, Brush value) => element.SetValue(ColumnBorderBrushProperty, value);

    public static Brush GetHeaderBorderBrush(DependencyObject element) => (Brush)element.GetValue(HeaderBorderBrushProperty);

    public static void SetHeaderBorderBrush(DependencyObject element, Brush value) => element.SetValue(HeaderBorderBrushProperty, value);

    public static string GetHeaderColorSpec(DependencyObject element) => (string)element.GetValue(HeaderColorSpecProperty);

    public static void SetHeaderColorSpec(DependencyObject element, string value) => element.SetValue(HeaderColorSpecProperty, value);

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
