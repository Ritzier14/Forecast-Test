using System.Windows;
using System.Windows.Controls;

namespace ProjectCostForecast.App;

public sealed class ManualCommentWindow : Window
{
    private readonly TextBox _editor;

    public ManualCommentWindow(string resourceName, string initialText)
    {
        Title = $"Manual comment - {resourceName}";
        Width = 520;
        Height = 300;
        MinWidth = 420;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "This manual comment overrides pulled-through comments until Auto mode is restored.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        _editor = new TextBox
        {
            Text = initialText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(8)
        };
        Grid.SetRow(_editor, 1);
        root.Children.Add(_editor);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(cancel);
        var save = new Button { Content = "Save manual", MinWidth = 110 };
        save.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) => { _editor.Focus(); _editor.SelectAll(); };
    }

    public string Comment => _editor.Text;
}
