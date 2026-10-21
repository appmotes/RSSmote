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
                             (shell, columns, Panels/), Services/ (ProcessLauncher), per-OS packaging metadata
src/RSSmote.Mobile           phone and tablet screens, shared by both mobile heads (plain net10.0, A15)
src/RSSmote.Android          Android head: AndroidApp, MainActivity, manifest
src/RSSmote.iOS              iOS head: AppDelegate, Program, Info.plist
tests/RSSmote.App.Tests      headless tests: view-models, palette key sets, gallery snapshots per theme (xUnit v3)
tests/RSSmote.Desktop.Tests  headless tests of the desktop screens: shell renders, panels, menus, captures
tests/RSSmote.Core.Tests     parser, dates, HTML helpers, feed discovery (fake HTTP), OPML
tests/RSSmote.Data.Tests     store: upsert, filters, counts, retention, migrations, macOS-library compatibility
build/                       per-OS packaging scripts
Docs/
```

Dependencies point one way: `Desktop → App → Data → Core` and `Android, iOS → Mobile → App`; Desktop and Mobile
never reference each other. Core and Data never reference Avalonia.
`App` holds building blocks and no screens: a `.axaml` with `x:DataType` on a view-model is a screen and lives in
a head; a `ControlTheme` or style lives in `App`. `RSSmote.Desktop.Tests` compiles `ShellFixture` and
`TestAppBuilder` from `RSSmote.App.Tests` as linked files.

**A2 — One desktop head for all three OSes.** `RSSmote.Desktop` is plain `net10.0` (no `-macos` workload),
published per RID: `osx-arm64`, `osx-x64`, `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`. The macOS
`.app` bundle (Info.plist, icns, ad-hoc or Developer ID signature) is assembled by `build/package-macos.sh`.

**A3 — Composition root, no container.** `App` creates `AppServices` once (store, settings, feed finder,
refresher, image cache, launcher, UI dispatcher, clock), applies the theme — the palette of the slot the settings
name, or for Automatic the slot the OS asks for, re-applied when either changes (D22) — builds `ShellViewModel` from
those services, opens the main window and starts the refresher; shutdown disposes them. The head passes an
`IShellHost` (`App.Host`) that supplies its launcher and creates its main window, so `App` never names a screen:
`AppBuilder.Configure(() => new App { Host = new DesktopShellHost() })`. View-models get services
through constructors and never reach for a static, so tests build them over a temporary library.

**A4 — Data folder and platform seams.** `AppFolders` resolves the data folder per OS in plain .NET (no seam
needed); `RSSMOTE_DATA_DIR` overrides it. The library is `rssmote.sqlite`, preferences `settings.json`, side by
side. `ILauncher` (open a URL in the browser, honouring "open in background" where the OS allows) is the
remaining seam; Desktop supplies `ProcessLauncher` through its `IShellHost` (A3).

| OS | Data folder |
|---|---|
| macOS | `~/Library/Application Support/com.ahrentlov.rssmote/` |
| Windows | `%LOCALAPPDATA%\RSSmote\` |
| Linux | `$XDG_DATA_HOME/rssmote/` (fallback `~/.local/share/rssmote/`) |
| iOS | `<app container>/Library/Application Support/` |
| Android | `/data/user/0/com.ahrentlov.rssmote/files/` (the app's private files folder) |

`OperatingSystem.IsMacOS()` is false on iOS and `IsLinux()` is false on Android, so each mobile OS needs its own
branch; without one it lands in the Linux fallback.

**A5 — Desktop shell.** Everything here lives in `RSSmote.Desktop`. `MainWindow` hosts `ShellView`: a `Grid` with three columns (sidebar · list · article) and two
invisible 5 pt `GridSplitter`s, the first on the list card's left edge, the second in the 8 pt gap between the cards.
The window background is the canvas (`Bg.Sidebar`); the sidebar sits on it and the list and article views are
cards (`Border.card` + `Border.cardClip`, D20, L12). Each column view (`SidebarView`, `ArticleListView`,
`ArticleView`) takes the `ShellViewModel` and owns its `TitleStrip` (D9): 52 pt on the sidebar, 44 pt inside a
card, where an 8 pt band of canvas above the cards is also a title-bar drag area. On macOS and Windows the window
extends its client area into a 52 pt title bar; `Views/Chrome` gives the per-OS strip insets (traffic lights
left on macOS, caption buttons right on Windows). Linux keeps system decorations (P2). The window handles single-key shortcuts in the tunnel phase
(D15, L3); macOS also gets a `NativeMenu`, Windows and Linux a menu button in the sidebar strip (D16).

**A6 — Overlays, not windows.** One panel at a time lives on `ShellViewModel.Panel` (a `PanelViewModel`: Add Feed,
Edit Feed, Edit Folder, or a `ConfirmViewModel` before anything that cannot be undone). `ShellView` shows it
through a `DataTemplate` per type, in a `PanelFrame` centred in the window over all three columns, on a scrim. The
article's native web view would draw over it, so `ArticleHost.IsObscured` hides that view while a panel is open (L9).
A click on the scrim, Escape, Cancel or the close button ends a panel; Save sits in the footer and answers Enter.
While a panel is open the window's single-key shortcuts are off.
Right-clicking a sidebar row opens its menu without selecting it: feeds get Open site, Reload Icon, Edit… and
Remove Feed…; folders get Edit… (rename, its feeds, Delete Folder…, which moves the feeds out). Folder choice in
Add Feed and Edit Feed is the shared `FolderPickerViewModel`. Settings is the only separate `Window`.

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

**A10 — Feed pipeline.** `FeedFinder` (HTTP + `<link rel="alternate">` discovery, then common feed paths; takes an
`HttpClient` so tests fake the network) → `FeedParser` (own `XDocument` parser for RSS 2.0 / Atom / RDF matched by
local name, `JsonDocument` for JSON Feed; HTML entities such as `&nbsp;` are rewritten as numeric references and
parsed once more; `FeedDates` reads ISO 8601 and RFC 822 with named zones, no zone = UTC) →
`Store.Upsert` (new items inserted, existing ones refreshed, read/star flags kept). `Refresher` runs on a timer
from `TimeProvider.CreateTimer` (manual / 15 / 30 / 60 / 120 min, rescheduled when the setting changes) with at
most six feeds in flight; a refresh asked for while one runs joins it. Each feed's failure is stored in
`last_error` and cleared by the next success; a feed whose title is still its URL takes the feed's own title.
After the pass it prunes read, unstarred items older than the retention setting and stamps
`AppSettings.LastRefresh`. `Refresher.AddFeed` stores a validated `FoundFeed` with its current items. `StateChanged` fires
on a background thread when a pass starts and after it ends.

**A11 — Threading.** Network and SQLite work run off the UI thread; results are marshalled with
`Dispatcher.UIThread.Post`. View-models expose `ObservableCollection`s that are only mutated on the UI thread.

**A12 — Headless verification.** `RSSmote.App.Tests` and `RSSmote.Desktop.Tests` boot the real `App` (styles, palettes, control themes) on
Avalonia's headless platform with Skia drawing, so a test can render any view to a PNG without opening a window.
This is how visuals are checked before the user runs the app. `DesignCaptureTests` renders every screen at 1280 × 800
over a copy of a real library (with real favicons and thumbnails) when `RSSMOTE_CAPTURE_DIR` and
`RSSMOTE_CAPTURE_DB` are set; otherwise it does nothing. `ReadmeCaptureTests` renders the README's screenshots
over an invented library (made-up feeds and articles, favicons and thumbnails drawn in the test) into
`RSSMOTE_README_DIR`, which is `Docs/images/`.

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
and `AppDelegate` (`AvaloniaAppDelegate<App>`) add `WithInterFont()` in `CustomizeAppBuilder`; an `IShellHost` goes
in through `CreateAppBuilder` (`AppBuilder.Configure(() => new App { Host = … })`). Android starts `App` under
`IActivityApplicationLifetime`, iOS under `ISingleViewApplicationLifetime`; `App` does not handle either yet, so
the heads start to an empty view. The heads need the `android` and `ios` workloads and live only in
`RSSmote.Mobile.slnx`; `RSSmote.slnx` builds `RSSmote.Mobile` but not the heads, so the desktop build needs no
mobile tooling. Building a head:

```
dotnet build src/RSSmote.Android/RSSmote.Android.csproj
dotnet build src/RSSmote.iOS/RSSmote.iOS.csproj -r iossimulator-arm64
```

Android needs the API 36 platform and JDK 17 or later, and a Debug build reaches an emulator through `-t:Install`
(P12); iOS needs the Xcode version the `ios` workload names.
