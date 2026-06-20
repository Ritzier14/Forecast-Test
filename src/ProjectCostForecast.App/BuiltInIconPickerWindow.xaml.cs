using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectCostForecast.App;

public partial class BuiltInIconPickerWindow : UserControl
{
    private static readonly IReadOnlyList<(string Name, string? Hex)> IconColourOptions =
    [
        ("Default", null),
        ("Slate", "#475569"),
        ("Blue", "#2563EB"),
        ("Green", "#16A34A"),
        ("Orange", "#EA580C"),
        ("Red", "#DC2626"),
        ("Purple", "#7C3AED")
    ];

    private readonly IReadOnlyList<BuiltInIconPickerOption> _allOptions;
    private readonly Action<string?, string?> _applyIcon;
    private readonly string _defaultKey;
    private readonly string? _defaultColorHex;
    private readonly string _initialCategory;
    private string _selectedCategory = "Standard";
    private bool _showCardBorders = true;

    public BuiltInIconPickerWindow(
        string title,
        IReadOnlyList<BuiltInIconPickerOption> options,
        string? selectedKey,
        string? selectedColorHex,
        string defaultKey,
        string? defaultColorHex,
        string initialCategory,
        Action<string?, string?> applyIcon)
    {
        InitializeComponent();
        DialogTitleTextBlock.Text = title;
        _allOptions = options;
        _applyIcon = applyIcon;
        _defaultKey = defaultKey;
        _defaultColorHex = defaultColorHex;
        _initialCategory = string.IsNullOrWhiteSpace(initialCategory) ? "Standard" : initialCategory;
        SelectedIconKey = selectedKey ?? defaultKey;
        SelectedIconColorHex = selectedColorHex;
        var availableCategories = _allOptions
            .Select(option => option.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _selectedCategory = availableCategories.Contains(_initialCategory, StringComparer.OrdinalIgnoreCase)
            ? _initialCategory
            : availableCategories.FirstOrDefault() ?? "Standard";
        ConfigureCategoryButtons(availableCategories);
        UpdateCategorySelection();
        RefreshVisibleOptions();
        Loaded += (_, _) =>
        {
            SearchTextBox.Focus();
        };
    }

    public string? SelectedIconKey { get; private set; }

    public string? SelectedIconColorHex { get; private set; }

    public bool ResetToDefault { get; private set; }

    public event EventHandler? CloseRequested;

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshVisibleOptions();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        _selectedCategory = button.Tag?.ToString() ?? "All";
        UpdateCategorySelection();
        RefreshVisibleOptions();
    }

    private void ToggleBordersButton_Click(object sender, RoutedEventArgs e)
    {
        _showCardBorders = !_showCardBorders;
        ToggleBordersButton.Content = _showCardBorders ? "Hide borders" : "Show borders";
        RefreshVisibleOptions();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SelectIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string iconKey)
        {
            return;
        }

