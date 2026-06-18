using System.Windows;

namespace ProjectCostForecast.App.Models;

public static class GridSelectionVisualState
{
    public static readonly DependencyProperty IsCurrentRowProperty = DependencyProperty.RegisterAttached(
        "IsCurrentRow",
        typeof(bool),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty CurrentRowBorderThicknessProperty = DependencyProperty.RegisterAttached(
        "CurrentRowBorderThickness",
        typeof(Thickness),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(new Thickness(0)));

    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.RegisterAttached(
        "BorderThickness",
        typeof(Thickness),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(new Thickness(0)));

    public static readonly DependencyProperty IsCellSelectedProperty = DependencyProperty.RegisterAttached(
        "IsCellSelected",
        typeof(bool),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsFillHandleCellProperty = DependencyProperty.RegisterAttached(
        "IsFillHandleCell",
        typeof(bool),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsReadOnlyCellProperty = DependencyProperty.RegisterAttached(
        "IsReadOnlyCell",
        typeof(bool),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsCalculatedCellProperty = DependencyProperty.RegisterAttached(
        "IsCalculatedCell",
        typeof(bool),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsLockedCellProperty = DependencyProperty.RegisterAttached(
        "IsLockedCell",
        typeof(bool),
        typeof(GridSelectionVisualState),
        new FrameworkPropertyMetadata(false));

    public static void SetIsCellSelected(DependencyObject element, bool value) =>
        element.SetValue(IsCellSelectedProperty, value);

    public static bool GetIsCellSelected(DependencyObject element) =>
        (bool)element.GetValue(IsCellSelectedProperty);

    public static void SetIsFillHandleCell(DependencyObject element, bool value) =>
        element.SetValue(IsFillHandleCellProperty, value);

    public static bool GetIsFillHandleCell(DependencyObject element) =>
        (bool)element.GetValue(IsFillHandleCellProperty);

    public static void SetIsReadOnlyCell(DependencyObject element, bool value) =>
        element.SetValue(IsReadOnlyCellProperty, value);

    public static bool GetIsReadOnlyCell(DependencyObject element) =>
        (bool)element.GetValue(IsReadOnlyCellProperty);

    public static void SetIsCalculatedCell(DependencyObject element, bool value) =>
        element.SetValue(IsCalculatedCellProperty, value);

    public static bool GetIsCalculatedCell(DependencyObject element) =>
        (bool)element.GetValue(IsCalculatedCellProperty);

    public static void SetIsLockedCell(DependencyObject element, bool value) =>
        element.SetValue(IsLockedCellProperty, value);

    public static bool GetIsLockedCell(DependencyObject element) =>
        (bool)element.GetValue(IsLockedCellProperty);

    public static void SetBorderThickness(DependencyObject element, Thickness value) =>
        element.SetValue(BorderThicknessProperty, value);

    public static Thickness GetBorderThickness(DependencyObject element) =>
        (Thickness)element.GetValue(BorderThicknessProperty);

    public static void SetIsCurrentRow(DependencyObject element, bool value) =>
        element.SetValue(IsCurrentRowProperty, value);

    public static bool GetIsCurrentRow(DependencyObject element) =>
        (bool)element.GetValue(IsCurrentRowProperty);

    public static void SetCurrentRowBorderThickness(DependencyObject element, Thickness value) =>
        element.SetValue(CurrentRowBorderThicknessProperty, value);

    public static Thickness GetCurrentRowBorderThickness(DependencyObject element) =>
        (Thickness)element.GetValue(CurrentRowBorderThicknessProperty);
}
