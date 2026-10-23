using System.Collections.ObjectModel;
using RSSmote.App.ViewModels;

namespace RSSmote.Mobile.Views;

/// <summary>A row in a card, with whether it is the card's last (which draws no hairline).</summary>
public sealed record FeedGroupRow(SidebarRowViewModel Row, bool IsLast);

/// <summary>One card on the Feeds root (D24): the section header above it, if any, and its rows.</summary>
public sealed record FeedGroup(SidebarRowViewModel? Header, IReadOnlyList<FeedGroupRow> Rows)
{
    public bool HasHeader => Header is not null;

    public bool HasRows => Rows.Count > 0;

    /// <summary>Splits the flat sidebar rows into cards at each section row.</summary>
    public static void Fill(ObservableCollection<FeedGroup> groups, IEnumerable<SidebarRowViewModel> rows)
    {
        var result = new List<FeedGroup>();
        SidebarRowViewModel? header = null;
        var current = new List<SidebarRowViewModel>();

        void Close()
        {
            if (header is not null || current.Count > 0)
                result.Add(new FeedGroup(header, [.. current.Select((r, i) => new FeedGroupRow(r, i == current.Count - 1))]));
            current = [];
        }

        foreach (var row in rows)
        {
            if (row.SectionKey is not null)
            {
                Close();
                header = row;
            }
            else
            {
                current.Add(row);
            }
        }
        Close();

        groups.Clear();
        foreach (var group in result)
            groups.Add(group);
    }
}
