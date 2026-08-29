using System.Text.RegularExpressions;
using ProjectCostForecast.App;
using Xunit;

namespace ProjectCostForecast.UnitTests;

public sealed class Luna26C2DeferredWorkTests
{
    private static readonly Regex DispatcherSchedulePattern = new(
        @"\b(?:[A-Za-z_]\w*\s*\.\s*)*(?:Dispatcher|CurrentDispatcher)\s*\.\s*(?:BeginInvoke|InvokeAsync)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void Every_main_window_partial_has_exactly_one_raw_schedule_owned_by_the_lifetime_helper()
    {
        var files = Directory
            .EnumerateFiles(AppSourceRoot, "MainWindow*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(files);

        var sites = files
            .SelectMany(path => FindDispatcherSchedules(StripCommentsAndLiterals(File.ReadAllText(path)))
                .Select(match => new DispatcherScheduleSite(
                    Path.GetRelativePath(AppSourceRoot, path),
                    match.Index)))
            .ToArray();

        var site = Assert.Single(sites);
        Assert.Equal("MainWindow.Lifecycle.cs", site.FileName);

        var lifecycle = StripCommentsAndLiterals(ReadProductionSource(site.FileName));
        var owner = GetPrivateMethodBody(lifecycle, "QueueMainWindowWork");
        Assert.InRange(site.Index, owner.OpenBrace, owner.CloseBrace);
    }

    [Fact]
    public void C2_paths_route_each_remaining_schedule_at_its_original_priority()
    {
        var expected = new[]
        {
            ("MainWindow.ColumnMenus.cs", "AddColumnOptionsMenuItems", "DispatcherPriority.Input"),
            ("MainWindow.ColumnMenus.cs", "AddColumnSpecificMenuItems", "DispatcherPriority.Input"),
            ("MainWindow.ColumnMenus.cs", "ExecuteAfterClosingMenu", "DispatcherPriority.Input"),
            ("MainWindow.GridFilters.cs", "BuildColumnFilterMenu", "DispatcherPriority.Input"),
            ("MainWindow.WindowChrome.cs", "KpiScrollViewer_PreviewMouseRightButtonUp", "DispatcherPriority.Normal"),
            ("MainWindow.WindowChrome.cs", "LedgerChartScrollViewer_PreviewMouseRightButtonUp", "DispatcherPriority.Normal"),
            ("MainWindow.WindowChrome.cs", "LedgerChartScrollViewer_PreviewMouseWheel", "DispatcherPriority.Loaded"),
            ("MainWindow.WorkspaceColumnState.cs", "SubscribedViewModel_PropertyChanged", "DispatcherPriority.Render"),
            ("MainWindow.WorkspacePanels.cs", "WorkspaceViewName_LostFocus", "DispatcherPriority.Input"),
            ("MainWindow.WorkspacePanels.cs", "WorkspaceViewName_KeyDown", "DispatcherPriority.Input"),
            ("MainWindow.WorkspacePanels.cs", "WorkspaceViewName_IsVisibleChanged", "DispatcherPriority.Input"),
            ("MainWindow.WorkspacePanels.cs", "QueueFocusPendingWorkspaceViewEditor", "DispatcherPriority.Input"),
            ("MainWindow.WorkspacePanels.cs", "DetailWorkspaceRail_MouseLeave", "DispatcherPriority.Background")
        };

        Assert.Equal(13, expected.Length);
        foreach (var (fileName, methodName, priority) in expected)
        {
            var source = StripCommentsAndLiterals(ReadProductionSource(fileName));
            var body = GetPrivateMethodBody(source, methodName).Text;

            Assert.True(
                CountOccurrences(body, "QueueMainWindowWork(") == 1,
                $"Expected exactly one lifetime-owned schedule in {fileName}:{methodName}.");
            Assert.Contains($"QueueMainWindowWork({priority}", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Menu_reopen_and_filter_focus_are_instance_owned_and_preserve_order()
    {
        var columnMenus = StripCommentsAndLiterals(ReadProductionSource("MainWindow.ColumnMenus.cs"));
        var executeAfterClosing = GetPrivateMethodBody(columnMenus, "ExecuteAfterClosingMenu");

        Assert.Contains("private void ExecuteAfterClosingMenu", columnMenus, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void ExecuteAfterClosingMenu", columnMenus, StringComparison.Ordinal);
        Assert.Contains("private static void CloseContainingMenu", columnMenus, StringComparison.Ordinal);
        Assert.DoesNotContain("item.Dispatcher", executeAfterClosing.Text, StringComparison.Ordinal);
        AssertAppearsBefore(executeAfterClosing.Text, "CloseContainingMenu(item);", "QueueMainWindowWork(");
        Assert.Contains("QueueMainWindowWork(DispatcherPriority.Input, action)", executeAfterClosing.Text, StringComparison.Ordinal);

        var gridFilters = StripCommentsAndLiterals(ReadProductionSource("MainWindow.GridFilters.cs"));
        var filterMenu = GetPrivateMethodBody(gridFilters, "BuildColumnFilterMenu").Text;
        Assert.Contains("QueueMainWindowWork(DispatcherPriority.Input", filterMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher.BeginInvoke", filterMenu, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejected_or_cancelled_shell_and_editor_work_clears_transient_state()
    {
        var lifecycle = StripCommentsAndLiterals(ReadProductionSource("MainWindow.Lifecycle.cs"));
        var cancel = GetPrivateMethodBody(lifecycle, "CancelPendingWindowWork").Text;
        Assert.Contains("_kpiRightDragging = false;", cancel, StringComparison.Ordinal);
        Assert.Contains("_ledgerChartRightDragging = false;", cancel, StringComparison.Ordinal);
        Assert.Contains("_ledgerChartZooming = false;", cancel, StringComparison.Ordinal);
        Assert.Contains("_pendingWorkspaceEditorFocusView = null;", cancel, StringComparison.Ordinal);

        var windowChrome = StripCommentsAndLiterals(ReadProductionSource("MainWindow.WindowChrome.cs"));
        var kpiUp = GetPrivateMethodBody(windowChrome, "KpiScrollViewer_PreviewMouseRightButtonUp").Text;
        var ledgerUp = GetPrivateMethodBody(windowChrome, "LedgerChartScrollViewer_PreviewMouseRightButtonUp").Text;
        var zoom = GetPrivateMethodBody(windowChrome, "LedgerChartScrollViewer_PreviewMouseWheel").Text;

        AssertRejectedQueueClears(kpiUp, "_kpiRightDragging = false;");
        AssertRejectedQueueClears(ledgerUp, "_ledgerChartRightDragging = false;");
        AssertRejectedQueueClears(zoom, "_ledgerChartZooming = false;");

        var workspacePanels = StripCommentsAndLiterals(ReadProductionSource("MainWindow.WorkspacePanels.cs"));
        var visibleChanged = GetPrivateMethodBody(workspacePanels, "WorkspaceViewName_IsVisibleChanged").Text;
        var pendingFocus = GetPrivateMethodBody(workspacePanels, "QueueFocusPendingWorkspaceViewEditor").Text;

        AssertRejectedQueueClears(visibleChanged, "_pendingWorkspaceEditorFocusView = null;");
        AssertRejectedQueueClears(pendingFocus, "_pendingWorkspaceEditorFocusView = null;");
    }

    [Fact]
    public void C2_reuses_the_pure_c1_lifetime_guard_for_cancellation_and_rejection()
    {
        var isActive = true;
        var lifetimeVersion = 1;
        var acceptQueue = true;
        var queued = new List<Action>();
        var actionRuns = 0;

        bool TryQueue(Action action) => MainWindowWorkLifetime.TryQueue(
            isActive: () => isActive,
            getLifetimeVersion: () => lifetimeVersion,
            queue: guardedAction =>
            {
                if (!acceptQueue)
                {
                    return false;
                }

                queued.Add(guardedAction);
                return true;
            },
            action: action);

        Assert.True(TryQueue(() => actionRuns++));
        lifetimeVersion++;
        queued.Single()();
        Assert.Equal(0, actionRuns);

        acceptQueue = false;
        Assert.False(TryQueue(() => actionRuns++));
        Assert.Equal(0, actionRuns);

        isActive = false;
        Assert.False(TryQueue(() => actionRuns++));
    }

    [Fact]
    public void Scanner_is_comment_literal_aware_and_covers_qualified_schedule_variants()
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

    private static string AppSourceRoot => Path.Combine(
        Luna11TestSupport.RepositoryRoot,
        "src",
        "ProjectCostForecast.App");

    private static string ReadProductionSource(string fileName) => File.ReadAllText(Path.Combine(AppSourceRoot, fileName));

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
        return new MethodBody(openBrace, closeBrace, source.Substring(openBrace, closeBrace - openBrace + 1));
    }

    private static void AssertRejectedQueueClears(string methodBody, string cleanup)
    {
        Assert.Contains("if (!QueueMainWindowWork", methodBody, StringComparison.Ordinal);
        Assert.True(
            CountOccurrences(methodBody, cleanup.TrimEnd(';')) >= 2,
            $"Expected execution and immediate rejection cleanup for {cleanup}.");
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
        var prefixStart = quoteIndex;
        while (prefixStart > 0 && source[prefixStart - 1] is '@' or '$')
        {
            prefixStart--;
        }

        return source[prefixStart..quoteIndex].Contains('@');
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

    private readonly record struct MethodBody(int OpenBrace, int CloseBrace, string Text);

    private readonly record struct DispatcherScheduleSite(string FileName, int Index);
}
