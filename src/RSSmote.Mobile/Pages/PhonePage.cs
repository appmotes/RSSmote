using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RSSmote.App.Controls.Headless;

namespace RSSmote.Mobile.Pages;

/// <summary>
/// A pushed settings page (D27): back names the page below, the title sits centred in the bar, the page shows the
/// canvas. The shell hands it only the bottom inset, which it keeps its content clear of (P15).
/// </summary>
public class PhonePage : ContentPage
{
    public PhonePage()
    {
        AutomaticallyApplySafeAreaPadding = false;
        Bind(BackgroundProperty, this.GetResourceObservable("Brush.Bg.Sidebar"));
    }

    /// <summary>The page's name in the bar.</summary>
    public string Title
    {
        get;
        set
        {
            field = value;
            Header = new TextBlock { Text = value, Classes = { "phonePageTitle" } };
        }
    } = "";

    /// <summary>The name of the page back returns to, after the chevron; both take the bar's accent.</summary>
    public string BackTitle
    {
        get;
        set
        {
            field = value;
            NavigationPage.SetBackButtonContent(this, new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                Children =
                {
                    new Icon { Data = Application.Current?.FindResource("Icon.ChevronLeft") as Geometry, Size = 24 },
                    new TextBlock
                    {
                        Text = value,
                        FontSize = Application.Current?.FindResource("TextTheme.Phone.Row.Size") as double? ?? 16,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            });
        }
    } = "";

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SafeAreaPaddingProperty)
            Padding = new Thickness(0, 0, 0, SafeAreaPadding.Bottom);
    }

    /// <summary>A text button at the right of the bar: Save, New Folder. The bar is not inside the page, so the
    /// button takes the page's data context from here (P21).</summary>
    public Control? BarButton
    {
        get => NavigationPage.GetTopCommandBar(this);
        set
        {
            value?.Bind(DataContextProperty, this.GetObservable(DataContextProperty));
            NavigationPage.SetTopCommandBar(this, value);
        }
    }
}
