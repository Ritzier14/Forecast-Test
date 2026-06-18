using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

public sealed class CsvTransactionService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".xlsx",
        ".xlsm"
    };

    public List<CostTransaction> Import(string path, int startingRowNumber)
    {
        var rows = ReadRows(path);
        if (rows.Count == 0)
        {
            return [];
        }

        var headers = rows[0]
            .Select((header, index) => new { Key = NormaliseHeader(header), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Index);

        var imported = new List<CostTransaction>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            imported.Add(new CostTransaction
            {
                RowNumber = startingRowNumber++,
                FyPeriod = Get(row, headers, "fyperiod", "fyperi", "fy", "periodlabel"),
                TaskNumber = Get(row, headers, "tasknumber", "tasknumb", "tasknum", "task"),
                Period = ParseInt(Get(row, headers, "period")),
                DocDate = ParseDate(Get(row, headers, "docdate", "date", "documentdate")),
                Units = ParseDecimal(Get(row, headers, "units", "quantity")),
                UnitRate = ParseDecimal(Get(row, headers, "unitrate", "unitra", "rate")),
                Amount = ParseDecimal(Get(row, headers, "amount", "amou", "cost", "value")),
                CostLedger = Get(row, headers, "costledger", "costle", "ledger"),
                CostAccount = Get(row, headers, "costaccount", "costac", "account"),
                ProjectCode = Get(row, headers, "projectcode", "projectco", "project"),
                ParentProjectCode = Get(row, headers, "parentprojectcode", "parentproject", "parentpro", "parent"),
                ResourceCode = Get(row, headers, "resourcecode", "resourcec", "resourceco", "resourc", "resource", "resour", "code"),
                ResourceDescription = Get(row, headers, "resourcedescription", "resourced", "resourcedesc", "resourcedescr", "resourcei"),
                Source = Get(row, headers, "source"),
                PoNumber = Get(row, headers, "ponumber", "ponumb", "ponum", "po"),
                PoComments = Get(row, headers, "pocomments", "pocomm", "pocor", "pocom", "pocomment"),
                SupplierName = Get(row, headers, "suppliername", "suppliern", "supplier"),
                Narrative1 = Get(row, headers, "narrative1", "narrative"),
                Narrative2 = Get(row, headers, "narrative2"),
                Narrative3 = Get(row, headers, "narrative3"),
                Who = Get(row, headers, "who"),
                EcmNumber = Get(row, headers, "ecmnumber", "ecmnum", "ecm"),
                ManualName = Get(row, headers, "manualname", "manualresource", "name")
            });
        }

        return imported;
    }

    public bool SupportsFile(string path)
    {
        var extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);
    }

    public string GetSupportedFileFilter()
    {
        return "Import files (*.csv;*.xlsx;*.xlsm)|*.csv;*.xlsx;*.xlsm|CSV files (*.csv)|*.csv|Excel files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All files (*.*)|*.*";
    }

    public void ExportTransactions(string path, IEnumerable<CostTransaction> transactions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("FY Period,Task Number,Period,Doc Date,Units,Unit Rate,Amount,Cost Ledger,Cost Account,Project Code,Parent Project,Resource Code,Resource Description,Source,PO Number,PO Comments,Supplier Name,Narrative 1,Narrative 2,Narrative 3,Who,ECM Number,Manual Name");

        foreach (var tx in transactions)
        {
            var fields = new[]
            {
                tx.FyPeriod,
                tx.TaskNumber,
                tx.Period.ToString(CultureInfo.InvariantCulture),
                tx.DocDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                tx.Units.ToString(CultureInfo.InvariantCulture),
                tx.UnitRate.ToString(CultureInfo.InvariantCulture),
                tx.Amount.ToString(CultureInfo.InvariantCulture),
                tx.CostLedger,
                tx.CostAccount,
                tx.ProjectCode,
                tx.ParentProjectCode,
                tx.ResourceCode,
                tx.ResourceDescription,
                tx.Source,
                tx.PoNumber,
                tx.PoComments,
                tx.SupplierName,
                tx.Narrative1,
                tx.Narrative2,
                tx.Narrative3,
                tx.Who,
                tx.EcmNumber,
                tx.ManualName
            };
            builder.AppendLine(string.Join(",", fields.Select(Escape)));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    public static string BuildNameMappingKey(CostTransaction transaction)
    {
        return string.Join("|", new[]
        {
            NormaliseKeyPart(transaction.ResourceDescription),
            NormaliseKeyPart(transaction.Narrative2),
            NormaliseKeyPart(transaction.Narrative3),
            NormaliseKeyPart(transaction.Who)
        });
    }

    public static string BuildDuplicateKey(CostTransaction transaction)
    {
        return string.Join("|", new[]
        {
            NormaliseKeyPart(transaction.FyPeriod),
            NormaliseKeyPart(transaction.TaskNumber),
            transaction.Period.ToString(CultureInfo.InvariantCulture),
            transaction.DocDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            transaction.Units.ToString(CultureInfo.InvariantCulture),
            transaction.UnitRate.ToString(CultureInfo.InvariantCulture),
            transaction.Amount.ToString(CultureInfo.InvariantCulture),
            NormaliseKeyPart(transaction.CostLedger),
            NormaliseKeyPart(transaction.CostAccount),
            NormaliseKeyPart(transaction.ProjectCode),
            NormaliseKeyPart(transaction.ParentProjectCode),
            NormaliseKeyPart(transaction.ResourceCode),
            NormaliseKeyPart(transaction.ResourceDescription),
            NormaliseKeyPart(transaction.Source),
            NormaliseKeyPart(transaction.PoNumber),
            NormaliseKeyPart(transaction.PoComments),
            NormaliseKeyPart(transaction.SupplierName),
            NormaliseKeyPart(transaction.Narrative1),
            NormaliseKeyPart(transaction.Narrative2),
            NormaliseKeyPart(transaction.Narrative3),
            NormaliseKeyPart(transaction.Who),
            NormaliseKeyPart(transaction.EcmNumber)
        });
    }

    private static List<List<string>> ReadRows(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
        {
            return ReadWorkbookRows(path);
        }

        var rows = new List<List<string>>();
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (!reader.EndOfStream)
        {
            rows.Add(ParseCsvLine(reader.ReadLine() ?? string.Empty));
        }

        return rows;
    }

    private static List<List<string>> ReadWorkbookRows(string path)
    {
        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("The workbook does not contain any worksheets.");
        var range = worksheet.RangeUsed();
        if (range is null)
        {
            return [];
        }

        var lastColumn = range.ColumnCount();
        var rows = new List<List<string>>();
        foreach (var row in range.RowsUsed())
        {
            var values = new List<string>(lastColumn);
            for (var column = 1; column <= lastColumn; column++)
            {
                values.Add(row.Cell(column).GetFormattedString().Trim());
            }

            rows.Add(values);
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static string Get(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> headers, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (headers.TryGetValue(key, out var index) && index < row.Count)
            {
                return row[index].Trim();
            }
        }

        return string.Empty;
    }

    private static string NormaliseHeader(string header)
    {
        var chars = header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant);
        return new string(chars.ToArray());
    }

    private static string NormaliseKeyPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalised = string.Join(" ", value.Trim().Split(default(string[]), StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        return normalised.All(character => character == '-') ? string.Empty : normalised;
    }

    private static decimal ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Currency | NumberStyles.Number, CultureInfo.CurrentCulture, out var current))
        {
            return current;
        }

        return decimal.TryParse(value, NumberStyles.Currency | NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : 0;
    }

    private static int ParseInt(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static DateOnly? ParseDate(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var current))
        {
            return current;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant) ? invariant : null;
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
