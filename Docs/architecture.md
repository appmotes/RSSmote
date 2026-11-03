# Architecture

How the app is put together. Ids are append-only (see CLAUDE.md).

**A1 — Projects.**

```
RSSmote.slnx
Directory.Build.props        net10.0, Nullable, implicit usings, warnings as errors in Release
Directory.Packages.props     central package versions (see dependencies.md)
NuGet.config                 nuget.org only
Dockerfile                   the server with the browser app beside it (A24)
src/RSSmote.Core             models, feed parsing, OPML, HTML helpers, the library contract (ILibrary, ISettingsStore,
                             IRefresher) and the API's types and JSON context (Api/) — no Avalonia
src/RSSmote.Data             SQLite store, settings store, refresher, data folder — the server's only
src/RSSmote.Server           ASP.NET Core: the API, sign-in, rate limits, events, image proxy, the browser app's files
src/RSSmote.Client           the API from a device: ApiClient, RemoteLibrary (the mirror), RemoteSettings,
                             RemoteRefresher, RemoteSession — plain .NET, no Avalonia, no SQLite
src/RSSmote.App              the Avalonia UI: view-models, controls, styles, themes, assets, services, and every
                             screen: Views/MainView (A22), Views/Desktop (the columns), Views/Phone (the stack),
                             Views/SignInView and SessionView (A23)
src/RSSmote.Browser          the WebAssembly head: Program.cs, BrowserStorage, wwwroot (index.html, main.js, interop.js)
tests/RSSmote.App.Tests      headless tests: view-models, palette key sets, gallery snapshots per theme (xUnit v3)
tests/RSSmote.Desktop.Tests  headless tests of the desktop columns: shell renders, Settings, menus, captures
tests/RSSmote.Mobile.Tests   headless tests of the phone stack, rendered at 402 × 874 in Sand and Ember, and MainView
tests/RSSmote.Server.Tests   the API in process (WebApplicationFactory), and two RemoteSessions syncing through it
tests/RSSmote.Core.Tests     parser, dates, HTML helpers, feed discovery (fake HTTP), OPML
tests/RSSmote.Data.Tests     store: upsert, filters, counts, retention, migrations, macOS-library compatibility
build/                       run-local.sh, publish.sh, icons.sh (A24)
Docs/
```

Dependencies point one way: `Server → Data → Core` and `Browser → App → Client → Core`. App never references Data,
so no SQLite reaches the browser; the server never references Avalonia. The view-models see the library only
through `ILibrary`, `ISettingsStore`, `IRefresher` and `IFeedFinder` (Core): on a device those are the `Remote*`
types, and in the headless tests `Store`, `SettingsStore` and `FeedFinder` themselves, so the tests run over a real
SQLite library without a server. The test projects reference Data for that. `RSSmote.Desktop.Tests` and
`RSSmote.Mobile.Tests` compile `ShellFixture` and `TestAppBuilder` from `RSSmote.App.Tests` as linked files.

**A3 — Composition root, no container.** The browser head starts `App` with an `HttpClient` whose base address
is the server and a `BrowserStorage` (A23). `App` makes the `ApiClient` and the device's `RemoteSettings`, applies the
theme — the palette of the slot the settings name, or for Automatic the slot the platform asks for, re-applied when
either changes (D22) — and hands the view to `SessionView`, which signs in and then builds `ShellViewModel` over a
`RemoteSession`. `App` handles `ISingleViewApplicationLifetime` only; without a lifetime (headless tests) it creates
nothing. View-models get services through constructors and never reach for a static, so tests build them over a
temporary library.

**A4 — Data folder and platform seams.** The server keeps the library: `AppFolders` resolves the data folder per
OS in plain .NET; `RSSMOTE_DATA_DIR` overrides it, and the Docker image sets it to `/data`. The library is
`rssmote.sqlite`, the library settings `settings.json`, side by side, and the sign-in cookie's keys in `keys/`.

