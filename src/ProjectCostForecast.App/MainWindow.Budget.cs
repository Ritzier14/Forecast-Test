using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public partial class MainWindow
{
    private void RebuildBudgetGridColumns()
    {
        if (DataContext is not MainWindowViewModel viewModel || BudgetGrid is null)
        {
            return;
        }

        viewModel.MeasureRefreshPhase(Services.RefreshPhase.GridColumns, () =>
        {
            BudgetGrid.Columns.Clear();
            BudgetGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Budget line",
                Binding = new Binding(nameof(FiscalYearBudgetLine.Name)),
                Width = 150,
                IsReadOnly = true,
                ElementStyle = CreatePlainTextStyle(10)
            });

            for (var index = 0; index < viewModel.BudgetFiscalYears.Count; index++)
            {
                var fiscalYear = viewModel.BudgetFiscalYears[index];
                BudgetGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = fiscalYear,
                    Binding = new Binding($"Amounts[{index}].Amount")
                    {
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                        Converter = AccountingConverter
                    },
                    Width = 112,
                    MinWidth = 90,
                    IsReadOnly = viewModel.IsViewingSavedMonth,
                    ElementStyle = CreateNumericTextStyle(),
                    EditingElementStyle = CreateBudgetEditingTextStyle()
                });
            }

            BudgetGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Total",
                Binding = BuildAccountingBinding(nameof(FiscalYearBudgetLine.Total), viewModel.ShowCurrencySymbols),
                Width = 125,
                IsReadOnly = true,
                ElementStyle = CreateNumericTextStyle()
            });
            BudgetGrid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Active",
                Binding = new Binding(nameof(FiscalYearBudgetLine.IsActive)),
                Width = 72,
                IsReadOnly = true
            });
        });
    }

    private static Style CreateBudgetEditingTextStyle()
    {
        var style = CreateEditingTextStyle();
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
        return style;
    }

    private void BudgetGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || e.OriginalSource is not DependencyObject source
            || FindParent<DataGridRow>(source) is not { Item: FiscalYearBudgetLine line } row)
        {
            return;
        }

        BudgetGrid.SelectedItem = line;
        row.IsSelected = true;
        var setActive = new MenuItem
        {
            Header = line.IsActive ? $"{line.Name} is the active budget" : "Set as active budget",
            IsEnabled = !line.IsActive && !viewModel.IsViewingSavedMonth
        };
        setActive.Click += (_, _) => viewModel.SetActiveBudgetLine(line);
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        menu.Items.Add(setActive);
        menu.IsOpen = true;
        e.Handled = true;
    }
}
