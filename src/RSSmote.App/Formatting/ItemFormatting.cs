using System.Globalization;
using RSSmote.Core.Models;

namespace RSSmote.App.Formatting;

/// <summary>User-visible text for times, dates and counts. Formatting lives here, never in views.</summary>
public static class ItemFormatting
{
    /// <summary>"13.14" for today, "13 Oct" otherwise, in the user's culture.</summary>
    public static string TimeText(DateTimeOffset published, DateTimeOffset now)
    {
        var local = published.ToLocalTime();
        var culture = CultureInfo.CurrentCulture;
        return local.Date == now.ToLocalTime().Date
            ? local.ToString(culture.DateTimeFormat.ShortTimePattern, culture)
            : local.ToString("d MMM", culture);
    }

    /// <summary>Day heading: TODAY, YESTERDAY, or the full date in capitals.</summary>
    public static string DayHeader(DateTime day, DateTime today)
    {
        if (day == today)
            return "TODAY";
        if (day == today.AddDays(-1))
            return "YESTERDAY";
        var culture = CultureInfo.CurrentCulture;
        var pattern = day.Year == today.Year ? "dddd, d MMMM" : "dddd, d MMMM yyyy";
        return day.ToString(pattern, culture).ToUpper(culture);
    }

    /// <summary>"No Unread Items", "1 Unread Item", "4 Unread Items" (starred under the Starred filter).</summary>
    public static string CountText(int count, StreamFilter filter, bool capitalized = true)
    {
        var noun = filter == StreamFilter.Starred ? "Starred Item" : "Unread Item";
        var text = count switch
        {
            0 => $"No {noun}s",
            1 => $"1 {noun}",
            _ => $"{count} {noun}s",
        };
        return capitalized ? text : text.ToLower(CultureInfo.CurrentCulture);
    }

    /// <summary>Sidebar status: "Today at 13.33", "Updating…" while a refresh runs.</summary>
    public static string LastRefreshText(DateTimeOffset? lastRefresh, bool refreshing, DateTimeOffset now)
    {
        if (refreshing)
            return "Updating…";
        if (lastRefresh is not { } when)
            return "Never updated";
        var culture = CultureInfo.CurrentCulture;
        var local = when.ToLocalTime();
        var time = local.ToString(culture.DateTimeFormat.ShortTimePattern, culture);
        var today = now.ToLocalTime().Date;
        if (local.Date == today)
            return $"Today at {time}";
        if (local.Date == today.AddDays(-1))
            return $"Yesterday at {time}";
        return $"{local.ToString("d MMM", culture)} at {time}";
    }

    public static string ArticleCountText(int count) => count == 1 ? "1 article" : $"{count} articles";

    public static string FeedCountText(int count) => count == 1 ? "1 feed" : $"{count} feeds";

    public static string FilterTitle(StreamFilter filter) => filter switch
    {
        StreamFilter.Starred => "Starred",
        StreamFilter.Unread => "Unread",
        _ => "All Items",
    };
}
