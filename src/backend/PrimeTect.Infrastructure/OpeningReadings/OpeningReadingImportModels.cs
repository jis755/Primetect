namespace PrimeTect.Infrastructure.OpeningReadings;

/// <summary>
/// Documented, enforced bounds for opening-reading import files. They apply to the raw upload size, the number
/// of data rows, individual cell length, and the decompressed size and entry count of an XLSX container.
/// </summary>
public static class OpeningReadingImportLimits
{
    /// <summary>Maximum raw uploaded file size (5 MB).</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Maximum number of opening-reading data rows accepted in a single file.</summary>
    public const int MaxRows = 500;

    /// <summary>Maximum length of a single cell value.</summary>
    public const int MaxCellLength = 200;

    /// <summary>Maximum total decompressed size of an XLSX container (20 MB).</summary>
    public const long MaxDecompressedBytes = 20L * 1024 * 1024;

    /// <summary>Maximum number of entries in an XLSX container.</summary>
    public const int MaxZipEntries = 200;

    /// <summary>Decimal scale accepted for an opening reading (matches the stored 18,4 precision).</summary>
    public const int ReadingScale = 4;

    /// <summary>Largest opening reading accepted (14 integer digits, matching the stored precision headroom).</summary>
    public const decimal MaxReading = 99999999999999.9999m;

    /// <summary>Template format version emitted by the generator and required by the parser.</summary>
    public const int TemplateVersion = 1;

    /// <summary>How long a validated preview remains eligible for commit.</summary>
    public static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Accepted upload extensions.</summary>
    public static readonly string[] AllowedExtensions = [".csv", ".xlsx"];
}

/// <summary>One row of a generated opening-reading template. Identifiers are always stable IDs.</summary>
public sealed record OpeningReadingTemplateRow(
    int TemplateVersion,
    Guid PropertyId,
    string PropertyName,
    Guid UnitId,
    string UnitName,
    Guid? MeterInstallationId,
    string MeterType,
    string OpeningReading,
    string ReadingDate,
    string FirstBillingMonth);

/// <summary>Everything a template generator needs: the scope and the eligible rows.</summary>
public sealed record OpeningReadingTemplateContext(
    int TemplateVersion,
    Guid PropertyId,
    string PropertyName,
    string FirstBillingMonth,
    IReadOnlyList<OpeningReadingTemplateRow> Rows);

/// <summary>One raw parsed data row. All fields are raw strings; the caller normalizes and validates them.</summary>
public sealed record ParsedOpeningReadingRow(
    int RowNumber,
    string TemplateVersion,
    string PropertyId,
    string PropertyName,
    string UnitId,
    string UnitName,
    string MeterInstallationId,
    string MeterType,
    string OpeningReading,
    string ReadingDate,
    string FirstBillingMonth);

/// <summary>The outcome of parsing an upload. A fatal error means the whole file is rejected.</summary>
public sealed record OpeningReadingFileParseResult(
    IReadOnlyList<ParsedOpeningReadingRow> Rows,
    string? FatalError);

/// <summary>The canonical column names shared by the CSV and XLSX templates.</summary>
public static class OpeningReadingTemplateColumns
{
    public const string TemplateVersion = "template_version";
    public const string PropertyId = "property_id";
    public const string PropertyName = "property_name";
    public const string UnitId = "unit_id";
    public const string UnitName = "unit_name";
    public const string MeterInstallationId = "meter_installation_id";
    public const string MeterType = "meter_type";
    public const string OpeningReading = "opening_reading";
    public const string ReadingDate = "reading_date";
    public const string FirstBillingMonth = "first_billing_month";

    public static readonly string[] All =
    [
        TemplateVersion,
        PropertyId,
        PropertyName,
        UnitId,
        UnitName,
        MeterInstallationId,
        MeterType,
        OpeningReading,
        ReadingDate,
        FirstBillingMonth,
    ];
}
