using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

public sealed class CostCenterMappingWindow : Window
{
    private const double CandidateRowHeight = 34;
    private const int InitialVisibleCandidateRows = 6;
    private const int ExpandedVisibleCandidateRows = 16;

    private static Point? _rememberedLocation;

    private readonly IReadOnlyList<CostTransaction> _matchingTransactions;
    private readonly HashSet<string> _existingNames;
    private readonly ObservableCollection<CandidateRow> _visibleCandidateRows = [];
    private readonly List<CandidateRow> _allCandidateRows = [];
    private readonly TextBox _nameTextBox;
    private readonly DataGrid _candidateGrid;
    private readonly ContextMenu _existingNamesMenu;
    private readonly TextBlock _selectionInfoTextBlock;
    private readonly TextBlock _candidateCountTextBlock;

    private bool _suppressNameTextChanged;
    private CandidateRow? _selectedCandidateRow;
    private ScrollViewer? _activeScrollViewer;
    private Point? _rightDragStart;
    private double _dragHorizontalStartOffset;
    private double _dragVerticalStartOffset;
    private bool _rightDragging;

    public CostCenterMappingWindow(
        CostTransaction transaction,
        IEnumerable<CostTransaction>? matchingTransactions,
        IEnumerable<CostCenterNameOption> candidates,
        CostCenterNameOption? suggestedOption,
        IEnumerable<string>? existingNames = null,
        int remainingGroupCount = 1)
    {
        Title = "Name Imported Cost Centre";
        Width = Math.Max(720, SystemParameters.WorkArea.Width * 0.52);
        Height = Math.Max(760, SystemParameters.WorkArea.Height * 0.88);
        MinWidth = 640;
        MinHeight = 700;
        WindowStartupLocation = _rememberedLocation.HasValue ? WindowStartupLocation.Manual : WindowStartupLocation.CenterScreen;
        if (_rememberedLocation.HasValue)
        {
            Left = _rememberedLocation.Value.X;
            Top = _rememberedLocation.Value.Y;
        }

        _matchingTransactions = (matchingTransactions ?? [transaction]).ToList();
        _existingNames = new HashSet<string>(
            (existingNames ?? [])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(NormaliseName),
            StringComparer.OrdinalIgnoreCase);

        _nameTextBox = new TextBox
        {
            MinHeight = 32,
            MinWidth = 320
        };
        _nameTextBox.TextChanged += NameTextBox_TextChanged;
        _nameTextBox.PreviewKeyDown += NameTextBox_PreviewKeyDown;

        _selectionInfoTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = BrushFrom("#64748B"),
            TextWrapping = TextWrapping.Wrap
        };

        _candidateCountTextBlock = new TextBlock
        {
            Foreground = BrushFrom("#64748B"),
            VerticalAlignment = VerticalAlignment.Center
        };

        _candidateGrid = BuildCandidateGrid();
        _existingNamesMenu = BuildExistingNamesMenu();

        var root = new DockPanel
        {
            Margin = new Thickness(14)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 100
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        var useButton = new Button
        {
            Content = _matchingTransactions.Count > 1
                ? $"Use Name for All {_matchingTransactions.Count} Matching Costs"
                : "Use Name",
            MinWidth = 180,
            Margin = new Thickness(10, 0, 0, 0)
        };
        useButton.Click += (_, _) => ConfirmSelection();

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(useButton);
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        root.Children.Add(buttonPanel);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = BuildContent(transaction, suggestedOption, remainingGroupCount)
        };
        root.Children.Add(scrollViewer);

        BuildCandidateRows(candidates, suggestedOption);
        Content = root;

