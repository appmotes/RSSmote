# Architecture

How the app is put together. Ids are append-only (see CLAUDE.md).

**A1 — Projects.**

```
RSSmote.slnx
Directory.Build.props        net10.0, Nullable, implicit usings, warnings as errors in Release
Directory.Packages.props     central package versions (see dependencies.md)
NuGet.config                 nuget.org only
src/RSSmote.Core             models, feed parsing, OPML, HTML helpers, service interfaces — no Avalonia
src/RSSmote.Data             SQLite store, settings store, refresher, data folder — no Avalonia
src/RSSmote.App              shared Avalonia UI with no screens: view-models, controls, styles, themes, assets, services
src/RSSmote.Desktop          the desktop executable and its screens: Program.cs, Shell/ (MainWindow), Views/
                             (shell, columns, Settings/), Services/ (ProcessLauncher), per-OS packaging metadata
src/RSSmote.Mobile           phone and tablet screens, shared by both mobile heads (plain net10.0, A15):
                             MobileShellHost, Shell/ (MobileShell, RootPage), Views/, Services/ (TopLevelLauncher)
src/RSSmote.Android          Android head: AndroidApp, MainActivity, manifest
src/RSSmote.iOS              iOS head: AppDelegate, Program, Info.plist
tests/RSSmote.App.Tests      headless tests: view-models, palette key sets, gallery snapshots per theme (xUnit v3)
tests/RSSmote.Desktop.Tests  headless tests of the desktop screens: shell renders, Settings, menus, captures
tests/RSSmote.Mobile.Tests   headless tests of the phone screens, rendered at 402 × 874 in Sand and Ember
tests/RSSmote.Core.Tests     parser, dates, HTML helpers, feed discovery (fake HTTP), OPML
tests/RSSmote.Data.Tests     store: upsert, filters, counts, retention, migrations, macOS-library compatibility
build/                       per-OS packaging scripts
Docs/
```

Dependencies point one way: `Desktop → App → Data → Core` and `Android, iOS → Mobile → App`; Desktop and Mobile
never reference each other. Core and Data never reference Avalonia.
`App` holds building blocks and no screens: a `.axaml` with `x:DataType` on a view-model is a screen and lives in
a head; a `ControlTheme` or style lives in `App`. `RSSmote.Desktop.Tests` compiles `ShellFixture` and
`TestAppBuilder` from `RSSmote.App.Tests` as linked files, and so does `RSSmote.Mobile.Tests`.

**A2 — One desktop head for all three OSes.** `RSSmote.Desktop` is plain `net10.0` (no `-macos` workload),
published per RID: `osx-arm64`, `osx-x64`, `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`. The macOS
`.app` bundle (Info.plist, icns, ad-hoc or Developer ID signature) is assembled by `build/package-macos.sh`.

**A3 — Composition root, no container.** `App` creates `AppServices` once (store, settings, feed finder,
refresher, image cache, launcher, UI dispatcher, clock), applies the theme — the palette of the slot the settings
name, or for Automatic the slot the OS asks for, re-applied when either changes (D22) — builds `ShellViewModel` from
those services, creates the root control and starts the refresher; desktop shutdown disposes them. The head passes an
`IShellHost` (`App.Host`) that supplies its launcher and its `Layout` (desktop columns or phone stack) and creates
its root (`CreateRoot(shell)`), so `App` never names a screen: `AppBuilder.Configure(() => new App { Host = new DesktopShellHost() })`. `App` handles three lifetimes:

| Lifetime | Head | Root goes to | File dialogs belong to |
|---|---|---|---|
| `IClassicDesktopStyleApplicationLifetime` | Desktop | `MainWindow`; a root that is not a `Window` throws | the main window |
| `IActivityApplicationLifetime` | Android | `MainViewFactory`, called per activity; every view shares one shell | `TopLevel.GetTopLevel(view)` |
| `ISingleViewApplicationLifetime` | iOS | `MainView` | `TopLevel.GetTopLevel(MainView)` |

