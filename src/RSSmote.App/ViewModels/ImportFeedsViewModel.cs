using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.App.Services;
using RSSmote.Core.Opml;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>A folder an import adds feeds to, or the feeds in no folder (<see cref="IsFolder"/> false).</summary>
public sealed record ImportGroup(string Title, bool IsFolder, int Count)
{
    public string CountText => Count == 1 ? "1 feed" : $"{Count} feeds";
}

/// <summary>
/// Import Feeds (D27, D30): a chosen OPML file shows what it would add, folder by folder, and how many of its feeds
/// are already added; nothing is saved until <see cref="ImportCommand"/>.
/// </summary>
public sealed partial class ImportFeedsViewModel(Store store, IFilePicker files, Action imported) : PanelViewModel
{
    private IReadOnlyList<OpmlOutline> _new = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    public partial string? FileName { get; private set; }

    public bool HasFile => FileName is not null;

    /// <summary>"12 feeds in 3 folders": everything the file holds.</summary>
    [ObservableProperty]
    public partial string FileSummary { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<ImportGroup> Groups { get; private set; } = [];

    /// <summary>The line under the groups: what is skipped, and that folders are reused.</summary>
    [ObservableProperty]
    public partial string SkippedText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportTitle), nameof(HasNew))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    public partial int NewCount { get; private set; }

    public bool HasNew => NewCount > 0;

    /// <summary>"Import 10 Feeds", or "Nothing to Import" when every feed is already added.</summary>
    public string ImportTitle => NewCount switch
    {
        0 => "Nothing to Import",
        1 => "Import 1 Feed",
        _ => $"Import {NewCount} Feeds",
    };

    [RelayCommand]
    private async Task Choose()
    {
        PickedText? picked;
        try
        {
            picked = await files.OpenTextAsync("Import Feeds", "OPML", ["*.opml", "*.xml"]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Could not read that file.";
            return;
        }
        if (picked is not null)
            Preview(picked.Name, OpmlDocument.Parse(picked.Text));
    }

    /// <summary>Shows what <paramref name="outlines"/> would add to the library as it is now.</summary>
    public void Preview(string fileName, IReadOnlyList<OpmlOutline> outlines)
    {
        FileName = fileName;
        var unique = outlines.DistinctBy(o => o.XmlUrl).ToList();
        var folders = unique.Select(o => o.Folder).OfType<string>().Distinct(StringComparer.CurrentCultureIgnoreCase).Count();
        FileSummary = $"{FeedsText(unique.Count)} in {(folders == 1 ? "1 folder" : $"{folders} folders")}";
        Error = unique.Count == 0 ? "No feeds in that file." : null;

        var stored = store.Feeds().Select(f => f.Url).ToHashSet();
        _new = [.. unique.Where(o => !stored.Contains(o.XmlUrl))];
        Groups = [.. _new.GroupBy(o => o.Folder, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g.Key is null).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ImportGroup(g.Key ?? "No folder", g.Key is not null, g.Count()))];
        var skipped = unique.Count - _new.Count;
        SkippedText = skipped switch
        {
            0 => "Folders that exist are reused.",
            1 => "1 feed is already added and is skipped. Folders that exist are reused.",
            _ => $"{skipped} feeds are already added and are skipped. Folders that exist are reused.",
        };
        NewCount = _new.Count;
    }

    private static string FeedsText(int count) => count == 1 ? "1 feed" : $"{count} feeds";

    private bool CanImport() => NewCount > 0;

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void Import()
    {
        if (store.ImportOpml(_new) > 0)
            imported();
        Close();
    }
}

/// <summary>Export Feeds (D27): what the OPML file holds, then the system's save or share sheet.</summary>
public sealed partial class ExportFeedsViewModel(Store store, IFilePicker files) : PanelViewModel
{
    public const string FileName = "RSSmote Feeds.opml";

    public int FeedCount { get; } = store.Feeds().Count;

    public int FolderCount { get; } = store.Folders().Count;

    /// <summary>After an export: "Saved", or why not.</summary>
    [ObservableProperty]
    public partial string ResultText { get; private set; } = "";

    [RelayCommand]
    private async Task Export()
    {
        try
        {
            ResultText = await files.SaveTextAsync("Export Feeds", FileName, OpmlDocument.Export(store.Folders(), store.Feeds()))
                ? "Saved."
                : "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ResultText = "Could not save the file.";
        }
    }
}
