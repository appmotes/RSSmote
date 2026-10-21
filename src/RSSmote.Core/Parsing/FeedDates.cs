using System.Globalization;
using System.Text.RegularExpressions;

namespace RSSmote.Core.Parsing;

/// <summary>
/// Lenient feed date parsing: ISO 8601 / RFC 3339 (Atom, JSON Feed, dc:date) and RFC 822 (RSS) with named zones.
/// A value without a zone is read as UTC.
/// </summary>
public static partial class FeedDates
{
    [GeneratedRegex(@"^(?:[A-Za-z]{2,9},?\s*)?(\d{1,2})\s+([A-Za-z]{3,9})\.?\s+(\d{2,4})(?:\s+(\d{1,2}):(\d{2})(?::(\d{2}))?)?\s*([A-Za-z]{1,5}|[+-]\d{2}:?\d{2})?")]
    private static partial Regex Rfc822();

    private static readonly Dictionary<string, int> Zones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GMT"] = 0, ["UT"] = 0, ["UTC"] = 0, ["Z"] = 0,
        ["EST"] = -5, ["EDT"] = -4, ["CST"] = -6, ["CDT"] = -5,
        ["MST"] = -7, ["MDT"] = -6, ["PST"] = -8, ["PDT"] = -7,
        ["CET"] = 1, ["CEST"] = 2, ["BST"] = 1, ["IST"] = 5,
    };

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    public static DateTimeOffset? Parse(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        if (char.IsDigit(text[0]) && text.Length >= 10 && text[4] == '-'
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var iso))
            return iso;

        var m = Rfc822().Match(text);
        if (!m.Success)
            return null;

        var month = Array.IndexOf(Months, m.Groups[2].Value[..3].ToLowerInvariant()) + 1;
        if (month == 0)
            return null;
        var year = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        if (year < 100)
            year += year < 50 ? 2000 : 1900;
        var day = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var hour = m.Groups[4].Success ? int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        var minute = m.Groups[5].Success ? int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) : 0;
        var second = m.Groups[6].Success ? int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture) : 0;

        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, Offset(m.Groups[7].Value));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static TimeSpan Offset(string zone)
    {
        if (string.IsNullOrEmpty(zone))
            return TimeSpan.Zero;
        if (Zones.TryGetValue(zone, out var hours))
            return TimeSpan.FromHours(hours);
        if (zone[0] is '+' or '-')
        {
            var digits = zone[1..].Replace(":", "");
            if (digits.Length == 4
                && int.TryParse(digits[..2], CultureInfo.InvariantCulture, out var h)
                && int.TryParse(digits[2..], CultureInfo.InvariantCulture, out var min))
            {
                var span = new TimeSpan(h, min, 0);
                return zone[0] == '-' ? -span : span;
            }
        }
        return TimeSpan.Zero;
    }
}
