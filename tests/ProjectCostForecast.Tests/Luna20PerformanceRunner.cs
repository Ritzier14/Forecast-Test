using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCostForecast.App;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using ProjectCostForecast.App.ViewModels;

internal static class Luna20PerformanceRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Run(string[] args)
    {
        var root = FindRepositoryRoot();
        var outputPath = ResolveOutputPath(root, GetOption(args, "--output")
            ?? Path.Combine("docs", "audit", "LUNA-20-PERFORMANCE-BASELINE.json"));
        var mode = GetOption(args, "--mode") ?? "baseline";
        var iterations = ParsePositiveOption(args, "--iterations", 3);
        var memoryCycles = ParsePositiveOption(args, "--memory-cycles", 5);
        var commit = GetOption(args, "--commit") ?? "working-tree";
        WorkloadSpecification[] specifications =
        [
            new WorkloadSpecification("small", 50, 500, 18, 100),
            new WorkloadSpecification("normal", 500, 20_000, 36, 1_000),
            new WorkloadSpecification("stress", 2_000, 100_000, 60, 2_500)
        ];

        var scenarios = new List<Luna20ScenarioResult>();
        var datasets = new List<Luna20DatasetSummary>();
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "ProjectCostForecast.Luna20",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            foreach (var specification in specifications)
            {
                var dataset = CreateDataset(specification);
                new CalculationService().Recalculate(dataset);

                var jsonPath = Path.Combine(temporaryRoot, $"{specification.Name}.json");
                var savePath = Path.Combine(temporaryRoot, $"{specification.Name}.save.json");
                var csvPath = Path.Combine(temporaryRoot, $"{specification.Name}.csv");
                var fileService = new ProjectFileService();
                fileService.Save(jsonPath, dataset);
                new CsvTransactionService().ExportTransactions(csvPath, dataset.Transactions);

                datasets.Add(new Luna20DatasetSummary
                {
                    Name = specification.Name,
                    ForecastLines = dataset.ForecastLines.Count,
                    Transactions = dataset.Transactions.Count,
                    ForecastPeriods = dataset.ForecastPeriods.Count,
                    MonthlyForecastCells = dataset.ForecastLines.Sum(line => line.MonthlyForecasts.Count),
                    ScheduleActivities = dataset.Schedule.Activities.Count,
                    ScheduleLinks = Math.Max(0, dataset.Schedule.Activities.Count - 1),
                    ProjectJsonBytes = new FileInfo(jsonPath).Length,
                    ImportCsvBytes = new FileInfo(csvPath).Length
                });

                AddCoreMeasurements(
                    scenarios,
                    specification,
                    dataset,
                    jsonPath,
                    savePath,
                    csvPath,
                    iterations);
            }

            var normal = specifications.Single(specification => specification.Name == "normal");
            var normalDataset = CreateDataset(normal);
            new CalculationService().Recalculate(normalDataset);
            AddViewModelMeasurements(scenarios, normal, normalDataset, iterations);
            AddMemoryMeasurement(scenarios, normal, normalDataset, iterations, memoryCycles);

            var report = new Luna20PerformanceReport
            {
                SchemaVersion = 1,
                Mode = mode,
                CapturedUtc = DateTimeOffset.UtcNow,
                RepositoryCommit = commit,
                Runtime = new Luna20RuntimeInfo
                {
                    Framework = RuntimeInformation.FrameworkDescription,
                    RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                    OperatingSystem = RuntimeInformation.OSDescription,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    Is64BitProcess = Environment.Is64BitProcess,
                    ProcessorCount = Environment.ProcessorCount,
                    Configuration = "Release",
                    Iterations = iterations,
                    MemoryCycles = memoryCycles
                },
                WorkloadPolicy = new Luna20WorkloadPolicy
                {
                    Small = "50 forecast lines, 500 transactions, 18 forecast periods, 100 schedule activities",
                    Normal = "500 forecast lines, 20,000 transactions, 36 forecast periods, 1,000 schedule activities",
                    Stress = "2,000 forecast lines, 100,000 transactions, 60 forecast periods, 2,500 schedule activities",
                    Data = "Synthetic deterministic identifiers and values; no workbook or personal data"
                },
                Datasets = datasets,
                Scenarios = scenarios
            };

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);
            Console.WriteLine($"LUNA-20 {mode} performance report: {outputPath}");
            foreach (var scenario in scenarios)
            {
                Console.WriteLine(
                    $"{scenario.Dataset}/{scenario.Name}: median={scenario.MedianMilliseconds:N2} ms, p95={scenario.P95Milliseconds:N2} ms");
            }

            Console.WriteLine("LUNA-20 benchmark completed with all workload correctness checks passing.");
        }
        finally
        {
            TryDeleteTemporaryDirectory(temporaryRoot);
        }
    }

    private static void AddCoreMeasurements(
        ICollection<Luna20ScenarioResult> scenarios,
        WorkloadSpecification specification,
        ProjectDataset dataset,
        string jsonPath,
        string savePath,
        string csvPath,
        int iterations)
    {
        var fileService = new ProjectFileService();
        var csvService = new CsvTransactionService();
        var calculationService = new CalculationService();
        var schedulingService = new SchedulingService();

        scenarios.Add(Measure(
            "startup-view-model",
            specification.Name,
            iterations,
            () =>
            {
                using var viewModel = CreateViewModel(dataset);
                EnsureViewModelShape(viewModel, specification);
            }));

        scenarios.Add(Measure(
            "project-load",
            specification.Name,
            iterations,
            () =>
            {
                var loaded = fileService.Load(jsonPath);
                EnsureDatasetShape(loaded, specification);
            }));

        scenarios.Add(Measure(
            "project-save",
            specification.Name,
            iterations,
            () => fileService.Save(savePath, dataset)));

        scenarios.Add(Measure(
            "csv-import",
            specification.Name,
            iterations,
            () =>
            {
                var imported = csvService.Import(csvPath, 1);
                if (imported.Count != specification.TransactionCount)
                {
                    throw new InvalidOperationException(
                        $"Synthetic CSV import returned {imported.Count} rows; expected {specification.TransactionCount}.");
                }
            }));

        var recalculationInputs = Enumerable.Range(0, iterations + 1)
            .Select(_ => new ProjectDatasetCloner().Clone(dataset))
            .ToArray();
        scenarios.Add(MeasureSeries(
            "full-recalculation",
            specification.Name,
            iterations,
            index => calculationService.Recalculate(recalculationInputs[index])));

        var scheduleInputs = Enumerable.Range(0, iterations + 1)
            .Select(_ => CreateSchedule(specification))
            .ToArray();
        scenarios.Add(MeasureSeries(
            "schedule-calculation",
            specification.Name,
            iterations,
            index =>
            {
                schedulingService.Recalculate(scheduleInputs[index]);
                if (scheduleInputs[index].Activities.Any(activity => activity.EarlyStart is null))
                {
                    throw new InvalidOperationException("Synthetic schedule did not calculate every activity.");
                }
            }));
    }

    private static void AddViewModelMeasurements(
        ICollection<Luna20ScenarioResult> scenarios,
        WorkloadSpecification specification,
        ProjectDataset dataset,
        int iterations)
    {
        using (var viewModel = CreateViewModel(dataset))
        {
            var forecast = viewModel.ForecastLines[0].MonthlyForecasts[0];
            var gridEditResult = MeasureSeries(
                "grid-edit-refresh",
                specification.Name,
                iterations,
                _ =>
                {
                    forecast.Amount += 0.01m;
                    viewModel.FlushPendingRefreshes();
                });
            var refreshSnapshot = viewModel.RefreshDiagnostics;
            gridEditResult.LastRefreshMilliseconds = Math.Round(refreshSnapshot.LastRefreshDuration.TotalMilliseconds, 3);
            gridEditResult.RefreshPhaseCounts = refreshSnapshot.PhaseCounts.ToDictionary(
                pair => pair.Key.ToString(),
                pair => pair.Value,
                StringComparer.Ordinal);
            gridEditResult.RefreshPhaseMilliseconds = refreshSnapshot.PhaseDurations.ToDictionary(
                pair => pair.Key.ToString(),
                pair => Math.Round(pair.Value.TotalMilliseconds, 3),
                StringComparer.Ordinal);
            scenarios.Add(gridEditResult);
        }

        using (var viewModel = CreateViewModel(dataset))
        {
            var workspaceKeys = new[] { "Resources", "CTC Forecast", "Monthly Report", "Actuals Pivot", "Raw Transactions" };
            var detailWorkspaceKeys = new[] { "Ledger Costs", "Ledger Monthly Forecast" };
            scenarios.Add(MeasureSeries(
                "workspace-switch",
                specification.Name,
                iterations,
                index =>
                {
                    viewModel.ActiveWorkspaceKey = workspaceKeys[index % workspaceKeys.Length];
                    viewModel.ActiveDetailWorkspaceKey = detailWorkspaceKeys[index % detailWorkspaceKeys.Length];
                    viewModel.FlushPendingRefreshes();
                }));
        }
    }

    private static void AddMemoryMeasurement(
        ICollection<Luna20ScenarioResult> scenarios,
        WorkloadSpecification specification,
        ProjectDataset dataset,
        int iterations,
        int memoryCycles)
    {
        var samples = new List<double>();
        var memoryBefore = new List<long>();
        var memoryAfter = new List<long>();
        var memoryDelta = new List<long>();

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            CollectGarbage();
            var before = GC.GetTotalMemory(forceFullCollection: true);
            var stopwatch = Stopwatch.StartNew();
            for (var cycle = 0; cycle < memoryCycles; cycle++)
            {
                using var viewModel = CreateViewModel(dataset);
                EnsureViewModelShape(viewModel, specification);
            }

            stopwatch.Stop();
            CollectGarbage();
            var after = GC.GetTotalMemory(forceFullCollection: true);
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            memoryBefore.Add(before);
            memoryAfter.Add(after);
            memoryDelta.Add(after - before);
        }

        scenarios.Add(BuildResult(
            "memory-repeated-open-close",
            specification.Name,
            samples,
            memoryBefore,
            memoryAfter,
            memoryDelta,
            $"Created and disposed {memoryCycles} headless view-model sessions per sample."));
    }

    private static Luna20ScenarioResult Measure(
        string name,
        string dataset,
        int iterations,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        var samples = new List<double>(iterations);
        for (var index = 0; index < iterations; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return BuildResult(name, dataset, samples, description: "Timed synthetic workload.");
    }

    private static Luna20ScenarioResult MeasureSeries(
        string name,
        string dataset,
        int iterations,
        Action<int> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action(0);
        var samples = new List<double>(iterations);
        for (var index = 0; index < iterations; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            action(index + 1);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return BuildResult(name, dataset, samples, description: "Timed synthetic workload.");
    }

    private static Luna20ScenarioResult BuildResult(
        string name,
        string dataset,
        IReadOnlyList<double> samples,
        IReadOnlyList<long>? memoryBefore = null,
        IReadOnlyList<long>? memoryAfter = null,
        IReadOnlyList<long>? memoryDelta = null,
        string? description = null)
    {
        var ordered = samples.OrderBy(value => value).ToArray();
        var median = ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[(ordered.Length / 2) - 1] + ordered[ordered.Length / 2]) / 2;
        var p95Index = Math.Clamp((int)Math.Ceiling(ordered.Length * 0.95) - 1, 0, ordered.Length - 1);
        return new Luna20ScenarioResult
        {
            Name = name,
            Dataset = dataset,
            Description = description,
            SamplesMilliseconds = samples.Select(value => Math.Round(value, 3)).ToArray(),
            MinimumMilliseconds = Math.Round(ordered[0], 3),
            MedianMilliseconds = Math.Round(median, 3),
            P95Milliseconds = Math.Round(ordered[p95Index], 3),
            MaximumMilliseconds = Math.Round(ordered[^1], 3),
            MemoryBeforeBytes = memoryBefore,
            MemoryAfterBytes = memoryAfter,
            MemoryDeltaBytes = memoryDelta
        };
    }

    private static MainWindowViewModel CreateViewModel(ProjectDataset source)
    {
        var cloner = new ProjectDatasetCloner();
        return new MainWindowViewModel(new MainWindowViewModelDependencies
        {
            UserPreferencesService = new InMemoryUserPreferencesService(),
            InitialDatasetFactory = () => cloner.Clone(source)
        });
    }

    private static ProjectDataset CreateDataset(WorkloadSpecification specification)
    {
        var periodStart = new DateOnly(2024, 1, 1);
        var periods = Enumerable.Range(0, specification.PeriodCount)
            .Select(index =>
            {
                var startDate = periodStart.AddMonths(index);
                return new ForecastPeriod
                {
                    Label = FiscalPeriod.LabelFromCalendarMonth(startDate),
                    StartDate = startDate
                };
            })
            .ToList();
        var lines = new BatchObservableCollection<ForecastLine>();
        for (var lineIndex = 0; lineIndex < specification.LineCount; lineIndex++)
        {
            var projectCode = $"PROJECT-{lineIndex % 25:D2}";
            var taskNumber = $"TASK-{lineIndex:D6}";
            var resourceName = $"Resource-{lineIndex % 100:D3}";
            lines.Add(new ForecastLine
            {
                RowNumber = lineIndex + 1,
                TaskNumber = taskNumber,
                ResourceName = resourceName,
                ProjectCode = projectCode,
                TransactionProjectCode = projectCode,
                UseLedgerResourceMatchOnly = true,
                Budget = 100_000m,
                MonthlyForecasts = periods
                    .Select((period, periodIndex) => new MonthlyForecast
                    {
                        PeriodLabel = period.Label,
                        PeriodStartDate = period.StartDate,
                        Amount = 100m + ((lineIndex + periodIndex) % 17)
                    })
                    .ToList()
            });
        }

        var transactions = new BatchObservableCollection<CostTransaction>();
        for (var transactionIndex = 0; transactionIndex < specification.TransactionCount; transactionIndex++)
        {
            var lineIndex = transactionIndex % specification.LineCount;
            var amount = 10m + (transactionIndex % 37);
            transactions.Add(new CostTransaction
            {
                RowNumber = transactionIndex + 1,
                FyPeriod = periods[transactionIndex % periods.Count].Label,
                TaskNumber = $"TASK-{lineIndex:D6}",
                Period = transactionIndex % periods.Count + 1,
                DocDate = periodStart.AddMonths(transactionIndex % specification.PeriodCount).AddDays(transactionIndex % 20),
                Units = 1m,
                UnitRate = amount,
                Amount = amount,
                CostLedger = "SYNTHETIC",
                CostAccount = $"ACCOUNT-{transactionIndex % 10:D2}",
                ProjectCode = $"PROJECT-{lineIndex % 25:D2}",
                ResourceCode = $"RESOURCE-{lineIndex % 100:D3}",
                ResourceDescription = $"Resource-{lineIndex % 100:D3}",
                Source = $"Synthetic-{transactionIndex:D7}",
                Who = $"Resource-{lineIndex % 100:D3}",
                EcmNumber = $"ECM-{transactionIndex:D7}",
                ManualName = $"Resource-{lineIndex % 100:D3}"
            });
        }

        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = $"LUNA-20 {specification.Name} synthetic workload",
                ReportTitle = "LUNA-20 performance baseline",
                CurrentPeriod = periods[specification.PeriodCount / 2].Label,
                SourceWorkbook = "synthetic"
            },
            ForecastPeriods = periods,
            ForecastLines = lines,
            Transactions = transactions,
            Schedule = CreateSchedule(specification)
        };
    }

    private static ScheduleData CreateSchedule(WorkloadSpecification specification)
    {
        var calendar = new ScheduleCalendar
        {
            Id = "LUNA20",
            Name = "LUNA-20 seven-day synthetic calendar",
            WorkingDays = [true, true, true, true, true, true, true]
        };
        var activities = new BatchObservableCollection<ScheduleActivity>();
        for (var index = 0; index < specification.ScheduleActivityCount; index++)
        {
            activities.Add(new ScheduleActivity
            {
                Id = $"ACT-{index:D6}",
                Name = $"Synthetic activity {index:D6}",
                DurationDays = 1 + index % 8,
                CalendarId = calendar.Id,
                PredecessorText = index == 0 ? string.Empty : $"ACT-{index - 1:D6} FS"
            });
        }

        return new ScheduleData
        {
            ProjectStart = new DateOnly(2024, 1, 1),
            DefaultCalendarId = calendar.Id,
            Calendars = new BatchObservableCollection<ScheduleCalendar>([calendar]),
            Activities = activities
        };
    }

    private static void EnsureDatasetShape(ProjectDataset dataset, WorkloadSpecification specification)
    {
        if (dataset.ForecastLines.Count != specification.LineCount
            || dataset.Transactions.Count != specification.TransactionCount
            || dataset.ForecastPeriods.Count != specification.PeriodCount)
        {
            throw new InvalidOperationException($"Loaded synthetic {specification.Name} dataset has an unexpected shape.");
        }
    }

    private static void EnsureViewModelShape(MainWindowViewModel viewModel, WorkloadSpecification specification)
    {
        if (viewModel.ForecastLines.Count != specification.LineCount
            || viewModel.Transactions.Count != specification.TransactionCount)
        {
            throw new InvalidOperationException($"Started synthetic {specification.Name} view model has an unexpected shape.");
        }
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static string ResolveOutputPath(string root, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(root, path);
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var index = 0; index + 1 < args.Length; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static int ParsePositiveOption(string[] args, string name, int fallback)
    {
        return int.TryParse(GetOption(args, name), out var value) && value > 0
            ? value
            : fallback;
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

    private static void TryDeleteTemporaryDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // The benchmark has already completed; a locked temp file must not
            // hide the measurements or turn the report into a false failure.
        }
        catch (UnauthorizedAccessException)
        {
            // See the IOException rationale above.
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

    private sealed record WorkloadSpecification(
        string Name,
        int LineCount,
        int TransactionCount,
        int PeriodCount,
        int ScheduleActivityCount);
}

internal sealed class Luna20PerformanceReport
{
    public int SchemaVersion { get; init; }
    public string Mode { get; init; } = string.Empty;
    public DateTimeOffset CapturedUtc { get; init; }
    public string RepositoryCommit { get; init; } = string.Empty;
    public Luna20RuntimeInfo Runtime { get; init; } = new();
    public Luna20WorkloadPolicy WorkloadPolicy { get; init; } = new();
    public IReadOnlyList<Luna20DatasetSummary> Datasets { get; init; } = [];
    public IReadOnlyList<Luna20ScenarioResult> Scenarios { get; init; } = [];
}

internal sealed class Luna20RuntimeInfo
{
    public string Framework { get; init; } = string.Empty;
    public string RuntimeIdentifier { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public string ProcessArchitecture { get; init; } = string.Empty;
    public bool Is64BitProcess { get; init; }
    public int ProcessorCount { get; init; }
    public string Configuration { get; init; } = string.Empty;
    public int Iterations { get; init; }
    public int MemoryCycles { get; init; }
}

internal sealed class Luna20WorkloadPolicy
{
    public string Small { get; init; } = string.Empty;
    public string Normal { get; init; } = string.Empty;
    public string Stress { get; init; } = string.Empty;
    public string Data { get; init; } = string.Empty;
}

internal sealed class Luna20DatasetSummary
{
    public string Name { get; init; } = string.Empty;
    public int ForecastLines { get; init; }
    public int Transactions { get; init; }
    public int ForecastPeriods { get; init; }
    public int MonthlyForecastCells { get; init; }
    public int ScheduleActivities { get; init; }
    public int ScheduleLinks { get; init; }
    public long ProjectJsonBytes { get; init; }
    public long ImportCsvBytes { get; init; }
}

internal sealed class Luna20ScenarioResult
{
    public string Name { get; init; } = string.Empty;
    public string Dataset { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<double> SamplesMilliseconds { get; init; } = [];
    public double MinimumMilliseconds { get; set; }
    public double MedianMilliseconds { get; set; }
    public double P95Milliseconds { get; set; }
    public double MaximumMilliseconds { get; set; }
    public IReadOnlyList<long>? MemoryBeforeBytes { get; set; }
    public IReadOnlyList<long>? MemoryAfterBytes { get; set; }
    public IReadOnlyList<long>? MemoryDeltaBytes { get; set; }
    public double? LastRefreshMilliseconds { get; set; }
    public IReadOnlyDictionary<string, int>? RefreshPhaseCounts { get; set; }
    public IReadOnlyDictionary<string, double>? RefreshPhaseMilliseconds { get; set; }
}
