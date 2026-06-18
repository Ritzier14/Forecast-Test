using System.Windows;
using System.Windows.Media;

namespace ProjectCostForecast.App.Models;

public static class GridColumnPresentationState
{
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
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xEA, 0xF0, 0xF8))));

    public static readonly DependencyProperty BaseHeaderBackgroundProperty =
        DependencyProperty.RegisterAttached(
            "BaseHeaderBackground",
            typeof(Brush),
            typeof(GridColumnPresentationState),
            new FrameworkPropertyMetadata(null));

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
}
