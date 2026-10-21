using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using RSSmote.Core.Models;

namespace RSSmote.App.Controls.Templates;

/// <summary>
/// The list's filter control in its title strip (D9): starred, unread and all as three icon segments; the active one
/// is raised. <see cref="Filter"/> binds two-way.
/// </summary>
[TemplatePart("PART_Starred", typeof(Button))]
[TemplatePart("PART_Unread", typeof(Button))]
[TemplatePart("PART_All", typeof(Button))]
public class FilterBar : TemplatedControl
{
    public static readonly StyledProperty<StreamFilter> FilterProperty =
        AvaloniaProperty.Register<FilterBar, StreamFilter>(nameof(Filter), StreamFilter.Unread, defaultBindingMode: BindingMode.TwoWay);

    private readonly Dictionary<StreamFilter, Button> _segments = new();

    public StreamFilter Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var button in _segments.Values)
            button.Click -= OnSegmentClick;
        _segments.Clear();

        Add(StreamFilter.Starred, e.NameScope.Find<Button>("PART_Starred"));
        Add(StreamFilter.Unread, e.NameScope.Find<Button>("PART_Unread"));
        Add(StreamFilter.All, e.NameScope.Find<Button>("PART_All"));
        UpdateSegments();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FilterProperty)
            UpdateSegments();
    }

    private void Add(StreamFilter filter, Button? button)
    {
        if (button is null)
            return;
        button.Tag = filter;
        button.Click += OnSegmentClick;
        _segments[filter] = button;
    }

    private void OnSegmentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: StreamFilter filter })
            Filter = filter;
    }

    private void UpdateSegments()
    {
        foreach (var (filter, button) in _segments)
            button.Classes.Set("selected", filter == Filter);
    }
}
