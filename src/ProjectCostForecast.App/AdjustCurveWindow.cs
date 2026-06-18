using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public sealed class AdjustCurveWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ForecastLine _line;
    private readonly ComboBox _profileCombo;
    private readonly TextBox _totalTextBox;
    private readonly ComboBox _fromCombo;
    private readonly ComboBox _toCombo;
    private readonly TextBlock _summaryText;

    public AdjustCurveWindow(MainWindowViewModel viewModel, ForecastLine line)
    {
        _viewModel = viewModel;
        _line = line;
        Title = $"Adjust curve - {line.ResourceName}";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 248));

        var months = viewModel.GetAdjustableForecastMonths(line);
        var labels = months.Select(month => month.PeriodLabel).ToList();
        var currentTotal = months.Sum(month => month.Amount);

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(new TextBlock
        {
            Text = $"{line.ResourceName}  ({line.TaskNumber})",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 2)
        });
        root.Children.Add(new TextBlock
        {
            Text = "Spread a total cost across the open forecast months using a curve profile. Locked (closed) months are never touched.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        root.Children.Add(new TextBlock { Text = "Curve profile", FontWeight = FontWeights.SemiBold });
        _profileCombo = new ComboBox { Margin = new Thickness(0, 4, 0, 10) };
        foreach (var profile in ForecastCurveService.Profiles)
        {
            _profileCombo.Items.Add(new ComboBoxItem
            {
                Content = ForecastCurveService.DescribeProfile(profile),
                Tag = profile
            });
        }

        _profileCombo.SelectedIndex = 0;
        root.Children.Add(_profileCombo);

        root.Children.Add(new TextBlock { Text = "Total to spread", FontWeight = FontWeights.SemiBold });
        _totalTextBox = new TextBox
        {
            Margin = new Thickness(0, 4, 0, 10),
            Text = currentTotal.ToString("0.##", CultureInfo.CurrentCulture)
        };
        root.Children.Add(_totalTextBox);

        var rangeGrid = new Grid();
        rangeGrid.ColumnDefinitions.Add(new ColumnDefinition());
        rangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        rangeGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var fromPanel = new StackPanel();
        fromPanel.Children.Add(new TextBlock { Text = "From period", FontWeight = FontWeights.SemiBold });
        _fromCombo = new ComboBox { Margin = new Thickness(0, 4, 0, 10), ItemsSource = labels, SelectedIndex = labels.Count > 0 ? 0 : -1 };
        fromPanel.Children.Add(_fromCombo);
        rangeGrid.Children.Add(fromPanel);

        var toPanel = new StackPanel();
        toPanel.Children.Add(new TextBlock { Text = "To period", FontWeight = FontWeights.SemiBold });
        _toCombo = new ComboBox { Margin = new Thickness(0, 4, 0, 10), ItemsSource = labels, SelectedIndex = labels.Count - 1 };
        toPanel.Children.Add(_toCombo);
        Grid.SetColumn(toPanel, 2);
        rangeGrid.Children.Add(toPanel);
        root.Children.Add(rangeGrid);

        _summaryText = new TextBlock
        {
            Text = $"{labels.Count} open months available. Current spread totals {currentTotal:C0}.",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(_summaryText);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var applyButton = new Button { Content = "Apply curve", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0) };
        applyButton.Click += (_, _) => Apply();
        var cancelButton = new Button { Content = "Cancel", Padding = new Thickness(16, 6, 16, 6) };
        cancelButton.Click += (_, _) => Close();
        buttons.Children.Add(applyButton);
        buttons.Children.Add(cancelButton);
        root.Children.Add(buttons);

        Content = root;
    }

    private void Apply()
    {
        if (!decimal.TryParse(_totalTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var total))
        {
            _summaryText.Text = "Enter a valid number for the total to spread.";
            return;
        }

        var profile = (_profileCombo.SelectedItem as ComboBoxItem)?.Tag is ForecastCurveProfile selected
            ? selected
            : ForecastCurveProfile.Linear;

        if (_viewModel.ApplyForecastCurve(_line, profile, total, _fromCombo.SelectedItem as string, _toCombo.SelectedItem as string))
        {
            Close();
        }
        else
        {
            _summaryText.Text = "No open months in that range - widen the period range.";
        }
    }
}
