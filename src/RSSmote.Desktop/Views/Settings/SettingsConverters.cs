using System.Globalization;
using Avalonia.Data.Converters;
using RSSmote.App.ViewModels;

namespace RSSmote.Desktop.Views.Settings;

public static class SettingsConverters
{
    /// <summary>True when the bound section is the one named by the parameter ("ManageFeeds").</summary>
    public static readonly IValueConverter SectionIs = new SectionIsConverter();

    private sealed class SectionIsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is SettingsSection section && parameter is string name && Enum.TryParse<SettingsSection>(name, out var wanted)
            && section == wanted;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