| OS | Data folder |
|---|---|
| macOS | `~/Library/Application Support/com.ahrentlov.rssmote/` |
| Windows | `%LOCALAPPDATA%\RSSmote\` |
| Linux | `$XDG_DATA_HOME/rssmote/` (fallback `~/.local/share/rssmote/`) |

A device's seams are `ILauncher` (`TopLevelLauncher`, the top level's launcher: a new browser tab, so "open in
background" is ignored), `IFilePicker` (`StorageFilePicker`, the top level's storage provider) and `IDeviceStorage`
(`BrowserStorage`, local storage). `IFilePicker.OpenTextAsync` returns the chosen file's name with its text
(`PickedText`), which Import Feeds shows.

**A5 — Desktop shell.** Everything here lives in `RSSmote.App/Views/Desktop`, and `MainView` shows it from 900 wide
(A22). `ShellView` is a `Grid` with three columns (sidebar · list · article), or Settings in their place (A6), and two
invisible 5 pt `GridSplitter`s, the first on the list card's left edge, the second in the 8 pt gap between the cards.
The window background is the canvas (`Bg.Sidebar`); the sidebar sits on it and the list and article views are
cards (`Border.card` + `Border.cardClip`, D20, L12). Each column view (`SidebarView`, `ArticleListView`,
`ArticleView`) takes the `ShellViewModel` and owns its `TitleStrip` (D9): 52 pt on the sidebar, 44 pt inside a card.
The sidebar's strip holds Refresh and the menu button; its status bar the refresh state and the Settings button. `MainView` handles the single-key shortcuts on the top level in the tunnel
phase, where the keys arrive when nothing has focus (D15, L3); command shortcuts take `App.CommandModifier`: ⌘ on an
Apple keyboard, Ctrl elsewhere (P31).

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
to Add Feed. Right-clicking a sidebar row opens its menu without selecting it: feeds get Open site
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
through `ArticleTemplate` (embedded `article.css` and `article.js`) with the palette and the body's size (D31), and
renders again when either or the theme changes. It uses `NativeWebView` (`NavigateToString`, no base address) in a
browser, where it is an iframe (A23), and where `WebViewAdapterInfo` reports an installed engine; otherwise an `HtmlLabel` (Avalonia.HtmlRenderer) in a
`ScrollViewer`, which gets the same page without the script and with video frames as links.
`RSSMOTE_ARTICLE_RENDERER=fallback` forces the fallback; tests always use it. The page never navigates: its
script posts link clicks and shortcut keys through `invokeCSharpAction`, or to the parent page in a browser (L9), any other top-level navigation after
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

**A11 — Threading.** On the server, network and SQLite work run off the request threads and the refresher's
events arrive on the thread pool. In the browser there is one thread: `RemoteLibrary` answers reads from memory and
awaits the network, and view-models still marshal events with `IUiDispatcher`, so the same code runs in the headless
tests. View-models expose `ObservableCollection`s that are only mutated on the UI thread.

**A12 — Headless verification.** `RSSmote.App.Tests`, `RSSmote.Desktop.Tests` and `RSSmote.Mobile.Tests` boot the real
`App` (styles, palettes, control themes) on Avalonia's headless platform with Skia drawing, so a test can render any view to a PNG without opening a window.
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

**A16 — Phone shell.** `MainView` creates `MobileShell` below 900 wide (A22), a `NavigationPage` that is the whole stack (D23).
Its root page, `RootPage` (a `ContentPage` with no navigation bar), holds the tab bar in `BottomCommandBar` and shows
`FeedsView` or `SettingsView` as its content; `RootPage.Show(RootTab)` switches them, and both views are created once.
The bar belongs to the root page, so a pushed page has none. `MobileShell` sets the shared `ShellViewModel` to
`SidebarLayout.Phone` (D24) and, while it is attached, drives the stack from the view-models: `Sidebar.StreamSelected` on the root pushes
`ArticleListPage`, `List.ArticleSelected` on the list pushes `ArticlePage` (once the list has finished sliding in,
P24), and popping the article clears the list's selection. A phone pick never stays selected, so the same row opens again; Previous and Next stay inside the list.
The shell's background and its top bar follow the top page, because the strips under the safe-area insets show the
shell (P14), the shell takes the insets and the system back itself (P15, P18), and it slides pages with
`LaidOutPageSlide`, which lays the incoming page out in place first (P20). Pushed pages live in
`Views/Phone/Pages`, root content in `Views`; nothing but `MobileShell` holds navigation state. A refresh that
changes only counts updates the sidebar rows in place, so a touch that began on a row is not lost when the rows are
rebuilt. The article list is built only while its page is in the stack: `ShellViewModel.UseLayout(Phone)` starts on the Feeds root without a list, `List.Show` builds it when a stream is picked, and
`List.Hide` on the way back to the root stops store changes from rebuilding rows nobody sees. The list page puts its
large title and the `ListBox` in one `ScrollViewer`, so the title scrolls away with the rows (D25) and the list still
realises only the rows in view. A rebuild keeps the top row where it was (P25).

The Settings tab shows `SettingsView` (D27); its rows push pages from `Views/Phone/Pages`, which derive from
`PhonePage` (back title, centred title, bar button). Preferences live in `SettingsViewModel` (App), which the desktop
menus bind as `Settings.*`; Manage Feeds has its own `ManageFeedsViewModel`, which follows the store while its page
lives and is disposed when the page leaves. The phone and the desktop Settings mode (A6) share the page view-models
from `ShellViewModel.Create*` and `Confirm*`; the phone pushes them: `MobileShell.PushPanelAsync` pushes a page
for a panel, the panel closing (Save, Add Feed) pops it and a page that goes any other way disposes its panel;
`MobileShell.ConfirmAsync` shows a `ConfirmViewModel` as a modal bottom sheet (D29) and runs what follows a
confirmation only after the sheet is gone (P24). The system back closes a sheet before it pops a page.
Import and Export Feeds are pages too (`ImportFeedsViewModel` previews a file before anything is saved), and Aa
on the Feeds root, the list and the article opens `TextSheet` through `MobileShell.ShowTextSheet` with that page's
pane chosen (D31); from the article it narrows the native view to the space above the sheet. The article page hides
both bars on a scroll down and shows them on a scroll up, a tap or a new article, from `ArticleHost.Scrolled` and
`PageTapped` (D26). An article whose host is native is pushed without a slide (P29). Back from the background
(`IActivatableLifetime.Activated`, where the platform has it), `MobileShell` calls `ShellViewModel.RefreshIfStale`,
which refreshes when automatic refresh is on and the last pass is older than its interval. The server refreshes on its
own timer either way.

**A17 — The API, and who may use it.** One key, `RSSMOTE_KEY` (at least 12 characters, or the server does not
start), signs a device in; there are no users. `POST /api/login` compares its hash in constant time and sets an
`HttpOnly`, `SameSite=Strict` cookie for a year, sliding, `Secure` when the request was. The cookie carries a stamp of
the key, so a new key signs every device out. Data protection keeps its keys in the data folder, so a restart signs
nobody out. Every other endpoint needs the cookie and answers 401 without it. A write must also carry
`X-RSSmote-Client` (a random id per loaded app), which a cross-site form cannot send. Rate limits: five sign-in
attempts a minute per address, and a token bucket of 600 (60 a second) per address over `/api`. Behind a reverse
proxy, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` gives the limits the client's address.

