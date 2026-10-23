using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using RSSmote.Core.Models;

namespace RSSmote.Mobile.Views;

/// <summary>Converters the phone views share.</summary>
public static class PhoneConverters
{
    /// <summary>A filter's icon (D12): Starred a star, Unread a dotted circle, All Items lines.</summary>
    public static readonly IValueConverter FilterIcon = new FuncValueConverter<StreamFilter, Geometry?>(filter =>
        Application.Current is { } app && app.TryGetResource(filter switch
        {
            StreamFilter.Starred => "Icon.Star",
            StreamFilter.Unread => "Icon.CircleDot",
            _ => "Icon.AlignLeft",
        }, null, out var icon) ? icon as Geometry : null);

    /// <summary>
    /// The top or the bottom of a page's safe-area insets. Pages place them themselves: the content under the top
    /// inset, a bottom bar over the bottom one, because a page's own padding never reaches its command bar (P15).
    /// </summary>
    public static readonly IValueConverter TopInset = new FuncValueConverter<Thickness, Thickness>(t => new Thickness(0, t.Top, 0, 0));

    public static readonly IValueConverter BottomInset = new FuncValueConverter<Thickness, Thickness>(t => new Thickness(0, 0, 0, t.Bottom));

    /// <summary>True when the bound filter is the one named by the parameter ("Starred", "Unread", "All").</summary>
    public static readonly IValueConverter FilterIs = new FilterIsConverter();

    private sealed class FilterIsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is StreamFilter filter && parameter is string name && Enum.TryParse<StreamFilter>(name, out var wanted) && filter == wanted;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
