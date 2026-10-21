using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using RSSmote.App.Services;
using RSSmote.App.Theming;
using RSSmote.App.ViewModels;
using RSSmote.Desktop.Views;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;
using RSSmote.Data;

namespace RSSmote.Desktop.Tests;

/// <summary>
/// The README's screenshots, over a made-up library: invented feeds and articles, favicons and thumbnails drawn here,
/// so the pictures show no one's subscriptions and no third-party images. Runs only when RSSMOTE_README_DIR names an
/// output folder.
/// </summary>
public class ReadmeCaptureTests
{
    private const int Width = 1280;
    private const int Height = 800;

    [AvaloniaFact]
    public async Task Capture_readme_screens()
    {
        var output = Environment.GetEnvironmentVariable("RSSMOTE_README_DIR");
        if (string.IsNullOrEmpty(output))
            return;
        Directory.CreateDirectory(output);

        var folder = Path.Combine(Path.GetTempPath(), "rssmote-readme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var store = new Store(Path.Combine(folder, "rssmote.sqlite"), TimeProvider.System);
            var (nightSky, folders) = DemoLibrary.Fill(store, DateTimeOffset.Now);
            var settings = new SettingsStore(Path.Combine(folder, "settings.json"));
            settings.Update(s =>
            {
                s.Filter = StreamFilter.Unread;
                s.LastRefresh = DateTimeOffset.Now.AddMinutes(-4);
                s.ExpandedFolders = [folders[0].Id, folders[1].Id];
                s.GrayscaleFavicons = false;
            });
            using var http = FeedFinder.CreateHttpClient();
            using var shell = new ShellViewModel(store, settings, refresher: null, new FeedFinder(http), new ShellFixture.FakeFiles(),
                new DemoImages(), new ShellFixture.RecordingLauncher(), new AvaloniaUiDispatcher(), TimeProvider.System);

            var window = new Window { Width = Width, Height = Height, Content = new ShellView(), DataContext = shell };
            window.Show();

            void Use(string id) => Application.Current!.RequestedThemeVariant = ThemeCatalogue.All.Single(t => t.Id == id).Variant;
            void Read() => shell.List.SelectedRow = shell.List.Articles.First(a => a.Item.Guid == DemoLibrary.FeatureGuid);

            foreach (var theme in ThemeCatalogue.All)
            {
                Use(theme.Id);
                Read();
                await Shot(window, output, "theme-" + theme.Id);
            }

            Use("sand");
            Read();
            await Shot(window, output, "reading-light");
            Use("ember");
            await Shot(window, output, "reading-dark");

            Use("sand");
            shell.GoBackCommand.Execute(null);
            shell.List.SearchText = "garden";
            await Shot(window, output, "filter-box");
            shell.List.EndSearch();

            Use("fjord");
            await Shot(window, output, "no-article");

            Use("sand");
            shell.EditFeedCommand.Execute(nightSky);
            await Shot(window, output, "edit-feed");
            shell.Panel?.CloseCommand.Execute(null);

            Use("ember");
            shell.ShowAddFeedCommand.Execute(null);
            ((AddFeedViewModel)shell.Panel!).Address = "https://example.org/blog";
            await Shot(window, output, "add-feed");
            shell.Panel?.CloseCommand.Execute(null);

            window.Close();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Lets images arrive and layout settle, then saves the frame.</summary>
    private static async Task Shot(Window window, string folder, string name)
    {
        for (var i = 0; i < 12; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(15);
        }
        using var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    /// <summary>Favicons as coloured squares with a letter, thumbnails as abstract shapes; nothing is fetched.</summary>
    private sealed class DemoImages : IImageLoader
    {
        private readonly Dictionary<string, Bitmap> _drawn = new();

        public Task<Bitmap?> LoadAsync(string url, int decodeWidth)
        {
            if (!_drawn.TryGetValue(url, out var bitmap))
                _drawn[url] = bitmap = url.Contains("favicons", StringComparison.Ordinal) ? Favicon(url) : Thumbnail(url);
            return Task.FromResult<Bitmap?>(bitmap);
        }

        public void Forget(string url) => _drawn.Remove(url);

        private static Bitmap Favicon(string url)
        {
            var host = Uri.UnescapeDataString(url[(url.IndexOf("domain=", StringComparison.Ordinal) + 7)..]);
            var (color, letter) = DemoLibrary.FaviconFor(host);
            var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
            using (var ctx = bitmap.CreateDrawingContext())
            {
                ctx.DrawRectangle(new SolidColorBrush(color), null, new RoundedRect(new Rect(0, 0, 64, 64), 14));
                var text = new FormattedText(letter, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("fonts:Inter#Inter", FontStyle.Normal, FontWeight.Bold), 38, Brushes.White);
                ctx.DrawText(text, new Point((64 - text.Width) / 2, (64 - text.Height) / 2));
            }
            return bitmap;
        }

        private static Bitmap Thumbnail(string url)
        {
            var (a, b, c) = DemoLibrary.ArtFor(url);
            var bitmap = new RenderTargetBitmap(new PixelSize(176, 176), new Vector(96, 96));
            using (var ctx = bitmap.CreateDrawingContext())
            {
                var ground = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = [new GradientStop(a, 0), new GradientStop(b, 1)],
                };
                ctx.FillRectangle(ground, new Rect(0, 0, 176, 176));
                ctx.DrawEllipse(new SolidColorBrush(c), null, new Point(116, 112), 46, 46);
                ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), null, new Point(52, 54), 22, 22);
                ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), new Rect(0, 140, 176, 36));
            }
            return bitmap;
        }
    }
}

