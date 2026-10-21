using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace RSSmote.Mobile.Spike;

/// <summary>Temporary spike app: one text view with the <see cref="PlatformCheck"/> report.</summary>
public sealed class SpikeApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IActivityApplicationLifetime activity)
            activity.MainViewFactory = CreateView;
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            single.MainView = CreateView();
        base.OnFrameworkInitializationCompleted();
    }

    private static Control CreateView()
    {
        var report = new SelectableTextBlock
        {
            Text = "Running checks…",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(16, 64, 16, 32),
        };
        _ = ShowAsync(report);
        return new ScrollViewer { Content = report };
    }

    private static async Task ShowAsync(SelectableTextBlock report)
    {
        var lines = await Task.Run(PlatformCheck.RunAsync);
        foreach (var line in lines)
            Console.WriteLine("[spike] " + line);
        report.Text = string.Join("\n", lines);
    }
}
