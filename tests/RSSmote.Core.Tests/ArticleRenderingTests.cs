using RSSmote.Core.Models;
using RSSmote.Core.Rendering;

namespace RSSmote.Core.Tests;

public class ArticleSanitizerTests
{
    private const string Base = "https://example.com/blog/post.html";

    [Fact]
    public void Removes_scripts_styles_and_handlers_without_leaving_their_text()
    {
        var html = ArticleSanitizer.Sanitize(
            "<p onclick=\"x()\">Hi</p><script>alert(1)</script><style>p{}</style><noscript>no</noscript>", Base);
        Assert.Equal("<p>Hi</p>", html);
    }

    [Fact]
    public void Keeps_the_text_of_unknown_wrappers() =>
        Assert.Contains("kept", ArticleSanitizer.Sanitize("<custom-box><b>kept</b></custom-box>", Base));

    [Fact]
    public void Drops_inline_styles_and_sizes() =>
        Assert.Equal("<p>a</p><img src=\"https://example.com/i.png\">",
            ArticleSanitizer.Sanitize("<p style=\"color:black\" class=\"x\">a</p><img src=\"/i.png\" width=\"900\" height=\"20\">", Base));

    [Fact]
    public void Resolves_relative_links_and_images_against_the_article()
    {
        var html = ArticleSanitizer.Sanitize("<a href=\"next.html\">n</a><img src=\"../img/a.png\">", Base);
        Assert.Contains("href=\"https://example.com/blog/next.html\"", html);
        Assert.Contains("src=\"https://example.com/img/a.png\"", html);
    }

    [Fact]
    public void Removes_javascript_urls() =>
        Assert.DoesNotContain("javascript", ArticleSanitizer.Sanitize("<a href=\"javascript:alert(1)\">x</a>", Base));

    [Fact]
    public void Lazy_images_get_their_real_source()
    {
        var html = ArticleSanitizer.Sanitize("<img src=\"data:image/gif;base64,R0lG\" data-src=\"/big.jpg\">", Base);
        Assert.Equal("<img src=\"https://example.com/big.jpg\">", html);
    }

    [Fact]
    public void Video_frames_stay_and_other_frames_become_links()
    {
        var html = ArticleSanitizer.Sanitize(
            "<iframe src=\"https://www.youtube.com/embed/abc\"></iframe><iframe src=\"https://ads.example.net/x\"></iframe>", Base);
        Assert.Contains("<iframe src=\"https://www.youtube.com/embed/abc\">", html);
        Assert.Contains("<p><a href=\"https://ads.example.net/x\">ads.example.net</a></p>", html);
        Assert.DoesNotContain("ads.example.net/x\"></iframe>", html);
    }

    [Fact]
    public void Empty_content_is_empty() => Assert.Equal("", ArticleSanitizer.Sanitize("  ", Base));
}

public class ArticleTemplateTests
{
    private static readonly ArticlePalette Palette = new("#fffefb", "#24201b", "#6b6257", "#a85607", "#ece6dc", "#f2ede4", "#f6ead9");

    private static Item Item(string content = "<p>Body</p>", string? image = null) => new(
        1, 1, "g", "Title <&>", "Ann", "https://example.com/post", content, "", image,
        DateTimeOffset.UnixEpoch, true, false, "Example");

    [Fact]
    public void Header_is_escaped_and_the_title_links_to_the_article()
    {
        var html = ArticleTemplate.Render(ArticleDocument.Create(Item(), "MONDAY"), new(Palette, 17, true));
        Assert.Contains("<h1><a href=\"https://example.com/post\">Title &lt;&amp;&gt;</a></h1>", html);
        Assert.Contains("<span>Ann</span> · <span class=\"source\">Example</span>", html);
        Assert.Contains("<div class=\"content\"><p>Body</p></div>", html);
    }

    [Fact]
    public void Stylesheet_has_literal_values_only()
    {
        var css = ArticleTemplate.Stylesheet(new(Palette, 17.6, false));
        Assert.DoesNotContain("var(--", css);
        Assert.Contains("font-size: 17.6px", css);
        Assert.Contains("background: #fffefb", css);
        Assert.Contains("::selection { background: #f6ead9", css);
        Assert.Contains("system-ui", css);
    }

    [Fact]
    public void Only_the_scripted_page_carries_the_script()
    {
        var document = ArticleDocument.Create(Item(), "");
        Assert.Contains("<script>", ArticleTemplate.Render(document, new(Palette, 17, true)));
        Assert.DoesNotContain("<script>", ArticleTemplate.Render(document, new(Palette, 17, false)));
    }

    [Fact]
    public void Without_scripts_video_frames_become_links()
    {
        var document = ArticleDocument.Create(Item("<iframe src=\"https://player.vimeo.com/video/1\"></iframe>"), "");
        var html = ArticleTemplate.Render(document, new(Palette, 17, false));
        Assert.DoesNotContain("<iframe", html);
        Assert.Contains("<a href=\"https://player.vimeo.com/video/1\">▶ Video on player.vimeo.com</a>", html);
    }

    [Fact]
    public void Hero_image_is_shown_only_when_the_body_does_not_contain_it()
    {
        var withHero = ArticleDocument.Create(Item(image: "https://example.com/hero.jpg"), "");
        var inBody = ArticleDocument.Create(Item("<img src=\"https://example.com/hero.jpg\">", "https://example.com/hero.jpg"), "");
        Assert.Equal("https://example.com/hero.jpg", withHero.HeroImage);
        Assert.Null(inBody.HeroImage);
    }
}
