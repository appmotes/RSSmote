using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace RSSmote.App.Controls.Headless;

/// <summary>
/// Strokes an icon geometry from Assets/Icons.axaml (D12). The stroke keeps <see cref="StrokeThickness"/>
/// on screen whatever <see cref="Size"/> is; <see cref="IsFilled"/> also fills the outline.
/// </summary>
public sealed class Icon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> ViewBoxSizeProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(ViewBoxSize), 24);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 1.5); // Double.IconStroke (D12)

    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<Icon, bool>(nameof(IsFilled));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icon>();

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ViewBoxSizeProperty, StrokeThicknessProperty, IsFilledProperty, ForegroundProperty);
        AffectsMeasure<Icon>(SizeProperty);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Edge length of the grid the geometry is drawn on: 24 for icons.</summary>
    public double ViewBoxSize
    {
        get => GetValue(ViewBoxSizeProperty);
        set => SetValue(ViewBoxSizeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool IsFilled
    {
        get => GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush || ViewBoxSize <= 0)
            return;

        var scale = Size / ViewBoxSize;
        var dx = (Bounds.Width - Size) / 2;
        var dy = (Bounds.Height - Size) / 2;
        var pen = new Pen(brush, StrokeThickness / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(dx, dy)))
            context.DrawGeometry(IsFilled ? brush : null, pen, data);
    }
}
