using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class ProjectFileService : IProjectFileService
{
    private readonly ProjectDatasetMigrationPipeline _migrationPipeline;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = true
    };

    public ProjectFileService(ProjectDatasetMigrationPipeline? migrationPipeline = null)
    {
        _migrationPipeline = migrationPipeline ?? new ProjectDatasetMigrationPipeline();
    }

    public ProjectDataset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return _migrationPipeline.Load(stream).Dataset;
    }

    public void Save(string path, ProjectDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        _migrationPipeline.PrepareForSave(dataset);
        AtomicJsonFile.Write(path, dataset, _jsonOptions);
    }

    public string CreateBackup(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        var backupDirectory = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, "backups");
        Directory.CreateDirectory(backupDirectory);
        var fileName = Path.GetFileNameWithoutExtension(path);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");

        for (var suffix = 0; ; suffix++)
        {
            var suffixText = suffix == 0 ? string.Empty : $"-{suffix}";
            var backupPath = Path.Combine(
                backupDirectory,
                $"{fileName}.{timestamp}{suffixText}.bak.json");

            try
            {
                File.Copy(path, backupPath, overwrite: false);
                return backupPath;
            }
            catch (IOException) when (File.Exists(backupPath))
            {
                // Another save claimed this name. Retry with a deterministic suffix.
            }
        }
    }
}