/// <summary>An invented library for screenshots: ten feeds in four folders, articles written for the purpose.</summary>
internal static class DemoLibrary
{
    public const string FeatureGuid = "sky-guide";

    private static readonly Dictionary<string, (Color Color, string Letter)> Icons = new()
    {
        ["nightsky.example"] = (Color.Parse("#1F3A6B"), "N"), ["fieldnotes.example"] = (Color.Parse("#4F7A2E"), "F"),
        ["osdigest.example"] = (Color.Parse("#B4541A"), "O"), ["bytepixel.example"] = (Color.Parse("#6B3FA0"), "B"),
        ["quietweb.example"] = (Color.Parse("#2F6F73"), "Q"), ["smallkitchen.example"] = (Color.Parse("#C0392B"), "S"),
        ["slowgarden.example"] = (Color.Parse("#3E7D4F"), "G"), ["harbourgazette.example"] = (Color.Parse("#1D5C8C"), "H"),
        ["longreads.example"] = (Color.Parse("#2B2B2B"), "L"), ["weatherdesk.example"] = (Color.Parse("#2E86C1"), "W"),
    };

    public static (Color, string) FaviconFor(string host) =>
        Icons.TryGetValue(host, out var icon) ? icon : (Color.Parse("#777777"), host[..1].ToUpperInvariant());

    public static (Color, Color, Color) ArtFor(string url)
    {
        (string, string, string)[] palettes =
        [
            ("#0B1D3A", "#2C4F8C", "#F2D16B"), ("#F4E3C1", "#E59A5B", "#7A3B2E"), ("#123B2F", "#3F8F6B", "#E8F1C8"),
            ("#3A1F5C", "#C2408A", "#F7C9D9"), ("#E8EEF4", "#8FB3D9", "#1D5C8C"), ("#2B2B2B", "#6E6E6E", "#E5B94B"),
        ];
        var (a, b, c) = palettes[Math.Abs(url.GetHashCode(StringComparison.Ordinal)) % palettes.Length];
        return (Color.Parse(a), Color.Parse(b), Color.Parse(c));
    }

