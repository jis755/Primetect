using System.Globalization;
using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;

namespace PrimeTect.Infrastructure.OpeningReadings;

/// <summary>
/// Parses an uploaded opening-reading template and enforces file-level safety limits. It reads raw string values
/// only (no evaluation of formulas), rejects macro-enabled, encrypted, or externally-linked workbooks, and never
/// trusts name columns for identity.
/// </summary>
public static class OpeningReadingFileParser
{
    public static OpeningReadingFileParseResult Parse(byte[] content, string fileName)
    {
        if (content is null || content.Length == 0)
        {
            return new([], "The file is empty.");
        }

        if (content.LongLength > OpeningReadingImportLimits.MaxFileBytes)
        {
            return new([], $"The file is larger than the {OpeningReadingImportLimits.MaxFileBytes / (1024 * 1024)} MB limit.");
        }

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ParseCsv(content),
            ".xlsx" => ParseXlsx(content),
            _ => new([], "Unsupported file type. Upload a .csv or .xlsx template."),
        };
    }

    private static OpeningReadingFileParseResult ParseCsv(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!csv.Read())
        {
            return new([], "The file has no header row.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        if (!TryMapColumns(headers, out var map, out var headerError))
        {
            return new([], headerError);
        }

        var columnCount = OpeningReadingTemplateColumns.All.Length;
        var rows = new List<ParsedOpeningReadingRow>();
        var rowNumber = 1;

        while (csv.Read())
        {
            rowNumber++;
            var values = new string[columnCount];
            var any = false;
            for (var logical = 0; logical < columnCount; logical++)
            {
                var value = map[logical] >= 0 && csv.TryGetField<string>(map[logical], out var field)
                    ? field ?? string.Empty
                    : string.Empty;
                if (value.Length > OpeningReadingImportLimits.MaxCellLength)
                {
                    return new([], $"A cell on row {rowNumber} exceeds the {OpeningReadingImportLimits.MaxCellLength} character limit.");
                }

                if (ContainsFormulaTrigger(value))
                {
                    return new([], $"A cell on row {rowNumber} looks like a spreadsheet formula and was rejected.");
                }

                values[logical] = value;
                any |= value.Trim().Length > 0;
            }

            if (!any)
            {
                continue;
            }

            if (rows.Count >= OpeningReadingImportLimits.MaxRows)
            {
                return new([], $"The file has more than the allowed {OpeningReadingImportLimits.MaxRows} data rows.");
            }

            rows.Add(BuildRow(rowNumber, values));
        }

        return new(rows, null);
    }

    private static OpeningReadingFileParseResult ParseXlsx(byte[] content)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            if (archive.Entries.Count > OpeningReadingImportLimits.MaxZipEntries)
            {
                return new([], "The workbook has too many internal parts to be a plain template.");
            }

            long totalLength = 0;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.ToLowerInvariant();
                if (name.Contains("vbaproject") || name.Contains("externallink"))
                {
                    return new([], "Macros and external links are not supported in the template.");
                }

                totalLength += entry.Length;
                if (totalLength > OpeningReadingImportLimits.MaxDecompressedBytes)
                {
                    return new([], "The workbook is too large when expanded.");
                }
            }
        }
        catch (InvalidDataException)
        {
            return new([], "The upload is not a readable .xlsx workbook (it may be encrypted or corrupt).");
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(content));
        }
        catch (Exception)
        {
            return new([], "The upload could not be opened as an .xlsx workbook (it may be encrypted, password protected, or corrupt).");
        }

        using (workbook)
        {
            if (!workbook.TryGetWorksheet("Readings", out var sheet))
            {
                return new([], "The workbook must contain a sheet named 'Readings'.");
            }

            var range = sheet.RangeUsed();
            if (range is null)
            {
                return new([], "The 'Readings' sheet is empty.");
            }

            if (range.RowCount() > OpeningReadingImportLimits.MaxRows + 1)
            {
                return new([], $"The 'Readings' sheet has more than the allowed {OpeningReadingImportLimits.MaxRows} data rows.");
            }

            if (range.ColumnCount() > OpeningReadingTemplateColumns.All.Length)
            {
                return new([], "The 'Readings' sheet has unexpected extra columns. Download a fresh template.");
            }

            foreach (var cell in range.Cells())
            {
                if (cell.HasFormula)
                {
                    return new([], "Formula cells are not allowed. Enter plain values and download a fresh template if needed.");
                }
            }

            var headerRowNumber = range.FirstRow().RowNumber();
            var firstColumn = range.FirstColumn().ColumnNumber();
            var lastColumn = range.LastColumn().ColumnNumber();
            var headers = new List<string>();
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                headers.Add(ReadCell(sheet.Cell(headerRowNumber, column)));
            }

            if (!TryMapColumns(headers, out var map, out var headerError))
            {
                return new([], headerError);
            }

            var rows = new List<ParsedOpeningReadingRow>();
            var lastRowNumber = range.LastRow().RowNumber();
            for (var row = headerRowNumber + 1; row <= lastRowNumber; row++)
            {
                var values = new string[OpeningReadingTemplateColumns.All.Length];
                var any = false;
                for (var logical = 0; logical < values.Length; logical++)
                {
                    var value = string.Empty;
                    if (map[logical] >= 0)
                    {
                        value = ReadCell(sheet.Cell(row, firstColumn + map[logical]));
                    }

                    if (value.Length > OpeningReadingImportLimits.MaxCellLength)
                    {
                        return new([], $"A cell on row {row} exceeds the {OpeningReadingImportLimits.MaxCellLength} character limit.");
                    }

                    values[logical] = value;
                    any |= value.Trim().Length > 0;
                }

                if (!any)
                {
                    continue;
                }

                if (rows.Count >= OpeningReadingImportLimits.MaxRows)
                {
                    return new([], $"The file has more than the allowed {OpeningReadingImportLimits.MaxRows} data rows.");
                }

                rows.Add(BuildRow(row, values));
            }

            return new(rows, null);
        }
    }

    private static bool TryMapColumns(IReadOnlyList<string> headers, out int[] map, out string? error)
    {
        map = new int[OpeningReadingTemplateColumns.All.Length];
        error = null;

        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var name = (headers[i] ?? string.Empty).Trim().TrimStart('\uFEFF');
            if (name.Length == 0)
            {
                continue;
            }

            normalized[name] = i;
        }

        var missing = new List<string>();
        for (var logical = 0; logical < OpeningReadingTemplateColumns.All.Length; logical++)
        {
            if (normalized.TryGetValue(OpeningReadingTemplateColumns.All[logical], out var index))
            {
                map[logical] = index;
            }
            else
            {
                map[logical] = -1;
                missing.Add(OpeningReadingTemplateColumns.All[logical]);
            }
        }

        if (missing.Count > 0)
        {
            error = $"The header row is missing required column(s): {string.Join(", ", missing)}. Download a fresh template.";
            return false;
        }

        return true;
    }

    private static ParsedOpeningReadingRow BuildRow(int rowNumber, string[] values) =>
        new(
            rowNumber,
            values[IndexOf(OpeningReadingTemplateColumns.TemplateVersion)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.PropertyId)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.PropertyName)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.UnitId)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.UnitName)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.MeterInstallationId)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.MeterType)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.OpeningReading)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.ReadingDate)].Trim(),
            values[IndexOf(OpeningReadingTemplateColumns.FirstBillingMonth)].Trim());

    private static int IndexOf(string column) => Array.IndexOf(OpeningReadingTemplateColumns.All, column);

    private static bool ContainsFormulaTrigger(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '@' or '\t' or '\r';

    private static string ReadCell(IXLCell cell)
    {
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Text => value.GetText(),
            XLDataType.Number => value.GetNumber().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
            XLDataType.Blank => string.Empty,
            _ => cell.GetString(),
        };
    }
}
