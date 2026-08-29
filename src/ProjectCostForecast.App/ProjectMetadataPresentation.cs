using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ProjectCostForecast.App;

/// <summary>
/// Materializes WPF visuals for the persisted project/task/category metadata.
/// The model keeps only icon keys and colour values so the JSON boundary stays
/// independent of the WPF shell.
/// </summary>
public static class ProjectMetadataPresentation
{
    private const string DefaultIconAssetPath = "/Assets/Icons/png/ic_category_project_management_20.png";

    public static ImageSource GetTaskIcon(string? iconKey, string? iconColorHex)
    {
        var assetPath = string.IsNullOrWhiteSpace(iconKey)
            ? DefaultIconAssetPath
            : $"/Assets/Icons/png/{iconKey}";
        return MainWindow.GetBuiltInImageSourceByPath(assetPath, iconColorHex);
    }

    public static ImageSource GetCategoryIcon(string? iconKey)
    {
        var assetPath = string.IsNullOrWhiteSpace(iconKey)
            ? DefaultIconAssetPath
            : $"/Assets/Icons/png/{iconKey}";
        return MainWindow.GetBuiltInImageSourceByPath(assetPath);
    }

    public static Brush GetColorBrush(string? selectedHex, string? fallbackHex)
    {
        if (BrushFactory.TryParseHexColor(selectedHex, out var selectedColor))
        {
            return new SolidColorBrush(selectedColor);
        }

        if (BrushFactory.TryParseHexColor(fallbackHex, out var fallbackColor))
        {
            return new SolidColorBrush(fallbackColor);
        }

        return BrushFactory.Frozen("#FFFFFF");
    }

    public static string GetColorLabel(string? hex)
    {
        return (hex ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "" => "Default",
            "#EDF8F0" or "#16A34A" => "Green",
            "#FFF4E7" or "#EA580C" => "Orange",
            "#EEF5FF" or "#2563EB" => "Blue",
            "#F5F0FF" or "#7C3AED" => "Purple",
            "#ECF9FA" => "Cyan",
            "#FFF0F5" or "#DC2626" => "Pink",
            "#475569" => "Slate",
            _ => hex ?? "Default"
        };
    }

    internal static string? GetString(object[] values, int index)
    {
        return values.Length > index ? values[index] as string : null;
    }
}

public sealed class ProjectTaskIconConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        ProjectMetadataPresentation.GetTaskIcon(ProjectMetadataPresentation.GetString(values, 0), ProjectMetadataPresentation.GetString(values, 1));

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ProjectCategoryIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ProjectMetadataPresentation.GetCategoryIcon(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ProjectMetadataColorBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        ProjectMetadataPresentation.GetColorBrush(ProjectMetadataPresentation.GetString(values, 0), ProjectMetadataPresentation.GetString(values, 1));

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ProjectMetadataColorLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var selectedHex = ProjectMetadataPresentation.GetString(values, 0);
        var fallbackHex = ProjectMetadataPresentation.GetString(values, 1);
        return ProjectMetadataPresentation.GetColorLabel(string.IsNullOrWhiteSpace(selectedHex) ? fallbackHex : selectedHex);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
