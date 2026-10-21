using System.Text;
using System.Xml;
using System.Xml.Linq;
using RSSmote.Core.Models;

namespace RSSmote.Core.Opml;

/// <summary>A feed from an OPML file; <see cref="Folder"/> is the nearest enclosing outline with a title.</summary>
public sealed record OpmlOutline(string Title, string XmlUrl, string? HtmlUrl, string? Folder);

/// <summary>OPML 1.0/2.0 import and export of the stored feeds.</summary>
public static class OpmlDocument
{
    public static IReadOnlyList<OpmlOutline> Parse(string text)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(text);
        }
        catch (XmlException)
        {
            return [];
        }
        var body = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "body");
        var result = new List<OpmlOutline>();
        if (body is not null)
            Walk(body, null, result);
        return result;
    }

    private static void Walk(XElement parent, string? folder, List<OpmlOutline> result)
    {
        foreach (var outline in parent.Elements().Where(e => e.Name.LocalName == "outline"))
        {
            var title = Attr(outline, "title") ?? Attr(outline, "text");
            if (Attr(outline, "xmlUrl") is { } xml)
            {
                result.Add(new OpmlOutline(title ?? xml, xml, Attr(outline, "htmlUrl"), folder));
                Walk(outline, folder, result);
            }
            else
            {
                Walk(outline, title ?? folder, result);
            }
        }
    }

    private static string? Attr(XElement e, string name)
    {
        var value = e.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static string Export(IEnumerable<Folder> folders, IEnumerable<Feed> feeds, string title = "RSSmote Feeds")
    {
        var feedList = feeds.ToList();
        XElement Outline(Feed f) => new("outline",
            new XAttribute("type", "rss"), new XAttribute("text", f.Title), new XAttribute("title", f.Title),
            new XAttribute("xmlUrl", f.Url), f.SiteUrl is null ? null : new XAttribute("htmlUrl", f.SiteUrl));

        var body = new XElement("body");
        foreach (var folder in folders)
            body.Add(new XElement("outline", new XAttribute("text", folder.Title), new XAttribute("title", folder.Title),
                feedList.Where(f => f.FolderId == folder.Id).Select(Outline)));
        body.Add(feedList.Where(f => f.FolderId is null).Select(Outline));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement("opml", new XAttribute("version", "2.0"),
                new XElement("head", new XElement("title", title)), body));
        using var writer = new Utf8StringWriter();
        doc.Save(writer);
        return writer.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
