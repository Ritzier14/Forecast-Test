using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class ProjectFileService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = true
    };

    public ProjectDataset Load(string path)
    {
        using var stream = File.OpenRead(path);
        var dataset = JsonSerializer.Deserialize<ProjectDataset>(stream, _jsonOptions);
        return dataset ?? new ProjectDataset();
    }

    public void Save(string path, ProjectDataset dataset)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, dataset, _jsonOptions);
    }

    public string CreateBackup(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        var backupDirectory = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, "backups");
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(
            backupDirectory,
            $"{Path.GetFileNameWithoutExtension(path)}.{DateTime.Now:yyyyMMdd-HHmmss}.bak.json");
        File.Copy(path, backupPath, overwrite: false);
        return backupPath;
    }
}
