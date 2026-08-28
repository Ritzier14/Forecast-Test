using System.Reflection;
using System.Threading;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;
using Xunit;

namespace ProjectCostForecast.UnitTests;

internal static class Luna11TestSupport
{
    private static readonly Lazy<string> RepositoryRootValue = new(FindRepositoryRoot);

    public static string RepositoryRoot => RepositoryRootValue.Value;

    public static string SampleDataPath => Path.Combine(
        RepositoryRoot,
        "src",
        "ProjectCostForecast.App",
        "Data",
        "SampleData.json");

    public static ProjectDataset LoadSeedDataset()
    {
        return new ProjectFileService().Load(SampleDataPath);
    }

    public static MainWindowViewModel CreateSeedViewModel(Func<ProjectDataset>? initialDatasetFactory = null)
    {
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = initialDatasetFactory ?? LoadSeedDataset
        });
    }

    public static ForecastLine FindForecastLine(ProjectDataset dataset, string taskNumber, string resourceName)
    {
        return dataset.ForecastLines.Single(line =>
            string.Equals(line.TaskNumber, taskNumber, StringComparison.OrdinalIgnoreCase)
            && string.Equals(line.ResourceName, resourceName, StringComparison.OrdinalIgnoreCase));
    }

    public static DateOnly GetCalendarMonthStart(string periodLabel)
    {
        Assert.True(FiscalPeriod.TryGetCalendarMonthStart(periodLabel, out var calendarMonthStart));
        return calendarMonthStart;
    }

    public static void InvokeLoadDataset(MainWindowViewModel viewModel, ProjectDataset dataset)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "LoadDataset",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(MainWindowViewModel).FullName, "LoadDataset");
        method.Invoke(viewModel, [dataset, false]);
    }

    public static double InvokeForecastWidthMigration(double savedWidth, double currentWidth, double minWidth, bool isTotal)
    {
        var method = typeof(MainWindow).GetMethod(
            "GetAppliedLayoutWidth",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(MainWindow).FullName, "GetAppliedLayoutWidth");
        var column = new System.Windows.Controls.DataGridTextColumn
        {
            Header = new ForecastMonthColumnDefinition
            {
                Key = isTotal ? "TOTAL:26-11" : "MONTH:26-11",
                IsTotal = isTotal
            },
            Width = new System.Windows.Controls.DataGridLength(currentWidth),
            MinWidth = minWidth
        };

        return (double)(method.Invoke(null, [column, savedWidth])
            ?? throw new InvalidOperationException("Forecast width migration returned null."));
    }

    public static void RunOnSta(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("STA test action failed.", failure);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ProjectCostForecast.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate ProjectCostForecast.sln.");
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

internal sealed class Luna11TemporaryDirectory : IDisposable
{
    public Luna11TemporaryDirectory()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "ProjectCostForecast.Luna11",
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
