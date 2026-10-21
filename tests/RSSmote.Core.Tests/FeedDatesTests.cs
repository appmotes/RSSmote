using RSSmote.Core.Parsing;

namespace RSSmote.Core.Tests;

public class FeedDatesTests
{
    [Theory]
    [InlineData("Mon, 06 Oct 2025 09:30:00 GMT", "2025-10-06T09:30:00+00:00")]
    [InlineData("Mon, 6 Oct 2025 09:30:00 +0200", "2025-10-06T09:30:00+02:00")]
    [InlineData("06 Oct 2025 09:30 EST", "2025-10-06T09:30:00-05:00")]
    [InlineData("Monday, 06 October 2025 09:30:00 PDT", "2025-10-06T09:30:00-07:00")]
    [InlineData("Mon, 06 Oct 25 09:30:00 Z", "2025-10-06T09:30:00+00:00")]
    [InlineData("2025-10-06T09:30:00Z", "2025-10-06T09:30:00+00:00")]
    [InlineData("2025-10-06T09:30:00.123+02:00", "2025-10-06T09:30:00.123+02:00")]
    [InlineData("2025-10-06", "2025-10-06T00:00:00+00:00")]
    [InlineData("Tue, 07 Oct 2025", "2025-10-07T00:00:00+00:00")]
    public void Parses(string text, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), FeedDates.Parse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("31 Foo 2025")]
    public void Rejects(string? text) => Assert.Null(FeedDates.Parse(text));
}
