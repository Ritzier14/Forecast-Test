using System.IO;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class SafeCloseTests
{
    [Fact]
    public void Clean_close_does_not_save()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);

        Assert.True(viewModel.ConfirmClose(CloseDecision.Cancel));
        Assert.False(viewModel.IsDirty);
        Assert.Equal(0, fileService.SaveCalls);
    }

    [Fact]
    public void Dirty_cancel_keeps_window_state_open()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.IsDirty = true;

        Assert.False(viewModel.ConfirmClose(CloseDecision.Cancel));
        Assert.True(viewModel.IsDirty);
        Assert.Equal(0, fileService.SaveCalls);
    }

    [Fact]
    public void Dirty_discard_closes_only_after_explicit_discard_decision()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.IsDirty = true;

        Assert.True(viewModel.ConfirmClose(CloseDecision.Discard));
        Assert.False(viewModel.IsDirty);
        Assert.Equal(0, fileService.SaveCalls);
    }

    [Fact]
    public void Dirty_save_closes_after_successful_persistence()
    {
        var fileService = new RecordingProjectFileService();
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "close-test.json";
        viewModel.IsDirty = true;

        Assert.True(viewModel.ConfirmClose(CloseDecision.Save));
        Assert.False(viewModel.IsDirty);
        Assert.Equal(1, fileService.SaveCalls);
    }

    [Fact]
    public void Dirty_save_failure_keeps_window_open_and_dirty()
    {
        var fileService = new RecordingProjectFileService { ThrowOnSave = true };
        var viewModel = CreateViewModel(fileService);
        viewModel.ProjectFilePath = "close-test.json";
        viewModel.IsDirty = true;

        Assert.False(viewModel.ConfirmClose(CloseDecision.Save));
        Assert.True(viewModel.IsDirty);
        Assert.Equal(1, fileService.SaveCalls);
    }

    private static MainWindowViewModel CreateViewModel(RecordingProjectFileService fileService)
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            ProjectFileService = fileService,
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = () => new ProjectDataset
            {
                Header = new ProjectHeader { ProjectTitle = "Close fixture" }
            }
        });
    }

    private sealed class RecordingProjectFileService : IProjectFileService
    {
        public int SaveCalls { get; private set; }
        public bool ThrowOnSave { get; init; }

        public ProjectDataset Load(string path) => new();

        public void Save(string path, ProjectDataset dataset)
        {
            SaveCalls++;
            if (ThrowOnSave)
            {
                throw new IOException("forced save failure");
            }
        }

        public string CreateBackup(string path) => string.Empty;
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
