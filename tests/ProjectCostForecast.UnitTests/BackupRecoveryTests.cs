using System.IO;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class BackupRecoveryTests
{
    [Fact]
    public void Backup_is_verified_and_restores_to_a_new_path_with_matching_data()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Root, "project.json");
        var service = CreateService();
        var source = CreateDataset("Recoverable project");

        service.Save(sourcePath, source);
        var backupPath = service.CreateBackup(sourcePath);
        var verification = service.VerifyBackup(backupPath);

        Assert.True(verification.IsUsable);
        Assert.NotNull(verification.Dataset);
        Assert.NotNull(verification.Revision);

        var restore = service.RestoreBackup(backupPath, sourcePath);
        var restored = service.Load(restore.RestoredPath);

        Assert.NotEqual(sourcePath, restore.RestoredPath);
        Assert.True(File.Exists(restore.RestoredPath));
        Assert.Empty(restore.PreRestoreBackupPath);
        Assert.Equal(backupPath, restore.BackupPath);
        Assert.Equal(source.Header.ProjectTitle, restored.Header.ProjectTitle);
        Assert.Equal(source.Transactions.Count, restored.Transactions.Count);
        Assert.Equal(source.Transactions.Single().Amount, restored.Transactions.Single().Amount);
        Assert.Equal(
            source.ForecastLines.Single().MonthlyForecasts.Select(month => month.Amount),
            restored.ForecastLines.Single().MonthlyForecasts.Select(month => month.Amount));
    }

    [Fact]
    public void Corrupt_backup_is_rejected_without_changing_current_project_or_destination()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Root, "project.json");
        var destinationPath = Path.Combine(directory.Root, "restored.json");
        var service = CreateService();

        service.Save(sourcePath, CreateDataset("Current project"));
        var backupPath = service.CreateBackup(sourcePath);
        File.WriteAllText(backupPath, "{ this is not valid project json");

        var verification = service.VerifyBackup(backupPath);
        Assert.False(verification.IsUsable);

        Assert.Throws<ProjectBackupException>(
            () => service.RestoreBackup(backupPath, sourcePath, destinationPath));

        Assert.Equal("Current project", service.Load(sourcePath).Header.ProjectTitle);
        Assert.False(File.Exists(destinationPath));
    }

    [Fact]
    public void Backup_names_are_deterministic_at_timestamp_collisions_and_retention_is_bounded()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Root, "project.json");
        var fixedClock = () => new DateTime(2026, 8, 29, 10, 11, 12, DateTimeKind.Utc);
        var collisionService = new ProjectFileService(utcNow: fixedClock);
        collisionService.Save(sourcePath, CreateDataset("Collision source"));

        var collisionBackups = Enumerable.Range(0, 3)
            .Select(_ => collisionService.CreateBackup(sourcePath))
            .ToList();

        Assert.Equal(3, collisionBackups.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(collisionBackups, path => Path.GetFileName(path).Contains("-1.bak.json", StringComparison.Ordinal));
        Assert.Contains(collisionBackups, path => Path.GetFileName(path).Contains("-2.bak.json", StringComparison.Ordinal));

        using var retentionDirectory = new TemporaryDirectory();
        var retentionSourcePath = Path.Combine(retentionDirectory.Root, "project.json");
        var retentionService = new ProjectFileService(
            backupPolicy: new ProjectBackupPolicy(2),
            utcNow: fixedClock);
        retentionService.Save(retentionSourcePath, CreateDataset("Retention source"));

        _ = Enumerable.Range(0, 5)
            .Select(_ => retentionService.CreateBackup(retentionSourcePath))
            .ToList();

        var retainedBackups = Directory.EnumerateFiles(
                Path.Combine(retentionDirectory.Root, "backups"),
                "*.bak.json",
                SearchOption.TopDirectoryOnly)
            .ToList();

        Assert.Equal(2, retainedBackups.Count);
        Assert.All(retainedBackups, path => Assert.True(retentionService.VerifyBackup(path).IsUsable));
    }

    [Fact]
    public void Restore_over_existing_file_creates_a_verified_pre_restore_backup_first()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Root, "project.json");
        var service = CreateService();

        service.Save(sourcePath, CreateDataset("Known good version"));
        var backupPath = service.CreateBackup(sourcePath);
        service.Save(sourcePath, CreateDataset("Current version"));

        var restore = service.RestoreBackup(backupPath, sourcePath, sourcePath);

        Assert.NotEmpty(restore.PreRestoreBackupPath);
        Assert.True(File.Exists(restore.PreRestoreBackupPath));
        Assert.Equal("Known good version", service.Load(sourcePath).Header.ProjectTitle);
        Assert.Equal("Current version", service.Load(restore.PreRestoreBackupPath).Header.ProjectTitle);
    }

    private static ProjectFileService CreateService()
    {
        return new ProjectFileService(
            utcNow: () => new DateTime(2026, 8, 29, 10, 11, 12, DateTimeKind.Utc));
    }

    private static ProjectDataset CreateDataset(string title)
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = title,
                CurrentPeriod = "26-09"
            },
            ForecastPeriods =
            [
                new ForecastPeriod { Label = "26-09", StartDate = new DateOnly(2026, 3, 1) },
                new ForecastPeriod { Label = "26-10", StartDate = new DateOnly(2026, 4, 1) }
            ],
            ForecastLines =
            [
                new ForecastLine
                {
                    RowNumber = 1,
                    TaskNumber = "TASK-1",
                    ResourceName = "Resource A",
                    ProjectCode = "Category A",
                    TransactionProjectCode = "PROJECT-1",
                    Budget = 500m,
                    MonthlyForecasts =
                    [
                        new MonthlyForecast { PeriodLabel = "26-09", Amount = 25m },
                        new MonthlyForecast { PeriodLabel = "26-10", Amount = 50m }
                    ]
                }
            ],
            Transactions =
            [
                new CostTransaction
                {
                    RowNumber = 1,
                    FyPeriod = "26-09",
                    TaskNumber = "TASK-1",
                    ProjectCode = "PROJECT-1",
                    ManualName = "Resource A",
                    Amount = 100m
                }
            ]
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "ProjectCostForecast.UnitTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