| Endpoint | Does |
|---|---|
| `POST /api/login`, `/logout`, `GET /session` | sign in, out, and ask whether signed in |
| `GET /library` | `LibrarySnapshot`: folders, feeds, every item without its content, library settings, refreshing |
| `GET /items/{id}` | one item with its content |
| `POST /items/unread`, `/items/starred`, `/items/read-all` | flags, by ids or for an `ArticleStream` |
| `POST /folders`, `PUT`/`DELETE /folders/{id}` | add (or the existing one by name), rename, delete |
| `POST /feeds/find`, `POST /feeds`, `PUT`/`DELETE /feeds/{id}` | discovery (422 with the reason), add a found feed, edit, remove |
| `POST /opml`, `GET /opml` | import outlines, export a file |
| `POST /refresh`, `GET`/`PUT /settings` | start a pass; the library settings |
| `GET /events`, `GET /image?url=` | A18, A19 |

Every type on the wire is in `ApiJsonContext` (Core), source-generated, as the browser has no reflection-based JSON.

**A18 — Events.** `GET /api/events` is a server-sent event stream: `flags` with each changed item's new flags and
the writer's client id, `library` for any other change (coalesced to one per 500 ms, as a refresh writes once per
feed), `refresh` (true or false) and `settings`. It opens with a `ping` and sends one every 25 s, so a proxy keeps the
line open; `X-Accel-Buffering: no` stops nginx holding events back. The origin of a write is `RequestOrigin`, an
async-local set from the header for as long as the request runs, as `Store.Changed` fires on the writing thread.

