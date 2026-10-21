using RSSmote.Core.Parsing;

namespace RSSmote.Core.Tests;

public class FeedParserTests
{
    [Fact]
    public void Rss2_with_content_module_and_relative_links()
    {
        const string xml = """
            <?xml version="1.0"?>
            <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/" xmlns:dc="http://purl.org/dc/elements/1.1/">
            <channel><title>Example &amp; Co</title><link>https://example.com/</link>
              <item>
                <title>Hello &amp; welcome</title>
                <link>/posts/1</link>
                <guid>post-1</guid>
                <pubDate>Mon, 06 Oct 2025 09:30:00 GMT</pubDate>
                <dc:creator>Jane</dc:creator>
                <description>Short</description>
                <content:encoded><![CDATA[<p>Body <img src="/a.png"></p>]]></content:encoded>
              </item>
            </channel></rss>
            """;
        var feed = FeedParser.Parse(xml);
        Assert.NotNull(feed);
        Assert.Equal("Example & Co", feed.Title);
        Assert.Equal("https://example.com/", feed.SiteUrl);
        var item = Assert.Single(feed.Items);
        Assert.Equal("Hello & welcome", item.Title);
        Assert.Equal("https://example.com/posts/1", item.Link);
        Assert.Equal("post-1", item.Guid);
        Assert.Equal("Jane", item.Author);
        Assert.Equal("https://example.com/a.png", item.ImageUrl);
        Assert.Contains("<p>Body", item.Content);
        Assert.Equal("Short", item.Summary);
        Assert.Equal(new DateTimeOffset(2025, 10, 6, 9, 30, 0, TimeSpan.Zero), item.Published);
    }

    [Fact]
    public void Atom_with_alternate_links_and_author_name()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>Atom Feed</title>
              <link rel="alternate" href="https://atom.example/"/>
              <link rel="self" href="https://atom.example/feed.xml"/>
              <entry>
                <title type="html">Entry</title>
                <id>urn:1</id>
                <link rel="alternate" href="https://atom.example/1"/>
                <published>2025-10-06T09:30:00Z</published>
                <author><name>John</name></author>
                <content type="html">&lt;p&gt;Hi&lt;/p&gt;</content>
              </entry>
            </feed>
            """;
        var feed = FeedParser.Parse(xml);
        Assert.NotNull(feed);
        Assert.Equal("Atom Feed", feed.Title);
        Assert.Equal("https://atom.example/", feed.SiteUrl);
        var item = Assert.Single(feed.Items);
        Assert.Equal("urn:1", item.Guid);
        Assert.Equal("John", item.Author);
        Assert.Equal("<p>Hi</p>", item.Content);
        Assert.Equal("Hi", item.Summary);
        Assert.Equal("https://atom.example/1", item.Link);
    }

    [Fact]
    public void Atom_xhtml_content_is_serialised_without_namespace_noise()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>X</title>
              <entry><title>T</title><id>1</id><updated>2025-10-06T09:30:00+02:00</updated>
                <content type="xhtml"><div xmlns="http://www.w3.org/1999/xhtml"><p>Hello <b>you</b></p></div></content>
              </entry>
            </feed>
            """;
        var item = Assert.Single(FeedParser.Parse(xml)!.Items);
        Assert.Equal("<p>Hello <b>you</b></p>", item.Content);
        Assert.Equal(new DateTimeOffset(2025, 10, 6, 7, 30, 0, TimeSpan.Zero), item.Published?.ToUniversalTime());
    }

    [Fact]
    public void Rss1_rdf_items_are_found()
    {
        const string xml = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns="http://purl.org/rss/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/">
              <channel><title>RDF Feed</title><link>https://rdf.example/</link></channel>
              <item><title>One</title><link>https://rdf.example/1</link><dc:date>2025-10-06T09:30:00Z</dc:date></item>
              <item><title>Two</title><link>https://rdf.example/2</link></item>
            </rdf:RDF>
            """;
        var feed = FeedParser.Parse(xml);
        Assert.NotNull(feed);
        Assert.Equal("RDF Feed", feed.Title);
        Assert.Equal(2, feed.Items.Count);
        Assert.Equal("https://rdf.example/1", feed.Items[0].Guid);
        Assert.NotNull(feed.Items[0].Published);
    }

    [Fact]
    public void Media_thumbnail_is_an_image_not_the_body()
    {
        const string xml = """
            <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/"><channel><title>M</title>
              <item><title>Pic</title><guid>g</guid><description>Text</description>
                <media:content url="https://m.example/video.mp4" type="video/mp4"/>
                <media:thumbnail url="https://m.example/t.jpg"/>
              </item>
            </channel></rss>
            """;
        var item = Assert.Single(FeedParser.Parse(xml)!.Items);
        Assert.Equal("Text", item.Content);
        Assert.Equal("https://m.example/t.jpg", item.ImageUrl);
    }

    [Fact]
    public void Html_entities_in_xml_are_recovered()
    {
        const string xml = """
            <rss version="2.0"><channel><title>Caf&eacute;</title>
              <item><title>A&nbsp;B</title><guid>1</guid><description>x&hellip;</description></item>
            </channel></rss>
            """;
        var feed = FeedParser.Parse(xml);
        Assert.NotNull(feed);
        Assert.Equal("Café", feed.Title);
        Assert.Equal("A B", Assert.Single(feed.Items).Title);
    }

    [Fact]
    public void Json_feed()
    {
        const string json = """
            {"version":"https://jsonfeed.org/version/1.1","title":"JF","home_page_url":"https://jf.example/",
             "items":[{"id":"1","url":"https://jf.example/1","title":"One","content_html":"<b>x</b>",
                       "date_published":"2025-10-06T09:30:00+02:00","authors":[{"name":"A"}]},
                      {"id":2,"content_text":"Para one\n\nPara two"}]}
            """;
        var feed = FeedParser.Parse(json);
        Assert.NotNull(feed);
        Assert.Equal("JF", feed.Title);
        Assert.Equal("A", feed.Items[0].Author);
        Assert.NotNull(feed.Items[0].Published);
        Assert.Equal("2", feed.Items[1].Guid);
        Assert.Equal("<p>Para one</p><p>Para two</p>", feed.Items[1].Content);
        Assert.Equal("Para one Para two", feed.Items[1].Title);
    }

    [Fact]
    public void Missing_guid_falls_back_to_link_then_title_and_date()
    {
        const string xml = """
            <rss version="2.0"><channel><title>F</title>
              <item><title>Linked</title><link>https://f.example/a</link></item>
              <item><title>Bare</title></item>
            </channel></rss>
            """;
        var items = FeedParser.Parse(xml)!.Items;
        Assert.Equal("https://f.example/a", items[0].Guid);
        Assert.Equal("Bare|0", items[1].Guid);
    }

    [Theory]
    [InlineData("<html><body>hi</body></html>")]
    [InlineData("not xml at all")]
    [InlineData("{\"no\":\"items\"}")]
    public void Non_feeds_return_null(string text) => Assert.Null(FeedParser.Parse(text));
}
