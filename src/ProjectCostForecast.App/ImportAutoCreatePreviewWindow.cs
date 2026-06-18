using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class ImportAutoCreatePreviewWindow : Window
{
    private const double PreviewRowHeight = 34;

    public ImportAutoCreatePreviewWindow(IEnumerable<ImportAutoCreatePreviewItem> items, bool showPreviewNextTime)
    {
        PreviewItems = new ObservableCollection<ImportAutoCreatePreviewItem>(items);
        ShowPreviewNextTime = showPreviewNextTime;

        Title = "Review forecast lines before import";
        Width = 1180;
        Height = 760;
        MinWidth = 860;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new DockPanel
        {
            Margin = new Thickness(14)
        };

        var footer = new DockPanel
        {
            Margin = new Thickness(0, 12, 0, 0),
            LastChildFill = false
        };
        DockPanel.SetDock(footer, Dock.Bottom);

        var preferenceToggle = new CheckBox
        {
            Content = "Show this preview before auto-creating forecast lines",
            IsChecked = ShowPreviewNextTime,
            VerticalAlignment = VerticalAlignment.Center
        };
        preferenceToggle.Checked += (_, _) => ShowPreviewNextTime = true;
        preferenceToggle.Unchecked += (_, _) => ShowPreviewNextTime = false;
        DockPanel.SetDock(preferenceToggle, Dock.Left);
        footer.Children.Add(preferenceToggle);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(buttons, Dock.Right);

        var importButton = new Button
        {
            Content = "Import and auto-create",
            MinWidth = 150
        };
        importButton.Click += (_, _) =>
        {
            NormaliseManualNames();
            if (PreviewItems.Any(item => string.IsNullOrWhiteSpace(item.ManualName)))
            {
                MessageBox.Show(this, "Manual Name is required for every auto-created forecast line.", "Import preview", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        };

        var cancelButton = new Button
        {
            Content = "Cancel import",
            MinWidth = 110,
            Margin = new Thickness(10, 0, 0, 0)
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        buttons.Children.Add(importButton);
        buttons.Children.Add(cancelButton);
        footer.Children.Add(buttons);
        root.Children.Add(footer);

        var intro = new TextBlock
        {
            Text = "These imported transactions will create new forecast lines. Review the grouped combinations before the import continues. Only Manual Name can be edited here.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(intro, Dock.Top);
        root.Children.Add(intro);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserResizeRows = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0,
            RowHeight = PreviewRowHeight,
            ColumnHeaderHeight = 32,
            ItemsSource = PreviewItems,
            RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed
        };
        grid.LoadingRow += (_, args) =>
        {
            if (args.Row.Item is ImportAutoCreatePreviewItem item)
            {
                args.Row.DetailsVisibility = item.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            }
        };
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = string.Empty,
            Width = 34,
            IsReadOnly = true,
            CellTemplate = BuildExpandButtonTemplate()
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Task code",
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.TaskNumber)),
            IsReadOnly = true,
            Width = 110
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Manual Name",
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.ManualName))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            },
            Width = 240
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Project code",
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.ProjectCode)),
            IsReadOnly = true,
            Width = 110
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Amount",
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.Amount)) { StringFormat = "{0:C0}" },
            IsReadOnly = true,
            Width = 100
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Transactions",
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.TransactionCount)),
            IsReadOnly = true,
            Width = 95
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Category",
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.Category)),
            IsReadOnly = true,
            Width = 170
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Source",
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.Source)),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.RowDetailsTemplate = BuildTransactionDetailsTemplate();

        root.Children.Add(SpreadsheetReadOnlyGridBehavior.Attach(grid));
        Content = root;
    }

    public ObservableCollection<ImportAutoCreatePreviewItem> PreviewItems { get; }

    public bool ShowPreviewNextTime { get; private set; }

    private void NormaliseManualNames()
    {
        foreach (var item in PreviewItems)
        {
            item.ManualName = item.ManualName.Trim();
        }
    }

    private DataTemplate BuildExpandButtonTemplate()
    {
        var template = new DataTemplate(typeof(ImportAutoCreatePreviewItem));
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetValue(Button.WidthProperty, 28d);
        button.SetValue(Button.HeightProperty, 28d);
        button.SetValue(Button.PaddingProperty, new Thickness(0));
        button.SetValue(Button.BackgroundProperty, Brushes.Transparent);
        button.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        button.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        button.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Center);
        button.SetValue(Button.ToolTipProperty, "Expand imported transactions");
        button.AddHandler(Button.ClickEvent, new RoutedEventHandler(TogglePreviewRowDetails));

        var glyph = new FrameworkElementFactory(typeof(TextBlock));
        glyph.SetValue(TextBlock.TextProperty, ">");
        glyph.SetValue(TextBlock.FontSizeProperty, 12d);
        glyph.SetValue(TextBlock.ForegroundProperty, BrushFrom("#596784"));
        glyph.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        glyph.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        var style = new Style(typeof(TextBlock));
        style.Triggers.Add(new DataTrigger
        {
            Binding = new Binding(nameof(ImportAutoCreatePreviewItem.IsExpanded)),
            Value = true,
            Setters =
            {
                new Setter(TextBlock.TextProperty, "v")
            }
        });
        glyph.SetValue(TextBlock.StyleProperty, style);

        button.AppendChild(glyph);
        template.VisualTree = button;
        return template;
    }

    private DataTemplate BuildTransactionDetailsTemplate()
    {
        var template = new DataTemplate(typeof(ImportAutoCreatePreviewItem));

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.MarginProperty, new Thickness(10, 0, 10, 10));
        border.SetValue(Border.PaddingProperty, new Thickness(8));
        border.SetValue(Border.BorderBrushProperty, BrushFrom("#D8DEE8"));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.BackgroundProperty, BrushFrom("#FAFBFC"));

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);

        stack.AppendChild(BuildDetailsHeaderRow());

        var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollViewer.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scrollViewer.SetValue(FrameworkElement.MaxHeightProperty, 210d);

        var itemsControl = new FrameworkElementFactory(typeof(ItemsControl));
        itemsControl.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(ImportAutoCreatePreviewItem.Transactions)));
        itemsControl.SetValue(ItemsControl.ItemTemplateProperty, BuildTransactionDetailRowTemplate());

        scrollViewer.AppendChild(itemsControl);
        stack.AppendChild(scrollViewer);
        border.AppendChild(stack);
        template.VisualTree = border;
        return template;
    }

    private static FrameworkElementFactory BuildDetailsHeaderRow()
    {
        var panel = BuildDetailRowPanelFactory();
        panel.SetValue(Panel.BackgroundProperty, BrushFrom("#EEF2F7"));
        panel.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 4));

        AddHeaderCell(panel, "FY", 80);
        AddHeaderCell(panel, "Task", 110);
        AddHeaderCell(panel, "Resource", 220);
        AddHeaderCell(panel, "Supplier", 170);
        AddHeaderCell(panel, "Narrative 1", 170);
        AddHeaderCell(panel, "Narrative 2", 170);
        AddHeaderCell(panel, "Narrative 3", 190);
        AddHeaderCell(panel, "Who", 160);
        AddHeaderCell(panel, "Source", 80);
        AddHeaderCell(panel, "Amount", 100);
        return panel;
    }

    private static DataTemplate BuildTransactionDetailRowTemplate()
    {
        var template = new DataTemplate(typeof(ImportAutoCreatePreviewTransactionDetail));
        var panel = BuildDetailRowPanelFactory();
        panel.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 2));

        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.FyPeriod), 80);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.TaskNumber), 110);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.ResourceDescription), 220);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.SupplierName), 170);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.Narrative1), 170);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.Narrative2), 170);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.Narrative3), 190);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.Who), 160);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.Source), 80);
        AddValueCell(panel, nameof(ImportAutoCreatePreviewTransactionDetail.Amount), 100, "{0:C0}");

        template.VisualTree = panel;
        return template;
    }

    private void TogglePreviewRowDetails(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || FindParent<DataGridRow>(button) is not DataGridRow row
            || row.Item is not ImportAutoCreatePreviewItem item)
        {
            return;
        }

        item.IsExpanded = !item.IsExpanded;
        row.DetailsVisibility = item.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private static FrameworkElementFactory BuildDetailRowPanelFactory()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        return panel;
    }

    private static void AddHeaderCell(FrameworkElementFactory panel, string text, double width)
    {
        var block = new FrameworkElementFactory(typeof(TextBlock));
        block.SetValue(TextBlock.TextProperty, text);
        block.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        block.SetValue(TextBlock.ForegroundProperty, BrushFrom("#465572"));
        block.SetValue(FrameworkElement.WidthProperty, width);
        block.SetValue(TextBlock.MarginProperty, new Thickness(4, 3, 8, 3));
        block.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        panel.AppendChild(block);
    }

    private static void AddValueCell(FrameworkElementFactory panel, string path, double width, string? stringFormat = null)
    {
        var block = new FrameworkElementFactory(typeof(TextBlock));
        var binding = new Binding(path);
        if (!string.IsNullOrWhiteSpace(stringFormat))
        {
            binding.StringFormat = stringFormat;
        }

        block.SetBinding(TextBlock.TextProperty, binding);
        block.SetValue(FrameworkElement.WidthProperty, width);
        block.SetValue(TextBlock.MarginProperty, new Thickness(4, 2, 8, 2));
        block.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        panel.AppendChild(block);
    }

    private static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static SolidColorBrush BrushFrom(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
