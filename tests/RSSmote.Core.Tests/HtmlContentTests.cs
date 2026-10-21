using RSSmote.Core.Parsing;

namespace RSSmote.Core.Tests;

public class HtmlContentTests
{
    [Fact]
    public void Decodes_numeric_and_named_entities_and_keeps_unknown_ones() =>
        Assert.Equal("’’&ø&bogus;", HtmlContent.DecodeEntities("&#8217;&#x2019;&amp;&oslash;&bogus;"));

    [Fact]
    public void Plain_text_drops_scripts_and_collapses_whitespace() =>
        Assert.Equal("a b", HtmlContent.PlainText("<p>a</p><script>x()</script>  <b>b</b>"));

    [Fact]
    public void Excerpt_cuts_with_ellipsis() =>
        Assert.Equal("abcde…", HtmlContent.Excerpt("<p>abcdefgh</p>", 5));

    [Fact]
    public void First_image_finds_src_after_other_attributes() =>
        Assert.Equal("/a&b.png", HtmlContent.FirstImage("<p><img alt='x' src=\"/a&amp;b.png\"></p>"));
}