    public static (Feed NightSky, IReadOnlyList<Folder> Folders) Fill(Store store, DateTimeOffset now)
    {
        var science = store.AddFolder("Science");
        var tech = store.AddFolder("Technology");
        var food = store.AddFolder("Food & Home");
        var local = store.AddFolder("Local");

        Feed Feed(string host, string title, Folder? folder) =>
            store.AddFeed($"https://{host}/feed", title, $"https://{host}", folder?.Id);

        var nightSky = Feed("nightsky.example", "Night Sky Journal", science);
        var fieldNotes = Feed("fieldnotes.example", "Field Notes", science);
        var digest = Feed("osdigest.example", "Open Source Digest", tech);
        var bytePixel = Feed("bytepixel.example", "Byte & Pixel", tech);
        var quietWeb = Feed("quietweb.example", "The Quiet Web", tech);
        var kitchen = Feed("smallkitchen.example", "Small Kitchen", food);
        var garden = Feed("slowgarden.example", "Slow Garden", food);
        var harbour = Feed("harbourgazette.example", "Harbour Gazette", local);
        var longReads = Feed("longreads.example", "Long Reads Weekly", null);
        var weather = Feed("weatherdesk.example", "Weather Desk", null);

        ParsedItem Item(string guid, string title, double hoursAgo, string summary, bool image = false, string? author = null,
                        string? body = null) =>
            new(guid, title, $"https://example.org/{guid}", author ?? "Staff", body ?? $"<p>{summary}</p>", summary,
                image ? $"https://images.example/{guid}.png" : null, now.AddHours(-hoursAgo));

        store.Upsert(nightSky.Id, [
            Item(FeatureGuid, "A field guide to the night sky, no telescope needed", 0.4,
                "Three constellations, two planets and one patient habit are enough to find your way around a clear night.",
                author: "Ida Holm", body: FeatureBody),
            Item("meteors", "This week's meteor shower peaks after midnight", 6,
                "Look east after the moon has set; the best hour is just before dawn.", image: true),
            Item("moon-map", "Why the Moon always shows us the same face", 30,
                "Tidal locking, explained with a kitchen table and two oranges.", image: true),
        ]);
        store.Upsert(fieldNotes.Id, [
            Item("bees", "Counting bees in a city park for a whole summer", 2.5,
                "Volunteers logged 14 species on a single lawn, more than anyone expected.", image: true),
            Item("moss", "Moss grows on every side of the tree", 27, "So much for using it as a compass."),
        ]);
        store.Upsert(digest.Id, [
            Item("release", "A calmer release process for small projects", 1.2,
                "Tag, build, publish: three steps that fit on a sticky note, and why that is the point.", image: true),
            Item("docs", "Write the docs first, then the code", 8, "A short case for README-driven development."),
            Item("tests", "Tests that render the UI without a screen", 29, "Headless rendering turns screenshots into checks."),
        ]);
        store.Upsert(bytePixel.Id, [
            Item("fonts", "Choosing a typeface for a reading app", 3,
                "Line length, x-height and figures that line up in a column matter more than the name of the font.", image: true),
            Item("dark-mode", "Dark mode is not a colour inversion", 26,
                "Surfaces lighten as they rise, and text on an accent turns dark."),
        ]);
        store.Upsert(quietWeb.Id, [
            Item("rss", "Following sites without an algorithm", 4.5,
                "A feed reader shows what was written, in the order it was written. That is the whole feature."),
        ]);
        store.Upsert(kitchen.Id, [
            Item("bread", "One loaf, four days: a weeknight sourdough plan", 5,
                "Feed on Sunday, mix on Monday, bake on Tuesday, eat all week.", image: true),
            Item("soup", "A soup for the first cold evening", 31, "Leeks, potatoes and patience."),
        ]);
        store.Upsert(garden.Id, [
            Item("tomatoes", "Saving tomato seeds from this year's best plant", 7,
                "Ferment, rinse, dry on a plate, label everything.", image: true),
            Item("compost", "The garden that composts itself", 50, "Leave the leaves where they fall, and let the worms work."),
        ]);
        store.Upsert(harbour.Id, [
            Item("ferry", "Harbour ferry adds an evening departure", 1.8,
                "From next month the last boat leaves at 23.10 on weekdays.", image: true),
            Item("market", "Saturday market moves to the old quay", 9, "Same stalls, more room, a view of the water."),
        ]);
        store.Upsert(longReads.Id, [
            Item("lighthouse", "The last lighthouse keeper on the coast", 3.6,
                "For thirty years she kept the lamp lit. Then the lamp no longer needed anyone.", image: true),
        ]);
        store.Upsert(weather.Id, [
            Item("frost", "First night frost expected inland", 0.9, "Bring in the pots; the coast stays just above zero."),
        ]);

        var starred = store.Items(new ArticleStream.All(), StreamFilter.All).Where(i => i.Guid is "lighthouse" or "bread").Select(i => i.Id).ToList();
        store.SetStarred(starred, true);
        var read = store.Items(new ArticleStream.All(), StreamFilter.All).Where(i => i.Guid is "moss" or "soup" or "docs").Select(i => i.Id).ToList();
        store.SetUnread(read, false);
        return (nightSky, [science, tech, food, local]);
    }

    private const string FeatureBody = """
        <p>You do not need a telescope to feel at home under a clear sky. You need a few landmarks, a little patience and
        twenty minutes away from bright lights while your eyes adjust.</p>
        <h2>Start with three shapes</h2>
        <p>Find the <a href="https://example.org/plough">Plough</a> first. Follow the two stars at the end of its bowl and
        they lead you to the Pole Star, which barely moves all night. From there, Cassiopeia's crooked W sits on the
        other side, and in autumn the great square of Pegasus rises in the east.</p>
        <blockquote>The sky turns, but it turns the same way every night. Learn one corner and the rest follows.</blockquote>
        <h2>Planets do not twinkle</h2>
        <p>A bright point that holds steady while the stars around it flicker is almost always a planet. This month two of
        them share the evening sky:</p>
        <ul><li>Jupiter, high in the south-east after dark</li><li>Saturn, lower and paler, a little to the west</li></ul>
        <p>Keep a notebook. Write the date, the time and what you saw. In a month you will know the sky better than any
        app can tell you.</p>
        """;
}
