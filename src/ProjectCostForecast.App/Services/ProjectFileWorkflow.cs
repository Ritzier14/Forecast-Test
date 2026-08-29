using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// Coordinates project open/save decisions at a headless application boundary.
/// It owns path selection, validation, revision conflict handling, and
/// operation outcomes; UI adapters only provide paths and user-facing prompts.
/// </summary>
public sealed class ProjectFileWorkflow
{
    private readonly IProjectFileService _fileService;
    private readonly IProjectFilePicker _filePicker;
    private readonly IProjectPrompt _prompt;
    private readonly ProjectDatasetMigrationPipeline _migrationPipeline;
    private readonly ValidationService _validationService;
    private readonly Func<ProjectSaveConflict, SaveConflictDecision> _saveConflictDecisionHandler;

    public ProjectFileWorkflow(
        IProjectFileService fileService,
        IProjectFilePicker filePicker,
        IProjectPrompt prompt,
        ProjectDatasetMigrationPipeline? migrationPipeline = null,
        ValidationService? validationService = null,
        Func<ProjectSaveConflict, SaveConflictDecision>? saveConflictDecisionHandler = null)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _migrationPipeline = migrationPipeline ?? new ProjectDatasetMigrationPipeline();
        _validationService = validationService ?? new ValidationService();
        _saveConflictDecisionHandler = saveConflictDecisionHandler ?? _prompt.ChooseSaveConflict;
    }

    public ProjectOperationResult<ProjectFileOpenOutcome> Open(
        bool hasUnsavedChanges,
        bool showError = true)
    {
        try
        {
            if (hasUnsavedChanges && !_prompt.ConfirmDiscardUnsavedChanges())
            {
                return Cancelled<ProjectFileOpenOutcome>();
            }

            var path = _filePicker.PickOpenProjectPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return Cancelled<ProjectFileOpenOutcome>();
            }

            var loadedProject = LoadAndValidate(path, "Open project");
            return Succeeded(new ProjectFileOpenOutcome(path, loadedProject));
        }
        catch (Exception ex)
        {
            return Failed<ProjectFileOpenOutcome>("Open failed", ex, showError);
        }
    }

    public ProjectOperationResult<ProjectFileSaveOutcome> Save(
        ProjectDataset dataset,
        string? currentPath,
        ProjectFileRevision? expectedRevision,
        string suggestedFileName,
        string operation = "Save project",
        bool showError = true,
        Func<string, Action?>? prepareSave = null)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        try
        {
            var validationReport = _validationService.ValidateForOperation(dataset);
            if (validationReport.HasErrors)
            {
                var validationException = new ProjectValidationException(
                    validationReport.BuildBlockingMessage(operation),
                    validationReport);
                return Failed<ProjectFileSaveOutcome>(
                    $"{operation} blocked",
                    validationException,
                    showError);
            }

            var targetPath = currentPath;
            var targetRevision = expectedRevision;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = _filePicker.PickSaveProjectPath(suggestedFileName);
                targetRevision = null;
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return Cancelled<ProjectFileSaveOutcome>();
                }
            }

            return SaveToPath(
                dataset,
                targetPath,
                targetRevision,
                suggestedFileName,
                operation,
                showError,
                prepareSave);
        }
        catch (Exception ex)
        {
            return Failed<ProjectFileSaveOutcome>($"{operation} failed", ex, showError);
        }
    }

    private ProjectOperationResult<ProjectFileSaveOutcome> SaveToPath(
        ProjectDataset dataset,
        string path,
        ProjectFileRevision? expectedRevision,
        string suggestedFileName,
        string operation,
        bool showError,
        Func<string, Action?>? prepareSave)
    {
        try
        {
            if (expectedRevision is not null)
            {
                var actualRevision = _fileService.GetRevision(path);
                if (!expectedRevision.Matches(actualRevision))
                {
                    return ResolveConflict(
                        new ProjectFileConflictException(path, operation, expectedRevision, actualRevision),
                        dataset,
                        suggestedFileName,
                        operation,
                        showError,
                        prepareSave);
                }
            }

            return WriteToPath(
                dataset,
                path,
                expectedRevision,
                suggestedFileName,
                operation,
                showError,
                prepareSave);
        }
        catch (ProjectFileConflictException ex)
        {
            return ResolveConflict(ex, dataset, suggestedFileName, operation, showError, prepareSave);
        }
        catch (Exception ex)
        {
            return Failed<ProjectFileSaveOutcome>($"{operation} failed", ex, showError);
        }
    }

    private ProjectOperationResult<ProjectFileSaveOutcome> WriteToPath(
        ProjectDataset dataset,
        string path,
        ProjectFileRevision? expectedRevision,
        string suggestedFileName,
        string operation,
        bool showError,
        Func<string, Action?>? prepareSave)
    {
        Action? rollback = null;
        try
        {
            rollback = prepareSave?.Invoke(path);
            var backupPath = _fileService.CreateBackup(path);
            var revision = _fileService.SaveWithRevision(path, dataset, expectedRevision, operation);
            return Succeeded(new ProjectFileSaveOutcome(
                ProjectFileSaveOutcomeKind.Saved,
                path,
                revision,
                backupPath));
        }
        catch (ProjectFileConflictException ex)
        {
            Rollback(rollback);
            return ResolveConflict(ex, dataset, suggestedFileName, operation, showError, prepareSave);
        }
        catch (Exception ex)
        {
            Rollback(rollback);
            return Failed<ProjectFileSaveOutcome>($"{operation} failed", ex, showError);
        }
    }

    private ProjectOperationResult<ProjectFileSaveOutcome> ResolveConflict(
        ProjectFileConflictException conflict,
        ProjectDataset dataset,
        string suggestedFileName,
        string operation,
        bool showError,
        Func<string, Action?>? prepareSave)
    {
        SaveConflictDecision decision;
        try
        {
            decision = _saveConflictDecisionHandler(conflict.Conflict);
        }
        catch (Exception ex)
        {
            return Failed<ProjectFileSaveOutcome>("Save conflict", ex, showError);
        }

        return decision switch
        {
            SaveConflictDecision.Reload => ReloadAfterConflict(conflict, showError),
            SaveConflictDecision.SaveAs => SaveAsAfterConflict(
                conflict,
                dataset,
                suggestedFileName,
                operation,
                showError,
                prepareSave),
            SaveConflictDecision.Cancel => Cancelled<ProjectFileSaveOutcome>(conflict),
            _ => Failed<ProjectFileSaveOutcome>(
                "Save conflict",
                new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown save conflict decision."),
                showError)
        };
    }

    private ProjectOperationResult<ProjectFileSaveOutcome> ReloadAfterConflict(
        ProjectFileConflictException conflict,
        bool showError)
    {
        try
        {
            var loadedProject = LoadAndValidate(conflict.Path, "Reload project");
            return Succeeded(new ProjectFileSaveOutcome(
                ProjectFileSaveOutcomeKind.Reloaded,
                conflict.Path,
                loadedProject.Revision,
                BackupPath: null,
                loadedProject));
        }
        catch (Exception ex)
        {
            return Failed<ProjectFileSaveOutcome>("Reload failed", ex, showError);
        }
    }

    private ProjectOperationResult<ProjectFileSaveOutcome> SaveAsAfterConflict(
        ProjectFileConflictException conflict,
        ProjectDataset dataset,
        string suggestedFileName,
        string operation,
        bool showError,
        Func<string, Action?>? prepareSave)
    {
        try
        {
            var path = _filePicker.PickSaveProjectPath(suggestedFileName);
            if (string.IsNullOrWhiteSpace(path))
            {
                return Cancelled<ProjectFileSaveOutcome>(conflict);
            }

            return SaveToPath(
                dataset,
                path,
                expectedRevision: null,
                suggestedFileName,
                operation,
                showError,
                prepareSave);
        }
        catch (Exception ex)
        {
            return Failed<ProjectFileSaveOutcome>("Save As failed", ex, showError);
        }
    }

    private ProjectFileLoadResult LoadAndValidate(string path, string operation)
    {
        var loadedProject = _fileService.LoadWithRevision(path);
        var normalizedDataset = _migrationPipeline.Normalize(loadedProject.Dataset).Dataset;
        var validationReport = _validationService.ValidateForOperation(normalizedDataset);
        if (validationReport.HasErrors)
        {
            throw new ProjectValidationException(
                validationReport.BuildBlockingMessage(operation),
                validationReport);
        }

        return new ProjectFileLoadResult(normalizedDataset, loadedProject.Revision);
    }

    private ProjectOperationResult<T> Failed<T>(
        string title,
        Exception error,
        bool showError)
    {
        if (showError)
        {
            try
            {
                _prompt.ShowError(title, error.Message);
            }
            catch
            {
                // A notification failure must not change the operation result.
            }
        }

        return new ProjectOperationResult<T>(ProjectOperationStatus.Failed, Error: error);
    }

    private static ProjectOperationResult<T> Succeeded<T>(T value)
    {
        return new ProjectOperationResult<T>(ProjectOperationStatus.Succeeded, value);
    }

    private static ProjectOperationResult<T> Cancelled<T>(Exception? error = null)
    {
        return new ProjectOperationResult<T>(ProjectOperationStatus.Cancelled, Error: error);
    }

    private static void Rollback(Action? rollback)
    {
        try
        {
            rollback?.Invoke();
        }
        catch
        {
            // The original file operation failure is the actionable result.
        }
    }
}
