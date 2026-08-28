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

    private readonly ImportBoundaryOptions _options;

    public CsvTransactionService(ImportBoundaryOptions? options = null)
    {
        _options = options ?? ImportBoundaryOptions.Default;
        _options.Validate();
    }

    public ImportBoundaryOptions Options => _options;

    public List<CostTransaction> Import(string path, int startingRowNumber)
    {
        return Import(path, startingRowNumber, CancellationToken.None);
    }

    public List<CostTransaction> Import(
        string path,
        int startingRowNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(startingRowNumber);
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new ImportBoundaryException(
                ImportBoundaryFailureKind.UnsupportedFileType,
                $"Import file '{Path.GetFileName(path)}' has unsupported type '{extension}'. Choose a .csv, .xlsx, or .xlsm file.",
                path);
        }

        return string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
            ? ImportCsv(path, startingRowNumber, cancellationToken)
            : ImportWorkbook(path, startingRowNumber, cancellationToken);
    }

    public bool SupportsFile(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && SupportedExtensions.Contains(Path.GetExtension(path));
    }

    public string GetSupportedFileFilter()
    {
        return "Import files (*.csv;*.xlsx;*.xlsm)|*.csv;*.xlsx;*.xlsm|CSV files (*.csv)|*.csv|Excel files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All files (*.*)|*.*";
    }

    public void ExportTransactions(string path, IEnumerable<CostTransaction> transactions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(transactions);

        var builder = new StringBuilder();
        builder.AppendLine("FY Period,Task Number,Period,Doc Date,Units,Unit Rate,Amount,Cost Ledger,Cost Account,Project Code,Parent Project,Resource Code,Resource Description,Source,PO Number,PO Comments,Supplier Name,Narrative 1,Narrative 2,Narrative 3,Who,ECM Number,Manual Name");

        foreach (var tx in transactions)
        {
            var encodedFields = new[]
            {
                SpreadsheetExportBoundary.EscapeCsvField(tx.FyPeriod ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.TaskNumber ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Period.ToString(CultureInfo.InvariantCulture), neutralizeFormula: false),
                SpreadsheetExportBoundary.EscapeCsvField(tx.DocDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Units.ToString(CultureInfo.InvariantCulture), neutralizeFormula: false),
                SpreadsheetExportBoundary.EscapeCsvField(tx.UnitRate.ToString(CultureInfo.InvariantCulture), neutralizeFormula: false),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Amount.ToString(CultureInfo.InvariantCulture), neutralizeFormula: false),
                SpreadsheetExportBoundary.EscapeCsvField(tx.CostLedger ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.CostAccount ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.ProjectCode ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.ParentProjectCode ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.ResourceCode ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.ResourceDescription ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Source ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.PoNumber ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.PoComments ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.SupplierName ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Narrative1 ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Narrative2 ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Narrative3 ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.Who ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.EcmNumber ?? string.Empty),
                SpreadsheetExportBoundary.EscapeCsvField(tx.ManualName ?? string.Empty)
            };
            // Numeric values use the same quoting rules but are deliberately
            // not sent through formula neutralization, so a negative amount
            // remains a numeric value when opened in a spreadsheet.
            builder.AppendLine(string.Join(",", encodedFields));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    public static string BuildNameMappingKey(CostTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
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
        ArgumentNullException.ThrowIfNull(transaction);
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

    private List<CostTransaction> ImportCsv(
        string path,
        int startingRowNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = OpenInputStream(path);
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096);

            return MapRows(
                ReadCsvRows(reader, path, cancellationToken),
                path,
                isWorkbookImport: false,
                startingRowNumber,
                cancellationToken);
        }
        catch (ImportBoundaryException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DecoderFallbackException ex)
        {
            throw ImportBoundaryException.Malformed(
                ImportBoundaryFailureKind.MalformedCsv,
                path,
                "the CSV is not valid UTF-8 text",
                ex);
        }
        catch (IOException ex)
        {
            throw ImportBoundaryException.FileAccess(path, ex);
        }
    }

    private List<CostTransaction> ImportWorkbook(
        string path,
        int startingRowNumber,
        CancellationToken cancellationToken)
    {
        EnsureFileSize(path);
        WorkbookBoundaryPreflight.Validate(path, _options, cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var workbook = new XLWorkbook(path);
            if (workbook.Worksheets.Count == 0)
            {
                throw ImportBoundaryException.Malformed(
                    ImportBoundaryFailureKind.MalformedWorkbook,
                    path,
                    "the workbook does not contain a worksheet");
            }

            if (workbook.Worksheets.Count > _options.MaxWorksheets)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.WorksheetLimitExceeded,
                    path,
                    "worksheet count",
                    nameof(_options.MaxWorksheets),
                    workbook.Worksheets.Count,
                    _options.MaxWorksheets);
            }

            var worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw ImportBoundaryException.Malformed(
                    ImportBoundaryFailureKind.MalformedWorkbook,
                    path,
                    "the workbook does not contain a readable worksheet");
            var range = worksheet.RangeUsed();
            if (range is null)
            {
                return [];
            }

            var rowCount = range.RowCount();
            var columnCount = range.ColumnCount();
            if (rowCount > _options.MaxRowsPerWorksheet)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.RowLimitExceeded,
                    path,
                    "worksheet row count",
                    nameof(_options.MaxRowsPerWorksheet),
                    rowCount,
                    _options.MaxRowsPerWorksheet,
                    worksheet.Name);
            }

            if (columnCount > _options.MaxColumnsPerWorksheet)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.ColumnLimitExceeded,
                    path,
                    "worksheet column count",
                    nameof(_options.MaxColumnsPerWorksheet),
                    columnCount,
                    _options.MaxColumnsPerWorksheet,
                    worksheet.Name);
            }

            var cellCount = checked((long)rowCount * columnCount);
            if (cellCount > _options.MaxCellsPerWorksheet)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.CellLimitExceeded,
                    path,
                    "worksheet cell count",
                    nameof(_options.MaxCellsPerWorksheet),
                    cellCount,
                    _options.MaxCellsPerWorksheet,
                    worksheet.Name);
            }

            return MapRows(
                ReadWorkbookRows(worksheet, range, path, cancellationToken),
                path,
                isWorkbookImport: true,
                startingRowNumber,
                cancellationToken);
        }
        catch (ImportBoundaryException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw ImportBoundaryException.FileAccess(path, ex);
        }
        catch (IOException ex)
        {
            throw ImportBoundaryException.Malformed(
                ImportBoundaryFailureKind.MalformedWorkbook,
                path,
                "the workbook could not be read",
                ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw ImportBoundaryException.Malformed(
                ImportBoundaryFailureKind.MalformedWorkbook,
                path,
                "the workbook could not be read",
                ex);
        }
    }

    private FileStream OpenInputStream(string path)
    {
        try
        {
            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);
            if (stream.Length > _options.MaxFileBytes)
            {
                var observedLength = stream.Length;
                stream.Dispose();
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.FileTooLarge,
                    path,
                    "file size in bytes",
                    nameof(_options.MaxFileBytes),
                    observedLength,
                    _options.MaxFileBytes);
            }

            return stream;
        }
        catch (ImportBoundaryException)
        {
            throw;
        }
        catch (FileNotFoundException ex)
        {
            throw ImportBoundaryException.FileNotFound(path, ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw ImportBoundaryException.FileNotFound(path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw ImportBoundaryException.FileAccess(path, ex);
        }
        catch (IOException ex)
        {
            throw ImportBoundaryException.FileAccess(path, ex);
        }
    }

    private void EnsureFileSize(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                throw ImportBoundaryException.FileNotFound(path);
            }

            if (fileInfo.Length > _options.MaxFileBytes)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.FileTooLarge,
                    path,
                    "file size in bytes",
                    nameof(_options.MaxFileBytes),
                    fileInfo.Length,
                    _options.MaxFileBytes);
            }
        }
        catch (ImportBoundaryException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw ImportBoundaryException.FileAccess(path, ex);
        }
        catch (IOException ex)
        {
            throw ImportBoundaryException.FileAccess(path, ex);
        }
    }

    private List<CostTransaction> MapRows(
        IEnumerable<IReadOnlyList<string>> rows,
        string path,
        bool isWorkbookImport,
        int startingRowNumber,
        CancellationToken cancellationToken)
    {
        using var enumerator = rows.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return [];
        }

        var header = enumerator.Current;
        if (header.Count == 0 || header.All(string.IsNullOrWhiteSpace))
        {
            throw ImportBoundaryException.Malformed(
                isWorkbookImport ? ImportBoundaryFailureKind.MalformedWorkbook : ImportBoundaryFailureKind.MalformedCsv,
                path,
                "the first row does not contain any column headings");
        }

        var headers = header
            .Select((value, index) => new { Key = NormaliseHeader(value), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Index);

        var imported = new List<CostTransaction>();
        while (enumerator.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = enumerator.Current;
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var transaction = new CostTransaction
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
                PoComments = Get(row, headers, "pocomments", "pocomm", "pocor", "pocomment"),
                SupplierName = Get(row, headers, "suppliername", "suppliern", "supplier"),
                Narrative1 = Get(row, headers, "narrative1", "narrative"),
                Narrative2 = Get(row, headers, "narrative2"),
                Narrative3 = Get(row, headers, "narrative3"),
                Who = Get(row, headers, "who"),
                EcmNumber = Get(row, headers, "ecmnumber", "ecmnum", "ecm"),
                ManualName = Get(row, headers, "manualname", "manualresource", "name")
            };

            // Exports can include filter descriptions or total rows below the
            // data range. Retaining those as transactions creates phantom costs.
            if (isWorkbookImport
                && (!FiscalPeriod.TryParseLabel(transaction.FyPeriod, out _, out _)
                    || string.IsNullOrWhiteSpace(transaction.TaskNumber)))
            {
                continue;
            }

            imported.Add(transaction);
        }

        return imported;
    }

    private IEnumerable<IReadOnlyList<string>> ReadWorkbookRows(
        IXLWorksheet worksheet,
        IXLRange range,
        string path,
        CancellationToken cancellationToken)
    {
        var lastColumn = range.ColumnCount();
        var rowCount = 0L;
        foreach (var row in range.RowsUsed())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowCount++;
            if (rowCount > _options.MaxRowsPerWorksheet)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.RowLimitExceeded,
                    path,
                    "worksheet row count",
                    nameof(_options.MaxRowsPerWorksheet),
                    rowCount,
                    _options.MaxRowsPerWorksheet,
                    worksheet.Name);
            }

            var values = new List<string>(lastColumn);
            for (var column = 1; column <= lastColumn; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var formatted = row.Cell(column).GetFormattedString();
                if (formatted.Length > _options.MaxCellCharacters)
                {
                    throw ImportBoundaryException.LimitExceeded(
                        ImportBoundaryFailureKind.CellCharacterLimitExceeded,
                        path,
                        "worksheet cell character count",
                        nameof(_options.MaxCellCharacters),
                        formatted.Length,
                        _options.MaxCellCharacters,
                        worksheet.Name);
                }

                values.Add(formatted.Trim());
            }

            yield return values;
        }
    }

    private IEnumerable<IReadOnlyList<string>> ReadCsvRows(
        TextReader reader,
        string path,
        CancellationToken cancellationToken)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldClosed = false;
        var fieldStarted = false;
        var recordNumber = 1L;
        var cellCount = 0L;

        void AppendCharacter(char character)
        {
            if (field.Length >= _options.MaxCellCharacters)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.CellCharacterLimitExceeded,
                    path,
                    "CSV cell character count",
                    nameof(_options.MaxCellCharacters),
                    field.Length + 1L,
                    _options.MaxCellCharacters);
            }

            field.Append(character);
        }

        void CompleteField()
        {
            if (row.Count >= _options.MaxColumnsPerWorksheet)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.ColumnLimitExceeded,
                    path,
                    "CSV column count",
                    nameof(_options.MaxColumnsPerWorksheet),
                    row.Count + 1L,
                    _options.MaxColumnsPerWorksheet);
            }

            cellCount++;
            if (cellCount > _options.MaxCellsPerWorksheet)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.CellLimitExceeded,
                    path,
                    "CSV cell count",
                    nameof(_options.MaxCellsPerWorksheet),
                    cellCount,
                    _options.MaxCellsPerWorksheet);
            }

            row.Add(field.ToString());
            field.Clear();
            fieldStarted = false;
            fieldClosed = false;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = reader.Read();
            if (next < 0)
            {
                break;
            }

            var character = (char)next;
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        AppendCharacter('"');
                    }
                    else
                    {
                        inQuotes = false;
                        fieldClosed = true;
                    }
                }
                else
                {
                    AppendCharacter(character);
                }

                continue;
            }

            if (fieldClosed)
            {
                if (character == ',')
                {
                    CompleteField();
                    continue;
                }

                if (character is '\r' or '\n')
                {
                    CompleteField();
                    if (character == '\r' && reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    if (recordNumber > _options.MaxRowsPerWorksheet)
                    {
                        throw ImportBoundaryException.LimitExceeded(
                            ImportBoundaryFailureKind.RowLimitExceeded,
                            path,
                            "CSV row count",
                            nameof(_options.MaxRowsPerWorksheet),
                            recordNumber,
                            _options.MaxRowsPerWorksheet);
                    }

                    yield return row;
                    recordNumber++;
                    row = [];
                    continue;
                }

                throw ImportBoundaryException.Malformed(
                    ImportBoundaryFailureKind.MalformedCsv,
                    path,
                    $"CSV record {recordNumber} has characters after a closing quote");
            }

            if (character == ',')
            {
                CompleteField();
                continue;
            }

            if (character is '\r' or '\n')
            {
                CompleteField();
                if (character == '\r' && reader.Peek() == '\n')
                {
                    reader.Read();
                }

                if (recordNumber > _options.MaxRowsPerWorksheet)
                {
                    throw ImportBoundaryException.LimitExceeded(
                        ImportBoundaryFailureKind.RowLimitExceeded,
                        path,
                        "CSV row count",
                        nameof(_options.MaxRowsPerWorksheet),
                        recordNumber,
                        _options.MaxRowsPerWorksheet);
                }

                yield return row;
                recordNumber++;
                row = [];
                continue;
            }

            if (character == '"')
            {
                if (fieldStarted || field.Length > 0)
                {
                    throw ImportBoundaryException.Malformed(
                        ImportBoundaryFailureKind.MalformedCsv,
                        path,
                        $"CSV record {recordNumber} contains an unexpected quote");
                }

                inQuotes = true;
                fieldStarted = true;
                continue;
            }

            AppendCharacter(character);
            fieldStarted = true;
        }

        if (inQuotes)
        {
            throw ImportBoundaryException.Malformed(
                ImportBoundaryFailureKind.MalformedCsv,
                path,
                $"CSV record {recordNumber} contains an unterminated quoted field");
        }

        if (fieldStarted || fieldClosed || row.Count > 0 || field.Length > 0)
        {
            CompleteField();
            if (recordNumber > _options.MaxRowsPerWorksheet)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.RowLimitExceeded,
                    path,
                    "CSV row count",
                    nameof(_options.MaxRowsPerWorksheet),
                    recordNumber,
                    _options.MaxRowsPerWorksheet);
            }

            yield return row;
        }
    }

    private static string Get(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headers,
        params string[] keys)
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

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static DateOnly? ParseDate(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var current))
        {
            return current;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant)
            ? invariant
            : null;
    }
}
