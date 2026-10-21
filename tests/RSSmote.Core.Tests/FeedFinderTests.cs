using System.Net;
using System.Text;
using RSSmote.Core.Discovery;

namespace RSSmote.Core.Tests;

public class FeedFinderTests
{
    private const string Rss = """<rss version="2.0"><channel><title>Site Feed</title><item><title>A</title><guid>1</guid></item></channel></rss>""";

    private sealed class FakeHandler(Dictionary<string, string> pages) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var found = pages.TryGetValue(request.RequestUri!.AbsoluteUri, out var body);
            return Task.FromResult(new HttpResponseMessage(found ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            {
                Content = new StringContent(body ?? "", Encoding.UTF8),
                RequestMessage = request,
            });
        }
    }

    private static FeedFinder Finder(Dictionary<string, string> pages) => new(new HttpClient(new FakeHandler(pages)));

    [Theory]
    [InlineData("feed://a.example/rss", "https://a.example/rss")]
    [InlineData("feed:https://a.example/rss", "https://a.example/rss")]
    [InlineData("a.example", "https://a.example/")]
    [InlineData("  http://a.example/x  ", "http://a.example/x")]
    public void Normalizes(string input, string expected) => Assert.Equal(expected, FeedFinder.Normalize(input)?.AbsoluteUri);

    [Theory]
    [InlineData("   ")]
    [InlineData("ftp://a.example")]
    public void Rejects_non_web_addresses(string input) => Assert.Null(FeedFinder.Normalize(input));

    [Fact]
    public void Finds_alternate_links_and_ignores_stylesheets()
    {
        const string html = """<head><link rel="alternate" type="application/rss+xml" href="/feed.xml"><link rel="stylesheet" href="x.css"><link type='application/atom+xml' rel='alternate home' href='atom.xml'></head>""";
        var links = FeedFinder.AlternateLinks(html, new Uri("https://site.example/blog/"));
        Assert.Equal(["https://site.example/feed.xml", "https://site.example/blog/atom.xml"], links.Select(u => u.AbsoluteUri));
    }

    [Fact]
    public async Task A_feed_address_is_returned_as_is()
    {
        var found = await Finder(new() { ["https://site.example/rss"] = Rss }).FindAllAsync("site.example/rss", TestContext.Current.CancellationToken);
        Assert.Equal("Site Feed", Assert.Single(found).Feed.Title);
    }

    [Fact]
    public async Task A_web_page_is_searched_for_alternate_links()
    {
        var pages = new Dictionary<string, string>
        {
            ["https://site.example/"] = """<html><head><link rel="alternate" type="application/rss+xml" href="/feeds/main"></head></html>""",
            ["https://site.example/feeds/main"] = Rss,
        };
        var found = await Finder(pages).FindAllAsync("site.example", TestContext.Current.CancellationToken);
        Assert.Equal("https://site.example/feeds/main", Assert.Single(found).Url.AbsoluteUri);
    }

    [Fact]
    public async Task Common_paths_are_tried_when_nothing_is_advertised()
    {
        var pages = new Dictionary<string, string> { ["https://site.example/"] = "<html></html>", ["https://site.example/rss.xml"] = Rss };
        var found = await Finder(pages).FindAllAsync("https://site.example", TestContext.Current.CancellationToken);
        Assert.Equal("https://site.example/rss.xml", Assert.Single(found).Url.AbsoluteUri);
    }

    [Fact]
    public async Task Nothing_found_throws_not_found()
    {
        var error = await Assert.ThrowsAsync<FeedException>(() =>
            Finder(new() { ["https://site.example/"] = "<html></html>" }).FindAllAsync("site.example", TestContext.Current.CancellationToken));
        Assert.Equal(FeedError.NotFound, error.Error);
    }

    [Fact]
    public async Task Http_errors_surface_with_status()
    {
        var error = await Assert.ThrowsAsync<FeedException>(() => Finder([]).FindAllAsync("site.example", TestContext.Current.CancellationToken));
        Assert.Equal(FeedError.Http, error.Error);
        Assert.Equal(HttpStatusCode.NotFound, error.Status);
    }

    [Fact]
    public void The_default_handler_decodes_compressed_responses() =>
        Assert.Equal(DecompressionMethods.All, FeedFinder.CreateHandler().AutomaticDecompression);
}
