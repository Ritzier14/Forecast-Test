using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class FormattedCommentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var commentPanel = new StackPanel();
        var root = new DockPanel { Margin = new Thickness(4, 3, 4, 3) };
        var text = value is ForecastLine line ? line.AllMonthComments : value?.ToString() ?? string.Empty;

        if (value is ForecastLine { HasManualAllMonthComment: true } forecastLine)
        {
            var icon = new Button
            {
                Content = "M",
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 1, 5, 0),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = forecastLine.UseManualAllMonthComment ? BrushFactory.Frozen("#F59E0B") : BrushFactory.Frozen("#3B82F6"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                ToolTip = forecastLine.UseManualAllMonthComment ? "Manual comment active" : "Auto comment active; manual comment retained"
            };
            icon.Click += (_, _) =>
            {
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow?.DataContext is ViewModels.MainWindowViewModel viewModel)
                {
                    viewModel.SetForecastCommentMode(forecastLine, !forecastLine.UseManualAllMonthComment);
                }
            };
            DockPanel.SetDock(icon, Dock.Left);
            root.Children.Add(icon);
        }
        else
        {
            commentPanel.Margin = new Thickness(16, 0, 0, 0);
        }

        foreach (var lineText in text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            commentPanel.Children.Add(BuildLine(lineText));
        }

        root.Children.Add(commentPanel);
        return root;
    }

    private static TextBlock BuildLine(string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3) };
        var firstColon = text.IndexOf(':');
        var secondColon = firstColon < 0 ? -1 : text.IndexOf(':', firstColon + 1);
        if (firstColon < 0 || secondColon < 0)
        {
            block.Inlines.Add(new Run(text) { FontStyle = FontStyles.Italic });
            return block;
        }

        block.Inlines.Add(new Run(text[..firstColon]) { Foreground = BrushFactory.Frozen("#2563EB") });
        block.Inlines.Add(new Run(": "));
        block.Inlines.Add(new Run(text[(firstColon + 1)..secondColon].Trim()) { FontWeight = FontWeights.Bold });
        block.Inlines.Add(new Run(": "));
        block.Inlines.Add(new Run(text[(secondColon + 1)..].Trim()) { FontStyle = FontStyles.Italic });
        return block;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
