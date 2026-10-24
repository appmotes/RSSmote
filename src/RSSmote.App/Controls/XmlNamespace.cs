using Avalonia.Metadata;

// One XAML namespace (xmlns:c) for both control folders, so markup reads c:Icon and c:TitleStrip alike.
[assembly: XmlnsDefinition("urn:RSSmote.App.Controls", "RSSmote.App.Controls.Templates")]
[assembly: XmlnsDefinition("urn:RSSmote.App.Controls", "RSSmote.App.Controls.Headless")]
[assembly: XmlnsPrefix("urn:RSSmote.App.Controls", "c")]
