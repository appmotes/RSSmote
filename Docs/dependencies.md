# Dependencies

Every package must be free and permissively licensed. Nothing that depends on `AvaloniaUI.Licensing`.
Versions checked against nuget.org on 2026-10-19.

| Package | Version | Licence | Used by | Why |
|---|---|---|---|---|
| Avalonia | 12.1.4 | MIT | App | UI framework |
| Avalonia.Desktop | 12.1.4 | MIT | Desktop | Windowing on macOS/Windows/Linux |
| Avalonia.Android | 12.1.4 | MIT | Android | Android head (A15); brings Xamarin.AndroidX.AppCompat, DocumentFile and Window (MIT) |
| Avalonia.iOS | 12.1.4 | MIT | iOS | iOS head (A15) |
| Avalonia.Themes.Fluent | 12.1.4 | MIT | App | Base theme, restyled by our tokens |
| Avalonia.Fonts.Inter | 12.1.4 | MIT (font: OFL) | App | Same font on every OS (D13) |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | App | View-models (A7) |
| Microsoft.Data.Sqlite | 10.0.13 | MIT | Data | SQLite (A8); native `e_sqlite3` for every RID via SQLitePCLRaw (Apache-2.0) |
| Avalonia.Controls.WebView | 12.1.1 | MIT | App | Article view in the OS web engine (A9) |
| Avalonia.HtmlRenderer | 12.0.0 | MIT | App | Article fallback where no web engine is installed (A9) |
| HtmlSanitizer | 9.2.1039 | MIT | Core | Strips active content from feed HTML (A9); brings AngleSharp and AngleSharp.Css (MIT) |
| Avalonia.Skia | 12.1.4 | MIT | App.Tests, Desktop.Tests | Real drawing in headless tests |
| Avalonia.Headless.XUnit | 12.1.4 | MIT | App.Tests, Desktop.Tests | Headless platform for xUnit v3 |
| xunit.v3 | 3.2.2 | Apache-2.0 | tests | Matches Avalonia.Headless.XUnit's xUnit 3.2 dependency |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | tests | `dotnet test` runner |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | tests | Test host |
| SkiaSharp | 3.119.4 | MIT | build/AppIcons.cs | Draws the app icon for packaging (A2); the same version Avalonia.Skia brings |
| SkiaSharp.NativeAssets.Linux | 3.119.4 | MIT | build/AppIcons.cs | SkiaSharp's native library when packaging on Linux |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 | MIT | Data.Tests, App.Tests, Desktop.Tests | `FakeTimeProvider` for retention and the refresh timer |

**Assets**

| Asset | Licence | Notes |
|---|---|---|
| Lucide icon paths | ISC | Copied geometry, notice in `src/RSSmote.App/Assets/Icons/LICENSE-lucide.txt` |
| Inter font | OFL 1.1 | Shipped by Avalonia.Fonts.Inter |

**Not used, on purpose**

| Package | Reason |
|---|---|
| Avalonia.Controls.TreeDataGrid 12.x | Commercial (`AvaloniaUI.Licensing`) |
| Avalonia.Controls.DataGrid | Deprecated; `TableView` in core Avalonia replaces it (D17) |
| Avalonia.Controls.ItemsRepeater | Not supported from Avalonia 12 (P6) |
| Avalonia.Controls.Markdown / Documents | Commercial |
| AvaloniaUI.DiagnosticsSupport / Developer Tools | Opening the tools needs a licence |
| Parcel | Proprietary packager |
| EF Core | Heavy, AOT experimental; plain ADO.NET is enough |
| CodeHollow.FeedReader | Unmaintained since 2023 |

**Later (packaging)**: Velopack 1.2.x (MIT) for installers and updates — decided when packaging starts.