        Loaded += (_, _) =>
        {
            RefreshTables();
            ApplyInitialSelection(suggestedOption);
            _nameTextBox.Clear();
            _nameTextBox.Focus();
        };
        Closed += (_, _) => RememberWindowPlacement();
    }

    public string SelectedManualName { get; private set; } = string.Empty;

    private UIElement BuildContent(CostTransaction transaction, CostCenterNameOption? suggestedOption, int remainingGroupCount)
    {
        var content = new StackPanel();

        content.Children.Add(new TextBlock
        {
            Text = _matchingTransactions.Count > 1
                ? $"Choose the CTC resource name to apply to all {_matchingTransactions.Count} imported costs in this group, and to match future imports."
                : "This imported cost combination has not been named yet. Choose the CTC resource name to use for matching future imports.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        content.Children.Add(new TextBlock
        {
            Text = remainingGroupCount <= 1
                ? "This is the last naming group in the current import."
                : $"{remainingGroupCount} naming groups remain in this import, including this one.",
            Foreground = BrushFrom("#64748B"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        if (suggestedOption is not null)
        {
            content.Children.Add(new Border
            {
                Background = BrushFrom("#FEF3C7"),
                BorderBrush = BrushFrom("#F59E0B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 12),
                Child = new TextBlock
                {
                    Text = $"Suggested name: {suggestedOption.RawName}",
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }

        content.Children.Add(BuildFieldPanel(transaction));

        content.Children.Add(new TextBlock
        {
            Text = "Matching imported costs",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 6)
        });

        content.Children.Add(new Border
        {
            BorderBrush = BrushFrom("#D8DEE8"),
            BorderThickness = new Thickness(1),
            Child = BuildMatchingTransactionsGrid(_matchingTransactions)
        });

        content.Children.Add(new TextBlock
        {
            Text = "CTC name",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 6)
        });

        var nameEntryPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var selectExistingButton = new Button
        {
            Content = "Select from existing",
            MinWidth = 140,
            Margin = new Thickness(10, 0, 0, 0)
        };
        selectExistingButton.Click += (_, _) =>
        {
            if (_existingNamesMenu.IsOpen)
            {
                _existingNamesMenu.IsOpen = false;
                return;
            }

            var searchBox = RefreshExistingNamesMenu(string.Empty);
            _existingNamesMenu.PlacementTarget = selectExistingButton;
            _existingNamesMenu.IsOpen = true;
            FocusExistingNamesSearchBox(searchBox);
        };
        nameEntryPanel.Children.Add(_nameTextBox);
        nameEntryPanel.Children.Add(selectExistingButton);
        content.Children.Add(nameEntryPanel);
        content.Children.Add(_selectionInfoTextBlock);

        content.Children.Add(new TextBlock
        {
            Text = "Available names",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 6)
        });

        content.Children.Add(new Border
        {
            BorderBrush = BrushFrom("#D8DEE8"),
            BorderThickness = new Thickness(1),
            Child = _candidateGrid
        });

        var candidateFooter = new Grid
        {
            Margin = new Thickness(0, 8, 0, 0)
        };
        candidateFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_candidateCountTextBlock, 0);
        candidateFooter.Children.Add(_candidateCountTextBlock);
        content.Children.Add(candidateFooter);

        return content;
    }

    private void ConfirmSelection()
    {
        var selectedName = _selectedCandidateRow?.RawName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            selectedName = NormaliseName(_nameTextBox.Text);
        }

        if (string.IsNullOrWhiteSpace(selectedName))
        {
            MessageBox.Show(this, "Choose or type a CTC name before importing this cost.", "Cost centre name", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedManualName = selectedName;
        DialogResult = true;
        Close();
    }

    private DataGrid BuildCandidateGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserResizeRows = false,
            CanUserResizeColumns = true,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0,
            IsReadOnly = true,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            SelectionMode = DataGridSelectionMode.Single,
            RowHeight = CandidateRowHeight,
            ColumnHeaderHeight = 32,
            ItemsSource = _visibleCandidateRows,
            MinHeight = 140,
            HorizontalAlignment = HorizontalAlignment.Left,
            RowStyle = BuildSelectedDataGridRowStyle()
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Type",
            Binding = new System.Windows.Data.Binding(nameof(CandidateRow.StatusLabel)),
            Width = 120
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Source",
            Binding = new System.Windows.Data.Binding(nameof(CandidateRow.SourceLabel)),
            Width = 180
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new System.Windows.Data.Binding(nameof(CandidateRow.RawName)),
            Width = 280
        });
        grid.SelectionChanged += CandidateGrid_SelectionChanged;
        grid.MouseDoubleClick += (_, _) => ConfirmSelection();
        grid.PreviewKeyDown += CandidateGrid_PreviewKeyDown;
        grid.Loaded += (_, _) => UpdateCandidateGridWidth();
        AttachRightClickPan(grid);
        return grid;
    }

    private ContextMenu BuildExistingNamesMenu()
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.Bottom,
            MinWidth = 380,
            MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height * 0.55)
        };
        return menu;
    }

    private TextBox RefreshExistingNamesMenu(string filter)
    {
        var normalisedFilter = NormaliseName(filter);
        var existingNames = _existingNames
            .Where(name => string.IsNullOrWhiteSpace(normalisedFilter)
                || name.Contains(normalisedFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _existingNamesMenu.Items.Clear();
        _existingNamesMenu.Items.Add(new TextBlock
        {
            Text = "SELECT FROM EXISTING",
            Foreground = BrushFrom("#94A3B8"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(12, 8, 12, 6)
        });

        var searchBox = new TextBox
        {
            MinHeight = 32,
            MinWidth = 320,
            Margin = new Thickness(10, 0, 10, 8),
            Text = filter
        };
        searchBox.TextChanged += (_, _) =>
        {
            var nextSearchBox = RefreshExistingNamesMenu(searchBox.Text);
            FocusExistingNamesSearchBox(nextSearchBox);
        };

        _existingNamesMenu.Items.Add(searchBox);
        _existingNamesMenu.Items.Add(new Separator());

        if (existingNames.Count == 0)
        {
            _existingNamesMenu.Items.Add(new MenuItem
            {
                Header = "No existing names found",
                IsEnabled = false
            });
            return searchBox;
        }

        foreach (var existingName in existingNames)
        {
            var row = new CandidateRow
            {
                RawName = existingName,
                StatusLabel = "Existing",
                SourceLabel = "Existing resource name",
                IsFromImport = false,
                SortRank = 1
            };
            var item = new MenuItem
            {
                Header = existingName,
                Tag = row,
                Padding = new Thickness(16, 8, 18, 8)
            };
            item.Click += (_, _) =>
            {
                SelectExistingName(row);
                _existingNamesMenu.IsOpen = false;
            };
            _existingNamesMenu.Items.Add(item);
        }

        return searchBox;
    }

    private void FocusExistingNamesSearchBox(TextBox searchBox)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_existingNamesMenu.IsOpen)
            {
                return;
            }

            FocusManager.SetFocusedElement(_existingNamesMenu, searchBox);
            _existingNamesMenu.Focus();
            searchBox.Focus();
            Keyboard.Focus(searchBox);
            searchBox.CaretIndex = searchBox.Text.Length;
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private DataGrid BuildMatchingTransactionsGrid(IEnumerable<CostTransaction> transactions)
    {
        var rows = transactions
            .Select(transaction => new MatchingTransactionRow
            {
                FyPeriod = transaction.FyPeriod,
                TaskNumber = transaction.TaskNumber,
                ResourceDescription = transaction.ResourceDescription,
                SupplierName = transaction.SupplierName,
                Narrative2 = transaction.Narrative2,
                Narrative3 = transaction.Narrative3,
                Who = transaction.Who,
                Amount = transaction.Amount.ToString("C0")
            })
            .ToList();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserResizeRows = false,
            CanUserResizeColumns = true,
            CanUserSortColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0,
            RowHeight = CandidateRowHeight,
            ColumnHeaderHeight = 32,
            MinHeight = 280,
            Height = 420,
            MaxHeight = 560,
            ItemsSource = rows
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "FY", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.FyPeriod)), Width = 70 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Task", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.TaskNumber)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Resource", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.ResourceDescription)), Width = 220 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Supplier", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.SupplierName)), Width = 170 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Narrative 2", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.Narrative2)), Width = 220 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Narrative 3", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.Narrative3)), Width = 200 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Who", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.Who)), Width = 160 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Amount", Binding = new System.Windows.Data.Binding(nameof(MatchingTransactionRow.Amount)), Width = 90 });
        AttachRightClickPan(grid);
        return grid;
    }

    private void BuildCandidateRows(IEnumerable<CostCenterNameOption> candidates, CostCenterNameOption? suggestedOption)
    {
        _allCandidateRows.Clear();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestedName = NormaliseName(suggestedOption?.RawName);

        foreach (var option in candidates.Where(option => !string.IsNullOrWhiteSpace(option.RawName)))
        {
            var rawName = NormaliseName(option.RawName);
            if (!seenNames.Add(rawName))
            {
                continue;
            }

            var isSuggested = !string.IsNullOrWhiteSpace(suggestedName)
                && string.Equals(rawName, suggestedName, StringComparison.OrdinalIgnoreCase);
            _allCandidateRows.Add(new CandidateRow
            {
                RawName = rawName,
                StatusLabel = isSuggested ? "Recommended" : option.IsExistingName ? "Existing" : string.Empty,
                SourceLabel = option.SourceLabel,
                IsFromImport = true,
                SortRank = isSuggested ? 0 : option.IsExistingName ? 1 : GetSourceSortRank(option.SourceLabel)
            });
        }

        foreach (var existingName in _existingNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!seenNames.Add(existingName))
            {
                continue;
            }

            _allCandidateRows.Add(new CandidateRow
            {
                RawName = existingName,
                StatusLabel = "Existing",
                SourceLabel = "Existing resource name",
                IsFromImport = false,
                SortRank = 1
            });
        }
    }

    private void ApplyInitialSelection(CostCenterNameOption? suggestedOption)
    {
        var suggestedName = NormaliseName(suggestedOption?.RawName);
        _selectedCandidateRow = !string.IsNullOrWhiteSpace(suggestedName)
            ? _visibleCandidateRows.FirstOrDefault(row => string.Equals(row.RawName, suggestedName, StringComparison.OrdinalIgnoreCase))
            : _visibleCandidateRows.FirstOrDefault();

        if (_selectedCandidateRow is null)
        {
            return;
        }

        _candidateGrid.SelectedItem = _selectedCandidateRow;
        UpdateSelectionInfo(_selectedCandidateRow.SourceLabel);
    }

    private void RefreshTables()
    {
        var filter = NormaliseName(_nameTextBox.Text);
        IEnumerable<CandidateRow> importRows = _allCandidateRows
            .Where(row => row.IsFromImport)
            .OrderBy(row => row.SortRank)
            .ThenBy(row => row.RawName, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            importRows = importRows.Where(row => MatchesCandidateFilter(row, filter));
        }

        ReplaceCollection(_visibleCandidateRows, importRows);
        ApplyCandidateGridHeight();
    }

    private void ApplyCandidateGridHeight()
    {
        var totalRows = _visibleCandidateRows.Count;
        var displayedRows = Math.Max(InitialVisibleCandidateRows, totalRows);
        _candidateGrid.Height = displayedRows * CandidateRowHeight + _candidateGrid.ColumnHeaderHeight + 6;
        UpdateCandidateGridWidth();
        _candidateCountTextBlock.Text = totalRows == 1
            ? "1 available name"
            : $"{totalRows} available names";
    }

    private void UpdateCandidateGridWidth()
    {
        var totalColumnWidth = _candidateGrid.Columns.Sum(column => column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue);
        _candidateGrid.Width = totalColumnWidth + 4;
    }

    private static Style BuildSelectedDataGridRowStyle()
    {
        var style = new Style(typeof(DataGridRow));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        style.Setters.Add(new Setter(Control.ForegroundProperty, BrushFrom("#111827")));
        style.Triggers.Add(new Trigger
        {
            Property = DataGridRow.IsSelectedProperty,
            Value = true,
            Setters =
            {
                new Setter(Control.BackgroundProperty, BrushFrom("#DBEAFE")),
                new Setter(Control.ForegroundProperty, BrushFrom("#0F172A")),
                new Setter(Control.BorderBrushProperty, BrushFrom("#60A5FA")),
                new Setter(Control.BorderThicknessProperty, new Thickness(1))
            }
        });
        style.Triggers.Add(new MultiTrigger
        {
            Conditions =
            {
                new Condition(DataGridRow.IsSelectedProperty, true),
                new Condition(Selector.IsSelectionActiveProperty, false)
            },
            Setters =
            {
                new Setter(Control.BackgroundProperty, BrushFrom("#DBEAFE")),
                new Setter(Control.ForegroundProperty, BrushFrom("#0F172A"))
            }
        });
        return style;
    }

    private static bool MatchesCandidateFilter(CandidateRow row, string filter)
    {
        return row.RawName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.SourceLabel.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.StatusLabel.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetSourceSortRank(string sourceLabel)
    {
        return sourceLabel switch
        {
            "Resource Desc" => 2,
            "Supplier Name" => 3,
            "Narrative 1" => 4,
            "Narrative 2" => 5,
            "Narrative 3" => 6,
            "Who" => 7,
            "Resource Code" => 8,
            _ => 9
        };
    }

    private void CandidateGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_candidateGrid.SelectedItem is not CandidateRow row)
        {
            return;
        }

        _selectedCandidateRow = row;
        SetNameText(row.RawName);
        UpdateSelectionInfo(row.SourceLabel);
    }

    private void SelectExistingName(CandidateRow row)
    {
        _selectedCandidateRow = row;
        SetNameText(row.RawName);
        UpdateSelectionInfo(row.SourceLabel);
        _existingNamesMenu.IsOpen = false;
    }

    private void CandidateGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmSelection();
            e.Handled = true;
        }
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressNameTextChanged)
        {
            return;
        }

        RefreshTables();
        var typedName = NormaliseName(_nameTextBox.Text);
        if (string.IsNullOrWhiteSpace(typedName))
        {
            UpdateSelectionInfo(_selectedCandidateRow?.SourceLabel);
            return;
        }

        var exactMatch = _visibleCandidateRows.FirstOrDefault(row => string.Equals(row.RawName, typedName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            _selectedCandidateRow = exactMatch;
            _candidateGrid.SelectedItem = exactMatch;
            UpdateSelectionInfo(exactMatch.SourceLabel);
            return;
        }

        var startsWithMatch = _visibleCandidateRows.FirstOrDefault(row => row.RawName.StartsWith(typedName, StringComparison.OrdinalIgnoreCase));
        if (startsWithMatch is not null)
        {
            _candidateGrid.SelectedItem = startsWithMatch;
            UpdateSelectionInfo(startsWithMatch.SourceLabel);
            return;
        }

        _candidateGrid.SelectedItem = null;
        UpdateSelectionInfo(null);
    }

    private void SetNameText(string? value)
    {
        _suppressNameTextChanged = true;
        try
        {
            _nameTextBox.Text = value ?? string.Empty;
            _nameTextBox.CaretIndex = _nameTextBox.Text.Length;
        }
        finally
        {
            _suppressNameTextChanged = false;
        }
    }

    private void NameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && _visibleCandidateRows.Count > 0)
        {
            _candidateGrid.Focus();
            _candidateGrid.SelectedIndex = Math.Max(0, _candidateGrid.SelectedIndex);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ConfirmSelection();
            e.Handled = true;
        }
    }

    private void UpdateSelectionInfo(string? sourceLabel)
    {
        _selectionInfoTextBlock.Text = string.IsNullOrWhiteSpace(sourceLabel)
            ? "Came from: Manual entry"
            : $"Came from: {sourceLabel}";
    }

    private void AttachRightClickPan(DataGrid grid)
    {
        grid.Loaded += (_, _) =>
        {
            _activeScrollViewer ??= FindChild<ScrollViewer>(grid);
        };
        grid.PreviewMouseDown += Grid_PreviewMouseDown;
        grid.PreviewMouseMove += Grid_PreviewMouseMove;
        grid.PreviewMouseUp += Grid_PreviewMouseUp;
    }

    private void Grid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right
            || sender is not DataGrid grid
            || e.OriginalSource is not DependencyObject source
            || FindParent<DataGridColumnHeader>(source) is not null)
        {
            return;
        }

        _activeScrollViewer = FindChild<ScrollViewer>(grid);
        if (_activeScrollViewer is null)
        {
            return;
        }

        _rightDragStart = e.GetPosition(_activeScrollViewer);
        _dragHorizontalStartOffset = _activeScrollViewer.HorizontalOffset;
        _dragVerticalStartOffset = _activeScrollViewer.VerticalOffset;
        _rightDragging = false;
    }

    private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_rightDragStart is null
            || _activeScrollViewer is null
            || sender is not DataGrid grid
            || e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(_activeScrollViewer);
        var deltaX = current.X - _rightDragStart.Value.X;
        var deltaY = current.Y - _rightDragStart.Value.Y;
        if (!_rightDragging && Math.Abs(deltaX) < 6 && Math.Abs(deltaY) < 6)
        {
            return;
        }

        _rightDragging = true;
        if (!grid.IsMouseCaptured)
        {
            grid.CaptureMouse();
        }

        _activeScrollViewer.ScrollToHorizontalOffset(Math.Max(0, _dragHorizontalStartOffset - deltaX));
        _activeScrollViewer.ScrollToVerticalOffset(Math.Max(0, _dragVerticalStartOffset - deltaY));
        e.Handled = true;
    }

    private void Grid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right)
        {
            return;
        }

        if (sender is DataGrid grid && grid.IsMouseCaptured)
        {
            grid.ReleaseMouseCapture();
        }

        var wasDragging = _rightDragging;
        _rightDragging = false;
        _rightDragStart = null;
        if (wasDragging)
        {
            e.Handled = true;
        }
    }

    private void RememberWindowPlacement()
    {
        if (!double.IsNaN(Left) && !double.IsNaN(Top))
        {
            _rememberedLocation = new Point(Left, Top);
        }
    }

    private static Border BuildFieldPanel(CostTransaction transaction)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddField(grid, "Task", transaction.TaskNumber);
        AddField(grid, "FY Period", transaction.FyPeriod);
        AddField(grid, "Resource Code", transaction.ResourceCode);
        AddField(grid, "Resource Desc", transaction.ResourceDescription);
        AddField(grid, "Supplier Name", transaction.SupplierName);
        AddField(grid, "Narrative 1", transaction.Narrative1);
        AddField(grid, "Narrative 2", transaction.Narrative2);
        AddField(grid, "Narrative 3", transaction.Narrative3);
        AddField(grid, "Who", transaction.Who);
        AddField(grid, "Amount", transaction.Amount.ToString("C0"));
        AddField(grid, "ECM Number", transaction.EcmNumber);

        return new Border
        {
            BorderBrush = BrushFrom("#D8DEE8"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = grid
        };
    }

    private static void AddField(Grid grid, string label, string value)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = BrushFrom("#64748B"),
            Margin = new Thickness(0, 0, 10, 6)
        };
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        var valueBlock = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetRow(valueBlock, row);
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
    }

    private static string NormaliseName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value.Trim();
        if (cleaned.StartsWith("SUGGESTED -- ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["SUGGESTED -- ".Length..].TrimStart();
        }

        if (cleaned.StartsWith("EXISTING CTC -- ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["EXISTING CTC -- ".Length..].TrimStart();
        }

        while (cleaned.StartsWith('('))
        {
            var closingIndex = cleaned.IndexOf(')');
            if (closingIndex <= 0 || closingIndex + 1 >= cleaned.Length || !char.IsWhiteSpace(cleaned[closingIndex + 1]))
            {
                break;
            }

            cleaned = cleaned[(closingIndex + 1)..].TrimStart();
        }

        if (cleaned.EndsWith(" (existing CTC)", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^" (existing CTC)".Length].TrimEnd();
        }

        return cleaned.Trim();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static T? FindChild<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static SolidColorBrush BrushFrom(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private sealed class CandidateRow
    {
        public string StatusLabel { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
        public string RawName { get; init; } = string.Empty;
        public bool IsFromImport { get; init; }
        public int SortRank { get; init; }
    }

    private sealed class MatchingTransactionRow
    {
        public string FyPeriod { get; init; } = string.Empty;
        public string TaskNumber { get; init; } = string.Empty;
        public string ResourceDescription { get; init; } = string.Empty;
        public string SupplierName { get; init; } = string.Empty;
        public string Narrative2 { get; init; } = string.Empty;
        public string Narrative3 { get; init; } = string.Empty;
        public string Who { get; init; } = string.Empty;
        public string Amount { get; init; } = string.Empty;
    }
}
