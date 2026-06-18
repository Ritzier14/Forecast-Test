using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCostForecast.App;
using Microsoft.Win32;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.Services;

namespace ProjectCostForecast.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string FormatKpiValue(KpiOption? option)
    {
        return GetKpiValue(option).ToString("C0");
    }

    public void AddKpiPill(string key)
    {
        var option = KpiOptions.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) ?? KpiOptions.First();
        var pill = new KpiPill
        {
            Id = _nextKpiPillId++,
            Key = option.Key
        };

        KpiPills.Add(pill);
        RefreshKpiPill(pill);
        SaveUserPreferences();
    }

    public void RemoveKpiPill(int pillId)
    {
        if (KpiPills.Count <= 1)
        {
            return;
        }

        var pill = KpiPills.FirstOrDefault(item => item.Id == pillId);
        if (pill is not null)
        {
            KpiPills.Remove(pill);
            SaveUserPreferences();
        }
    }

    public void SetKpiSelection(int pillId, string key)
    {
        var pill = KpiPills.FirstOrDefault(item => item.Id == pillId);
        var option = KpiOptions.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (pill is null || option is null)
        {
            return;
        }

        pill.Key = option.Key;
        RefreshKpiPill(pill);
        SaveUserPreferences();
    }

    public bool IsKpiPillActive(string key)
    {
        return KpiPills.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public void SetKpiPillActive(string key, bool isActive)
    {
        if (isActive)
        {
            if (!IsKpiPillActive(key))
            {
                AddKpiPill(key);
            }

            return;
        }

        var matchingPills = KpiPills
            .Where(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingPills.Count == 0)
        {
            return;
        }

        if (KpiPills.Count == matchingPills.Count && matchingPills.Count > 0)
        {
            return;
        }

        foreach (var pill in matchingPills)
        {
            KpiPills.Remove(pill);
        }

        SaveUserPreferences();
    }

    public KpiOption? GetSelectedKpi(int pillId)
    {
        var pill = KpiPills.FirstOrDefault(item => item.Id == pillId);
        return pill is null
            ? null
            : KpiOptions.FirstOrDefault(item => string.Equals(item.Key, pill.Key, StringComparison.OrdinalIgnoreCase));
    }

    private void SeedDefaultKpiPills()
    {
        AddKpiPill("PlannedCostFcc");
        AddKpiPill("TotalCostToDate");
        AddKpiPill("TotalForecastCtc");
        AddKpiPill("TotalBudgetVariance");
        AddKpiPill("MonthlyVariance");
        AddKpiPill("TotalBudget");
    }

    private void SeedDefaultPivotLayout()
    {
        SelectedPivotField = PivotFields.FirstOrDefault();
        _suppressPivotRefresh = true;
        try
        {
            AddPivotFieldByKey(PivotRowFields, "ProjectCode");
            AddPivotFieldByKey(PivotColumnFields, "FyPeriod");
            AddPivotFieldByKey(PivotValueFields, "Amount");
        }
        finally
        {
            _suppressPivotRefresh = false;
        }

        RebuildCustomPivot();
    }

    private static IReadOnlyList<PivotFieldDefinition> CreatePivotFieldDefinitions()
    {
        return
        [
            new() { Key = "FyPeriod", Name = "FY-Period" },
            new() { Key = "TaskNumber", Name = "Task Number" },
            new() { Key = "Period", Name = "Period", IsNumeric = true },
            new() { Key = "DocDate", Name = "Doc Date" },
            new() { Key = "Units", Name = "Units", IsNumeric = true },
            new() { Key = "UnitRate", Name = "Unit Rate", IsNumeric = true },
            new() { Key = "Amount", Name = "Amount", IsNumeric = true },
            new() { Key = "CostLedger", Name = "Cost Ledger" },
            new() { Key = "CostAccount", Name = "Cost Account" },
            new() { Key = "ProjectCode", Name = "Project Code" },
            new() { Key = "ParentProjectCode", Name = "Parent Project Code" },
            new() { Key = "ResourceCode", Name = "Resource Code" },
            new() { Key = "ResourceDescription", Name = "Resource Description" },
            new() { Key = "LedgerResourceName", Name = "Resource" },
            new() { Key = "Source", Name = "Source" },
            new() { Key = "PoNumber", Name = "PO Number" },
            new() { Key = "PoComments", Name = "PO Comments" },
            new() { Key = "SupplierName", Name = "Supplier Name" },
            new() { Key = "Narrative1", Name = "Narrative 1" },
            new() { Key = "Narrative2", Name = "Narrative 2" },
            new() { Key = "Narrative3", Name = "Narrative 3" },
            new() { Key = "Who", Name = "Who" },
            new() { Key = "EcmNumber", Name = "ECM Number" },
            new() { Key = "ManualName", Name = "Manual Name" }
        ];
    }

    private void AddSelectedPivotField(ObservableCollection<PivotAreaField> target, bool requireNumeric)
    {
        if (SelectedPivotField is null || (requireNumeric && !SelectedPivotField.IsNumeric))
        {
            return;
        }

        AddPivotField(target, SelectedPivotField);
    }

    private void AddPivotFieldByKey(ObservableCollection<PivotAreaField> target, string key)
    {
        var field = PivotFields.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (field is not null)
        {
            AddPivotField(target, field);
        }
    }

    private void AddPivotField(ObservableCollection<PivotAreaField> target, PivotFieldDefinition field)
    {
        if (target.Any(item => string.Equals(item.Key, field.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var areaField = new PivotAreaField
        {
            Key = field.Key,
            Name = field.Name,
            IsNumeric = field.IsNumeric
        };
        if (ReferenceEquals(target, PivotFilterFields))
        {
            RefreshPivotFilterValues(areaField);
        }

        areaField.PropertyChanged += PivotAreaField_PropertyChanged;
        target.Add(areaField);
        if (!_suppressPivotRefresh)
        {
            RebuildCustomPivot();
        }
    }

    private void RemovePivotField(ObservableCollection<PivotAreaField> target, PivotAreaField? field)
    {
        if (field is null)
        {
            return;
        }

        field.PropertyChanged -= PivotAreaField_PropertyChanged;
        target.Remove(field);
        if (!_suppressPivotRefresh)
        {
            RebuildCustomPivot();
        }
    }

    private void ClearPivotLayout()
    {
        foreach (var field in PivotRowFields.Concat(PivotColumnFields).Concat(PivotValueFields).Concat(PivotFilterFields))
        {
            field.PropertyChanged -= PivotAreaField_PropertyChanged;
        }

        PivotRowFields.Clear();
        PivotColumnFields.Clear();
        PivotValueFields.Clear();
        PivotFilterFields.Clear();
        RebuildCustomPivot();
    }

    private void PivotAreaField_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressPivotRefresh)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(PivotAreaField.SelectedFilterValue), StringComparison.Ordinal))
        {
            RebuildCustomPivot();
        }
    }

    private void RefreshPivotFilterValues(PivotAreaField areaField)
    {
        var currentValue = areaField.SelectedFilterValue;
        var values = Transactions
            .Select(transaction => FormatPivotValue(GetPivotValue(transaction, areaField.Key)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceCollection(areaField.FilterValues, new[] { PivotAreaField.PivotBuilderAllFilterValue }.Concat(values));
        areaField.SelectedFilterValue = areaField.FilterValues.Contains(currentValue)
            ? currentValue
            : PivotAreaField.PivotBuilderAllFilterValue;
    }

    private void RefreshPivotFilterValues()
    {
        if (!_pivotFilterValuesDirty)
        {
            return;
        }

        foreach (var field in PivotFilterFields)
        {
            RefreshPivotFilterValues(field);
        }

        _pivotFilterValuesDirty = false;
    }

    private void InvalidatePivotFilterValues()
    {
        _pivotFilterValuesDirty = true;
    }

    private void RebuildCustomPivot()
    {
        _suppressPivotRefresh = true;
        try
        {
            RefreshPivotFilterValues();
        }
        finally
        {
            _suppressPivotRefresh = false;
        }

        var rowFields = PivotRowFields.ToList();
        var columnFields = PivotColumnFields.ToList();
        var valueFields = PivotValueFields.Count > 0
            ? PivotValueFields.ToList()
            : PivotFields.Where(field => string.Equals(field.Key, "Amount", StringComparison.OrdinalIgnoreCase))
                .Select(field => new PivotAreaField { Key = field.Key, Name = field.Name, IsNumeric = true })
                .ToList();

        var rowGroups = new Dictionary<string, PivotGroupKey>(StringComparer.OrdinalIgnoreCase);
        var columnGroups = new Dictionary<string, PivotColumnGroup>(StringComparer.OrdinalIgnoreCase);
        var aggregates = new Dictionary<PivotAggregateKey, decimal>();

        if (rowFields.Count == 0)
        {
            rowGroups[string.Empty] = new PivotGroupKey(string.Empty, "Grand Total");
        }

        if (columnFields.Count == 0)
        {
            columnGroups[string.Empty] = new PivotColumnGroup(string.Empty, string.Empty);
        }

        foreach (var transaction in Transactions)
        {
            if (!PassesPivotFilters(transaction))
            {
                continue;
            }

            var rowGroup = rowFields.Count == 0
                ? rowGroups[string.Empty]
                : BuildPivotGroupKey(transaction, rowFields);
            if (!rowGroups.TryGetValue(rowGroup.Key, out var storedRowGroup))
            {
                rowGroups[rowGroup.Key] = rowGroup;
                storedRowGroup = rowGroup;
            }

            var columnGroup = columnFields.Count == 0
                ? columnGroups[string.Empty]
                : CreatePivotColumnGroup(BuildPivotGroupKey(transaction, columnFields));
            if (!columnGroups.TryGetValue(columnGroup.Key, out var storedColumnGroup))
            {
                columnGroups[columnGroup.Key] = columnGroup;
                storedColumnGroup = columnGroup;
            }

            foreach (var valueField in valueFields)
            {
                var key = new PivotAggregateKey(storedRowGroup.Key, storedColumnGroup.Key, valueField.Key);
                aggregates[key] = aggregates.GetValueOrDefault(key) + GetPivotNumericValue(transaction, valueField.Key);
            }
        }

        var resultColumns = new List<PivotResultColumn>();
        foreach (var rowField in rowFields)
        {
            resultColumns.Add(new PivotResultColumn { Key = $"ROW:{rowField.Key}", Header = rowField.Name });
        }

        var orderedColumnGroups = columnGroups.Values
            .OrderBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var valueColumnMap = new List<(PivotColumnGroup ColumnGroup, PivotAreaField ValueField, string Key)>();
        var columnIndex = 0;
        foreach (var columnGroup in orderedColumnGroups)
        {
            foreach (var valueField in valueFields)
            {
                var key = $"VAL:{columnIndex++}";
                var header = columnFields.Count == 0
                    ? $"Sum of {valueField.Name}"
                    : $"{columnGroup.Label} | Sum of {valueField.Name}";
                resultColumns.Add(new PivotResultColumn { Key = key, Header = header, IsNumeric = true });
                valueColumnMap.Add((columnGroup, valueField, key));
            }
        }

        ReplaceCollection(PivotResultColumns, resultColumns);

        var orderedRowGroups = rowGroups.Values
            .OrderBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rows = new List<PivotResultRow>();
        foreach (var rowGroup in orderedRowGroups)
        {
            var row = new PivotResultRow();
            var rowLabels = SplitPivotKey(rowGroup.Label, rowFields.Count);
            for (var index = 0; index < rowFields.Count; index++)
            {
                row[$"ROW:{rowFields[index].Key}"] = rowLabels.ElementAtOrDefault(index) ?? string.Empty;
            }

            foreach (var map in valueColumnMap)
            {
                row[map.Key] = aggregates.GetValueOrDefault(new PivotAggregateKey(rowGroup.Key, map.ColumnGroup.Key, map.ValueField.Key));
            }

            rows.Add(row);
        }

        ReplaceCollection(PivotResultRows, rows);
    }

    private static PivotColumnGroup CreatePivotColumnGroup(PivotGroupKey key)
    {
        return new PivotColumnGroup(key.Key, key.Label);
    }

    private bool PassesPivotFilters(CostTransaction transaction)
    {
        foreach (var filter in PivotFilterFields)
        {
            if (string.Equals(filter.SelectedFilterValue, PivotAreaField.PivotBuilderAllFilterValue, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(FormatPivotValue(GetPivotValue(transaction, filter.Key)), filter.SelectedFilterValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static PivotGroupKey BuildPivotGroupKey(CostTransaction transaction, IEnumerable<PivotAreaField> fields)
    {
        var values = fields
            .Select(field => FormatPivotValue(GetPivotValue(transaction, field.Key)))
            .ToList();
        var label = string.Join(" | ", values);
        return new PivotGroupKey(label, string.IsNullOrWhiteSpace(label) ? "(Blank)" : label);
    }

    private static string[] SplitPivotKey(string label, int expectedParts)
    {
        if (expectedParts <= 1)
        {
            return [label];
        }

        return label.Split(" | ", StringSplitOptions.None);
    }

    private static object? GetPivotValue(CostTransaction transaction, string key)
    {
        return key switch
        {
            "FyPeriod" => transaction.FyPeriod,
            "TaskNumber" => transaction.TaskNumber,
            "Period" => transaction.Period,
            "DocDate" => transaction.DocDate,
            "Units" => transaction.Units,
            "UnitRate" => transaction.UnitRate,
            "Amount" => transaction.Amount,
            "CostLedger" => transaction.CostLedger,
            "CostAccount" => transaction.CostAccount,
            "ProjectCode" => transaction.ProjectCode,
            "ParentProjectCode" => transaction.ParentProjectCode,
            "ResourceCode" => transaction.ResourceCode,
            "ResourceDescription" => transaction.ResourceDescription,
            "LedgerResourceName" => transaction.LedgerResourceName,
            "Source" => transaction.Source,
            "PoNumber" => transaction.PoNumber,
            "PoComments" => transaction.PoComments,
            "SupplierName" => transaction.SupplierName,
            "Narrative1" => transaction.Narrative1,
            "Narrative2" => transaction.Narrative2,
            "Narrative3" => transaction.Narrative3,
            "Who" => transaction.Who,
            "EcmNumber" => transaction.EcmNumber,
            "ManualName" => transaction.ManualName,
            _ => string.Empty
        };
    }

    private static decimal GetPivotNumericValue(CostTransaction transaction, string key)
    {
        return GetPivotValue(transaction, key) switch
        {
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            _ => 0m
        };
    }

    private static string FormatPivotValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd"),
            decimal decimalValue => decimalValue.ToString("0.##"),
            string text => text,
            _ => value.ToString() ?? string.Empty
        };
    }

    private sealed record PivotGroupKey(string Key, string Label);
    private sealed record PivotColumnGroup(string Key, string Label);
    private readonly record struct PivotAggregateKey(string RowKey, string ColumnKey, string ValueKey);

    private sealed class AppTotals
    {
        public decimal TotalForecastCtc { get; set; }
        public decimal TotalCostToDate { get; set; }
        public decimal PlannedCostFcc { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalBudgetVariance { get; set; }
        public decimal CurrentMonthCostTotal { get; set; }
        public decimal RemainingForecastTotal { get; set; }
        public decimal MonthlyVarianceTotal { get; set; }
        public decimal TotalContingencyRemaining { get; set; }
        public decimal FiscalReportSpentTotal { get; set; }
        public decimal FiscalReportCostToCompleteTotal { get; set; }
        public decimal FiscalReportPlannedCostTotal { get; set; }
        public decimal FiscalReportBudgetTotal { get; set; }
        public decimal FiscalReportVarianceTotal { get; set; }
        public decimal ProjectContingencyTotal { get; set; }
        public decimal ContingencyExpendedTotal { get; set; }
        public decimal ContingencyProposedTotal { get; set; }
        public decimal ContingencyRemainingTotal { get; set; }
    }

    private sealed class LedgerTotals
    {
        public int TransactionCount { get; set; }
        public decimal TransactionTotal { get; set; }
        public decimal UnitsTotal { get; set; }
        public decimal AverageRate { get; set; }
        public decimal ForecastTotal { get; set; }
        public decimal ProjectedTotal { get; set; }
    }

    private void RefreshKpiPills()
    {
        foreach (var pill in KpiPills)
        {
            RefreshKpiPill(pill);
        }
    }

    private void RefreshKpiPill(KpiPill pill)
    {
        var option = KpiOptions.FirstOrDefault(item => string.Equals(item.Key, pill.Key, StringComparison.OrdinalIgnoreCase));
        pill.Name = option?.Name ?? "Select total";
        pill.ValueText = FormatKpiValue(option);
        pill.Subtext = GetKpiSubtext(option);
        pill.IconPath = GetKpiIconPath(option?.Key);
        if (TryGetPreviousKpiValue(option, out var previousValue))
        {
            var currentValue = GetKpiValue(option);
            pill.ComparisonText = KpiComparisonFormatter.Format(currentValue, previousValue);
            pill.ComparisonDirection = KpiComparisonFormatter.GetDirection(currentValue, previousValue);
            pill.ComparisonVisibility = string.IsNullOrWhiteSpace(pill.ComparisonText) ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            pill.ComparisonText = string.Empty;
            pill.ComparisonDirection = string.Empty;
            pill.ComparisonVisibility = Visibility.Collapsed;
        }
    }

    private static string GetKpiIconPath(string? key)
    {
        var fileName = key switch
        {
            "PlannedCostFcc" => "ic_metric_planned_cost_28.png",
            "TotalCostToDate" => "ic_metric_cost_to_date_28.png",
            "TotalForecastCtc" => "ic_metric_forecast_at_completion_28.png",
            "TotalBudgetVariance" => "ic_metric_forecast_variance_28.png",
            "MonthlyVariance" => "ic_metric_variance_percent_28.png",
            "TotalBudget" => "ic_metric_budget_remaining_28.png",
            _ => "ic_metric_forecast_at_completion_28.png"
        };

        return $"/Assets/Icons/png/{fileName}";
    }

    private bool TryGetPreviousKpiValue(KpiOption? option, out decimal previousValue)
    {
        previousValue = 0;
        if (option is null)
        {
            return false;
        }

        if (string.Equals(option.Key, "PlannedCostFcc", StringComparison.OrdinalIgnoreCase))
        {
            previousValue = ForecastLines.Sum(line => line.LastMonthPlannedCost);
            return previousValue != 0;
        }

        var snapshot = SavedMonthSnapshots.OrderByDescending(item => item.SavedAt).FirstOrDefault();
        if (snapshot is null)
        {
            return false;
        }

        previousValue = option.Key switch
        {
            "TotalForecastCtc" => snapshot.CostToComplete,
            "TotalCostToDate" => snapshot.CostToDate,
            "TotalBudget" => snapshot.ForecastLines.Sum(line => line.Budget),
            "TotalBudgetVariance" => snapshot.TotalBudgetVariance,
            "CurrentMonthCost" => snapshot.ForecastLines.Sum(line => line.CurrentPeriodForecast),
            "RemainingForecast" => snapshot.CostToComplete,
            _ => 0
        };
        return previousValue != 0;
    }

    private decimal GetKpiValue(KpiOption? option)
    {
        return option?.Key switch
        {
            "TotalForecastCtc" => TotalForecastCtc,
            "TotalCostToDate" => TotalCostToDate,
            "PlannedCostFcc" => PlannedCostFcc,
            "TotalBudget" => TotalBudget,
            "TotalBudgetVariance" => TotalBudgetVariance,
            "CurrentMonthCost" => CurrentMonthCostTotal,
            "RemainingForecast" => RemainingForecastTotal,
            "MonthlyVariance" => MonthlyVarianceTotal,
            _ => 0
        };
    }

    private string GetKpiSubtext(KpiOption? option)
    {
        return option?.Key switch
        {
            "TotalForecastCtc" => $"{ForecastLineCount} forecast lines",
            "TotalCostToDate" => $"{TransactionCount} transactions",
            "CurrentMonthCost" => Header.CurrentPeriod,
            "RemainingForecast" => "future forecast lines",
            "MonthlyVariance" => "negative = forecast exceeds actuals",
            "TotalBudgetVariance" => $"{ValidationIssueCount} validation issues",
            _ => string.Empty
        };
    }
}
