using System.Globalization;
using System.IO;
using System.Text;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class DateTimeLocaleContractTests
{
    [Fact]
    public void New_zealand_daylight_saving_transitions_preserve_the_instant_and_business_date()
    {
        var beforeStart = new DateTimeOffset(2026, 9, 26, 13, 59, 0, TimeSpan.Zero);
        var afterStart = new DateTimeOffset(2026, 9, 26, 14, 0, 0, TimeSpan.Zero);
        var beforeEnd = new DateTimeOffset(2026, 4, 4, 13, 59, 0, TimeSpan.Zero);
        var afterEnd = new DateTimeOffset(2026, 4, 4, 14, 0, 0, TimeSpan.Zero);

        var nzBeforeStart = DateTimeContract.ToNewZealand(beforeStart);
        var nzAfterStart = DateTimeContract.ToNewZealand(afterStart);
        var nzBeforeEnd = DateTimeContract.ToNewZealand(beforeEnd);
        var nzAfterEnd = DateTimeContract.ToNewZealand(afterEnd);

        Assert.Equal(TimeSpan.FromHours(12), nzBeforeStart.Offset);
        Assert.Equal(TimeSpan.FromHours(13), nzAfterStart.Offset);
        Assert.Equal(TimeSpan.FromHours(13), nzBeforeEnd.Offset);
        Assert.Equal(TimeSpan.FromHours(12), nzAfterEnd.Offset);
        Assert.Equal(new DateOnly(2026, 9, 27), DateTimeContract.ToNewZealandDate(beforeStart));
        Assert.Equal(new DateOnly(2026, 9, 27), DateTimeContract.ToNewZealandDate(afterStart));
        Assert.Equal(beforeStart, nzBeforeStart.ToUniversalTime());
        Assert.Equal(afterStart, nzAfterStart.ToUniversalTime());
        Assert.Equal(beforeEnd, nzBeforeEnd.ToUniversalTime());
        Assert.Equal(afterEnd, nzAfterEnd.ToUniversalTime());

        var ambiguousLocal = new DateTime(2026, 4, 5, 2, 30, 0, DateTimeKind.Unspecified);
        Assert.True(DateTimeContract.NewZealandTimeZone.IsAmbiguousTime(ambiguousLocal));
        var normalizedAmbiguous = DateTimeContract.FromNewZealandLocal(ambiguousLocal);
        Assert.Equal(new DateTimeOffset(2026, 4, 4, 14, 30, 0, TimeSpan.Zero), normalizedAmbiguous);
        Assert.Equal(TimeSpan.FromHours(12), DateTimeContract.ToNewZealand(normalizedAmbiguous).Offset);
        Assert.Throws<ArgumentException>(() => DateTimeContract.FromNewZealandLocal(
            new DateTime(2026, 9, 27, 2, 30, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Clock_uses_nz_business_date_across_month_and_year_rollover()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 12, 31, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2027, 1, 1), clock.TodayInNewZealand);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0), clock.NewZealandNow.DateTime);
        Assert.Equal(TimeSpan.FromHours(13), clock.NewZealandNow.Offset);
    }

    [Fact]
    public void Legacy_offset_free_json_is_interpreted_as_nz_local_without_changing_fiscal_periods()
    {
        const string legacyJson = """
        {
          "header": { "projectTitle": "Legacy date fixture", "currentPeriod": "26-09" },
          "forecastPeriods": [
            { "label": " 26-09 ", "startDate": "2026-03-18" },
            { "label": "26-10", "startDate": null }
          ],
          "forecastLines": [
            {
              "rowNumber": 1,
              "taskNumber": "TASK-1",
              "resourceName": "Resource A",
              "projectCode": "PROJECT-1",
              "manualCommentRecordedAt": "2026-04-01T08:00:00",
              "monthlyCommentHistory": [
                { "periodLabel": "26-09", "recordedAt": "2026-04-01T08:15:00" }
              ],
              "monthlyForecasts": []
            }
          ],
          "costCenterNameMappings": [
            { "manualName": "Resource A", "lastUsedAt": "2026-04-01T08:30:00" }
          ],
          "unmatchedImportCombinations": [
            { "recordedAt": "2026-04-01T08:45:00" }
          ],
          "savedMonthSnapshots": [
            { "period": "26-09", "savedAt": "2026-04-01T09:00:00", "forecastLines": [] }
          ],
          "auditEvents": [
            { "auditId": "legacy-audit", "changedAt": "2026-04-01T09:15:00" }
          ],
          "schedule": { "baselines": [ { "name": "Legacy", "capturedAt": "2026-04-01T09:30:00" } ] }
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(legacyJson));
        var result = new ProjectDatasetMigrationPipeline().Load(stream);
        var dataset = result.Dataset;
        var expected = DateTimeContract.FromNewZealandLocal(new DateTime(2026, 4, 1, 8, 0, 0));

        Assert.Equal(ProjectDatasetMigrationPipeline.LegacyUnversionedVersion, result.SourceVersion);
        Assert.Equal(ProjectDatasetMigrationPipeline.CurrentVersion, dataset.FormatVersion);
        Assert.Equal("26-09", dataset.Header.CurrentPeriod);
        Assert.Equal(
            new DateOnly(2026, 3, 1),
            dataset.ForecastPeriods.Single(period => period.Label == "26-09").StartDate);
        Assert.Equal(expected, Assert.Single(dataset.ForecastLines).ManualCommentRecordedAt);
        Assert.Equal(expected.Offset, Assert.Single(dataset.ForecastLines).ManualCommentRecordedAt!.Value.Offset);
        Assert.Equal(expected, Assert.Single(dataset.SavedMonthSnapshots).SavedAt - TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.Zero, Assert.Single(dataset.AuditEvents).ChangedAt.Offset);
    }

    [Fact]
    public void Non_nz_machine_culture_does_not_change_invariant_storage_or_nz_display()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var instant = new DateTimeOffset(2026, 12, 31, 11, 0, 0, TimeSpan.Zero);

            Assert.Equal("2027-01-01T00:00:00", DateTimeContract.FormatNewZealand(instant, "yyyy-MM-dd'T'HH:mm:ss"));
            Assert.Equal("2026-12-31T11:00:00.0000000Z", DateTimeContract.FormatUtc(instant));
            Assert.Equal("01/01/2027 00:00", DateTimeContract.FormatNewZealand(instant, "dd/MM/yyyy HH:mm"));
            Assert.Equal("01 Jan 2027", DateTimeContract.FormatBusinessDate(new DateOnly(2027, 1, 1), "dd MMM yyyy"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void Project_save_and_reopen_normalizes_all_durable_instants_to_utc_without_shifting_periods()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "project.json");
        var eventInstant = new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.FromHours(12));
        var snapshotInstant = new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.FromHours(13));
        var dataset = CreateRoundTripDataset(eventInstant, snapshotInstant);
        var service = new ProjectFileService(clock: new FixedClock(eventInstant));

        service.Save(path, dataset);
        var persisted = File.ReadAllText(path);
        var reopened = service.Load(path);

        Assert.Contains(DateTimeContract.FormatUtc(eventInstant), persisted, StringComparison.Ordinal);
        Assert.Contains(DateTimeContract.FormatUtc(snapshotInstant), persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("+12:00", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("+13:00", persisted, StringComparison.Ordinal);
        Assert.Equal("26-09", reopened.Header.CurrentPeriod);
        Assert.Equal(new DateOnly(2026, 3, 1), Assert.Single(reopened.ForecastPeriods).StartDate);
        Assert.Equal(DateTimeContract.NormalizeUtc(eventInstant), Assert.Single(reopened.AuditEvents).ChangedAt);
        Assert.Equal(DateTimeContract.NormalizeUtc(snapshotInstant), Assert.Single(reopened.SavedMonthSnapshots).SavedAt);
        Assert.Equal(TimeSpan.Zero, Assert.Single(reopened.AuditEvents).ChangedAt.Offset);
        Assert.Equal(TimeSpan.Zero, Assert.Single(reopened.SavedMonthSnapshots).SavedAt.Offset);
        Assert.Equal(DateTimeContract.NormalizeUtc(eventInstant), Assert.Single(reopened.CostCenterNameMappings).LastUsedAt);
        Assert.Equal(DateTimeContract.NormalizeUtc(snapshotInstant), Assert.Single(reopened.UnmatchedImportCombinations).RecordedAt);
        Assert.Equal(DateTimeContract.NormalizeUtc(eventInstant), Assert.Single(reopened.Schedule.Baselines).CapturedAt);
        Assert.Equal(DateTimeContract.NormalizeUtc(snapshotInstant), Assert.Single(reopened.ForecastLines).ManualCommentRecordedAt);
        Assert.Equal(DateTimeContract.NormalizeUtc(eventInstant), Assert.Single(Assert.Single(reopened.ForecastLines).MonthlyCommentHistory).RecordedAt);
    }

    [Fact]
    public void Preferences_legacy_and_current_round_trips_keep_curve_instants_utc()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "user-preferences.json");
        var diagnostics = new DiagnosticsService(Path.Combine(directory.Root, "diagnostics"));
        var legacyCreated = DateTimeContract.FromNewZealandLocal(new DateTime(2026, 4, 1, 8, 0, 0));
        File.WriteAllText(path, """
        {
          "forecastCurvePresets": [
            { "name": "Legacy curve", "createdUtc": "2026-04-01T08:00:00", "updatedUtc": "2026-04-01T09:00:00", "weights": [1] }
          ]
        }
        """);

        var service = new UserPreferencesService(path, diagnostics, clock: new FixedClock(legacyCreated));
        var legacy = service.Load();
        var preset = Assert.Single(legacy.ForecastCurvePresets);
        Assert.Equal(legacyCreated, preset.CreatedUtc);
        Assert.Equal(TimeSpan.Zero, preset.CreatedUtc.Offset);

        service.Save(legacy);
        var reopened = service.Load();
        Assert.Equal(legacyCreated, Assert.Single(reopened.ForecastCurvePresets).CreatedUtc);
        Assert.Contains(DateTimeContract.FormatUtc(legacyCreated), File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void New_month_uses_the_injected_clock_and_models_have_no_ambient_timestamp_defaults()
    {
        var instant = new DateTimeOffset(2026, 8, 29, 10, 11, 12, TimeSpan.Zero);
        var operation = new NewMonthOperation(
            new CalculationService(),
            new ProjectDatasetCloner(),
            new FixedClock(instant));

        var preparation = operation.Prepare(CreateRoundTripDataset(instant, instant));

        Assert.True(preparation.IsReady);
        Assert.Equal(
            instant,
            preparation.StagedDataset!.SavedMonthSnapshots.Single(snapshot => snapshot.Period == "26-09").SavedAt);
        Assert.All(preparation.StagedDataset.AuditEvents.Take(2), audit => Assert.Equal(instant, audit.ChangedAt));
        Assert.Equal(DateTimeOffset.UnixEpoch, new AuditEvent().ChangedAt);
        Assert.Equal(DateTimeOffset.UnixEpoch, new SavedMonthSnapshot().SavedAt);
        Assert.Equal(DateTimeOffset.UnixEpoch, new ScheduleBaseline().CapturedAt);
        Assert.Equal(DateTimeOffset.UnixEpoch, new CostCenterNameMapping().LastUsedAt);
        Assert.Equal(DateTimeOffset.UnixEpoch, new UnmatchedImportCombination().RecordedAt);
        Assert.Equal(DateTimeOffset.UnixEpoch, new ForecastMonthlyComment().RecordedAt);
    }

    private static ProjectDataset CreateRoundTripDataset(
        DateTimeOffset eventInstant,
        DateTimeOffset snapshotInstant)
    {
        return new ProjectDataset
        {
            Header = new ProjectHeader
            {
                ProjectTitle = "Date/time fixture",
                CurrentPeriod = "26-09"
            },
            ForecastPeriods =
            [
                new ForecastPeriod { Label = "26-09", StartDate = new DateOnly(2026, 3, 1) }
            ],
            ForecastLines =
            [
                new ForecastLine
                {
                    RowNumber = 1,
                    TaskNumber = "TASK-1",
                    ResourceName = "Resource A",
                    ProjectCode = "PROJECT-1",
                    TransactionProjectCode = "PROJECT-1",
                    ManualCommentRecordedAt = snapshotInstant,
                    MonthlyCommentHistory =
                    [
                        new ForecastMonthlyComment
                        {
                            PeriodLabel = "26-09",
                            RecordedAt = eventInstant,
                            Text = "Comment"
                        }
                    ],
                    MonthlyForecasts =
                    [
                        new MonthlyForecast { PeriodLabel = "26-09", Amount = 25m }
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
            ],
            CostCenterNameMappings =
            [
                new CostCenterNameMapping { ManualName = "Resource A", LastUsedAt = eventInstant }
            ],
            UnmatchedImportCombinations =
            [
                new UnmatchedImportCombination { RecordedAt = snapshotInstant }
            ],
            SavedMonthSnapshots =
            [
                new SavedMonthSnapshot { Period = "26-08", SavedAt = snapshotInstant }
            ],
            AuditEvents =
            [
                new AuditEvent { AuditId = "date-time-audit", ChangedAt = eventInstant }
            ],
            Schedule = new ScheduleData
            {
                Baselines =
                [
                    new ScheduleBaseline { Name = "Date/time baseline", CapturedAt = eventInstant }
                ]
            }
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
