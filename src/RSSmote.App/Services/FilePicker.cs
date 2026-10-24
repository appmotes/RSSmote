using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace RSSmote.App.Services;

/// <summary>A text file the user chose: its name, for display, and its contents.</summary>
public sealed record PickedText(string Name, string Text);

/// <summary>Open and save dialogs for text files (A4); view-models see only this interface.</summary>
public interface IFilePicker
{
    /// <summary>The chosen file, or null when the user cancels.</summary>
    Task<PickedText?> OpenTextAsync(string title, string fileTypeName, IReadOnlyList<string> patterns);

    /// <summary>Writes <paramref name="text"/> to the chosen file; false when the user cancels.</summary>
    Task<bool> SaveTextAsync(string title, string suggestedName, string text);
}

/// <summary>The platform dialogs of the window that <paramref name="topLevel"/> returns when a dialog opens.</summary>
public sealed class StorageFilePicker(Func<TopLevel?> topLevel) : IFilePicker
{
    public async Task<PickedText?> OpenTextAsync(string title, string fileTypeName, IReadOnlyList<string> patterns)
    {
        if (topLevel()?.StorageProvider is not { CanOpen: true } storage)
            return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(fileTypeName) { Patterns = patterns }, FilePickerFileTypes.All],
        });
        if (files.Count == 0)
            return null;
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        return new PickedText(files[0].Name, await reader.ReadToEndAsync());
    }

    public async Task<bool> SaveTextAsync(string title, string suggestedName, string text)
    {
        if (topLevel()?.StorageProvider is not { CanSave: true } storage)
            return false;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { Title = title, SuggestedFileName = suggestedName });
        if (file is null)
            return false;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
        return true;
    }
}
