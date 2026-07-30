using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

/// <summary>
/// The application's composition boundary. Keeping construction here makes the
/// view model deterministic in tests and avoids a dependency-injection framework.
/// </summary>
public sealed class MainWindowViewModelDependencies
{
    public CalculationService CalculationService { get; init; } = new();
    public IProjectFileService ProjectFileService { get; init; } = new ProjectFileService();
    public CsvTransactionService CsvTransactionService { get; init; } = new();
    public ValidationService ValidationService { get; init; } = new();
    public IUserPreferencesService UserPreferencesService { get; init; } = new UserPreferencesService();
    public SchedulingService SchedulingService { get; init; } = new();
    public ForecastCurveService ForecastCurveService { get; init; } = new();
    public Func<ProjectDataset> InitialDatasetFactory { get; init; } = () => new SampleDataService().Load();

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(CalculationService);
        ArgumentNullException.ThrowIfNull(ProjectFileService);
        ArgumentNullException.ThrowIfNull(CsvTransactionService);
        ArgumentNullException.ThrowIfNull(ValidationService);
        ArgumentNullException.ThrowIfNull(UserPreferencesService);
        ArgumentNullException.ThrowIfNull(SchedulingService);
        ArgumentNullException.ThrowIfNull(ForecastCurveService);
        ArgumentNullException.ThrowIfNull(InitialDatasetFactory);
    }
}
