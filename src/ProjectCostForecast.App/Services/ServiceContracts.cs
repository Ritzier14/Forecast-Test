using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public interface IProjectFileService
{
    ProjectDataset Load(string path);

    string CreateBackup(string path);

    ProjectFileLoadResult LoadWithRevision(string path)
    {
        return new ProjectFileLoadResult(Load(path), null);
    }

    ProjectFileRevision? GetRevision(string path) => null;

    ProjectFileRevision SaveWithRevision(
        string path,
        ProjectDataset dataset,
        ProjectFileRevision? expectedRevision,
        string operation = "Save project");

    ProjectBackupVerification VerifyBackup(string backupPath)
    {
        throw new NotSupportedException("This project file service does not support backup verification.");
    }

    ProjectRestoreResult RestoreBackup(
        string backupPath,
        string? currentProjectPath = null,
        string? destinationPath = null)
    {
        throw new NotSupportedException("This project file service does not support backup restore.");
    }
}

public interface IUserPreferencesService
{
    AppUserPreferences Load();

    void Save(AppUserPreferences preferences);

    string? LastLoadNotice => null;

    string? LastQuarantinedPath => null;
}

/// <summary>
/// Supplies project-file paths without coupling an application workflow to a
/// particular UI toolkit.
/// </summary>
public interface IProjectFilePicker
{
    string? PickOpenProjectPath();

    string? PickSaveProjectPath(string suggestedFileName);
}

/// <summary>
/// Supplies the user decisions and notifications needed by project-file
/// workflows. The WPF implementation is an adapter at the App boundary;
/// tests can provide a deterministic implementation.
/// </summary>
public interface IProjectPrompt
{
    bool ConfirmDiscardUnsavedChanges();

    SaveConflictDecision ChooseSaveConflict(ProjectSaveConflict conflict);

    void ShowError(string title, string message);
}

/// <summary>
/// Supplies file paths, import decisions, and import notifications without
/// coupling the import workflow to WPF. The desktop implementation is an
/// adapter; tests can provide deterministic decisions without opening a
/// window.
/// </summary>
public interface IImportExportInteraction
{
    string? PickOpenFile(string title, string filter);

    string? PickSaveFile(string title, string filter, string suggestedFileName);

    bool CanShowCostCenterMapping { get; }

    CostCenterMappingPromptResult ChooseCostCenterMapping(CostCenterMappingPrompt prompt);

    bool CanShowAutoCreatePreview { get; }

    ImportAutoCreatePreviewResult ReviewAutoCreatePreview(ImportAutoCreatePreviewPrompt prompt);

    bool CanShowUnmatchedImports { get; }

    void ShowUnmatchedImports(IReadOnlyCollection<UnmatchedImportCombination> items);

    void ShowInformation(string title, string message);

    void ShowError(string title, string message);
}

public sealed record CostCenterMappingPrompt(
    CostTransaction Sample,
    IReadOnlyCollection<CostTransaction> MatchingTransactions,
    IReadOnlyList<CostCenterNameOption> Candidates,
    CostCenterNameOption? SuggestedOption,
    IReadOnlyCollection<string> ExistingNames,
    int RemainingGroupCount,
    IReadOnlyCollection<string> MappingKeys);

public sealed record CostCenterMappingPromptResult(
    bool Accepted,
    string SelectedManualName,
    IReadOnlyDictionary<string, string> MappingNameOverrides,
    IReadOnlyCollection<string> ExcludedMappingKeys);

public sealed record ImportAutoCreatePreviewPrompt(
    IReadOnlyList<ImportAutoCreatePreviewItem> PreviewItems,
    bool ShowPreviewNextTime);

public sealed record ImportAutoCreatePreviewResult(
    bool Accepted,
    bool ShowPreviewNextTime,
    IReadOnlyList<ImportAutoCreatePreviewItem> PreviewItems);

public enum ProjectOperationStatus
{
    Succeeded,
    Cancelled,
    Failed
}

public sealed record ProjectOperationResult<T>(
    ProjectOperationStatus Status,
    T? Value = default,
    Exception? Error = null)
{
    public bool IsSuccess => Status == ProjectOperationStatus.Succeeded;

    public bool IsCancelled => Status == ProjectOperationStatus.Cancelled;

    public bool IsFailure => Status == ProjectOperationStatus.Failed;
}

public sealed record ProjectFileOpenOutcome(
    string Path,
    ProjectFileLoadResult LoadedProject);

public enum ProjectFileSaveOutcomeKind
{
    Saved,
    Reloaded
}

public sealed record ProjectFileSaveOutcome(
    ProjectFileSaveOutcomeKind Kind,
    string Path,
    ProjectFileRevision? Revision,
    string? BackupPath,
    ProjectFileLoadResult? ReloadedProject = null);
