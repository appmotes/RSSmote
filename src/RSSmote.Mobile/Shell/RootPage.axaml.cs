using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.ViewModels;
using RSSmote.Mobile.Views;

namespace RSSmote.Mobile.Shell;

public enum RootTab
{
    Feeds,
    Settings,
}

/// <summary>The root of the navigation stack: Feeds or Settings, switched by the tab bar (D23).</summary>
public partial class RootPage : ContentPage
{
    private readonly FeedsView _feeds = new();
    private readonly SettingsView _settings = new();

    public RootPage()
    {
        InitializeComponent();
        Show(RootTab.Feeds);
    }

    public RootPage(ShellViewModel shell) : this() => DataContext = shell;

    public RootTab Tab { get; private set; }

    public void Show(RootTab tab)
    {
        Tab = tab;
        Content = tab == RootTab.Feeds ? _feeds : _settings;
        FeedsTab.Classes.Set("selected", tab == RootTab.Feeds);
        SettingsTab.Classes.Set("selected", tab == RootTab.Settings);
    }

    private void OnFeedsClick(object? sender, RoutedEventArgs e) => Show(RootTab.Feeds);

    private void OnSettingsClick(object? sender, RoutedEventArgs e) => Show(RootTab.Settings);
}
