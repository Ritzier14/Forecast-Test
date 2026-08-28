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
