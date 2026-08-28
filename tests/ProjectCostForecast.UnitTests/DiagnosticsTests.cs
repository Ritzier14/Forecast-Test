using System.Collections.Generic;
using System.IO;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class DiagnosticsTests
{
    [Fact]
    public void Malformed_preferences_are_quarantined_defaults_loaded_and_reason_sanitized()
    {
        using var directory = new TemporaryDirectory();
        var preferencesPath = Path.Combine(directory.Root, "user-preferences.json");
        const string malformedPreferences = "{ \"SelectedProjectCode\": \"Private Project\", ";
        File.WriteAllText(preferencesPath, malformedPreferences);

        var diagnostics = new DiagnosticsService(
            Path.Combine(directory.Root, "diagnostics"),
            maxFileBytes: 512,
            maxFileCount: 2);
        var service = new UserPreferencesService(
            preferencesPath,
            diagnostics,
            () => new DateTime(2026, 8, 29, 10, 11, 12, DateTimeKind.Utc));

        var preferences = service.Load();

        Assert.Equal("All", preferences.SelectedProjectCode);
        Assert.Equal("Invalid preferences were quarantined; defaults are in use.", service.LastLoadNotice);
        Assert.False(File.Exists(preferencesPath));
        Assert.NotNull(service.LastQuarantinedPath);
        Assert.True(File.Exists(service.LastQuarantinedPath));
        Assert.Equal(malformedPreferences, File.ReadAllText(service.LastQuarantinedPath));
        Assert.Contains(
            "user-preferences.corrupt-20260829-101112-000.json",
            Path.GetFileName(service.LastQuarantinedPath),
            StringComparison.Ordinal);

        var log = File.ReadAllText(diagnostics.LogPath);
        Assert.Contains("preferences.load", log, StringComparison.Ordinal);
        Assert.Contains("JsonException", log, StringComparison.Ordinal);
        Assert.Contains("Invalid preferences quarantined", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Private Project", log, StringComparison.Ordinal);
        Assert.DoesNotContain("user-preferences.json", log, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostics_log_keeps_only_a_bounded_active_and_rotated_set()
    {
        using var directory = new TemporaryDirectory();
        var diagnostics = new DiagnosticsService(directory.Root, maxFileBytes: 256, maxFileCount: 2);

        for (var index = 0; index < 100; index++)
        {
            diagnostics.Record(new DiagnosticEvent(
                DateTimeOffset.UtcNow,
                DiagnosticSeverity.Warning,
                "test-operation",
                "InvalidOperationException",
                "A bounded diagnostic reason with no persisted project values."));
        }

        var logs = Directory.GetFiles(directory.Root, "diagnostics*.log");

        Assert.InRange(logs.Length, 1, 2);
        Assert.All(logs, path => Assert.InRange(new FileInfo(path).Length, 1, 256));
        Assert.InRange(logs.Sum(path => new FileInfo(path).Length), 1, 512);
    }

    [Fact]
    public void Diagnostics_write_failure_does_not_escape_the_recording_boundary()
    {
        using var directory = new TemporaryDirectory();
        var blockedDirectory = Path.Combine(directory.Root, "blocked-diagnostics");
        File.WriteAllText(blockedDirectory, "a file, not a directory");
        var diagnostics = new DiagnosticsService(blockedDirectory);

        var failure = Record.Exception(() => diagnostics.RecordException(
            "test-operation",
            new InvalidOperationException("C:\\Users\\Alice\\private-project.json"),
            "A safe reason"));

        Assert.Null(failure);
    }

    [Fact]
    public void Ui_boundary_policy_records_sanitized_failure_and_requests_fail_fast()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var policy = new RuntimeExceptionPolicy(diagnostics);

        var result = policy.Handle(
            RuntimeExceptionBoundary.UiDispatcher,
            new InvalidOperationException("C:\\Users\\Alice\\private-project.json"));

        Assert.True(result.FailFast);
        Assert.Contains("will close", result.UserMessage, StringComparison.Ordinal);
        var diagnostic = Assert.Single(diagnostics.Events);
        Assert.Equal("UI dispatcher", diagnostic.Operation);
        Assert.Equal("InvalidOperationException", diagnostic.ExceptionType);
        Assert.Equal("Unexpected application failure at a top-level boundary.", diagnostic.Reason);
        Assert.DoesNotContain("Alice", diagnostic.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("private-project", diagnostic.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void View_model_surfaces_preference_recovery_notice_in_startup_status()
    {
        var preferences = new NotifyingUserPreferencesService();
        var viewModel = new ProjectCostForecast.App.ViewModels.MainWindowViewModel(
            new ProjectCostForecast.App.ViewModels.MainWindowViewModelDependencies
            {
                UserPreferencesService = preferences,
                InitialDatasetFactory = () => new ProjectDataset
                {
                    Header = new ProjectHeader { ProjectTitle = "Diagnostics fixture" }
                }
            });

        Assert.Contains("Invalid preferences were quarantined", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_keeps_isolated_background_failures_observed_and_diagnostics_failures_non_fatal()
    {
        var diagnostics = new ThrowingDiagnosticsService();
        var policy = new RuntimeExceptionPolicy(diagnostics);

        var failure = Record.Exception(() => policy.Handle(
            RuntimeExceptionBoundary.UnobservedTask,
            new InvalidOperationException("background failure")));

        Assert.Null(failure);
        var result = policy.Handle(
            RuntimeExceptionBoundary.UnobservedTask,
            new InvalidOperationException("background failure"));
        Assert.False(result.FailFast);
    }

    private sealed class RecordingDiagnosticsService : IDiagnosticsService
    {
        public List<DiagnosticEvent> Events { get; } = [];

        public void Record(DiagnosticEvent diagnostic) => Events.Add(diagnostic);

        public void RecordException(
            string operation,
            Exception exception,
            string reason,
            DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            Events.Add(new DiagnosticEvent(
                DateTimeOffset.UtcNow,
                severity,
                operation,
                exception.GetType().Name,
                reason));
        }
    }

    private sealed class ThrowingDiagnosticsService : IDiagnosticsService
    {
        public void Record(DiagnosticEvent diagnostic) => throw new IOException("diagnostics unavailable");

        public void RecordException(
            string operation,
            Exception exception,
            string reason,
            DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            throw new IOException("diagnostics unavailable");
        }
    }

    private sealed class NotifyingUserPreferencesService : IUserPreferencesService
    {
        public AppUserPreferences Load() => new();

        public void Save(AppUserPreferences preferences)
        {
        }

        public string? LastLoadNotice => "Invalid preferences were quarantined; defaults are in use.";
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
