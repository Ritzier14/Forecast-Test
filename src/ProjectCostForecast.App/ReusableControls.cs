using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectCostForecast.App;

public sealed class WarningBar : Border
{
    private readonly ItemsControl _itemsControl = new();
    private INotifyCollectionChanged? _observedCollection;

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(WarningBar),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public WarningBar()
    {
        Background = BrushFactory.Frozen("#FFF5F5");
        BorderBrush = BrushFactory.Frozen("#FECACA");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(7);
        Padding = new Thickness(6, 4, 6, 4);
        Margin = new Thickness(0, 0, 0, 8);
        Child = _itemsControl;
        _itemsControl.ItemTemplate = BuildWarningTemplate();
        UpdateVisibility();
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var bar = (WarningBar)dependencyObject;
        if (bar._observedCollection is not null)
        {
            bar._observedCollection.CollectionChanged -= bar.ItemsSource_CollectionChanged;
        }

        bar._itemsControl.ItemsSource = (IEnumerable?)e.NewValue;
        bar._observedCollection = e.NewValue as INotifyCollectionChanged;
        if (bar._observedCollection is not null)
        {
            bar._observedCollection.CollectionChanged += bar.ItemsSource_CollectionChanged;
        }

        bar.UpdateVisibility();
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        Visibility = HasItems(ItemsSource) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool HasItems(IEnumerable? items)
    {
        if (items is null)
        {
            return false;
        }

        foreach (var _ in items)
        {
            return true;
        }

        return false;
    }

    private static DataTemplate BuildWarningTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding());
        text.SetValue(TextBlock.ForegroundProperty, BrushFactory.Frozen("#B91C1C"));
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.FontSizeProperty, 12.0);
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(TextBlock.MarginProperty, new Thickness(2, 1, 2, 1));
        return new DataTemplate { VisualTree = text };
    }
}

public sealed class AddRowButton : Button
{
    public AddRowButton()
    {
        Style = TryFindResource("ForecastStatePillButtonStyle") as Style;
        Content = BuildContent();
    }

    private static FrameworkElement BuildContent()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = "+",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, -1, 6, 0)
        });
        panel.Children.Add(new TextBlock { Text = "Add row" });
        return panel;
    }
}

public class BandOverlayCanvas : Canvas
{
    public BandOverlayCanvas()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }
}

public sealed class ProjectColumnHeader : ContentControl
{
}

public sealed class ProjectFilterMenu : ContextMenu
{
}

public sealed class ProjectContextProvider : DependencyObject
{
}

public sealed class ProjectViewStrip : ItemsControl
{
}

public sealed class MetricCard : ContentControl
{
}

public sealed class CommandGroup : ItemsControl
{
}

public sealed class DialogShell : ContentControl
{
}

public sealed class ValidationIndicator : ContentControl
{
}

public sealed class CodeMappingEditorHost : ContentControl
{
}

public static class PeriodColumnFactory
{
    public static DataGridTextColumn CreateAccountingTextColumn(string header, string bindingPath, double width, IValueConverter converter)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(bindingPath) { Converter = converter },
            Width = width,
            IsReadOnly = true
        };
    }
}

public static class ProjectPanBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ProjectPanBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject target)
    {
        return (bool)target.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer scrollViewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            scrollViewer.PreviewMouseRightButtonDown += ScrollViewer_PreviewMouseRightButtonDown;
            scrollViewer.PreviewMouseRightButtonUp += ScrollViewer_PreviewMouseRightButtonUp;
        }
        else
        {
            scrollViewer.PreviewMouseRightButtonDown -= ScrollViewer_PreviewMouseRightButtonDown;
            scrollViewer.PreviewMouseRightButtonUp -= ScrollViewer_PreviewMouseRightButtonUp;
        }
    }

    private static void ScrollViewer_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.Cursor = Cursors.SizeAll;
        }
    }

    private static void ScrollViewer_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ClearValue(FrameworkElement.CursorProperty);
        }
    }
}
