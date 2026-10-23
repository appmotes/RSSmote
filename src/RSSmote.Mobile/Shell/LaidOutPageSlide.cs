using Avalonia;
using Avalonia.Animation;
using Avalonia.Layout;

namespace RSSmote.Mobile.Shell;

/// <summary>
/// The theme's page slide with one change: the incoming page is laid out where it ends up before it is moved aside to
/// slide in, so a list on it realises the rows it shows instead of none (P20).
/// </summary>
public sealed class LaidOutPageSlide : PageSlide
{
    public LaidOutPageSlide(PageSlide slide) : base(slide.Duration, slide.Orientation)
    {
        SlideInEasing = slide.SlideInEasing;
        SlideOutEasing = slide.SlideOutEasing;
        FillMode = slide.FillMode;
    }

    public override Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        LayOutInPlace(to);
        return base.Start(from, to, forward, cancellationToken);
    }

    /// <summary>Runs a layout pass while <paramref name="to"/> carries no slide offset.</summary>
    public static void LayOutInPlace(Visual? to) => (to as Layoutable)?.UpdateLayout();
}
