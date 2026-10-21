using System.Diagnostics;
using System.Runtime.InteropServices;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;
using RSSmote.Core.Parsing;
using RSSmote.Core.Rendering;
using RSSmote.Data;

namespace RSSmote.Mobile.Spike;

/// <summary>
/// Temporary spike: runs the shared Core and Data code on the device, without UI, and reports each step.
/// Deleted once the mobile shell exists.
/// </summary>
public static class PlatformCheck
{
    private const string Rss = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/">
          <channel>
            <title>Spike&nbsp;Feed</title>
            <link>https://spike.example/</link>
            <item>
              <guid>one</guid>
              <title>First&nbsp;item</title>
              <link>https://spike.example/posts/one</link>
              <pubDate>Tue, 20 Oct 2026 10:00:00 GMT</pubDate>
              <content:encoded><![CDATA[<p>Hello</p><script>alert(1)</script><img src="/a.png"><p style="color:red">styled</p>]]></content:encoded>
            </item>
            <item>
              <guid>two</guid>
              <title>Second item</title>
              <link>https://spike.example/posts/two</link>
              <pubDate>Mon, 19 Oct 2026 10:00:00 GMT</pubDate>
              <description>Plain summary</description>
            </item>
          </channel>
        </rss>
        """;

    public static async Task<IReadOnlyList<string>> RunAsync()
    {
        var lines = new List<string>
        {
            $"OS: {RuntimeInformation.OSDescription}",
            $"Runtime: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}",
            "",
        };
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "spike");

        void Step(string name, Func<string> run)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                lines.Add($"OK   {name} ({clock.ElapsedMilliseconds} ms): {run()}");
            }
            catch (Exception e)
            {
                lines.Add($"FAIL {name}: {e.GetType().Name}: {e.Message}");
            }
        }

        Step("AppFolders.Data", () =>
        {
            var data = AppFolders.Data();
            Directory.CreateDirectory(data);
            var probe = Path.Combine(data, "probe.txt");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return $"{data} is writable";
        });

        Store? store = null;
        Feed? feed = null;
        ParsedFeed? parsed = null;
        Step("SQLite open + add", () =>
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
            store = new Store(Path.Combine(folder, AppFolders.DatabaseFile));
            var news = store.AddFolder("News");
            feed = store.AddFeed("https://spike.example/rss", "Spike Feed", "https://spike.example/", news.Id);
            return $"{store.Folders().Count} folder, {store.Feeds().Count} feed";
        });

        Step("FeedParser (RSS, &nbsp;)", () =>
        {
            parsed = FeedParser.Parse(Rss, new Uri("https://spike.example/rss")) ?? throw new InvalidOperationException("not a feed");
            return $"\"{parsed.Title}\", {parsed.Items.Count} items";
        });

        Item? first = null;
        Step("Store upsert + read", () =>
        {
            store!.Upsert(feed!.Id, parsed!.Items);
            var items = store.Items(new ArticleStream.OfFeed(feed.Id), StreamFilter.All);
            first = items.First(i => i.Guid == "one");
            store.SetStarred([first.Id], true);
            return $"{items.Count} items, {store.Count(new ArticleStream.All(), StreamFilter.Starred)} starred";
        });

        Step("HtmlSanitizer (AngleSharp)", () =>
        {
            var html = ArticleDocument.Create(first!, "today").ContentHtml;
            if (html.Contains("<script", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("script survived");
            if (html.Contains("style=", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("inline style survived");
            if (!html.Contains("https://spike.example/a.png", StringComparison.Ordinal))
                throw new InvalidOperationException("image not made absolute: " + html);
            return html;
        });

        Step("OPML export + parse", () =>
        {
            var outlines = OpmlDocument.Parse(OpmlDocument.Export(store!.Folders(), store.Feeds()));
            return $"{outlines.Count} outline(s)";
        });

        Step("SettingsStore (System.Text.Json)", () =>
        {
            var path = Path.Combine(folder, AppFolders.SettingsFile);
            new SettingsStore(path).Update(s => s.ArticleFontSize = 19);
            var size = new SettingsStore(path).Current.ArticleFontSize;
            return size == 19 ? "font size 19 written and read back" : throw new InvalidOperationException($"read back {size}");
        });

        store?.Dispose();

        var network = Stopwatch.StartNew();
        try
        {
            using var http = FeedFinder.CreateHttpClient();
            var found = await new FeedFinder(http).FindAsync("https://feeds.bbci.co.uk/news/rss.xml");
            lines.Add($"OK   Network FeedFinder ({network.ElapsedMilliseconds} ms): \"{found.Feed.Title}\", {found.Feed.Items.Count} items");
        }
        catch (Exception e)
        {
            lines.Add($"FAIL Network FeedFinder: {e.GetType().Name}: {e.Message}");
        }

        lines.Add("");
        lines.Add(lines.Any(l => l.StartsWith("FAIL", StringComparison.Ordinal)) ? "RESULT: FAILURES" : "RESULT: ALL OK");
        return lines;
    }
}
