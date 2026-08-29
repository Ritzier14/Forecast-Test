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
        if (!BrushFactory.TryParseHexColor(selectedHex, out _)
            && !BrushFactory.TryParseHexColor(fallbackHex, out _))
        {
            return BrushFactory.Frozen("#FFFFFF");
        }

        return BrushFactory.CreateSolidColor(selectedHex, fallbackHex);
    }

    public static string GetColorLabel(string? hex) => ColorPalette.GetLabel(hex);

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
