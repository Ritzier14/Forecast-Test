using System.IO;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// The limits applied before untrusted CSV or workbook content is turned into
/// application objects. The defaults are deliberately large enough for a
/// substantial transaction export, while still bounding the amount of data a
/// desktop import can materialise.
/// </summary>
public sealed record ImportBoundaryOptions
{
    public const long DefaultMaxFileBytes = 128L * 1024 * 1024;
    public const long DefaultMaxWorkbookUncompressedBytes = 512L * 1024 * 1024;
    public const int DefaultMaxWorksheets = 32;
    public const int DefaultMaxRowsPerWorksheet = 250_000;
    public const int DefaultMaxColumnsPerWorksheet = 512;
    public const long DefaultMaxCellsPerWorksheet = 5_000_000;
    public const int DefaultMaxCellCharacters = 1_000_000;

    public static ImportBoundaryOptions Default { get; } = new();

    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;
    public long MaxWorkbookUncompressedBytes { get; init; } = DefaultMaxWorkbookUncompressedBytes;
    public int MaxWorksheets { get; init; } = DefaultMaxWorksheets;
    public int MaxRowsPerWorksheet { get; init; } = DefaultMaxRowsPerWorksheet;
    public int MaxColumnsPerWorksheet { get; init; } = DefaultMaxColumnsPerWorksheet;
    public long MaxCellsPerWorksheet { get; init; } = DefaultMaxCellsPerWorksheet;
    public int MaxCellCharacters { get; init; } = DefaultMaxCellCharacters;

    internal void Validate()
    {
        if (MaxFileBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFileBytes), MaxFileBytes, "The file-size limit must be greater than zero.");
        }

        if (MaxWorkbookUncompressedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxWorkbookUncompressedBytes), MaxWorkbookUncompressedBytes, "The workbook uncompressed-size limit must be greater than zero.");
        }

        if (MaxWorksheets <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxWorksheets), MaxWorksheets, "The worksheet limit must be greater than zero.");
        }

        if (MaxRowsPerWorksheet <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRowsPerWorksheet), MaxRowsPerWorksheet, "The row limit must be greater than zero.");
        }

        if (MaxColumnsPerWorksheet <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxColumnsPerWorksheet), MaxColumnsPerWorksheet, "The column limit must be greater than zero.");
        }

        if (MaxCellsPerWorksheet <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxCellsPerWorksheet), MaxCellsPerWorksheet, "The cell limit must be greater than zero.");
        }

        if (MaxCellCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxCellCharacters), MaxCellCharacters, "The cell-character limit must be greater than zero.");
        }
    }
}

public enum ImportBoundaryFailureKind
{
    FileNotFound,
    FileTooLarge,
    WorkbookUncompressedSizeExceeded,
    UnsupportedFileType,
    MalformedCsv,
    MalformedWorkbook,
    WorksheetLimitExceeded,
    RowLimitExceeded,
    ColumnLimitExceeded,
    CellLimitExceeded,
    CellCharacterLimitExceeded,
    FileAccess
}

/// <summary>
/// A typed failure at the CSV/workbook boundary. Callers can show the safe
/// message and use the structured limit fields without parsing exception text.
/// </summary>
public sealed class ImportBoundaryException : Exception
{
    public ImportBoundaryException(
        ImportBoundaryFailureKind failureKind,
        string message,
        string filePath,
        Exception? innerException = null,
        string? worksheetName = null,
        string? limitName = null,
        long? limitValue = null,
        long? observedValue = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        FilePath = filePath;
        WorksheetName = worksheetName;
        LimitName = limitName;
        LimitValue = limitValue;
        ObservedValue = observedValue;
    }

    public ImportBoundaryFailureKind FailureKind { get; }
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string? WorksheetName { get; }
    public string? LimitName { get; }
    public long? LimitValue { get; }
    public long? ObservedValue { get; }

    public static ImportBoundaryException LimitExceeded(
        ImportBoundaryFailureKind failureKind,
        string filePath,
        string resource,
        string limitName,
        long observedValue,
        long limitValue,
        string? worksheetName = null)
    {
        var worksheetSuffix = string.IsNullOrWhiteSpace(worksheetName)
            ? string.Empty
            : $" in worksheet '{Path.GetFileName(worksheetName)}'";
        var message = $"Import rejected for '{Path.GetFileName(filePath)}'{worksheetSuffix}: {resource} "
            + $"{observedValue:N0} exceeds the configured {limitName} limit of {limitValue:N0}. "
            + $"Select a smaller input or increase {limitName}.";
        return new ImportBoundaryException(
            failureKind,
            message,
            filePath,
            worksheetName: worksheetName,
            limitName: limitName,
            limitValue: limitValue,
            observedValue: observedValue);
    }

    public static ImportBoundaryException Malformed(
        ImportBoundaryFailureKind failureKind,
        string filePath,
        string detail,
        Exception? innerException = null)
    {
        return new ImportBoundaryException(
            failureKind,
            $"Import rejected for '{Path.GetFileName(filePath)}': {detail} Verify the file is a complete, supported CSV/XLSX/XLSM export and try again.",
            filePath,
            innerException);
    }

    public static ImportBoundaryException FileNotFound(string filePath, Exception? innerException = null)
    {
        return new ImportBoundaryException(
            ImportBoundaryFailureKind.FileNotFound,
            $"Import file '{Path.GetFileName(filePath)}' was not found. Choose an existing CSV, XLSX, or XLSM file.",
            filePath,
            innerException);
    }

    public static ImportBoundaryException FileAccess(string filePath, Exception innerException)
    {
        return new ImportBoundaryException(
            ImportBoundaryFailureKind.FileAccess,
            $"Import file '{Path.GetFileName(filePath)}' could not be opened. Close the file in other applications, check permissions, and try again.",
            filePath,
            innerException);
    }
}

/// <summary>
/// Spreadsheet-safe text encoding belongs at the export boundary. The source
/// model remains unchanged, and numeric CSV fields are never passed through
/// this encoder so legitimate negative numbers retain their numeric meaning.
/// </summary>
public static class SpreadsheetExportBoundary
{
    public static string EscapeCsvField(string value, bool neutralizeFormula = true)
    {
        ArgumentNullException.ThrowIfNull(value);
        var safeValue = neutralizeFormula ? NeutralizeFormula(value) : value;
        if (!safeValue.Contains(',')
            && !safeValue.Contains('"')
            && !safeValue.Contains('\n')
            && !safeValue.Contains('\r'))
        {
            return safeValue;
        }

        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
    }

    public static string NeutralizeFormula(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (IsIgnorableLeadingCharacter(character))
            {
                continue;
            }

            return character is '=' or '+' or '-' or '@'
                ? $"'{value}"
                : value;
        }

        return value;
    }

    private static bool IsIgnorableLeadingCharacter(char character)
    {
        return char.IsWhiteSpace(character)
            || char.IsControl(character)
            || character is '\uFEFF' or '\u200B' or '\u200C' or '\u200D' or '\u2060';
    }
}
