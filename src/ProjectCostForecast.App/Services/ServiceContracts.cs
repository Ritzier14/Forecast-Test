using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public interface IProjectFileService
{
    ProjectDataset Load(string path);

    void Save(string path, ProjectDataset dataset);

    string CreateBackup(string path);

    ProjectFileLoadResult LoadWithRevision(string path)
    {
        return new ProjectFileLoadResult(Load(path), null);
    }

    ProjectFileRevision? GetRevision(string path) => null;

    ProjectFileRevision? SaveWithRevision(
        string path,
        ProjectDataset dataset,
        ProjectFileRevision? expectedRevision,
        string operation = "Save project")
    {
        if (expectedRevision is not null)
        {
            var actualRevision = GetRevision(path);
            if (!expectedRevision.Matches(actualRevision))
            {
                throw new ProjectFileConflictException(path, operation, expectedRevision, actualRevision);
            }
        }

        Save(path, dataset);
        return GetRevision(path);
    }

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