Without a lifetime (headless tests) `App` creates nothing. View-models get services
through constructors and never reach for a static, so tests build them over a temporary library.

**A4 — Data folder and platform seams.** `AppFolders` resolves the data folder per OS in plain .NET (no seam
needed); `RSSMOTE_DATA_DIR` overrides it. The library is `rssmote.sqlite`, preferences `settings.json`, side by
side. `ILauncher` (open a URL in the browser, honouring "open in background" where the OS allows) is the
remaining seam; each head supplies one through its `IShellHost` (A3): Desktop `ProcessLauncher`, the phone heads
`TopLevelLauncher`, which hands the link to the top level's `Launcher` and ignores "open in background".

| OS | Data folder |
|---|---|
| macOS | `~/Library/Application Support/com.ahrentlov.rssmote/` |
| Windows | `%LOCALAPPDATA%\RSSmote\` |
| Linux | `$XDG_DATA_HOME/rssmote/` (fallback `~/.local/share/rssmote/`) |
| iOS | `<app container>/Library/Application Support/` |
| Android | `/data/user/0/com.ahrentlov.rssmote/files/` (the app's private files folder) |

`OperatingSystem.IsMacOS()` is false on iOS and `IsLinux()` is false on Android, so each mobile OS needs its own
branch; without one it lands in the Linux fallback.

`IFilePicker.OpenTextAsync` returns the chosen file's name with its text (`PickedText`), which Import Feeds shows.

**A5 — Desktop shell.** Everything here lives in `RSSmote.Desktop`. `MainWindow` hosts `ShellView`: a `Grid` with three columns (sidebar · list · article), or Settings in their place (A6), and two
invisible 5 pt `GridSplitter`s, the first on the list card's left edge, the second in the 8 pt gap between the cards.
The window background is the canvas (`Bg.Sidebar`); the sidebar sits on it and the list and article views are
cards (`Border.card` + `Border.cardClip`, D20, L12). Each column view (`SidebarView`, `ArticleListView`,
`ArticleView`) takes the `ShellViewModel` and owns its `TitleStrip` (D9): 52 pt on the sidebar, 44 pt inside a
card, where an 8 pt band of canvas above the cards is also a title-bar drag area. The sidebar's strip holds Refresh
(and the menu button on Windows and Linux); its status bar the refresh state and the Settings button. On macOS and
Windows the window extends its client area into a 52 pt title bar; `Views/Chrome` gives the per-OS strip insets (traffic lights
left on macOS, caption buttons right on Windows). Linux keeps system decorations (P2). The window handles single-key shortcuts in the tunnel phase
(D15, L3); macOS also gets a `NativeMenu`, Windows and Linux a menu button in the sidebar strip (D16).

**A6 — Settings are a mode of the window (D30).** `ShellViewModel.SettingsMode` holds a `SettingsModeViewModel`
while Settings show, and `IsSettingsOpen` says so; `OpenSettingsCommand` (with a `SettingsSection`, or none for
Articles & Refresh) creates it or moves it to a section, and `CloseSettings` disposes it. `ShellView` hides the three
columns and shows `Views/Settings/SettingsView` in their place: the menu, and the mode's `Page` through a
`DataTemplate` per view-model type. The pages are the phone's view-models (A16): Add Feed, `ManageFeedsViewModel`,
Import and Export Feeds, and `AppearancePageViewModel` / `ReadingPageViewModel` over the shared `SettingsViewModel`.
Under Manage Feeds the mode stacks Edit Feed, Edit Folder and New Folder (`PanelViewModel`s from `ShellViewModel.Create*`);
one closing (Save) pops it and any page above it, a menu item replaces the whole stack, and each page removed is
disposed, so Manage Feeds stops following the store when it goes. `Confirm` holds a `ConfirmViewModel` from
`ShellViewModel.Confirm*`, shown in a `PanelFrame` over the window; a click on its scrim or Escape cancels it. The
article's native web view would draw over Settings, so `ArticleHost.IsObscured` follows `IsSettingsOpen` (L9).
While Settings show the window's single-key shortcuts are off; Escape cancels the question or leaves, ⌘/Ctrl+N moves
to Add Feed. The macOS application menu gets "Settings…" from `MainWindow.AddSettingsItem`, as it belongs to the
application, not the window. Right-clicking a sidebar row opens its menu without selecting it: feeds get Open site
and Reload Icon; folders and the smart rows have none. Right-clicking an article row opens its menu the same way:
Mark as Read (or Unread), Mark Above as Read and Mark Below as Read (the unread rows as listed, under the filter, the
search and the order; off when there are none), Star (or Unstar) and Open in Browser. They act on that row, never on
the article being read, and marked rows stay listed until the next rebuild. There is no separate window.

**A7 — MVVM.** CommunityToolkit.Mvvm source generators (`[ObservableProperty]` on partial properties,
`[RelayCommand]`). Compiled bindings everywhere (`x:DataType` on every view and template). Views are created
explicitly by their parent or by a `DataTemplate` keyed on the view-model type; no reflection ViewLocator.
`ShellViewModel` owns `SidebarViewModel`, `ArticleListViewModel` and `ArticleViewModel`; selection flows
sidebar → list → article through events. Store and settings events are applied on the UI thread; a flag-only
change updates rows in place and recounts the sidebar, a structural change (refresh, adding a feed) is debounced
into one full reload (300 ms). Rows load favicons and thumbnails only when their container is realised.

**A8 — Storage.** One SQLite file (`rssmote.sqlite`): `folders`, `feeds`, `items`, versioned with
`PRAGMA user_version` and forward-only migration steps. Version 1 is exactly the macOS app's tables, created
with `IF NOT EXISTS`, so a library that app wrote (at version 0) opens as-is and is stamped 1. `published` is
REAL Unix seconds, as there. All access goes through `Store` on one connection guarded by a lock; writes raise
`Changed` after the lock is released (with item ids for flag-only changes) so lists update in place. The
`TimeProvider` passed to `Store` is the clock for retention and undated items. Settings (`AppSettings` in Core)
are a JSON file next to it, written by `SettingsStore` with source-generated `System.Text.Json`: enums as text,
atomic replace via a temp file, values normalised into range, defaults when the file is missing or unreadable.
`Current` is an immutable snapshot; `Update(change)` is the only way to write. Normalising also carries older
files forward: a stored `Black` theme becomes Dark with the `black` palette in the dark slot (D22).

**A9 — Article rendering.** `ArticleDocument.Create` (Core) sanitizes the item once with `ArticleSanitizer`
(HtmlSanitizer: no scripts, styles, forms or inline styles; URLs absolute against the article; lazy images given
their real source; iframes kept only for YouTube/Vimeo, others become links). `ArticleViewModel` rebuilds the
document only when another article is selected, so flag changes do not reload the page. `ArticleHost` renders it
through `ArticleTemplate` (embedded `article.css` and `article.js`) with the palette, font and size, and renders
again when any of them or the theme changes. It uses `NativeWebView` (`NavigateToString`, no base address) when
`WebViewAdapterInfo` reports an installed engine, otherwise an `HtmlLabel` (Avalonia.HtmlRenderer) in a
`ScrollViewer`, which gets the same page without the script and with video frames as links.
`RSSMOTE_ARTICLE_RENDERER=fallback` forces the fallback; tests always use it. The page never navigates: its
script posts link clicks and shortcut keys through `invokeCSharpAction` (L9), any other top-level navigation after
the initial load is cancelled, and both arrive as routed events (`LinkClickedEvent`, `ShortcutEvent`). Space
scrolls through `rssmote.scrollPage`, which answers false at the end so the shell moves to the next article.

`ArticleHost` reports the reader's scroll position (`Scrolled`, offset and 0–1 progress) and taps off links
(`PageTapped`): the fallback from its `ScrollViewer`, the native view through `scroll` and `tap` messages from
`article.js`, sent at most once a frame.

**A10 — Feed pipeline.** `FeedFinder` (HTTP + `<link rel="alternate">` discovery, then common feed paths; takes an
`HttpClient` so tests fake the network) → `FeedParser` (own `XDocument` parser for RSS 2.0 / Atom / RDF matched by
local name, `JsonDocument` for JSON Feed; HTML entities such as `&nbsp;` are rewritten as numeric references and
parsed once more; `FeedDates` reads ISO 8601 and RFC 822 with named zones, no zone = UTC) →
`Store.Upsert` (new items inserted, existing ones refreshed, read/star flags kept). `Refresher` runs on a timer
from `TimeProvider.CreateTimer` (manual / 15 / 30 / 60 / 120 min, rescheduled when the setting changes) with at
most six feeds in flight; a refresh asked for while one runs joins it. Each feed's failure is stored in
`last_error` and cleared by the next success; a feed whose title is still its URL takes the feed's own title.
After the pass it marks unread items older than `MarkReadAfterDays` read (default 5, 0 = never; also at once when the
setting changes), prunes read, unstarred items older than the retention setting and stamps
`AppSettings.LastRefresh`. `Refresher.AddFeed` stores a validated `FoundFeed` with its current items. `StateChanged` fires
on a background thread when a pass starts and after it ends.

**A11 — Threading.** Network and SQLite work run off the UI thread; results are marshalled with
`Dispatcher.UIThread.Post`. View-models expose `ObservableCollection`s that are only mutated on the UI thread.

**A12 — Headless verification.** `RSSmote.App.Tests` and `RSSmote.Desktop.Tests` boot the real `App` (styles, palettes, control themes) on
Avalonia's headless platform with Skia drawing, so a test can render any view to a PNG without opening a window.
This is how visuals are checked before the user runs the app. `DesignCaptureTests` renders every screen at 1280 × 800
over a copy of a real library (with real favicons and thumbnails) when `RSSMOTE_CAPTURE_DIR` and
`RSSMOTE_CAPTURE_DB` are set; otherwise it does nothing. `ReadmeCaptureTests` renders the README's screenshots
over an invented library (`DemoLibrary` in `RSSmote.App.Tests`: made-up feeds and articles, favicons and thumbnails
drawn in the test) into `RSSMOTE_README_DIR`, which is `Docs/images/`: the desktop in `RSSmote.Desktop.Tests` at
1280 × 800, the phone (`phone-*.png`) in `RSSmote.Mobile.Tests` at 402 × 874.

**A13 — Domain names.** The sidebar's selectable source is `ArticleStream` (`All`, `InFolder`, `OfFeed`), not
`Stream`, which would clash with `System.IO.Stream` under implicit usings. `StreamFilter` is Starred / Unread / All.

**A14 — A feed is saved or removed; nothing is subscribed.** There is no account and no subscription state. A
feed is saved only after `FeedFinder` has fetched and parsed it (or it arrives from OPML), and from then on it is
active: every refresh fetches it. Removing a feed deletes it and all of its articles, starred ones included, for
good (`Store.DeleteFeed`, `ON DELETE CASCADE`); adding the address again starts over from what the feed holds
then. The UI says "Add Feed", "Save", "Remove Feed", never "subscribe".

**A15 — Mobile heads.** `RSSmote.Android` (`net10.0-android`, API 24+) and `RSSmote.iOS` (`net10.0-ios`, iOS 15+)
are thin heads over `RSSmote.Mobile`, the same way `RSSmote.Desktop` is over `App`. Both share the id
`com.ahrentlov.rssmote`. Avalonia builds the app itself on mobile: `AndroidApp` (`AvaloniaAndroidApplication<App>`)
and `AppDelegate` (`AvaloniaAppDelegate<App>`) start `App` with `Host = new MobileShellHost()` from
`CreateAppBuilder` (`AppBuilder.Configure(() => new App { Host = … }).UseAndroid()` / `.UseiOS(this)`) and add
`WithInterFont()` in `CustomizeAppBuilder`. Android starts `App` under `IActivityApplicationLifetime`, iOS under
`ISingleViewApplicationLifetime` (A3). The heads need the `android` and `ios` workloads and live only in
`RSSmote.Mobile.slnx`; `RSSmote.slnx` builds `RSSmote.Mobile` but not the heads, so the desktop build needs no
mobile tooling. Building a head:

```
dotnet build src/RSSmote.Android/RSSmote.Android.csproj
dotnet build src/RSSmote.iOS/RSSmote.iOS.csproj -r iossimulator-arm64
```

Android needs the API 36 platform and JDK 17 or later, and a Debug build reaches an emulator through `-t:Install`
(P12), a Release APK through `adb install -r --no-incremental` (P19); iOS needs the Xcode version the `ios` workload names.

**A16 — Phone shell.** `MobileShellHost` creates `MobileShell`, a `NavigationPage` that is the whole stack (D23).
Its root page, `RootPage` (a `ContentPage` with no navigation bar), holds the tab bar in `BottomCommandBar` and shows
`FeedsView` or `SettingsView` as its content; `RootPage.Show(RootTab)` switches them, and both views are created once.
The bar belongs to the root page, so a pushed page has none. `MobileShell` sets the shared `SidebarViewModel` to
`SidebarLayout.Phone` (D24) and drives the stack from the view-models: `Sidebar.StreamSelected` on the root pushes
`ArticleListPage`, `List.ArticleSelected` on the list pushes `ArticlePage`, and popping the article clears the list's
selection. A phone pick never stays selected, so the same row opens again; Previous and Next stay inside the list.
The shell's background and its top bar follow the top page, because the strips under the safe-area insets show the
shell (P14), the shell takes the insets and the system back itself (P15, P18), and it slides pages with
`LaidOutPageSlide`, which lays the incoming page out in place first (P20). Pushed pages live in
`RSSmote.Mobile/Pages`, root content in `Views`; nothing but `MobileShell` holds navigation state. A refresh that
changes only counts updates the sidebar rows in place, so a touch that began on a row is not lost when the rows are
rebuilt. The article list is built only while its page is in the stack: the host's `IShellHost.Layout` tells
`ShellViewModel` to start on the Feeds root without a list, `List.Show` builds it when a stream is picked, and
`List.Hide` on the way back to the root stops store changes from rebuilding rows nobody sees. The list page puts its
large title and the `ListBox` in one `ScrollViewer`, so the title scrolls away with the rows (D25) and the list still
realises only the rows in view. A rebuild keeps the top row where it was (P25).

The Settings tab shows `SettingsView` (D27); its rows push pages from `RSSmote.Mobile/Pages`, which derive from
`PhonePage` (back title, centred title, bar button). Preferences live in `SettingsViewModel` (App), which the desktop
menus bind as `Settings.*`; Manage Feeds has its own `ManageFeedsViewModel`, which follows the store while its page
lives and is disposed when the page leaves. The phone and the desktop Settings mode (A6) share the page view-models
from `ShellViewModel.Create*` and `Confirm*`; the phone pushes them: `MobileShell.PushPanelAsync` pushes a page
for a panel, the panel closing (Save, Add Feed) pops it and a page that goes any other way disposes its panel;
`MobileShell.ConfirmAsync` shows a `ConfirmViewModel` as a modal bottom sheet (D29) and runs what follows a
confirmation only after the sheet is gone (P24). The system back closes a sheet before it pops a page.
Import and Export Feeds are pages too (`ImportFeedsViewModel` previews a file before anything is saved), and Aa
on the article opens `TextSheet` over `SettingsViewModel`'s text size, font and appearance. The article page hides
both bars on a scroll down and shows them on a scroll up, a tap or a new article, from `ArticleHost.Scrolled` and
`PageTapped` (D26). An article whose host is native is pushed without a slide (P29). Back from the background
(`IActivatableLifetime.Activated`), `MobileShell` calls `ShellViewModel.RefreshIfStale`, which refreshes when
automatic refresh is on and the last pass is older than its interval.
