using System.IO;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna15FileWorkflowTests
{
    [Fact]
    public void Open_cancelled_by_unsaved_prompt_preserves_the_active_session()
    {
        var fileService = new RecordingProjectFileService();
        var picker = new RecordingProjectFilePicker { OpenPath = "new-project.json" };
        var prompt = new RecordingProjectPrompt { ConfirmDiscardResult = false };
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.ProjectFilePath = "active-project.json";
        viewModel.IsDirty = true;
        var activeTitle = viewModel.Header.ProjectTitle;

        viewModel.OpenProjectCommand.Execute(null);

        Assert.Equal(activeTitle, viewModel.Header.ProjectTitle);
        Assert.Equal("active-project.json", viewModel.ProjectFilePath);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(1, prompt.ConfirmCalls);
        Assert.Equal(0, picker.OpenCalls);
        Assert.Equal(0, fileService.LoadCalls);
    }

    [Fact]
    public void Open_io_failure_preserves_the_active_session_and_dirty_state()
    {
        var fileService = new RecordingProjectFileService
        {
            LoadHandler = _ => throw new IOException("forced open failure")
        };
        var picker = new RecordingProjectFilePicker { OpenPath = "missing-project.json" };
        var prompt = new RecordingProjectPrompt();
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.ProjectFilePath = "active-project.json";
        viewModel.IsDirty = true;
        var activeTitle = viewModel.Header.ProjectTitle;

        viewModel.OpenProjectCommand.Execute(null);

        Assert.Equal(activeTitle, viewModel.Header.ProjectTitle);
        Assert.Equal("active-project.json", viewModel.ProjectFilePath);
        Assert.True(viewModel.IsDirty);
        Assert.Contains(prompt.Errors, error => error.Message.Contains("forced open failure", StringComparison.Ordinal));
    }

    [Fact]
    public void Open_validation_failure_preserves_the_active_session_and_reports_the_boundary_error()
    {
        var invalidProject = CreateDataset("Invalid project");
        invalidProject.ForecastLines[0].ProjectCode = string.Empty;
        var fileService = new RecordingProjectFileService
        {
            LoadHandler = _ => new ProjectFileLoadResult(invalidProject, null)
        };
        var picker = new RecordingProjectFilePicker { OpenPath = "invalid-project.json" };
        var prompt = new RecordingProjectPrompt();
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.ProjectFilePath = "active-project.json";
        viewModel.IsDirty = true;
        var activeTitle = viewModel.Header.ProjectTitle;

        viewModel.OpenProjectCommand.Execute(null);

        Assert.Equal(activeTitle, viewModel.Header.ProjectTitle);
        Assert.Equal("active-project.json", viewModel.ProjectFilePath);
        Assert.True(viewModel.IsDirty);
        Assert.Contains(ValidationIssueCodes.ForecastLineProjectCodeRequired, viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains(prompt.Errors, error => error.Title == "Open failed");
    }

    [Fact]
    public void Successful_open_replaces_the_active_session_and_clears_dirty_state()
    {
        var loadedProject = CreateDataset("Loaded project");
        var path = "loaded-project.json";
        var fileService = new RecordingProjectFileService
        {
            LoadHandler = _ => new ProjectFileLoadResult(
                loadedProject,
                new ProjectFileRevision(path, 10, "loaded"))
        };
        var picker = new RecordingProjectFilePicker { OpenPath = path };
        var prompt = new RecordingProjectPrompt();
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.ProjectFilePath = "active-project.json";
        viewModel.IsDirty = true;

        viewModel.OpenProjectCommand.Execute(null);

        Assert.Equal("Loaded project", viewModel.Header.ProjectTitle);
        Assert.Equal(path, viewModel.ProjectFilePath);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, fileService.LoadCalls);
        Assert.Empty(prompt.Errors);
    }

    [Fact]
    public void Save_as_cancellation_keeps_the_active_session_dirty()
    {
        var fileService = new RecordingProjectFileService();
        var picker = new RecordingProjectFilePicker { SavePath = null };
        var prompt = new RecordingProjectPrompt();
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.IsDirty = true;

        viewModel.SaveProjectCommand.Execute(null);

        Assert.True(viewModel.IsDirty);
        Assert.Equal(string.Empty, viewModel.ProjectFilePath);
        Assert.Equal(1, picker.SaveCalls);
        Assert.Equal(0, fileService.SaveCalls);
        Assert.Empty(prompt.Errors);
    }

    [Fact]
    public void Save_validation_failure_does_not_write_or_clear_dirty_state()
    {
        var fileService = new RecordingProjectFileService();
        var picker = new RecordingProjectFilePicker();
        var prompt = new RecordingProjectPrompt();
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.ProjectFilePath = "validation-project.json";
        viewModel.ForecastLines[0].ProjectCode = string.Empty;
        viewModel.IsDirty = true;

        viewModel.SaveProjectCommand.Execute(null);

        Assert.True(viewModel.IsDirty);
        Assert.Equal("validation-project.json", viewModel.ProjectFilePath);
        Assert.Equal(0, fileService.SaveCalls);
        Assert.Contains(ValidationIssueCodes.ForecastLineProjectCodeRequired, viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains(prompt.Errors, error => error.Title == "Save project blocked");
    }

    [Fact]
    public void Save_io_failure_rolls_back_the_save_audit_and_keeps_the_session_dirty()
    {
        var fileService = new RecordingProjectFileService { ThrowOnSave = true };
        var picker = new RecordingProjectFilePicker();
        var prompt = new RecordingProjectPrompt();
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.ProjectFilePath = "io-failure-project.json";
        viewModel.IsDirty = true;
        var auditCount = viewModel.AuditEvents.Count;

        viewModel.SaveProjectCommand.Execute(null);

        Assert.True(viewModel.IsDirty);
        Assert.Equal("io-failure-project.json", viewModel.ProjectFilePath);
        Assert.Equal(auditCount, viewModel.AuditEvents.Count);
        Assert.Equal(1, fileService.SaveCalls);
        Assert.Contains(prompt.Errors, error => error.Message.Contains("forced save failure", StringComparison.Ordinal));
    }

    [Fact]
    public void Save_conflict_cancel_keeps_the_active_session_dirty_without_audit_mutation()
    {
        var path = "conflict-project.json";
        var expectedRevision = new ProjectFileRevision(path, 10, "expected");
        var fileService = new RecordingProjectFileService
        {
            LoadHandler = _ => new ProjectFileLoadResult(CreateDataset("Loaded project"), expectedRevision),
            CurrentRevision = new ProjectFileRevision(path, 11, "external")
        };
        var picker = new RecordingProjectFilePicker { OpenPath = path };
        var prompt = new RecordingProjectPrompt { ConflictDecision = SaveConflictDecision.Cancel };
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.OpenProjectCommand.Execute(null);
        viewModel.Header.ProjectTitle = "Local changes";
        viewModel.IsDirty = true;
        var auditCount = viewModel.AuditEvents.Count;

        viewModel.SaveProjectCommand.Execute(null);

        Assert.True(viewModel.IsDirty);
        Assert.Equal("Local changes", viewModel.Header.ProjectTitle);
        Assert.Equal(path, viewModel.ProjectFilePath);
        Assert.Equal(auditCount, viewModel.AuditEvents.Count);
        Assert.Equal(0, fileService.SaveCalls);
        Assert.Equal(1, prompt.ConflictCalls);
        Assert.Contains("cancelled", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_conflict_reload_replaces_the_session_and_discards_unsaved_changes()
    {
        var path = "reload-project.json";
        var expectedRevision = new ProjectFileRevision(path, 10, "expected");
        var externalRevision = new ProjectFileRevision(path, 11, "external");
        var loadCalls = 0;
        var fileService = new RecordingProjectFileService
        {
            LoadHandler = _ =>
            {
                loadCalls++;
                return loadCalls == 1
                    ? new ProjectFileLoadResult(CreateDataset("Loaded project"), expectedRevision)
                    : new ProjectFileLoadResult(CreateDataset("External project"), externalRevision);
            },
            CurrentRevision = externalRevision
        };
        var picker = new RecordingProjectFilePicker { OpenPath = path };
        var prompt = new RecordingProjectPrompt { ConflictDecision = SaveConflictDecision.Reload };
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.OpenProjectCommand.Execute(null);
        viewModel.Header.ProjectTitle = "Local changes";
        viewModel.IsDirty = true;

        viewModel.SaveProjectCommand.Execute(null);

        Assert.Equal("External project", viewModel.Header.ProjectTitle);
        Assert.Equal(path, viewModel.ProjectFilePath);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(0, fileService.SaveCalls);
        Assert.Equal(2, fileService.LoadCalls);
        Assert.Contains("Reloaded", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_conflict_save_as_writes_to_the_new_path_and_clears_dirty_state()
    {
        var path = "conflict-project.json";
        var saveAsPath = "preserved-project.json";
        var expectedRevision = new ProjectFileRevision(path, 10, "expected");
        var fileService = new RecordingProjectFileService
        {
            LoadHandler = _ => new ProjectFileLoadResult(CreateDataset("Loaded project"), expectedRevision),
            CurrentRevision = new ProjectFileRevision(path, 11, "external"),
            ReturnedRevision = new ProjectFileRevision(saveAsPath, 12, "saved")
        };
        var picker = new RecordingProjectFilePicker { OpenPath = path };
        picker.SavePaths.Enqueue(saveAsPath);
        var prompt = new RecordingProjectPrompt { ConflictDecision = SaveConflictDecision.SaveAs };
        var viewModel = CreateViewModel(fileService, picker, prompt);
        viewModel.OpenProjectCommand.Execute(null);
        viewModel.Header.ProjectTitle = "Local changes";
        viewModel.IsDirty = true;

        viewModel.SaveProjectCommand.Execute(null);

        Assert.Equal("Local changes", viewModel.Header.ProjectTitle);
        Assert.Equal(saveAsPath, viewModel.ProjectFilePath);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, fileService.SaveCalls);
        Assert.NotNull(fileService.LastSavedDataset);
        Assert.Contains(
            fileService.LastSavedDataset!.AuditEvents,
            auditEvent => auditEvent.FieldName == "Saved" && auditEvent.NewValue == saveAsPath);
    }

    private static MainWindowViewModel CreateViewModel(
        RecordingProjectFileService fileService,
        RecordingProjectFilePicker picker,
        RecordingProjectPrompt prompt)
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            ProjectFileService = fileService,
            ProjectFilePicker = picker,
            ProjectPrompt = prompt,
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = () => CreateDataset("Active project")
        });
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
                new ForecastPeriod
                {
                    Label = "26-09",
                    StartDate = new DateOnly(2026, 3, 1)
                }
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
                    MonthlyForecasts =
                    [
                        new MonthlyForecast
                        {
                            PeriodLabel = "26-09",
                            PeriodStartDate = new DateOnly(2026, 3, 1),
                            Amount = 25m
                        }
                    ]
                }
            ]
        };
    }

    private sealed class RecordingProjectFilePicker : IProjectFilePicker
    {
        public string? OpenPath { get; init; }
        public string? SavePath { get; init; }
        public Queue<string?> SavePaths { get; } = new();
        public int OpenCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public string? PickOpenProjectPath()
        {
            OpenCalls++;
            return OpenPath;
        }

        public string? PickSaveProjectPath(string suggestedFileName)
        {
            SaveCalls++;
            return SavePaths.Count > 0 ? SavePaths.Dequeue() : SavePath;
        }
    }

    private sealed class RecordingProjectPrompt : IProjectPrompt
    {
        public bool ConfirmDiscardResult { get; init; } = true;
        public SaveConflictDecision ConflictDecision { get; init; } = SaveConflictDecision.Cancel;
        public List<(string Title, string Message)> Errors { get; } = [];
        public int ConfirmCalls { get; private set; }
        public int ConflictCalls { get; private set; }

        public bool ConfirmDiscardUnsavedChanges()
        {
            ConfirmCalls++;
            return ConfirmDiscardResult;
        }

        public SaveConflictDecision ChooseSaveConflict(ProjectSaveConflict conflict)
        {
            ConflictCalls++;
            return ConflictDecision;
        }

        public void ShowError(string title, string message)
        {
            Errors.Add((title, message));
        }
    }

    private sealed class RecordingProjectFileService : IProjectFileService
    {
        public Func<string, ProjectFileLoadResult>? LoadHandler { get; init; }
        public bool ThrowOnSave { get; init; }
        public ProjectFileRevision? CurrentRevision { get; init; }
        public ProjectFileRevision? ReturnedRevision { get; init; }
        public int LoadCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public ProjectDataset? LastSavedDataset { get; private set; }

        public ProjectDataset Load(string path) => LoadWithRevision(path).Dataset;

        public ProjectFileLoadResult LoadWithRevision(string path)
        {
            LoadCalls++;
            return LoadHandler?.Invoke(path)
                ?? new ProjectFileLoadResult(CreateDataset("Loaded project"), null);
        }

        public void Save(string path, ProjectDataset dataset)
        {
            SaveCalls++;
            LastSavedDataset = dataset;
            if (ThrowOnSave)
            {
                throw new IOException("forced save failure");
            }
        }

        public string CreateBackup(string path) => string.Empty;

        public ProjectFileRevision? GetRevision(string path) => CurrentRevision;

        public ProjectFileRevision? SaveWithRevision(
            string path,
            ProjectDataset dataset,
            ProjectFileRevision? expectedRevision,
            string operation = "Save project")
        {
            Save(path, dataset);
            return ReturnedRevision;
        }
    }

    private sealed class InMemoryUserPreferencesService : IUserPreferencesService
    {
        private AppUserPreferences _preferences = new();

        public AppUserPreferences Load() => _preferences;

        public void Save(AppUserPreferences preferences)
        {
            _preferences = preferences;
        }
    }
}
