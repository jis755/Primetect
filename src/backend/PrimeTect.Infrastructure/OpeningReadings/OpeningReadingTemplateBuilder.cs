using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;

namespace PrimeTect.Infrastructure.OpeningReadings;

/// <summary>
/// Builds the downloadable opening-reading templates. The CSV template is UTF-8 with a BOM and RFC 4180 quoting;
/// the XLSX template has a <c>Readings</c> data sheet and a bilingual <c>Instructions</c> sheet with no formulas,
/// macros, or external links. Identifiers are written as text so spreadsheet software cannot reformat them.
/// </summary>
public static class OpeningReadingTemplateBuilder
{
    /// <summary>Builds the CSV template bytes (UTF-8 BOM, RFC 4180).</summary>
    public static byte[] BuildCsv(OpeningReadingTemplateContext context)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var header in OpeningReadingTemplateColumns.All)
            {
                csv.WriteField(header);
            }

            csv.NextRecord();

            foreach (var row in context.Rows)
            {
                WriteCsvRecord(csv, row);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Builds the XLSX template bytes.</summary>
    public static byte[] BuildXlsx(OpeningReadingTemplateContext context)
    {
        using var workbook = new XLWorkbook();

        var readings = workbook.AddWorksheet("Readings");
        WriteReadingsSheet(readings, context);

        var instructions = workbook.AddWorksheet("Instructions");
        WriteInstructionsSheet(instructions, context);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteCsvRecord(CsvWriter csv, OpeningReadingTemplateRow row)
    {
        csv.WriteField(row.TemplateVersion.ToString(CultureInfo.InvariantCulture));
        csv.WriteField(Sanitize(row.PropertyId.ToString()));
        csv.WriteField(Sanitize(row.PropertyName));
        csv.WriteField(Sanitize(row.UnitId.ToString()));
        csv.WriteField(Sanitize(row.UnitName));
        csv.WriteField(row.MeterInstallationId?.ToString() ?? string.Empty);
        csv.WriteField(Sanitize(row.MeterType));
        csv.WriteField(row.OpeningReading);
        csv.WriteField(row.ReadingDate);
        csv.WriteField(row.FirstBillingMonth);
        csv.NextRecord();
    }

    private static void WriteReadingsSheet(IXLWorksheet sheet, OpeningReadingTemplateContext context)
    {
        for (var column = 0; column < OpeningReadingTemplateColumns.All.Length; column++)
        {
            var cell = sheet.Cell(1, column + 1);
            cell.Value = OpeningReadingTemplateColumns.All[column];
            cell.Style.Font.Bold = true;
            cell.Style.NumberFormat.Format = "@";
        }

        var rowIndex = 2;
        foreach (var row in context.Rows)
        {
            SetText(sheet.Cell(rowIndex, 1), row.TemplateVersion.ToString(CultureInfo.InvariantCulture));
            SetText(sheet.Cell(rowIndex, 2), row.PropertyId.ToString());
            SetText(sheet.Cell(rowIndex, 3), row.PropertyName);
            SetText(sheet.Cell(rowIndex, 4), row.UnitId.ToString());
            SetText(sheet.Cell(rowIndex, 5), row.UnitName);
            SetText(sheet.Cell(rowIndex, 6), row.MeterInstallationId?.ToString() ?? string.Empty);
            SetText(sheet.Cell(rowIndex, 7), row.MeterType);
            SetText(sheet.Cell(rowIndex, 8), row.OpeningReading);
            SetText(sheet.Cell(rowIndex, 9), row.ReadingDate);
            SetText(sheet.Cell(rowIndex, 10), row.FirstBillingMonth);
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteInstructionsSheet(IXLWorksheet sheet, OpeningReadingTemplateContext context)
    {
        var lines = new (string English, string Bangla)[]
        {
            ("Opening electricity reading import", "প্রারম্ভিক বিদ্যুৎ রিডিং আমদানি"),
            ("Property", "প্রপার্টি"),
            ($"{context.PropertyName} ({context.PropertyId})", $"{context.PropertyName} ({context.PropertyId})"),
            ("Default first billing month", "ডিফল্ট প্রথম বিলিং মাস"),
            (context.FirstBillingMonth, context.FirstBillingMonth),
            ("Template version", "টেমপ্লেট সংস্করণ"),
            (context.TemplateVersion.ToString(CultureInfo.InvariantCulture), context.TemplateVersion.ToString(CultureInfo.InvariantCulture)),
            ("Fill opening_reading with the meter's baseline. Leave blank if there is no baseline to record.", "মিটারের প্রারম্ভিক রিডিং opening_reading-এ লিখুন। রিডিং না থাকলে খালি রাখুন।"),
            ("Enter zero as 0 when the meter legitimately started at zero. Blank means missing, not zero.", "মিটার সত্যিই শূন্য থেকে শুরু হলে 0 লিখুন। খালি মানে অনুপস্থিত, শূন্য নয়।"),
            ("Use reading_date in YYYY-MM-DD form; its month must match first_billing_month.", "reading_date অবশ্যই YYYY-MM-DD আকারে দিন; এর মাস first_billing_month-এর সাথে মিলতে হবে।"),
            ("Use first_billing_month in YYYY-MM form.", "first_billing_month অবশ্যই YYYY-MM আকারে দিন।"),
            ("Match rows by unit_id and meter_installation_id. Do not edit or rename identifier columns.", "unit_id এবং meter_installation_id দিয়ে সারি মেলানো হয়। আইডি কলাম সম্পাদনা বা নাম পরিবর্তন করবেন না।"),
            ("Do not add, reorder, or rename columns, and do not add sheets.", "কলাম যোগ, পুনর্বিন্যাস বা নাম পরিবর্তন করবেন না, এবং নতুন শিট যোগ করবেন না।"),
            ("This template records opening baselines only. It is not an approved monthly reading.", "এই টেমপ্লেট শুধু প্রারম্ভিক বেসলাইন সংরক্ষণ করে। এটি অনুমোদিত মাসিক রিডিং নয়।"),
            ("If any row is invalid or conflicts with existing readings, the whole file is rejected.", "কোনো সারি ভুল বা বিদ্যমান রিডিংয়ের সাথে সাংঘর্ষিক হলে পুরো ফাইল প্রত্যাখ্যাত হবে।"),
        };

        sheet.Cell(1, 1).Value = "English";
        sheet.Cell(1, 2).Value = "বাংলা";
        sheet.Row(1).Style.Font.Bold = true;

        for (var i = 0; i < lines.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = lines[i].English;
            sheet.Cell(i + 2, 2).Value = lines[i].Bangla;
            sheet.Cell(i + 2, 1).Style.NumberFormat.Format = "@";
            sheet.Cell(i + 2, 2).Style.NumberFormat.Format = "@";
        }

        sheet.Column(1).Width = 70;
        sheet.Column(2).Width = 70;
    }

    private static void SetText(IXLCell cell, string value)
    {
        cell.Style.NumberFormat.Format = "@";
        cell.Value = Sanitize(value);
    }

    /// <summary>Neutralizes spreadsheet formula triggers in text values.</summary>
    internal static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value[0] switch
        {
            '=' or '+' or '@' or '\t' or '\r' => "'" + value,
            '-' => "'" + value,
            _ => value,
        };
    }
}
