using System.Text.RegularExpressions;
using ProjectCostForecast.App;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna26C1DeferredWorkTests
{
    private static readonly string[] C1ProductionFiles =
    [
        "MainWindow.Lifecycle.cs",
        "MainWindow.ForecastGridInteraction.cs",
        "MainWindow.GridBuilders.cs",
        "MainWindow.ManagementResources.cs",
        "MainWindow.ScheduleCommands.cs",
        "MainWindow.SpreadsheetGridInteraction.cs"
    ];

    private static readonly Regex DispatcherSchedulePattern = new(
        @"\b(?:[A-Za-z_]\w*\s*\.\s*)*(?:Dispatcher|CurrentDispatcher)\s*\.\s*(?:BeginInvoke|InvokeAsync)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void Inactive_lifetime_rejects_without_enqueuing()
    {
        var scheduler = new ListBackedScheduler { IsActive = false };
        var actionRuns = 0;

        var accepted = scheduler.TryQueue(() => actionRuns++);

        Assert.False(accepted);
        Assert.Equal(0, scheduler.QueueAttempts);
        Assert.Empty(scheduler.QueuedActions);
        Assert.Equal(0, actionRuns);
    }

    [Fact]
    public void Underlying_queue_rejection_returns_false_without_enqueuing()
    {
        var scheduler = new ListBackedScheduler { AcceptQueue = false };
        var actionRuns = 0;

        var accepted = scheduler.TryQueue(() => actionRuns++);

        Assert.False(accepted);
        Assert.Equal(1, scheduler.QueueAttempts);
        Assert.Empty(scheduler.QueuedActions);
        Assert.Equal(0, actionRuns);
    }

    [Theory]
    [InlineData("unload")]
    [InlineData("close")]
    public void Unload_or_close_invalidates_queued_work(string transition)
    {
        var scheduler = new ListBackedScheduler();
        var actionRuns = 0;

        Assert.True(scheduler.TryQueue(() => actionRuns++));

        scheduler.IsActive = false;
        scheduler.LifetimeVersion++;
        scheduler.RunNext();

        Assert.Contains(transition, new[] { "unload", "close" });
        Assert.Equal(0, actionRuns);
    }

    [Fact]
    public void Data_context_generation_replacement_invalidates_work_while_active()
    {
        var scheduler = new ListBackedScheduler();
        var actionRuns = 0;

        Assert.True(scheduler.TryQueue(() => actionRuns++));

        scheduler.LifetimeVersion++;
        scheduler.RunNext();

        Assert.True(scheduler.IsActive);
        Assert.Equal(0, actionRuns);
    }

    [Fact]
    public void Nested_work_is_invalidated_when_generation_changes_before_inner_execution()
    {
        var scheduler = new ListBackedScheduler();
        var outerRuns = 0;
        var innerRuns = 0;

        Assert.True(scheduler.TryQueue(() =>
        {
            outerRuns++;
            Assert.True(scheduler.TryQueue(() => innerRuns++));
        }));

        scheduler.RunNext();
        Assert.Equal(1, outerRuns);
        Assert.Single(scheduler.QueuedActions);

        scheduler.LifetimeVersion++;
        scheduler.RunNext();

        Assert.Equal(0, innerRuns);
    }

    [Fact]
    public void Lifetime_guard_validates_all_collaborators()
    {
        Assert.Throws<ArgumentNullException>(() => MainWindowWorkLifetime.TryQueue(
            null!,
            () => 1,
            _ => true,
            () => { }));
        Assert.Throws<ArgumentNullException>(() => MainWindowWorkLifetime.TryQueue(
            () => true,
            null!,
            _ => true,
            () => { }));
        Assert.Throws<ArgumentNullException>(() => MainWindowWorkLifetime.TryQueue(
            () => true,
            () => 1,
            null!,
            () => { }));
        Assert.Throws<ArgumentNullException>(() => MainWindowWorkLifetime.TryQueue(
            () => true,
            () => 1,
            _ => true,
            null!));
    }

    [Fact]
    public void Data_context_change_invalidates_before_rewire_and_refresh()
    {
        var lifecycle = StripCommentsAndLiterals(ReadProductionSource("MainWindow.Lifecycle.cs"));
        var body = GetPrivateMethodBody(lifecycle, "MainWindow_DataContextChanged").Text;

        AssertAppearsBefore(body, "_mainWindowLifetimeVersion++;", "CancelPendingWindowWork();");
        AssertAppearsBefore(body, "CancelPendingWindowWork();", "WireViewModelSubscriptions();");
        AssertAppearsBefore(body, "CancelPendingWindowWork();", "WireGanttSubscriptions();");
        AssertAppearsBefore(body, "CancelPendingWindowWork();", "RefreshMainWindowVisuals();");
    }

    [Fact]
    public void Queue_main_window_work_owns_the_lifetime_delegate_and_shutdown_race()
    {
        var lifecycle = StripCommentsAndLiterals(ReadProductionSource("MainWindow.Lifecycle.cs"));
        var body = GetPrivateMethodBody(lifecycle, "QueueMainWindowWork").Text;

        Assert.Contains("MainWindowWorkLifetime.TryQueue", body, StringComparison.Ordinal);
        Assert.Contains("isActive: () => IsMainWindowWorkActive && !Dispatcher.HasShutdownStarted", body, StringComparison.Ordinal);
        Assert.Contains("getLifetimeVersion: () => _mainWindowLifetimeVersion", body, StringComparison.Ordinal);
        Assert.Contains("queue: guardedAction =>", body, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.BeginInvoke(priority, guardedAction)", body, StringComparison.Ordinal);
        Assert.Contains("catch (InvalidOperationException) when (Dispatcher.HasShutdownStarted)", body, StringComparison.Ordinal);
        Assert.Contains("return false;", body, StringComparison.Ordinal);
        Assert.Contains("action: action", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Converted_paths_preserve_priorities_and_clear_state_when_queueing_is_rejected()
    {
        var forecast = StripCommentsAndLiterals(ReadProductionSource("MainWindow.ForecastGridInteraction.cs"));
        Assert.Contains("QueueMainWindowWork(DispatcherPriority.Render", forecast, StringComparison.Ordinal);
        Assert.Contains("QueueMainWindowWork(DispatcherPriority.Normal", forecast, StringComparison.Ordinal);

        var builders = StripCommentsAndLiterals(ReadProductionSource("MainWindow.GridBuilders.cs"));
        Assert.Equal(4, CountOccurrences(builders, "QueueMainWindowWork("));
        Assert.Equal(3, CountOccurrences(builders, "DispatcherPriority.Background"));
        var yearBandQueue = GetPrivateMethodBody(builders, "QueueRebuildForecastYearBands").Text;
        Assert.Contains("if (!QueueMainWindowWork", yearBandQueue, StringComparison.Ordinal);
        Assert.Contains("_forecastYearBandRebuildQueued = false;", yearBandQueue, StringComparison.Ordinal);

        var management = StripCommentsAndLiterals(ReadProductionSource("MainWindow.ManagementResources.cs"));
        var edit = GetPrivateMethodBody(management, "BeginManagementResourceCellEdit").Text;
        Assert.Contains("QueueMainWindowWork(DispatcherPriority.Normal", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherPriority.Input", edit, StringComparison.Ordinal);
        Assert.Contains("QueueMainWindowWork(DispatcherPriority.Render", management, StringComparison.Ordinal);
        var managementSync = GetPrivateMethodBody(management, "QueueSynchronizeManagementResourceGrids").Text;
        Assert.Contains("if (!QueueMainWindowWork", managementSync, StringComparison.Ordinal);
        Assert.Contains("_managementResourceGridSyncQueued = false;", managementSync, StringComparison.Ordinal);

        var schedule = StripCommentsAndLiterals(ReadProductionSource("MainWindow.ScheduleCommands.cs"));
        Assert.Equal(3, CountOccurrences(schedule, "QueueMainWindowWork("));
        Assert.Contains("DispatcherPriority.ContextIdle", schedule, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Loaded", schedule, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Input", schedule, StringComparison.Ordinal);

        var spreadsheet = StripCommentsAndLiterals(ReadProductionSource("MainWindow.SpreadsheetGridInteraction.cs"));
        Assert.Contains("DispatcherPriority.Normal", spreadsheet, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Background", spreadsheet, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Input", spreadsheet, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.ApplicationIdle", spreadsheet, StringComparison.Ordinal);
        var selectionUpdate = GetPrivateMethodBody(spreadsheet, "QueueSpreadsheetSelectionUpdate").Text;
        Assert.Contains("if (!QueueMainWindowWork", selectionUpdate, StringComparison.Ordinal);
        Assert.Contains("_spreadsheetSelectionUpdateQueued.Remove(grid);", selectionUpdate, StringComparison.Ordinal);
        Assert.Contains("_spreadsheetSelectionVisualPendingItems.Remove(grid);", selectionUpdate, StringComparison.Ordinal);
        Assert.Contains("_spreadsheetSelectionVisualFullRefresh.Remove(grid);", selectionUpdate, StringComparison.Ordinal);
        var cellEditEnding = GetPrivateMethodBody(spreadsheet, "SpreadsheetGrid_CellEditEnding").Text;
        Assert.Contains("if (!QueueMainWindowWork", cellEditEnding, StringComparison.Ordinal);
        Assert.True(
            CountOccurrences(cellEditEnding, "_spreadsheetEditSnapshots.Remove(grid);") >= 2,
            "The edit snapshot must be cleared after execution and when scheduling is rejected.");
    }

    [Fact]
    public void Selected_files_have_one_raw_dispatcher_schedule_inside_the_lifecycle_owner()
    {
        var sites = C1ProductionFiles
            .SelectMany(fileName => FindDispatcherSchedules(
                    StripCommentsAndLiterals(ReadProductionSource(fileName)))
                .Select(match => new DispatcherScheduleSite(fileName, match.Index)))
            .ToArray();

        var site = Assert.Single(sites);
        Assert.Equal("MainWindow.Lifecycle.cs", site.FileName);

        var lifecycle = StripCommentsAndLiterals(ReadProductionSource(site.FileName));
        var owner = GetPrivateMethodBody(lifecycle, "QueueMainWindowWork");
        Assert.InRange(site.Index, owner.OpenBrace, owner.CloseBrace);
    }

    [Fact]
    public void Dispatcher_scanner_strips_comments_and_literals_and_detects_qualified_variants()
    {
        var source = string.Join(
            Environment.NewLine,
            [
                "// Dispatcher.BeginInvoke(() => { });",
                "/* window.Dispatcher.InvokeAsync(() => { }); */",
                "var ordinary = \"Dispatcher.BeginInvoke\";",
                "var verbatim = @\"System.Windows.Threading.Dispatcher.InvokeAsync\";",
                "var interpolated = $\"owner.Dispatcher.BeginInvoke: {42}\";",
                "var raw = \"\"\"Dispatcher.CurrentDispatcher.BeginInvoke\"\"\";",
                "this.Dispatcher.BeginInvoke(() => { });",
                "System.Windows.Threading.Dispatcher.InvokeAsync(() => { });",
                "Dispatcher.CurrentDispatcher.BeginInvoke(() => { });",
                "owner.Dispatcher.InvokeAsync(() => { });"
            ]);

        var schedules = FindDispatcherSchedules(StripCommentsAndLiterals(source));

        Assert.Equal(4, schedules.Count);
    }

    private static string ReadProductionSource(string fileName) => File.ReadAllText(Path.Combine(
        Luna11TestSupport.RepositoryRoot,
        "src",
        "ProjectCostForecast.App",
        fileName));

    private static IReadOnlyList<Match> FindDispatcherSchedules(string source) =>
        DispatcherSchedulePattern.Matches(source).Cast<Match>().ToArray();

    private static MethodBody GetPrivateMethodBody(string source, string methodName)
    {
        var declaration = Regex.Match(
            source,
            $@"(?m)^\s*private\s+(?:static\s+)?[^\r\n(]+\b{Regex.Escape(methodName)}\s*\(",
            RegexOptions.CultureInvariant);
        Assert.True(declaration.Success, $"Could not find private method {methodName}.");

        var openBrace = source.IndexOf('{', declaration.Index + declaration.Length);
        Assert.True(openBrace >= 0, $"Method {methodName} has no body.");
        var closeBrace = FindMatchingBrace(source, openBrace);
        return new MethodBody(
            openBrace,
            closeBrace,
            source.Substring(openBrace, closeBrace - openBrace + 1));
    }

    private static void AssertAppearsBefore(string source, string earlier, string later)
    {
        var earlierIndex = source.IndexOf(earlier, StringComparison.Ordinal);
        var laterIndex = source.IndexOf(later, StringComparison.Ordinal);
        Assert.True(earlierIndex >= 0, $"Could not find '{earlier}'.");
        Assert.True(laterIndex >= 0, $"Could not find '{later}'.");
        Assert.True(earlierIndex < laterIndex, $"Expected '{earlier}' before '{later}'.");
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        for (var index = 0; ; )
        {
            index = source.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += value.Length;
        }
    }

    private static string StripCommentsAndLiterals(string source)
    {
        var result = source.ToCharArray();
        for (var index = 0; index < source.Length; )
        {
            if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                var end = index + 2;
                while (end < source.Length && source[end] is not ('\r' or '\n'))
                {
                    end++;
                }

                MaskNonNewlines(result, index, end);
                index = end;
                continue;
            }

            if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                var closing = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                var end = closing < 0 ? source.Length : closing + 2;
                MaskNonNewlines(result, index, end);
                index = end;
                continue;
            }

            if (source[index] is '"' or '\'')
            {
                var end = FindQuotedLiteralEnd(source, index);
                MaskNonNewlines(result, index, end);
                index = end;
                continue;
            }

            index++;
        }

        return new string(result);
    }

    private static int FindQuotedLiteralEnd(string source, int start)
    {
        var quote = source[start];
        var openingQuoteCount = quote == '"' ? CountRun(source, start, '"') : 1;
        if (openingQuoteCount >= 3)
        {
            for (var index = start + openingQuoteCount; index < source.Length; )
            {
                if (source[index] == '"')
                {
                    var closingQuoteCount = CountRun(source, index, '"');
                    if (closingQuoteCount >= openingQuoteCount)
                    {
                        return index + openingQuoteCount;
                    }

                    index += closingQuoteCount;
                    continue;
                }

                index++;
            }

            return source.Length;
        }

        var verbatim = quote == '"' && IsVerbatimStringPrefix(source, start);
        for (var index = start + 1; index < source.Length; index++)
        {
            if (!verbatim && source[index] == '\\' && index + 1 < source.Length)
            {
                index++;
                continue;
            }

            if (source[index] != quote)
            {
                continue;
            }

            if (verbatim && index + 1 < source.Length && source[index + 1] == '"')
            {
                index++;
                continue;
            }

            return index + 1;
        }

        return source.Length;
    }

    private static bool IsVerbatimStringPrefix(string source, int quoteIndex)
    {
        if (quoteIndex > 0 && source[quoteIndex - 1] == '@')
        {
            return true;
        }

        return quoteIndex > 1
            && source[quoteIndex - 2] == '@'
            && source[quoteIndex - 1] == '$';
    }

    private static int CountRun(string source, int start, char value)
    {
        var index = start;
        while (index < source.Length && source[index] == value)
        {
            index++;
        }

        return index - start;
    }

    private static void MaskNonNewlines(char[] result, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (result[index] is not ('\r' or '\n'))
            {
                result[index] = ' ';
            }
        }
    }

    private static int FindMatchingBrace(string source, int openBrace)
    {
        var depth = 0;
        for (var index = openBrace; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return index;
                    }

                    break;
            }
        }

        throw new InvalidOperationException("The method body is not balanced.");
    }

    private sealed class ListBackedScheduler
    {
        public bool IsActive { get; set; } = true;

        public int LifetimeVersion { get; set; } = 1;

        public bool AcceptQueue { get; set; } = true;

        public int QueueAttempts { get; private set; }

        public List<Action> QueuedActions { get; } = [];

        public bool TryQueue(Action action) => MainWindowWorkLifetime.TryQueue(
            isActive: () => IsActive,
            getLifetimeVersion: () => LifetimeVersion,
            queue: queuedAction =>
            {
                QueueAttempts++;
                if (!AcceptQueue)
                {
                    return false;
                }

                QueuedActions.Add(queuedAction);
                return true;
            },
            action: action);

        public void RunNext()
        {
            Assert.NotEmpty(QueuedActions);
            var action = QueuedActions[0];
            QueuedActions.RemoveAt(0);
            action();
        }
    }

    private readonly record struct MethodBody(int OpenBrace, int CloseBrace, string Text);

    private readonly record struct DispatcherScheduleSite(string FileName, int Index);
}
