using RSSmote.Core.Discovery;
using RSSmote.Data;

namespace RSSmote.App.Services;

/// <summary>
/// Composition root (A3): creates the long-lived services once and hands them to view-models through
/// constructors. View-models never reach for this class.
/// </summary>
public sealed class AppServices : IDisposable
{
    public Store Store { get; }
    public SettingsStore Settings { get; }
    public FeedFinder Finder { get; }
    public Refresher Refresher { get; }
    public IImageLoader Images { get; }
    public IFilePicker Files { get; }
    public ILauncher Launcher { get; }
    public IUiDispatcher Ui { get; }
    public TimeProvider Clock { get; } = TimeProvider.System;

    private readonly HttpClient _http;

    /// <param name="topLevel">The window that file dialogs belong to, looked up when a dialog opens.</param>
    /// <param name="launcher">The head's way of opening links.</param>
    public AppServices(Func<Avalonia.Controls.TopLevel?> topLevel, ILauncher launcher)
    {
        _http = FeedFinder.CreateHttpClient();
        Store = new Store(AppFolders.DatabasePath(), Clock);
        Settings = new SettingsStore(AppFolders.SettingsPath());
        Finder = new FeedFinder(_http);
        Refresher = new Refresher(Store, Finder, Settings, Clock);
        Images = new ImageCache(_http);
        Files = new StorageFilePicker(topLevel);
        Launcher = launcher;
        Ui = new AvaloniaUiDispatcher();
    }

    public void Dispose()
    {
        Refresher.Dispose();
        Store.Dispose();
        _http.Dispose();
    }
}
