using System.Globalization;
using System.Text;

namespace PrimeTect.Infrastructure.OpeningReadings;

/// <summary>
/// Normalizes and validates raw opening-reading cell values. Bangla digits are normalized to ASCII, readings must
/// be non-negative within the stored precision, dates must be unambiguous <c>yyyy-MM-dd</c>, and the first billing
/// month must be <c>yyyy-MM</c>.
/// </summary>
public static class OpeningReadingValues
{
    /// <summary>Converts Bangla (and extended) digits to ASCII digits and trims surrounding whitespace.</summary>
    public static string NormalizeText(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            builder.Append(ch switch
            {
                >= '\u09E6' and <= '\u09EF' => (char)('0' + (ch - '\u09E6')),
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                '\u00A0' => ' ',
                _ => ch,
            });
        }

        return builder.ToString().Trim();
    }

    /// <summary>Strips spaces and thousands separators used inside numeric cells.</summary>
    private static string StripNumericSeparators(string value) => value.Replace(" ", string.Empty).Replace(",", string.Empty);

    /// <summary>Parses a non-negative reading with at most <see cref="OpeningReadingImportLimits.ReadingScale"/> decimals.</summary>
    public static bool TryParseReading(string? raw, out decimal value, out string? error)
    {
        value = 0m;
        error = null;
        var normalized = StripNumericSeparators(NormalizeText(raw));
        if (normalized.Length == 0)
        {
            error = "empty";
            return false;
        }

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            error = "not a number";
            return false;
        }

        if (parsed < 0m)
        {
            error = "negative";
            return false;
        }

        if (parsed > OpeningReadingImportLimits.MaxReading)
        {
            error = "out of range";
            return false;
        }

        if (decimal.Round(parsed, OpeningReadingImportLimits.ReadingScale) != parsed)
        {
            error = $"more than {OpeningReadingImportLimits.ReadingScale} decimal places";
            return false;
        }

        value = parsed;
        return true;
    }

    /// <summary>Parses an unambiguous calendar date in <c>yyyy-MM-dd</c> form.</summary>
    public static bool TryParseDate(string? raw, out DateOnly value)
    {
        value = default;
        var normalized = NormalizeText(raw);
        if (normalized.Length != 10 || normalized[4] != '-' || normalized[7] != '-')
        {
            return false;
        }

        return DateOnly.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    /// <summary>Parses a <c>yyyy-MM</c> month, returning its first day.</summary>
    public static bool TryParseMonth(string? raw, out DateOnly firstOfMonth)
    {
        firstOfMonth = default;
        var normalized = NormalizeText(raw);
        if (normalized.Length != 7 || normalized[4] != '-')
        {
            return false;
        }

        var digits = normalized.AsSpan();
        for (var i = 0; i < digits.Length; i++)
        {
            if (i == 4)
            {
                continue;
            }

            if (digits[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return DateOnly.TryParseExact($"{normalized}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out firstOfMonth);
    }

    /// <summary>Formats a reading for a template cell using the invariant culture.</summary>
    public static string FormatReading(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats a date for a template cell.</summary>
    public static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Formats a month for a template cell.</summary>
    public static string FormatMonth(DateOnly firstOfMonth) => firstOfMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
