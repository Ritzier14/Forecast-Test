using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Presentation;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

/// <summary>
/// The application's composition boundary. Keeping construction here makes the
/// view model deterministic in tests and avoids a dependency-injection framework.
/// </summary>
public sealed class MainWindowViewModelDependencies
{
    public IClock Clock { get; init; } = SystemClock.Instance;
    public CalculationService CalculationService { get; init; } = new();
    public ProjectDatasetCloner ProjectDatasetCloner { get; init; } = new();
    public ProjectDatasetMigrationPipeline ProjectDatasetMigrationPipeline { get; init; } = new();
    public IProjectFileService ProjectFileService { get; init; } = new ProjectFileService();
    public IProjectFilePicker ProjectFilePicker { get; init; } = new WpfProjectFilePicker();
    public IProjectPrompt ProjectPrompt { get; init; } = new WpfProjectPrompt();
    public CsvTransactionService CsvTransactionService { get; init; } = new();
    public ValidationService ValidationService { get; init; } = new();
    public IUserPreferencesService UserPreferencesService { get; init; } = new UserPreferencesService();
    public SchedulingService SchedulingService { get; init; } = new();
    public ForecastCurveService ForecastCurveService { get; init; } = new();
    public Func<ProjectDataset> InitialDatasetFactory { get; init; } = () => new SampleDataService().Load();
    public Func<ProjectSaveConflict, SaveConflictDecision>? SaveConflictDecisionHandler { get; init; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Clock);
        ArgumentNullException.ThrowIfNull(CalculationService);
        ArgumentNullException.ThrowIfNull(ProjectDatasetCloner);
        ArgumentNullException.ThrowIfNull(ProjectDatasetMigrationPipeline);
        ArgumentNullException.ThrowIfNull(ProjectFileService);
        ArgumentNullException.ThrowIfNull(ProjectFilePicker);
        ArgumentNullException.ThrowIfNull(ProjectPrompt);
        ArgumentNullException.ThrowIfNull(CsvTransactionService);
        ArgumentNullException.ThrowIfNull(ValidationService);
        ArgumentNullException.ThrowIfNull(UserPreferencesService);
        ArgumentNullException.ThrowIfNull(SchedulingService);
        ArgumentNullException.ThrowIfNull(ForecastCurveService);
        ArgumentNullException.ThrowIfNull(InitialDatasetFactory);
    }
}
