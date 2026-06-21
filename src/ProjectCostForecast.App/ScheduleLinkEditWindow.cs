using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public sealed class ScheduleLinkEditWindow : Window
{
    private readonly ComboBox _typeBox;
    private readonly TextBox _lagBox;

    public bool DeleteRequested { get; private set; }

    public ScheduleLinkEditWindow(ActivityLinkType linkType, int lagDays)
    {
        Title = "Edit relationship";
        Width = 360;
        SizeToContent = SizeToContent.Height;
        MinHeight = 270;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushFactory.Frozen("#F8FAFC");

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "Lead/lag and link type",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18)
        });

        var typePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        typePanel.Children.Add(new TextBlock { Text = "Link type", Margin = new Thickness(0, 0, 0, 4) });
        _typeBox = new ComboBox
        {
            ItemsSource = MainWindowViewModel.ScheduleLinkTypeOptions,
            SelectedItem = linkType
        };
        typePanel.Children.Add(_typeBox);
        Grid.SetRow(typePanel, 1);
        root.Children.Add(typePanel);

        var lagPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        lagPanel.Children.Add(new TextBlock { Text = "Lead / lag days", Margin = new Thickness(0, 0, 0, 4) });
        var lagInput = new Grid();
        lagInput.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lagInput.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _lagBox = new TextBox
        {
            Text = lagDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MinHeight = 30,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _lagBox.GotKeyboardFocus += (_, _) => _lagBox.SelectAll();
        _lagBox.PreviewKeyDown += LagBox_PreviewKeyDown;
        lagInput.Children.Add(_lagBox);
        var spinner = new StackPanel { Width = 28, Margin = new Thickness(4, 0, 0, 0) };
        var up = CreateLagRepeatButton("▲", 1);
        var down = CreateLagRepeatButton("▼", -1);
        spinner.Children.Add(up);
        spinner.Children.Add(down);
        Grid.SetColumn(spinner, 1);
        lagInput.Children.Add(spinner);
        lagPanel.Children.Add(lagInput);
        Grid.SetRow(lagPanel, 2);
        root.Children.Add(lagPanel);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var delete = new Button
        {
            Content = "Delete link",
            MinWidth = 92,
            Margin = new Thickness(0, 0, 8, 0)
        };
        delete.Click += (_, _) =>
        {
            DeleteRequested = true;
            DialogResult = true;
        };
        buttons.Children.Add(delete);
        var cancel = new Button { Content = "Cancel", MinWidth = 84, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(cancel);
        var apply = new Button { Content = "Apply", MinWidth = 84 };
        apply.Click += (_, _) =>
        {
            if (!int.TryParse(_lagBox.Text, out var _))
            {
                MessageBox.Show(this, "Enter a whole number for lag days.", "Edit relationship", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        };
        buttons.Children.Add(apply);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) =>
        {
            _lagBox.Focus();
            _lagBox.SelectAll();
        };
    }

    public ActivityLinkType LinkType => _typeBox.SelectedItem is ActivityLinkType linkType
        ? linkType
        : ActivityLinkType.FinishToStart;

    public int LagDays => int.TryParse(_lagBox.Text, out var lagDays) ? lagDays : 0;

    private RepeatButton CreateLagRepeatButton(string text, int delta)
    {
        var button = new RepeatButton
        {
            Content = text,
            Height = 15,
            MinHeight = 0,
            Padding = new Thickness(0),
            Delay = 350,
            Interval = 65,
            ToolTip = delta > 0 ? "Add one working day lag" : "Add one working day lead"
        };
        button.Click += (_, _) => AdjustLag(delta);
        return button;
    }

    private void LagBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            AdjustLag(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            AdjustLag(-1);
            e.Handled = true;
        }
    }

    private void AdjustLag(int delta)
    {
        var current = LagDays;
        _lagBox.Text = (current + delta).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _lagBox.CaretIndex = _lagBox.Text.Length;
    }
}