**A19 — Images through the server.** A browser may not fetch another site's favicon or thumbnail, and an http image
on an https page is blocked, so `ImageCache` asks `GET /api/image?url=` (`ImageCache.ThroughServer`). The server
fetches http and https only, answers only `image/*` up to 5 MB, and marks an answer cacheable for a week; a failure is
a 404 that is not cached.

**A20 — The mirror.** `RemoteLibrary` implements `ILibrary` over the last `LibrarySnapshot`, so every read the
view-models make answers at once from memory, with the store's own rules: newest first then id, search over title,
summary and feed title ignoring case. Item content is fetched on demand (`LoadItemAsync`, the last 50 kept);
`ShellViewModel` shows the article once it is here (`ArticleViewModel.ShowContent`). Flag writes change the mirror
first and then post; a failed post fetches the library again. Every other write posts, fetches the library and then
returns, so the caller reads its own result (a new folder is in `Folders()`). Flags from the server are always applied
and raise `Changed` only for items that differed, so a device's own echo costs nothing and a snapshot older than a
write is corrected by the write's event. An older snapshot answer never replaces a newer one. `RemoteSession` follows
the event stream and, each time it opens, fetches the library, so nothing is missed while it was away (a phone in
the background, a server restart); it retries after 1 s, doubling to 30 s, and stops when signed out.

**A21 — Settings, split.** `LibrarySettings` — refresh interval, retention, mark read after, last refresh — live on
the server in `settings.json` and reach every device through the snapshot and `settings` events. Everything else —
theme and palettes, text sizes, filter, sort, grouping, favicons, folders open — belongs to the device and is kept in
its local storage (`rssmote.settings`), so a phone and a desktop each keep their own. `RemoteSettings.Update` saves
the device part at once and puts the library part to the server when it changed; the server's answer wins.
`LastRefresh` is the server's to stamp.

**A22 — One root, two layouts.** `MainView` shows the desktop columns from 900 wide (the columns' minimum widths with
their splitters) and the phone stack below, and swaps them as the window is resized. Both show the one
`ShellViewModel`; `ShellViewModel.UseLayout` switches the sidebar's layout, closes the Settings mode on the phone,
hides the list on the phone's root and shows the selected stream on the desktop. `MobileShell` follows the shell's
events only while attached, so one left behind no longer pushes pages. A tablet in portrait is below 900 and gets the
phone stack (W3).

**A23 — The browser head.** `RSSmote.Browser` (`net10.0-browser`, Avalonia.Browser) imports `interop.js`, takes the
server as the folder the page was loaded from (so the app also works under a path behind a proxy; every API path is
relative), and starts `App` with `BrowserStorage` and `AppleKeyboard` from `navigator.platform` (P31). Globalization is
invariant: no ICU data is downloaded, and dates format the same in every browser. `SessionView` asks
`GET /api/session`: signed in, it opens a `RemoteSession` and shows `MainView`; otherwise `SignInView`. A 401 anywhere
closes the session and asks for the key again. The article is the same `ArticleHost` as on the desktop:
`Avalonia.Controls.WebView` shows it in an `<iframe srcdoc>` there, and `article.js` posts to the app around it with
`parent.postMessage` where no `invokeCSharpAction` exists. The iframe is a DOM element over the canvas, so it covers
anything drawn over it, as a native web view does (L9).

**A24 — Hosting and running.** The server serves the published browser app from `RSSMOTE_WEB_DIR` or `wwwroot` beside
it (`WebApp`); without that folder it is the API alone. A file the publish compressed with brotli is sent compressed;
a file whose name carries its content hash (`_framework/name.<10 characters>.ext`) is cached for good and everything
else is `no-cache`, so a new deploy reaches every device on its next load. `build/run-local.sh` publishes the app and
runs the server on port 5000 (`--lan` for a phone on the same network); `build/publish.sh [rid]` writes
`artifacts/server`; the `Dockerfile` builds both (the SDK image needs `wasm-tools`) and keeps the library in `/data`.
A server reached from outside belongs behind a reverse proxy with HTTPS, so the cookie is `Secure`.

