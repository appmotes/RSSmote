# Dependencies

Every package must be free and permissively licensed. Nothing that depends on `AvaloniaUI.Licensing`.
Versions checked against nuget.org on 2026-10-19; Avalonia.Browser and Microsoft.AspNetCore.Mvc.Testing on 2026-11-03.
The server runs on ASP.NET Core (MIT), part of the .NET 10 shared framework, so it needs no package.

| Package | Version | Licence | Used by | Why |
|---|---|---|---|---|
| Avalonia | 12.1.4 | MIT | App | UI framework |
| Avalonia.Browser | 12.1.4 | MIT | Browser | WebAssembly head (A23); brings Avalonia.Skia and Avalonia.HarfBuzz (MIT) |
| Avalonia.Themes.Fluent | 12.1.4 | MIT | App | Base theme, restyled by our tokens |
| Avalonia.Fonts.Inter | 12.1.4 | MIT (font: OFL) | App | Same font on every OS (D13) |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | App | View-models (A7) |
| Microsoft.Data.Sqlite | 10.0.13 | MIT | Data | SQLite (A8), on the server only; native `e_sqlite3` for every RID via SQLitePCLRaw (Apache-2.0) |
| Avalonia.Controls.WebView | 12.1.1 | MIT | App | Article view: an `<iframe srcdoc>` in a browser (A23), the OS web engine elsewhere (A9) |
| Avalonia.HtmlRenderer | 12.0.0 | MIT | App | Article fallback where no web engine is installed (A9) |
| HtmlSanitizer | 9.2.1039 | MIT | Core | Strips active content from feed HTML (A9); brings AngleSharp and AngleSharp.Css (MIT) |
| Avalonia.Skia | 12.1.4 | MIT | App, Desktop and Mobile tests | Real drawing in headless tests |
| Avalonia.Headless.XUnit | 12.1.4 | MIT | App, Desktop and Mobile tests | Headless platform for xUnit v3 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | MIT | Server.Tests | The server in process (`WebApplicationFactory`) |
| xunit.v3 | 3.2.2 | Apache-2.0 | tests | Matches Avalonia.Headless.XUnit's xUnit 3.2 dependency |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | tests | `dotnet test` runner |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | tests | Test host |
| SkiaSharp | 3.119.4 | MIT | build/AppIcons.cs | Draws the app icon (D13); the same version Avalonia.Skia brings |
| SkiaSharp.NativeAssets.Linux | 3.119.4 | MIT | build/AppIcons.cs | SkiaSharp's native library when drawing the icon on Linux |
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

**Build tools**: the `wasm-tools` workload (MIT) relinks the browser runtime on publish; the Dockerfile installs it.
