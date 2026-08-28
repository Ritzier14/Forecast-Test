using System.IO.Compression;
using System.IO;
using System.Xml;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// Performs a streaming ZIP/XML check before ClosedXML materialises a
/// workbook. This keeps worksheet, row, and cell limits meaningful even when a
/// small compressed workbook contains a very large XML payload.
/// </summary>
internal static class WorkbookBoundaryPreflight
{
    private const string WorksheetPrefix = "xl/worksheets/";

    public static void Validate(
        string path,
        ImportBoundaryOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);
            if (stream.Length > options.MaxFileBytes)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.FileTooLarge,
                    path,
                    "file size in bytes",
                    nameof(options.MaxFileBytes),
                    stream.Length,
                    options.MaxFileBytes);
            }

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var worksheetEntries = archive.Entries
                .Where(IsWorksheetEntry)
                .ToList();
            if (worksheetEntries.Count > options.MaxWorksheets)
            {
                throw ImportBoundaryException.LimitExceeded(
                    ImportBoundaryFailureKind.WorksheetLimitExceeded,
                    path,
                    "worksheet count",
                    nameof(options.MaxWorksheets),
                    worksheetEntries.Count,
                    options.MaxWorksheets);
            }

            long uncompressedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Length <= 0)
                {
                    continue;
                }

                uncompressedBytes = checked(uncompressedBytes + entry.Length);
                if (uncompressedBytes > options.MaxWorkbookUncompressedBytes)
                {
                    throw ImportBoundaryException.LimitExceeded(
                        ImportBoundaryFailureKind.WorkbookUncompressedSizeExceeded,
                        path,
                        "workbook uncompressed size in bytes",
                        nameof(options.MaxWorkbookUncompressedBytes),
                        uncompressedBytes,
                        options.MaxWorkbookUncompressedBytes);
                }
            }

            foreach (var entry in worksheetEntries)
            {
                ValidateWorksheet(path, entry, options, cancellationToken);
            }
        }
        catch (ImportBoundaryException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException ex)
        {
            throw ImportBoundaryException.FileNotFound(path, ex);
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
                "the workbook container could not be read",
                ex);
        }
        catch (XmlException ex)
        {
            throw ImportBoundaryException.Malformed(
                ImportBoundaryFailureKind.MalformedWorkbook,
                path,
                "a worksheet contains invalid XML",
                ex);
        }
        catch (InvalidDataException ex)
        {
            throw ImportBoundaryException.Malformed(
                ImportBoundaryFailureKind.MalformedWorkbook,
                path,
                "the workbook is not a valid XLSX/XLSM container",
                ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw ImportBoundaryException.Malformed(
                ImportBoundaryFailureKind.MalformedWorkbook,
                path,
                "the workbook could not be validated",
                ex);
        }
    }

    private static void ValidateWorksheet(
        string path,
        ZipArchiveEntry entry,
        ImportBoundaryOptions options,
        CancellationToken cancellationToken)
    {
        var rowCount = 0L;
        var cellCount = 0L;
        using var stream = entry.Open();
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                XmlResolver = null
            });

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (string.Equals(reader.LocalName, "row", StringComparison.Ordinal))
            {
                rowCount++;
                if (rowCount > options.MaxRowsPerWorksheet)
                {
                    throw ImportBoundaryException.LimitExceeded(
                        ImportBoundaryFailureKind.RowLimitExceeded,
                        path,
                        "worksheet row count",
                        nameof(options.MaxRowsPerWorksheet),
                        rowCount,
                        options.MaxRowsPerWorksheet,
                        entry.FullName);
                }
            }
            else if (string.Equals(reader.LocalName, "c", StringComparison.Ordinal))
            {
                cellCount++;
                if (cellCount > options.MaxCellsPerWorksheet)
                {
                    throw ImportBoundaryException.LimitExceeded(
                        ImportBoundaryFailureKind.CellLimitExceeded,
                        path,
                        "worksheet cell count",
                        nameof(options.MaxCellsPerWorksheet),
                        cellCount,
                        options.MaxCellsPerWorksheet,
                        entry.FullName);
                }
            }
        }
    }

    private static bool IsWorksheetEntry(ZipArchiveEntry entry)
    {
        var name = entry.FullName.Replace('\\', '/');
        if (!name.StartsWith(WorksheetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativeName = name[WorksheetPrefix.Length..];
        return relativeName.Length > 0
            && !relativeName.Contains('/')
            && relativeName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }
}
