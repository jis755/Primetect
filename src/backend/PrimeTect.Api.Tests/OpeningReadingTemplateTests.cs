using System.Text;
using ClosedXML.Excel;
using PrimeTect.Infrastructure.OpeningReadings;
using Xunit;

namespace PrimeTect.Api.Tests;

/// <summary>
/// Fast, non-HTTP coverage of the template builder, parser, and value normalization.
/// </summary>
public sealed class OpeningReadingTemplateTests
{
    private const string HeaderLine = "template_version,property_id,property_name,unit_id,unit_name,meter_installation_id,meter_type,opening_reading,reading_date,first_billing_month";

    [Fact]
    public void Csv_template_round_trips_with_utf8_bom_quoting_and_bangla_text()
    {
        var propertyId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var context = new OpeningReadingTemplateContext(1, propertyId, "Apt, \"Tower\" 0", "2026-02",
        [
            new OpeningReadingTemplateRow(1, propertyId, "Apt, \"Tower\" 0", unitId, "রুম ৫", installationId, "Electricity", string.Empty, string.Empty, "2026-02"),
            new OpeningReadingTemplateRow(1, propertyId, "Apt, \"Tower\" 0", Guid.NewGuid(), "Room, B", null, "Electricity", string.Empty, string.Empty, "2026-02"),
        ]);

        var bytes = OpeningReadingTemplateBuilder.BuildCsv(context);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

        var parsed = OpeningReadingFileParser.Parse(bytes, "opening-readings.csv");
        Assert.Null(parsed.FatalError);
        Assert.Equal(2, parsed.Rows.Count);
        Assert.Equal(unitId.ToString(), parsed.Rows[0].UnitId);
        Assert.Equal("রুম ৫", parsed.Rows[0].UnitName);
        Assert.Equal(installationId.ToString(), parsed.Rows[0].MeterInstallationId);
        Assert.Equal("Apt, \"Tower\" 0", parsed.Rows[0].PropertyName);
        Assert.Equal("Room, B", parsed.Rows[1].UnitName);
        Assert.Equal(string.Empty, parsed.Rows[1].MeterInstallationId);
    }

    [Fact]
    public void Csv_writer_neutralizes_formula_triggers()
    {
        var context = new OpeningReadingTemplateContext(1, Guid.NewGuid(), "=SUM(A1)", "2026-02",
        [
            new OpeningReadingTemplateRow(1, Guid.NewGuid(), "=SUM(A1)", Guid.NewGuid(), "+Room", null, "Electricity", string.Empty, string.Empty, "2026-02"),
        ]);

        var bytes = OpeningReadingTemplateBuilder.BuildCsv(context);
        var parsed = OpeningReadingFileParser.Parse(bytes, "opening-readings.csv");

        Assert.Null(parsed.FatalError);
        Assert.Equal("'=SUM(A1)", parsed.Rows[0].PropertyName);
        Assert.Equal("'+Room", parsed.Rows[0].UnitName);
    }

    [Fact]
    public void Xlsx_template_has_readings_and_bilingual_instructions_with_no_formulas()
    {
        var propertyId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = new OpeningReadingTemplateContext(1, propertyId, "Tower", "2026-02",
        [
            new OpeningReadingTemplateRow(1, propertyId, "Tower", unitId, "রুম ৫", null, "Electricity", "12.5", "2026-02-05", "2026-02"),
        ]);

        var bytes = OpeningReadingTemplateBuilder.BuildXlsx(context);

        using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
        {
            Assert.True(workbook.TryGetWorksheet("Readings", out var readings));
            Assert.True(workbook.TryGetWorksheet("Instructions", out var instructions));

            var used = readings.RangeUsed();
            Assert.NotNull(used);
            Assert.Null(used!.Cells().FirstOrDefault(cell => cell.HasFormula));

            // Identifiers and readings are stored as text so spreadsheet software cannot reformat them.
            Assert.Equal(XLDataType.Text, readings.Cell(2, 2).Value.Type);
            Assert.Equal(unitId.ToString(), readings.Cell(2, 4).GetString());
            Assert.Equal("12.5", readings.Cell(2, 8).GetString());

            var instructionText = instructions.CellsUsed().Select(cell => cell.GetString()).ToList();
            Assert.Contains("Opening electricity reading import", instructionText);
            Assert.Contains(instructionText, value => value.Contains("প্রারম্ভিক", StringComparison.Ordinal));
            Assert.Contains(instructionText, value => value.Contains("প্রারম্ভিক", StringComparison.Ordinal));
        }

        var parsed = OpeningReadingFileParser.Parse(bytes, "opening-readings.xlsx");
        Assert.Null(parsed.FatalError);
        Assert.Single(parsed.Rows);
        Assert.Equal("12.5", parsed.Rows[0].OpeningReading);
        Assert.Equal("2026-02-05", parsed.Rows[0].ReadingDate);
        Assert.Equal(unitId.ToString(), parsed.Rows[0].UnitId);
    }

