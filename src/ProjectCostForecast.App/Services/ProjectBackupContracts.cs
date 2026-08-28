using System.IO;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// Controls how many verified project backups are retained for one source
/// file. Two is the minimum so pruning cannot remove the only backup copy.
/// </summary>
public sealed class ProjectBackupPolicy
{
    public const int DefaultRetainedBackupCount = 10;
    public const int MinimumRetainedBackupCount = 2;

    public ProjectBackupPolicy(int retainedBackupCount = DefaultRetainedBackupCount)
    {
        if (retainedBackupCount < MinimumRetainedBackupCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retainedBackupCount),
                retainedBackupCount,
                $"At least {MinimumRetainedBackupCount} backups must be retained.");
        }

        RetainedBackupCount = retainedBackupCount;
    }

    public int RetainedBackupCount { get; }
}

public sealed record ProjectBackupVerification(
    string Path,
    bool IsUsable,
    ProjectDataset? Dataset,
    ProjectFileRevision? Revision,
    string? Error);

public sealed record ProjectRestoreResult(
    string BackupPath,
    string RestoredPath,
    string PreRestoreBackupPath,
    ProjectFileRevision Revision,
    ProjectBackupVerification BackupVerification);

public sealed class ProjectBackupException : IOException
{
    public ProjectBackupException(string message)
        : base(message)
    {
    }

    public ProjectBackupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
