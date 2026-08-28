using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class ProjectFileService : IProjectFileService
{
    private readonly ProjectDatasetMigrationPipeline _migrationPipeline;
    private readonly ValidationService _validationService;
    private readonly ProjectBackupPolicy _backupPolicy;
    private readonly IClock _clock;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = true
    };

    public ProjectFileService(
        ProjectDatasetMigrationPipeline? migrationPipeline = null,
        ValidationService? validationService = null,
        ProjectBackupPolicy? backupPolicy = null,
        Func<DateTime>? utcNow = null,
        IClock? clock = null)
    {
        _migrationPipeline = migrationPipeline ?? new ProjectDatasetMigrationPipeline();
        _validationService = validationService ?? new ValidationService();
        _backupPolicy = backupPolicy ?? new ProjectBackupPolicy();
        _clock = clock ?? (utcNow is null
            ? SystemClock.Instance
            : DateTimeContract.FromLegacyUtcFactory(utcNow));
        DateTimeContract.AddJsonConverters(_jsonOptions);
    }

    public ProjectDataset Load(string path)
    {
        return LoadWithRevision(path).Dataset;
    }

    public void Save(string path, ProjectDataset dataset)
    {
        _ = SaveWithRevision(path, dataset, expectedRevision: null);
    }

    public ProjectFileLoadResult LoadWithRevision(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var content = File.ReadAllBytes(fullPath);
        using var stream = new MemoryStream(content, writable: false);
        var dataset = _migrationPipeline.Load(stream).Dataset;
        EnsureValid(dataset, "Open project");
        return new ProjectFileLoadResult(dataset, ProjectFileRevision.FromBytes(fullPath, content));
    }

    public ProjectFileRevision? GetRevision(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return File.Exists(path) ? ProjectFileRevision.Capture(path) : null;
    }

    public ProjectFileRevision SaveWithRevision(
        string path,
        ProjectDataset dataset,
        ProjectFileRevision? expectedRevision,
        string operation = "Save project")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var prepared = _migrationPipeline.PrepareForSave(dataset).Dataset;
        EnsureValid(prepared, operation);

        var actualRevision = GetRevision(path);
        if (expectedRevision is not null && !expectedRevision.Matches(actualRevision))
        {
            throw new ProjectFileConflictException(path, operation, expectedRevision, actualRevision);
        }

        AtomicJsonFile.Write(path, prepared, _jsonOptions);
        return ProjectFileRevision.Capture(path);
    }

    public string CreateBackup(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return string.Empty;
        }

        var backupDirectory = Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, "backups");
        Directory.CreateDirectory(backupDirectory);
        var fileName = Path.GetFileNameWithoutExtension(fullPath);
        var timestamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);

        for (var suffix = 0; ; suffix++)
        {
            var suffixText = suffix == 0 ? string.Empty : $"-{suffix}";
            var backupPath = Path.Combine(
                backupDirectory,
                $"{fileName}.{timestamp}{suffixText}.bak.json");

            try
            {
                File.Copy(fullPath, backupPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(backupPath))
            {
                // Another save claimed this name. Retry with a deterministic suffix.
                continue;
            }

            var verification = VerifyBackup(backupPath);
            if (!verification.IsUsable)
            {
                TryDeleteBackup(backupPath);
                throw new ProjectBackupException(
                    $"Backup '{backupPath}' was created but failed verification: {verification.Error}");
            }

            PruneBackups(fullPath);
            return backupPath;
        }
    }

    public ProjectBackupVerification VerifyBackup(string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        var fullPath = Path.GetFullPath(backupPath);
        try
        {
            var loadedProject = LoadWithRevision(fullPath);
            return new ProjectBackupVerification(
                fullPath,
                IsUsable: true,
                loadedProject.Dataset,
                loadedProject.Revision,
                Error: null);
        }
        catch (Exception ex)
        {
            return new ProjectBackupVerification(
                fullPath,
                IsUsable: false,
                Dataset: null,
                Revision: null,
                Error: ex.Message);
        }
    }

    public ProjectRestoreResult RestoreBackup(
        string backupPath,
        string? currentProjectPath = null,
        string? destinationPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        var backupFullPath = Path.GetFullPath(backupPath);
        var verification = VerifyBackup(backupFullPath);
        if (!verification.IsUsable || verification.Dataset is null)
        {
            throw new ProjectBackupException(
                $"Backup '{backupFullPath}' is not usable and was not restored: {verification.Error}");
        }

        var currentFullPath = string.IsNullOrWhiteSpace(currentProjectPath)
            ? null
            : Path.GetFullPath(currentProjectPath);
        var restoredFullPath = string.IsNullOrWhiteSpace(destinationPath)
            ? BuildDefaultRestorePath(currentFullPath ?? backupFullPath)
            : Path.GetFullPath(destinationPath);

        if (string.Equals(backupFullPath, restoredFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectBackupException("The restore destination must be different from the backup source.");
        }

        var preRestoreBackupPath = string.Empty;
        if (File.Exists(restoredFullPath))
        {
            preRestoreBackupPath = CreateBackup(restoredFullPath);
            if (string.IsNullOrWhiteSpace(preRestoreBackupPath))
            {
                throw new ProjectBackupException(
                    $"The existing restore destination '{restoredFullPath}' could not be protected before restore.");
            }
        }

        var restoredRevision = SaveWithRevision(
            restoredFullPath,
            verification.Dataset,
            expectedRevision: null,
            operation: "Restore backup");

        return new ProjectRestoreResult(
            backupFullPath,
            restoredFullPath,
            preRestoreBackupPath,
            restoredRevision,
            verification);
    }

    private void PruneBackups(string sourcePath)
    {
        var backups = EnumerateBackupPaths(sourcePath)
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();

        if (backups.Count <= _backupPolicy.RetainedBackupCount)
        {
            return;
        }

        var verifications = backups
            .Select(path => (Path: path, Verification: VerifyBackup(path)))
            .ToList();
        var keep = backups
            .Take(_backupPolicy.RetainedBackupCount)
            .ToList();

        if (!keep.Any(path => verifications.Single(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)).Verification.IsUsable))
        {
            var newestUsable = verifications
                .Where(item => item.Verification.IsUsable)
                .Select(item => item.Path)
                .FirstOrDefault();
            if (newestUsable is not null)
            {
                keep[^1] = newestUsable;
            }
        }

        var keepSet = keep.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var backup in backups.Where(path => !keepSet.Contains(path)))
        {
            File.Delete(backup);
        }
    }

    private IReadOnlyList<string> EnumerateBackupPaths(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var directory = Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, "backups");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var prefix = $"{Path.GetFileNameWithoutExtension(fullPath)}.";
        return Directory.EnumerateFiles(directory, "*.bak.json", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private string BuildDefaultRestorePath(string referencePath)
    {
        var fullPath = Path.GetFullPath(referencePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Could not determine the directory for '{referencePath}'.");
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var extension = Path.GetExtension(fullPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".json";
        }

        var timestamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
        for (var suffix = 0; ; suffix++)
        {
            var suffixText = suffix == 0 ? string.Empty : $"-{suffix}";
            var candidate = Path.Combine(
                directory,
                $"{baseName}.restored-{timestamp}{suffixText}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void TryDeleteBackup(string backupPath)
    {
        try
        {
            File.Delete(backupPath);
        }
        catch (IOException)
        {
            // Preserve the verification failure as the actionable error.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the verification failure as the actionable error.
        }
    }

    private void EnsureValid(ProjectDataset dataset, string operation)
    {
        var report = _validationService.ValidateForOperation(dataset);
        if (report.HasErrors)
        {
            throw new ProjectValidationException(report.BuildBlockingMessage(operation), report);
        }
    }
}
