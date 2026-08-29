using System.Globalization;
using System.Windows.Media;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna13ProjectMetadataTests
{
    [Fact]
    public void Project_metadata_models_do_not_reference_wpf_presentation_or_main_window()
    {
        var modelSourcePath = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "src",
            "ProjectCostForecast.App",
            "Models",
            "ProjectDataset.cs");
        var modelSource = File.ReadAllText(modelSourcePath);

        Assert.DoesNotContain("System.Windows", modelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow", modelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageSource", modelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Brush", modelSource, StringComparison.Ordinal);

        foreach (var modelType in new[] { typeof(ProjectTaskCode), typeof(ProjectCategory) })
        {
            Assert.All(modelType.GetProperties(), property =>
                Assert.False(
                    property.PropertyType.FullName?.StartsWith("System.Windows", StringComparison.Ordinal) == true,
                    $"{modelType.Name}.{property.Name} must not expose a WPF type."));
        }
    }

    [Fact]
    public void Project_metadata_color_projection_preserves_selected_fallback_and_labels()
    {
        var selected = Assert.IsType<SolidColorBrush>(
            ProjectMetadataPresentation.GetColorBrush("#123456", "#654321"));
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), selected.Color);

        var fallback = Assert.IsType<SolidColorBrush>(
            ProjectMetadataPresentation.GetColorBrush("not-a-colour", "#16A34A"));
        Assert.Equal(Color.FromRgb(0x16, 0xA3, 0x4A), fallback.Color);

        var white = Assert.IsType<SolidColorBrush>(
            ProjectMetadataPresentation.GetColorBrush("not-a-colour", string.Empty));
        Assert.Equal(Colors.White, white.Color);
        Assert.True(white.IsFrozen);

        Assert.Equal("Default", ProjectMetadataPresentation.GetColorLabel(""));
        Assert.Equal("Green", ProjectMetadataPresentation.GetColorLabel("#16a34a"));
        Assert.Equal("Orange", ProjectMetadataPresentation.GetColorLabel("#FFF4E7"));
        Assert.Equal("#ABCDEF", ProjectMetadataPresentation.GetColorLabel("#ABCDEF"));

        var converter = new ProjectMetadataColorBrushConverter();
        var converterBrush = Assert.IsType<SolidColorBrush>(converter.Convert(
            ["invalid", "#2563EB"],
            typeof(Brush),
            null!,
            CultureInfo.InvariantCulture));
        Assert.Equal(Color.FromRgb(0x25, 0x63, 0xEB), converterBrush.Color);

        var labelConverter = new ProjectMetadataColorLabelConverter();
        Assert.Equal(
            "Purple",
            labelConverter.Convert([string.Empty, "#7C3AED"], typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Project_metadata_icon_converters_return_sources_for_default_and_missing_assets()
    {
        Luna11TestSupport.RunOnSta(() =>
        {
            var taskIcon = new ProjectTaskIconConverter().Convert(
                [string.Empty, "#16A34A"],
                typeof(ImageSource),
                null!,
                CultureInfo.InvariantCulture);
            Assert.IsAssignableFrom<ImageSource>(taskIcon);

            var categoryIcon = new ProjectCategoryIconConverter().Convert(
                "missing-icon.png",
                typeof(ImageSource),
                null!,
                CultureInfo.InvariantCulture);
            Assert.IsAssignableFrom<ImageSource>(categoryIcon);
        });
    }

    [Theory]
    [InlineData("current-v1.json")]
    [InlineData("legacy-unversioned.json")]
    public void Existing_project_fixtures_round_trip_plain_metadata_without_presentation_properties(string fixtureName)
    {
        using var directory = new Luna11TemporaryDirectory();
        var sourcePath = Path.Combine(
            Luna11TestSupport.RepositoryRoot,
            "tests",
            "ProjectCostForecast.UnitTests",
            "Fixtures",
            "ProjectFiles",
            fixtureName);
        var outputPath = Path.Combine(directory.Root, "metadata-round-trip.json");
        var service = new ProjectFileService();
        var dataset = service.Load(sourcePath);

        dataset.ProjectTaskCodes =
        [
            new ProjectTaskCode
            {
                SystemCode = "TASK-META",
                TaskName = "Metadata task",
                IconKey = "ic_task_16.png",
                IconColorHex = "#123456",
                HeaderColorHex = "#16A34A",
                DisplayOrder = 1
            }
        ];
        dataset.ProjectCategories =
        [
            new ProjectCategory
            {
                Name = "Metadata category",
                IconKey = "ic_category_16.png",
                ColorHex = "#EA580C",
                DisplayOrder = 1
            }
        ];

        service.Save(outputPath, dataset);
        var json = File.ReadAllText(outputPath);
        var reopened = service.Load(outputPath);
        var task = Assert.Single(reopened.ProjectTaskCodes);
        var category = Assert.Single(reopened.ProjectCategories);

        Assert.Equal(dataset.FormatVersion, reopened.FormatVersion);
        Assert.Equal("TASK-META", task.SystemCode);
        Assert.Equal("ic_task_16.png", task.IconKey);
        Assert.Equal("#123456", task.IconColorHex);
        Assert.Equal("#16A34A", task.HeaderColorHex);
        Assert.Equal("ic_category_16.png", category.IconKey);
        Assert.Equal("#EA580C", category.ColorHex);
        Assert.Contains("\"IconKey\": \"ic_task_16.png\"", json, StringComparison.Ordinal);
        Assert.Contains("\"IconColorHex\": \"#123456\"", json, StringComparison.Ordinal);
        Assert.Contains("\"HeaderColorHex\": \"#16A34A\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ColorHex\": \"#EA580C\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("IconPreview", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorBrush", json, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultHeaderColorHex", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("defaultColorHex", json, StringComparison.OrdinalIgnoreCase);
    }
}
