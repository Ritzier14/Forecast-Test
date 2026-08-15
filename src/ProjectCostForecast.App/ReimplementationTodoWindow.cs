using System.Windows;
using System.Windows.Controls;

namespace ProjectCostForecast.App;

public sealed class ReimplementationTodoWindow : Window
{
    private static readonly IReadOnlyList<ReimplementationTodoItem> Items =
    [
        new(
            "Unsaved changes prompt when quitting",
            "Disabled for now",
            "The function already exists in the application; the quit-time call is disabled temporarily.")
    ];

    public ReimplementationTodoWindow()
    {
        Title = "TODO / Reimplement";
        Width = 760;
        Height = 360;
        MinWidth = 560;
        MinHeight = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushFactory.Frozen("#F7F9FC");

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "TODO / Reimplement",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#17213F")
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Items that are planned, disabled, or need to be reintroduced.",
            Margin = new Thickness(0, 4, 0, 14),
            Foreground = BrushFactory.Frozen("#64748B")
        });
        root.Children.Add(heading);

        var list = new ListView
        {
            ItemsSource = Items,
            BorderBrush = BrushFactory.Frozen("#DCE4EF"),
            Background = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 14)
        };
        var view = new GridView();
        view.Columns.Add(new GridViewColumn
        {
            Header = "Item",
            Width = 250,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(ReimplementationTodoItem.Item))
        });
        view.Columns.Add(new GridViewColumn
        {
            Header = "Status",
            Width = 140,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(ReimplementationTodoItem.Status))
        });
        view.Columns.Add(new GridViewColumn
        {
            Header = "Note",
            Width = 320,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(ReimplementationTodoItem.Note))
        });
        list.View = view;
        Grid.SetRow(list, 1);
        root.Children.Add(list);

        var closeButton = new Button
        {
            Content = "Close",
            Width = 84,
            Padding = new Thickness(12, 5, 12, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
            IsCancel = true
        };
        closeButton.Click += (_, _) => Close();
        Grid.SetRow(closeButton, 2);
        root.Children.Add(closeButton);

        Content = root;
    }

    private sealed record ReimplementationTodoItem(string Item, string Status, string Note);
}
