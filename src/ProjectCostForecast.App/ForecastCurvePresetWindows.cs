using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public sealed class SaveForecastCurvePresetWindow : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _noteBox;
    private readonly ComboBox _overwriteBox;
    private readonly MainWindowViewModel _viewModel;
    private readonly IReadOnlyList<decimal> _values;
    private readonly string _resourceName;

    public SaveForecastCurvePresetWindow(
        MainWindowViewModel viewModel,
        string resourceName,
        IReadOnlyList<decimal> values)
    {
        _viewModel = viewModel;
        _resourceName = resourceName;
        _values = values;

        Title = "Save curve preset";
        Width = 460;
        Height = 330;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "Save User Curve Preset",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#0F172A")
        });

        _nameBox = new TextBox
        {
            Text = $"Curve {DateTime.Now:dd MMM HHmm}",
            Margin = new Thickness(0, 18, 0, 8)
        };
        var nameField = WrapField("Name", _nameBox);
        Grid.SetRow(nameField, 1);
        root.Children.Add(nameField);

        _noteBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 70,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8)
        };
        var noteField = WrapField("Note", _noteBox);
        Grid.SetRow(noteField, 2);
        root.Children.Add(noteField);

        _overwriteBox = new ComboBox
        {
            Margin = new Thickness(0, 8, 0, 0),
            DisplayMemberPath = nameof(UserForecastCurvePreset.Name)
        };
        _overwriteBox.Items.Add(new UserForecastCurvePreset { Name = "Create new preset" });
        foreach (var preset in _viewModel.UserForecastCurvePresets)
        {
            _overwriteBox.Items.Add(preset);
        }

        _overwriteBox.SelectedIndex = 0;
        var overwriteField = WrapField("Overwrite", _overwriteBox);
        Grid.SetRow(overwriteField, 3);
        root.Children.Add(overwriteField);

        var helper = new TextBlock
        {
            Text = $"Stores shape only. Reference total {_values.Sum():C0}, {_values.Count} months.",
            Foreground = BrushFactory.Frozen("#64748B"),
            Margin = new Thickness(0, 12, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(helper, 4);
        root.Children.Add(helper);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };
        cancel.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(cancel);
        var save = new Button { Content = "Save preset", MinWidth = 115 };
        save.Click += (_, _) => SavePreset();
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 5);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) =>
        {
            _nameBox.Focus();
            _nameBox.SelectAll();
        };
    }

    private static FrameworkElement WrapField(string label, Control control)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#334155"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(control);
        return panel;
    }

    private void SavePreset()
    {
        var overwrite = _overwriteBox.SelectedIndex > 0 ? _overwriteBox.SelectedItem as UserForecastCurvePreset : null;
        _viewModel.SaveForecastCurvePreset(
            _nameBox.Text,
            _noteBox.Text,
            _resourceName,
            _values.Sum(),
            ForecastCurvePresets.CaptureShape(_values),
            overwrite);
        DialogResult = true;
    }
}

public sealed class ManageForecastCurvePresetsWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ObservableCollection<UserForecastCurvePreset> _presets;
    private readonly DataGrid _grid;

    public ManageForecastCurvePresetsWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        _presets = viewModel.UserForecastCurvePresets;

        Title = "Manage curve presets";
        Width = 720;
        Height = 460;
        MinWidth = 620;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "User Curve Presets",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFactory.Frozen("#0F172A"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        _grid = new DataGrid
        {
            ItemsSource = _presets,
            AutoGenerateColumns = false,
            IsReadOnly = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(UserForecastCurvePreset.Name)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 180 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Note", Binding = new Binding(nameof(UserForecastCurvePreset.Note)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Months", Binding = new Binding(nameof(UserForecastCurvePreset.MonthCount)), IsReadOnly = true, Width = 78 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Reference", Binding = new Binding(nameof(UserForecastCurvePreset.ReferenceTotal)) { StringFormat = "{0:C0}" }, IsReadOnly = true, Width = 95 });
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var delete = new Button { Content = "Delete", MinWidth = 88 };
        delete.Click += (_, _) =>
        {
            if (_grid.SelectedItem is UserForecastCurvePreset preset)
            {
                _viewModel.DeleteForecastCurvePreset(preset);
            }
        };
        buttons.Children.Add(delete);
        var close = new Button { Content = "Done", MinWidth = 88 };
        close.Click += (_, _) =>
        {
            _viewModel.SaveForecastCurvePresetChanges();
            DialogResult = true;
        };
        buttons.Children.Add(close);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
    }
}
