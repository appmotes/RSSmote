using System.Globalization;

namespace RSSmote.Server;

/// <summary>
/// The strings the Reader API protocol requires verbatim (A25). They are the only place they appear: callers use
/// these names.
/// </summary>
public static class ReaderIds
{
    public const string AuthScheme = "GoogleLogin";

    private const string State = "user/-/state/com.google/";
    public const string ReadingList = State + "reading-list";
    public const string Read = State + "read";
    public const string Starred = State + "starred";

    public const string LabelPrefix = "user/-/label/";
    public const string FeedPrefix = "feed/";
    private const string ItemPrefix = "tag:google.com,2005:reader/item/";

    public static string Label(string title) => LabelPrefix + title;

    public static string Feed(long id) => FeedPrefix + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>The long item id: the prefix and the id as 16 hex digits.</summary>
    public static string LongItem(long id) => ItemPrefix + id.ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>
    /// An item id in the long form, as bare hex, or as a decimal number; null when it is none of them. Only digits
    /// with no leading zero are decimal: anything else, a zero-padded 16-digit id among it, is hex.
    /// </summary>
    public static long? ParseItem(string value)
    {
        var text = value.StartsWith(ItemPrefix, StringComparison.Ordinal) ? value[ItemPrefix.Length..] : value;
        if (text.Length > 0 && text[0] != '0' && text.All(char.IsAsciiDigit) && text.Length == value.Length)
        {
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        return text.Length is > 0 and <= 16
            && long.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)
            ? hex
            : null;
    }

    /// <summary>A stream id with its user part as <c>-</c>, as a client may name its own user id there.</summary>
    public static string Normalize(string stream)
    {
        if (!stream.StartsWith("user/", StringComparison.Ordinal))
        {
            return stream;
        }

        var end = stream.IndexOf('/', "user/".Length);
        return end < 0 ? stream : "user/-" + stream[end..];
    }
}