        SelectedIconKey = iconKey;
        ResetToDefault = false;
        ApplyAndClose();
    }

    private void SelectIcon_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string iconKey)
        {
            return;
        }

        SelectedIconKey = iconKey;
        ResetToDefault = false;
        button.ContextMenu = BuildColourMenu(iconKey);
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.MousePoint;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildColourMenu(string iconKey)
    {
        var menu = new ContextMenu();
        foreach (var (name, hex) in IconColourOptions)
        {
            var item = new MenuItem
            {
                Header = name,
                IsCheckable = true,
                IsChecked = string.Equals(SelectedIconKey, iconKey, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(SelectedIconColorHex ?? string.Empty, hex ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                Icon = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(2),
                    Background = string.IsNullOrWhiteSpace(hex)
                        ? BrushFactory.Frozen("#FFFFFF")
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    BorderBrush = BrushFactory.Frozen("#CBD5E1"),
                    BorderThickness = new Thickness(1)
                }
            };
            item.Click += (_, _) =>
            {
                SelectedIconKey = iconKey;
                SelectedIconColorHex = hex;
                ResetToDefault = false;
                ApplyAndClose();
            };
            menu.Items.Add(item);
        }

        return menu;
    }

    private void RefreshVisibleOptions()
    {
        var searchText = SearchTextBox.Text?.Trim() ?? string.Empty;
        var visibleOptions = _allOptions
            .Where(option => string.Equals(option.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(searchText)
                             || option.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                             || option.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                             || option.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        IconItemsControl.Items.Clear();
        foreach (var option in visibleOptions)
        {
            IconItemsControl.Items.Add(CreateIconCard(option));
        }

        if (visibleOptions.Count == 0)
        {
            IconItemsControl.Items.Add(new TextBlock
            {
                Text = "No icons match the current search.",
                Foreground = BrushFactory.Frozen("#64748B"),
                FontSize = 13,
                Margin = new Thickness(4)
            });
        }
    }

    private FrameworkElement CreateIconCard(BuiltInIconPickerOption option)
    {
        var isSelected = string.Equals(option.Key, SelectedIconKey, StringComparison.OrdinalIgnoreCase);
        var button = new Button
        {
            Tag = option.Key,
            Width = 42,
            Height = 42,
            Margin = new Thickness(0, 0, 4, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = option.Label
        };
        button.Click += SelectIcon_Click;
        button.MouseRightButtonUp += SelectIcon_MouseRightButtonUp;

        var border = new Border
        {
            BorderBrush = isSelected
                ? BrushFactory.Frozen("#3B82F6")
                : (_showCardBorders ? BrushFactory.Frozen("#E5EAF3") : Brushes.Transparent),
            BorderThickness = isSelected
                ? new Thickness(2)
                : (_showCardBorders ? new Thickness(1) : new Thickness(0)),
            CornerRadius = new CornerRadius(0),
            Background = isSelected ? BrushFactory.Frozen("#F5F9FF") : BrushFactory.Frozen("#FFFFFF"),
            Padding = new Thickness(4)
        };

        var grid = new Grid();
        var image = new Image
        {
            Source = MainWindow.GetBuiltInImageSourceByPath(option.AssetPath, SelectedIconKey == option.Key ? SelectedIconColorHex : null),
            Width = 26,
            Height = 26,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(image);

        if (isSelected)
        {
            var checkBadge = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = BrushFactory.Frozen("#3B82F6"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            checkBadge.Child = new TextBlock
            {
                Text = "OK",
                Foreground = Brushes.White,
                FontSize = 5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            grid.Children.Add(checkBadge);
        }

        if (isSelected && !string.IsNullOrWhiteSpace(SelectedIconColorHex))
        {
            grid.Children.Add(new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedIconColorHex)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2)
            });
        }

        border.Child = grid;
        button.Content = border;
        return button;
    }

    private void ApplyAndClose()
    {
        if (string.IsNullOrWhiteSpace(SelectedIconKey))
        {
            return;
        }

        _applyIcon(ResetToDefault ? null : SelectedIconKey, ResetToDefault ? null : SelectedIconColorHex);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCategorySelection()
    {
        SetCategoryButtonState(AllCategoryButton, "Standard");
        SetCategoryButtonState(KpiCategoryButton, "Kpi");
        SetCategoryButtonState(TabsCategoryButton, "Tabs");
        SetCategoryButtonState(GroupsCategoryButton, "Groups");
    }

    private void ConfigureCategoryButtons(IReadOnlyCollection<string> availableCategories)
    {
        SetCategoryButtonVisibility(AllCategoryButton, availableCategories, "Standard");
        SetCategoryButtonVisibility(KpiCategoryButton, availableCategories, "Kpi");
        SetCategoryButtonVisibility(TabsCategoryButton, availableCategories, "Tabs");
        SetCategoryButtonVisibility(GroupsCategoryButton, availableCategories, "Groups");
    }

    private static void SetCategoryButtonVisibility(Button button, IReadOnlyCollection<string> availableCategories, string category)
    {
        button.Visibility = availableCategories.Contains(category, StringComparer.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SetCategoryButtonState(Button button, string category)
    {
        var isSelected = string.Equals(_selectedCategory, category, StringComparison.OrdinalIgnoreCase);
        button.Foreground = isSelected ? BrushFactory.Frozen("#2563EB") : BrushFactory.Frozen("#475569");
        button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        button.BorderBrush = isSelected ? BrushFactory.Frozen("#3B82F6") : Brushes.Transparent;
        button.BorderThickness = isSelected ? new Thickness(0, 0, 0, 3) : new Thickness(0);
    }
}
