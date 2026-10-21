using RSSmote.Core.Models;
using RSSmote.Core.Opml;

namespace RSSmote.Core.Tests;

public class OpmlTests
{
    [Fact]
    public void Export_then_parse_round_trips_folders_and_escaping()
    {
        var folders = new[] { new Folder(1, "Tech & News") };
        var feeds = new[]
        {
            new Feed(10, "A <1>", "https://a.example/feed?x=1&y=2", "https://a.example", 1, null),
            new Feed(11, "B", "https://b.example/feed", null, null, null),
        };
        var outlines = OpmlDocument.Parse(OpmlDocument.Export(folders, feeds));
        Assert.Equal(2, outlines.Count);
        var a = outlines.Single(o => o.Title == "A <1>");
        Assert.Equal("Tech & News", a.Folder);
        Assert.Equal("https://a.example/feed?x=1&y=2", a.XmlUrl);
        Assert.Equal("https://a.example", a.HtmlUrl);
        Assert.Null(outlines.Single(o => o.Title == "B").Folder);
    }

    [Fact]
    public void Nested_folders_use_the_nearest_titled_outline_and_text_when_title_is_missing()
    {
        const string opml = """
            <opml version="1.0"><body>
              <outline text="Outer"><outline text="Inner">
                <outline text="Feed" xmlUrl="https://f.example/rss" htmlurl="https://f.example"/>
              </outline></outline>
              <outline title="Loose" xmlUrl="https://l.example/rss"/>
            </body></opml>
            """;
        var outlines = OpmlDocument.Parse(opml);
        Assert.Equal(new OpmlOutline("Feed", "https://f.example/rss", "https://f.example", "Inner"), outlines[0]);
        Assert.Equal(new OpmlOutline("Loose", "https://l.example/rss", null, null), outlines[1]);
    }

    [Fact]
    public void Broken_xml_gives_no_outlines() => Assert.Empty(OpmlDocument.Parse("<opml><body>"));
}
