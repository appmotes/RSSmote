using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RSSmote.App.Controls.Templates;

/// <summary>
/// An overlay panel (A6, D9): a sheet as tall as its content, centred in the window on a scrim that catches clicks.
/// Clicking the scrim or pressing Escape raises <see cref="DismissRequested"/>. The style class <c>narrow</c> makes the
/// sheet 360 wide, for confirmations.
/// </summary>
[TemplatePart("PART_Backdrop", typeof(Border))]
public class PanelFrame : ContentControl
{
    public static readonly RoutedEvent<RoutedEventArgs> DismissRequestedEvent =
        RoutedEvent.Register<PanelFrame, RoutedEventArgs>(nameof(DismissRequested), RoutingStrategies.Bubble);

    private Border? _backdrop;

    public event EventHandler<RoutedEventArgs>? DismissRequested
    {
        add => AddHandler(DismissRequestedEvent, value);
        remove => RemoveHandler(DismissRequestedEvent, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_backdrop is not null)
            _backdrop.PointerPressed -= OnBackdropPressed;
        _backdrop = e.NameScope.Find<Border>("PART_Backdrop");
        if (_backdrop is not null)
            _backdrop.PointerPressed += OnBackdropPressed;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            RaiseEvent(new RoutedEventArgs(DismissRequestedEvent));
            e.Handled = true;
        }
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(DismissRequestedEvent));
        e.Handled = true;
    }
}
