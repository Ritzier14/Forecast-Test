using Microsoft.Win32;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using System.Threading;
using System.Windows;

namespace ProjectCostForecast.App.Presentation;

/// <summary>
/// WPF-only adapter for import/export paths and import review decisions.
/// Application workflows depend on <see cref="IImportExportInteraction"/>
/// instead of constructing dialogs or windows directly.
/// </summary>
public sealed class WpfImportExportInteraction : IImportExportInteraction
{
    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string title, string filter, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = suggestedFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool CanShowCostCenterMapping => CanShowWpfWindow;

    public CostCenterMappingPromptResult ChooseCostCenterMapping(CostCenterMappingPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (!CanShowWpfWindow)
        {
            return new CostCenterMappingPromptResult(false, string.Empty, new Dictionary<string, string>(), Array.Empty<string>());
        }

        var window = new CostCenterMappingWindow(
            prompt.Sample,
            prompt.MatchingTransactions,
            prompt.Candidates,
            prompt.SuggestedOption,
            prompt.ExistingNames,
            prompt.RemainingGroupCount)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
        {
            return new CostCenterMappingPromptResult(false, string.Empty, new Dictionary<string, string>(), Array.Empty<string>());
        }

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mappingKey in prompt.MappingKeys)
        {
            if (window.TryGetAssignedName(mappingKey, out var assignedName))
            {
                overrides[mappingKey] = assignedName;
            }
        }

        return new CostCenterMappingPromptResult(
            true,
            window.SelectedManualName,
            overrides,
            window.ExcludedMappingKeys.ToArray());
    }

    public bool CanShowAutoCreatePreview => CanShowWpfWindow;

    public ImportAutoCreatePreviewResult ReviewAutoCreatePreview(ImportAutoCreatePreviewPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (!CanShowWpfWindow)
        {
            return new ImportAutoCreatePreviewResult(false, prompt.ShowPreviewNextTime, prompt.PreviewItems);
        }

        var window = new ImportAutoCreatePreviewWindow(prompt.PreviewItems, prompt.ShowPreviewNextTime)
        {
            Owner = Application.Current?.MainWindow
        };
        var accepted = window.ShowDialog() == true;

        return new ImportAutoCreatePreviewResult(
            accepted,
            window.ShowPreviewNextTime,
            window.PreviewItems.ToList());
    }

    public bool CanShowUnmatchedImports => CanShowWpfWindow;

    public void ShowUnmatchedImports(IReadOnlyCollection<UnmatchedImportCombination> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!CanShowWpfWindow)
        {
            return;
        }

        var window = new UnmatchedImportWindow(items)
        {
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    public void ShowInformation(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static bool CanShowWpfWindow =>
        Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
        && Application.Current is not null;
}