    [Fact]
    public void Xlsx_with_formula_cells_is_rejected()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Readings");
        for (var column = 0; column < OpeningReadingTemplateColumns.All.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = OpeningReadingTemplateColumns.All[column];
        }

        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 2).Value = Guid.NewGuid().ToString();
        sheet.Cell(2, 8).FormulaA1 = "1+1";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var parsed = OpeningReadingFileParser.Parse(stream.ToArray(), "opening-readings.xlsx");
        Assert.NotNull(parsed.FatalError);
        Assert.Contains("Formula", parsed.FatalError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_workbook_bytes_with_xlsx_extension_are_rejected()
    {
        var parsed = OpeningReadingFileParser.Parse(Encoding.UTF8.GetBytes("definitely not a workbook"), "broken.xlsx");
        Assert.NotNull(parsed.FatalError);
        Assert.Contains("xlsx", parsed.FatalError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Csv_with_a_formula_trigger_is_rejected()
    {
        var unitId = Guid.NewGuid();
        var csv = $"{HeaderLine}\n1,{Guid.NewGuid()},Property,{unitId},=cmd,,Electricity,5,2026-02-05,2026-02\n";
        var parsed = OpeningReadingFileParser.Parse(Encoding.UTF8.GetBytes(csv), "opening-readings.csv");
        Assert.NotNull(parsed.FatalError);
        Assert.Contains("formula", parsed.FatalError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Csv_missing_headers_and_oversized_files_are_rejected()
    {
        var missingHeader = $"unit_id,opening_reading\n{Guid.NewGuid()},5\n";
        Assert.NotNull(OpeningReadingFileParser.Parse(Encoding.UTF8.GetBytes(missingHeader), "opening-readings.csv").FatalError);

        var builder = new StringBuilder();
        builder.Append(HeaderLine).Append('\n');
        for (var index = 0; index <= OpeningReadingImportLimits.MaxRows; index++)
        {
            builder.Append($"1,{Guid.NewGuid()},Property,{Guid.NewGuid()},Unit,,Electricity,5,2026-02-05,2026-02\n");
        }

        var parsed = OpeningReadingFileParser.Parse(Encoding.UTF8.GetBytes(builder.ToString()), "opening-readings.csv");
        Assert.NotNull(parsed.FatalError);
        Assert.Contains(OpeningReadingImportLimits.MaxRows.ToString(), parsed.FatalError!);
    }

    [Theory]
    [InlineData("১২৩.৫", 123.5)]
    [InlineData("0", 0)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("  42  ", 42)]
    public void Reading_normalization_accepts_bangla_digits_and_separators(string raw, decimal expected)
    {
        Assert.True(OpeningReadingValues.TryParseReading(raw, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1.23456")]
    [InlineData("abc")]
    [InlineData("1e3")]
    [InlineData("100000000000000")]
    public void Reading_normalization_rejects_invalid_values(string raw)
    {
        Assert.False(OpeningReadingValues.TryParseReading(raw, out _, out _));
    }

    [Fact]
    public void Date_and_month_parsing_is_strict_and_normalizes_bangla_digits()
    {
        Assert.True(OpeningReadingValues.TryParseDate("২০২৬-০২-০৫", out var date));
        Assert.Equal(new DateOnly(2026, 2, 5), date);
        Assert.False(OpeningReadingValues.TryParseDate("05/02/2026", out _));
        Assert.False(OpeningReadingValues.TryParseDate("2026-02-30", out _));
        Assert.False(OpeningReadingValues.TryParseDate("2026-2-5", out _));

        Assert.True(OpeningReadingValues.TryParseMonth("2026-02", out var month));
        Assert.Equal(new DateOnly(2026, 2, 1), month);
        Assert.False(OpeningReadingValues.TryParseMonth("2026-13", out _));
        Assert.False(OpeningReadingValues.TryParseMonth("2026", out _));
    }
}
