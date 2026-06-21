using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private static readonly Dictionary<string, string> DefaultWorkspaceTabIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CTC Forecast"] = "ic_tab_forecast_16.png",
        ["Resources"] = "ic_tab_resources_16.png",
        ["Raw Transactions"] = "ic_tab_raw_transactions_16.png",
        ["Summary View"] = "ic_tab_summary_16.png",
        ["Monthly Report"] = "ic_tab_monthly_report_16.png",
        ["Pivot Builder"] = "ic_tab_pivot_builder_16.png",
        ["Contingency"] = "ic_tab_contingency_16.png",
        ["Audit"] = "ic_tab_audit_16.png"
    };

    private static readonly Dictionary<string, ImageSource> BuiltInImageSources = new(StringComparer.OrdinalIgnoreCase);

    private void WorkspaceTabHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element
            || DataContext is not MainWindowViewModel viewModel
            || string.IsNullOrWhiteSpace(element.Tag?.ToString()))
        {
            return;
        }

        OpenWorkspaceTabIconContextMenu(element, viewModel, element.Tag.ToString()!);
        e.Handled = true;
    }

    private void OpenWorkspaceTabIconContextMenu(FrameworkElement element, MainWindowViewModel viewModel, string workspaceKey)
    {
        var menu = CreateColumnContextMenu();
        var changeIcon = new MenuItem { Header = "Change icon" };
        changeIcon.Click += (_, _) => ExecuteAfterClosingMenu(changeIcon, () =>
            OpenBuiltInIconPicker(
                "Tab Icon",
                viewModel.GetWorkspaceTabIconKey(workspaceKey),
                viewModel.GetWorkspaceTabIconColorHex(workspaceKey),
                GetDefaultWorkspaceTabIconKey(workspaceKey),
                null,
                "Tabs",
                (iconKey, iconColorHex) =>
                {
                    viewModel.SetWorkspaceTabIcon(workspaceKey, iconKey, iconColorHex);
                    RefreshWorkspaceTabIcons();
                }));
        menu.Items.Add(changeIcon);
        ApplyMenuIcons(menu);
        menu.PlacementTarget = element;
        menu.Placement = PlacementMode.MousePoint;
        element.ContextMenu = menu;
        menu.IsOpen = true;
    }

    internal void OpenForecastGroupHeaderIconContextMenu(ForecastGroupHeaderPresenter presenter, string groupName, string category)
    {
        OpenForecastGroupHeaderContextMenu(presenter, groupName, category, includeAppearanceOptions: true, isExpanded: presenter.IsExpanded);
    }

    internal void OpenForecastGroupHeaderContextMenu(
        ForecastGroupHeaderPresenter presenter,
        string groupName,
        string category,
        bool includeAppearanceOptions,
        bool isExpanded)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var menu = CreateColumnContextMenu();
        var expandGroupItem = new MenuItem
        {
            Header = isExpanded ? "Collapse Group" : "Expand Group"
        };
        expandGroupItem.Click += (_, _) =>
        {
            presenter.SetExpandedState(!isExpanded);
            CloseContainingMenu(expandGroupItem);
        };
        menu.Items.Add(expandGroupItem);

        var expandAll = new MenuItem { Header = "Expand All" };
        expandAll.Click += (_, _) =>
        {
            SetGroupedExpandState(ForecastLinesGrid, true);
            CloseContainingMenu(expandAll);
        };
        menu.Items.Add(expandAll);

        var collapseAll = new MenuItem { Header = "Collapse All" };
        collapseAll.Click += (_, _) =>
        {
            SetGroupedExpandState(ForecastLinesGrid, false);
            CloseContainingMenu(collapseAll);
        };
        menu.Items.Add(collapseAll);

        if (includeAppearanceOptions)
        {
            menu.Items.Add(new Separator());

            var changeIcon = new MenuItem { Header = "Change icon" };
            changeIcon.Click += (_, _) => ExecuteAfterClosingMenu(changeIcon, () =>
                OpenBuiltInIconPicker(
                    "Group Header Icon",
                    viewModel.GetForecastGroupHeaderIconKey(groupName),
                    viewModel.GetForecastGroupHeaderIconColorHex(groupName),
                    GetDefaultForecastGroupHeaderIconKey(category),
                    null,
                    "Groups",
                    (iconKey, iconColorHex) =>
                    {
                        viewModel.SetForecastGroupHeaderIcon(groupName, iconKey, iconColorHex);
                        QueueRefreshForecastGroupHeaderPresenters();
                    }));
            menu.Items.Add(changeIcon);
            menu.Items.Add(BuildForecastGroupHeaderColourMenu(viewModel, groupName));
        }

        ApplyMenuIcons(menu);
        menu.PlacementTarget = presenter;
        menu.Placement = PlacementMode.MousePoint;
        presenter.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private MenuItem BuildForecastGroupHeaderColourMenu(MainWindowViewModel viewModel, string groupName)
    {
        var colourMenu = new MenuItem { Header = "Header colour", Icon = CreateMenuTextIcon("C") };
        var selectedHex = viewModel.GetForecastGroupHeaderColorHex(groupName);
        foreach (var option in ColumnColourOptions)
        {
            var optionHex = string.Equals(option.Label, "Default", StringComparison.OrdinalIgnoreCase)
                ? null
                : option.HeaderHex;
            var item = new MenuItem
            {
                Header = option.Label,
                Icon = CreateColourSwatch(option.HeaderHex),
                IsCheckable = true,
                IsChecked = string.IsNullOrWhiteSpace(optionHex)
                    ? string.IsNullOrWhiteSpace(selectedHex)
                    : string.Equals(selectedHex, optionHex, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) =>
            {
                viewModel.SetForecastGroupHeaderColor(groupName, optionHex);
                QueueRefreshForecastGroupHeaderPresenters();
                CloseContainingMenu(item);
            };
            colourMenu.Items.Add(item);
        }

        colourMenu.Items.Add(new Separator());
        var customItem = new MenuItem { Header = "Custom..." };
        customItem.Click += (_, _) => ExecuteAfterClosingMenu(customItem, () =>
            OpenHeaderColorPicker(
                $"Group Header Colour: {groupName}",
                selectedHex,
                colorSpec =>
                {
                    viewModel.SetForecastGroupHeaderColor(groupName, colorSpec);
                    QueueRefreshForecastGroupHeaderPresenters();
                }));
        colourMenu.Items.Add(customItem);

        return colourMenu;
    }

    private void OpenBuiltInIconPicker(
        string title,
        string? selectedKey,
        string? selectedColorHex,
        string defaultKey,
        string? defaultColorHex,
        string initialCategory,
        Action<string?, string?> applyIcon)
    {
        var options = BuiltInImageIconOptions
            .Select(option => new BuiltInIconPickerOption(
                option.Key,
                option.Label,
                option.AssetPath,
                GetBuiltInIconCategory(option.Key)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(initialCategory))
        {
            var scopedOptions = options
                .Where(option => string.Equals(option.Category, initialCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (scopedOptions.Count > 0)
            {
                options = scopedOptions;
            }
        }

        var picker = new BuiltInIconPickerWindow(title, options, selectedKey, selectedColorHex, defaultKey, defaultColorHex, initialCategory, applyIcon);
        var popup = new Popup
        {
            AllowsTransparency = true,
            Child = picker,
            Placement = PlacementMode.Center,
            PlacementTarget = this,
            StaysOpen = false
        };
        picker.CloseRequested += (_, _) => popup.IsOpen = false;
        popup.IsOpen = true;
    }

    private void OpenHeaderColorPicker(string title, string? selectedSpec, Action<string?> apply)
    {
        var picker = new HeaderColorPickerPopup(title, selectedSpec, apply);
        var popup = new Popup
        {
            AllowsTransparency = true,
            Child = picker,
            Placement = PlacementMode.Center,
            PlacementTarget = this,
            StaysOpen = false
        };
        picker.CloseRequested += (_, _) => popup.IsOpen = false;
        popup.IsOpen = true;
    }

    private void RefreshWorkspaceTabIcons()
    {
        ApplyWorkspaceTabHeaderIcon(ForecastTabHeaderIcon, "CTC Forecast");
        ApplyWorkspaceTabHeaderIcon(ResourcesTabHeaderIcon, "Resources");
        ApplyWorkspaceTabHeaderIcon(RawTransactionsTabHeaderIcon, "Raw Transactions");
        ApplyWorkspaceTabHeaderIcon(SummaryTabHeaderIcon, "Summary View");
        ApplyWorkspaceTabHeaderIcon(MonthlyReportTabHeaderIcon, "Monthly Report");
        ApplyWorkspaceTabHeaderIcon(PivotBuilderTabHeaderIcon, "Pivot Builder");
        ApplyWorkspaceTabHeaderIcon(ContingencyTabHeaderIcon, "Contingency");
        ApplyWorkspaceTabHeaderIcon(AuditTabHeaderIcon, "Audit");
    }

    private void ApplyWorkspaceTabHeaderIcon(Image image, string workspaceKey)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var iconKey = viewModel.GetWorkspaceTabIconKey(workspaceKey) ?? GetDefaultWorkspaceTabIconKey(workspaceKey);
        var iconColorHex = viewModel.GetWorkspaceTabIconColorHex(workspaceKey);
        image.Source = GetBuiltInImageSource(iconKey, iconColorHex);
    }

    internal string GetResolvedForecastGroupHeaderIconKey(string groupName, string category)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            return viewModel.GetForecastGroupHeaderIconKey(groupName) ?? GetDefaultForecastGroupHeaderIconKey(category);
        }

        return GetDefaultForecastGroupHeaderIconKey(category);
    }

    internal string? GetResolvedForecastGroupHeaderIconColorHex(string groupName)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            return viewModel.GetForecastGroupHeaderIconColorHex(groupName);
        }

        return null;
    }

    internal Brush? GetResolvedForecastGroupHeaderBackground(string groupName)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return null;
        }

        var colorHex = viewModel.GetForecastGroupHeaderColorHex(groupName);
        return string.IsNullOrWhiteSpace(colorHex) ? null : BrushFactory.FrozenHeaderGradient(colorHex);
    }

    internal void OpenKpiIconPicker(int pillId)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var option = viewModel.GetSelectedKpi(pillId);
        if (option is null)
        {
            return;
        }

        OpenBuiltInIconPicker(
            "KPI Pill Icon",
            viewModel.GetKpiIconKey(option.Key),
            viewModel.GetKpiIconColorHex(option.Key),
            option.Key,
            null,
            "Kpi",
            (iconKey, iconColorHex) => viewModel.SetKpiIcon(option.Key, iconKey, iconColorHex));
    }

    private static string GetDefaultWorkspaceTabIconKey(string workspaceKey)
    {
        return DefaultWorkspaceTabIcons.TryGetValue(workspaceKey, out var iconKey)
            ? iconKey
            : "ic_tab_forecast_16.png";
    }

    private static string GetDefaultForecastGroupHeaderIconKey(string category)
    {
        return category switch
        {
            "Internal Staff Costs" => "ic_category_internal_staff_20.png",
            "Design Consultants" => "ic_category_design_consultants_20.png",
            "Contractors" => "ic_category_contractors_20.png",
            "Compliance" => "ic_category_compliance_20.png",
            "Close Out" => "ic_category_closeout_20.png",
            _ => "ic_category_project_management_20.png"
        };
    }

    private static string GetBuiltInIconCategory(string iconKey)
    {
        if (iconKey.StartsWith("ic_metric_", StringComparison.OrdinalIgnoreCase))
        {
            return "Kpi";
        }

        if (iconKey.StartsWith("ic_tab_", StringComparison.OrdinalIgnoreCase))
        {
            return "Tabs";
        }

        if (iconKey.StartsWith("ic_category_", StringComparison.OrdinalIgnoreCase))
        {
            return "Groups";
        }

        return "Standard";
    }

    private static ImageSource GetBuiltInImageSource(string? iconKey, string? tintHex = null)
    {
        var option = BuiltInImageIconOptions.FirstOrDefault(item => string.Equals(item.Key, iconKey, StringComparison.OrdinalIgnoreCase));
        return GetBuiltInImageSourceByPath(option?.AssetPath ?? "/Assets/Icons/png/ic_tab_forecast_16.png", tintHex);
    }

    internal static ImageSource GetBuiltInImageSourceByPath(string assetPath, string? tintHex = null)
    {
        var cacheKey = $"{assetPath}|{tintHex ?? string.Empty}";
        if (BuiltInImageSources.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        if (Application.Current is null)
        {
            var fallback = CreateFallbackIconSource(tintHex);
            BuiltInImageSources[cacheKey] = fallback;
            return fallback;
        }

        BitmapSource bitmap;
        try
        {
            bitmap = LoadBuiltInBitmap(assetPath);
        }
        catch
        {
            var fallback = CreateFallbackIconSource(tintHex);
            BuiltInImageSources[cacheKey] = fallback;
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(tintHex) || !TryParseHexColor(tintHex, out var tintColor))
        {
            BuiltInImageSources[cacheKey] = bitmap;
            return bitmap;
        }

        var converted = new FormatConvertedBitmap();
        converted.BeginInit();
        converted.Source = bitmap;
        converted.DestinationFormat = PixelFormats.Bgra32;
        converted.EndInit();
        converted.Freeze();

        var stride = (converted.PixelWidth * converted.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var alpha = pixels[offset + 3];
            if (alpha == 0)
            {
                continue;
            }

            pixels[offset] = tintColor.B;
            pixels[offset + 1] = tintColor.G;
            pixels[offset + 2] = tintColor.R;
        }

        var tinted = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            converted.DpiX,
            converted.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        tinted.Freeze();
        BuiltInImageSources[cacheKey] = tinted;
        return tinted;
    }

    private static BitmapSource LoadBuiltInBitmap(string assetPath)
    {
        var normalizedAssetPath = assetPath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalizedAssetPath))
        {
            throw new InvalidOperationException("Icon asset path is missing.");
        }

        if (!normalizedAssetPath.StartsWith("/", StringComparison.Ordinal))
        {
            normalizedAssetPath = $"/{normalizedAssetPath}";
        }

        var absoluteBitmap = new BitmapImage();
        absoluteBitmap.BeginInit();
        absoluteBitmap.UriSource = new Uri(
            $"pack://application:,,,/{normalizedAssetPath.TrimStart('/')}",
            UriKind.Absolute);
        absoluteBitmap.CacheOption = BitmapCacheOption.OnLoad;
        absoluteBitmap.EndInit();
        absoluteBitmap.Freeze();
        return absoluteBitmap;
    }

    private static ImageSource CreateFallbackIconSource(string? tintHex)
    {
        var color = TryParseHexColor(tintHex, out var parsedColor)
            ? parsedColor
            : Color.FromRgb(0x47, 0x55, 0x69);
        var pen = new Pen(new SolidColorBrush(color), 1.6);
        pen.Freeze();
        var fill = new SolidColorBrush(Color.FromArgb(24, color.R, color.G, color.B));
        fill.Freeze();
        var geometry = new RectangleGeometry(new Rect(2, 2, 16, 16), 4, 4);
        geometry.Freeze();
        var drawing = new GeometryDrawing(fill, pen, geometry);
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }

    private static bool TryParseHexColor(string? tintHex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(tintHex))
        {
            return false;
        }

        try
        {
            var converter = ColorConverter.ConvertFromString(tintHex);
            if (converter is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}

public sealed record BuiltInIconPickerOption(string Key, string Label, string AssetPath, string Category);
