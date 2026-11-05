using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
using RSSmote.App.Views.Desktop;
using RSSmote.Core.Models;

namespace RSSmote.Desktop.Tests;

/// <summary>Every scroll bar is a bare, rounded thumb that shows while its content moves (D32).</summary>
public class ScrollBarTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    [AvaloniaFact]
    public void The_list_bar_is_a_hidden_thumb_until_the_list_moves_and_hides_again_after()
    {
        using var f = new ShellFixture();
        f.Shell.ShowFilterCommand.Execute(StreamFilter.All);
        var view = new ShellView();
        var window = new Window
        {
            Width = 1045,
            Height = 260,
            Content = view,
            DataContext = f.Shell,
        };
        window.Show();
        Settle();

        var list = view.GetVisualDescendants().OfType<ArticleListView>().Single();
        var viewer = list.GetVisualDescendants().OfType<ScrollViewer>().First(v => v.Extent.Height > v.Viewport.Height);
        var bar = viewer.GetVisualDescendants().OfType<ScrollBar>().Single(b => b.Orientation == Orientation.Vertical);
        Assert.True(bar.IsVisible);
        Assert.Equal(0, bar.Opacity);
        Assert.Empty(bar.GetVisualDescendants().OfType<RepeatButton>());
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
        Assert.Equal(8, thumb.Bounds.Width);
        Assert.Equal(14, bar.Bounds.Width);
        Assert.Equal(viewer.Bounds.Width, viewer.Viewport.Width);

        viewer.Offset = new Vector(0, viewer.Extent.Height);
        Settle();
        Assert.Contains(ScrollActivity.Class, viewer.Classes);
        Assert.True(thumb.Bounds.Y > bar.Bounds.Height / 2, "The thumb sits at the bottom of a list scrolled to its end.");

        Thread.Sleep(ScrollActivity.Linger + TimeSpan.FromMilliseconds(200));
        Settle();
        Assert.DoesNotContain(ScrollActivity.Class, viewer.Classes);
        window.Close();
    }
}
