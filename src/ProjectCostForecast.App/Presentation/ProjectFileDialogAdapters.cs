using Microsoft.Win32;
using ProjectCostForecast.App.Services;
using System.Windows;

namespace ProjectCostForecast.App.Presentation;

/// <summary>
/// WPF-only project path adapter. Project workflows depend on
/// <see cref="IProjectFilePicker"/> instead of constructing dialogs.
/// </summary>
public sealed class WpfProjectFilePicker : IProjectFilePicker
{
    public string? PickOpenProjectPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Project Cost Forecast file",
            Filter = "Project Cost Forecast JSON (*.json)|*.json|All files (*.*)|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveProjectPath(string suggestedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        var dialog = new SaveFileDialog
        {
            Title = "Save Project Cost Forecast file",
            Filter = "Project Cost Forecast JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = suggestedFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

/// <summary>
/// WPF-only prompt/notification adapter for project-file workflows.
/// </summary>
public sealed class WpfProjectPrompt : IProjectPrompt
{
    public bool ConfirmDiscardUnsavedChanges()
    {
        return MessageBox.Show(
            "There are unsaved changes. Continue without saving?",
            "Unsaved changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public SaveConflictDecision ChooseSaveConflict(ProjectSaveConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        var result = MessageBox.Show(
            $"The project file '{conflict.Path}' changed outside this session.\n\n"
            + "Yes: reload the newer file and discard current unsaved changes.\n"
            + "No: use Save As to preserve current changes in another file.\n"
            + "Cancel: keep working without saving.",
            "Project changed externally",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => SaveConflictDecision.Reload,
            MessageBoxResult.No => SaveConflictDecision.SaveAs,
            _ => SaveConflictDecision.Cancel
        };
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
