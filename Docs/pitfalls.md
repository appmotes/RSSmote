# Pitfalls

What fails silently, the fix, and the evidence. `L` = likely to bite, `P` = possible, `U` = unusual.
Ids are append-only (see CLAUDE.md). Entries marked *(docs)* are taken from the Avalonia documentation and
not yet measured in this app.

**L1 — Theme colours read with `StaticResource` freeze.** A palette swap only reaches `DynamicResource`
references. Colour tokens are always `DynamicResource`; neutral tokens may be `StaticResource`. *(docs)*

**L2 — A `Brush` inside a `ResourceDictionary` cannot point at another key with `DynamicResource`.** Declare each
palette's brushes in the same palette file, built from that file's colours. *(docs)*

**L3 — Single-key shortcuts and text boxes.** A window-level key handler for `j`, `k`, `space`… also fires while
typing in the filter box or a panel field unless it checks the focused element first. Handle `KeyDown` on the
window in the tunnel phase and skip when `FocusManager` reports a `TextBox`.

**P1 — `NativeWebView` needs system libraries on Linux.** WebKitGTK 4.1 + libsoup 3 (or WPE WebKit). Without
them the control cannot start; the app must fall back (A9). Ubuntu does not package WPE. *(docs)*

**P2 — `ExtendClientAreaToDecorationsHint` is limited on Linux.** Keep system decorations there (A5). *(docs)*

**P3 — Windows draws its caption buttons over the right of the client area** when the client area is extended.
The article column's title strip must reserve that width on Windows. *(to measure)*

**P4 — `{Binding}` compiles by default in Avalonia 12.** A missing `x:DataType` is a build error, not a runtime
miss — set it on every view, `DataTemplate` and `ControlTheme`. *(docs)*

**P5 — `TableView` virtualizes rows, not columns.** Every column is always realized; keep tables narrow. *(docs)*

**P6 — `ItemsRepeater` is unsupported from Avalonia 12, and plain `ItemsControl` does not virtualize.** Use
`ListBox`, or `ItemsControl` with a `VirtualizingStackPanel` panel inside a `ScrollViewer`. *(docs)*

**L4 — A `ControlTheme` style may hold one `/template/` hop and no child or descendant selector.** Both throw
`InvalidOperationException` when the theme is first applied — at runtime, not at build. Measured with the gallery
test. Styles that reach into a templated child's own template (`Button.x:pointerover /template/ ContentPresenter`)
go into `Styles/*.axaml` as plain style classes.

**L5 — Controls placed directly in an `ItemsControl` are their own containers.** Selectors aimed at
`ContentPresenter` containers never match them. Target the controls through the logical tree instead, e.g.
`c|PanelGroup > c|PanelRow:nth-child(n+2)`. Measured with the gallery test.

**P7 — `Panel.Render` is sealed.** A panel subclass cannot draw decorations; draw them on the children
(borders) or in a separate control.

**P8 — Feed XML is often not well-formed XML.** HTML entities (`&nbsp;`, `&eacute;`) make `XDocument` throw; the
parser rewrites them as numeric references and retries (A10). Measured with `Html_entities_in_xml_are_recovered`.

**P9 — `media:content` has the local name `content`.** Matching by local name alone would make a video or image
enclosure the article body; the parser excludes the Media RSS namespace. Measured with
`Media_thumbnail_is_an_image_not_the_body`.

**P10 — `Environment.SpecialFolder.ApplicationData` is `~/.config` on macOS.** The native place is
`~/Library/Application Support`; `AppFolders` builds that path itself (A4).

**U1 — A refresh test that calls `RefreshAllAsync` to wait can start a second pass.** If the timer's pass has
already finished, the call begins a new one. Tests wait on observable state (request count, `IsRefreshing`)
instead. Measured with `The_timer_follows_the_interval_setting`.

**L6 — `ColumnDefinition.Width` takes a `GridLength`; a `x:Double` resource builds but throws `InvalidCastException`
when the view loads.** Width tokens for grid columns are declared as `GridLength` (`GridLength.Column.*`). Measured
with `Shell_renders_with_an_article_open`.

**P11 — An `x:Name` that matches a property on the code-behind class does not compile** (the name generator
declares a field of that name). Name controls after what they are (`Rows`, `Search`, `Scroller`).

**L7 — `HttpClient` does not decompress by default.** Some feed servers send gzip or brotli even when the request
did not ask for it; the body then fails to parse and the feed reports "no longer returns a feed". The feed client
uses `FeedFinder.CreateHandler()` with `AutomaticDecompression = All`. Seen with swift.org's Atom feed.


**L8 — A rooted path such as `/a.png` parses as an absolute `file://` URI on Unix.** `Uri.TryCreate(value,
UriKind.Absolute, …)` succeeds on macOS and Linux, so a relative image never gets resolved against the article.
Web addresses are accepted by scheme only and everything else goes through `Uri.TryCreate(base, value, …)`
(`ArticleSanitizer.Resolve`). Measured with `Lazy_images_get_their_real_source`.

**L9 — The native web view takes the keyboard.** Once clicked, `NativeWebView` is a native child view and Avalonia
sees no key events, so `j`, `k`, `space`… stop working. The article page forwards its shortcut keys through
`invokeCSharpAction`, and `ArticleHost` raises them as `ShortcutEvent` for the window's handler. The same native
view is drawn above Avalonia content: anything that overlaps the article column (popups, overlays) must hide it
through `ArticleHost.IsObscured`.

**L10 — HtmlRenderer reads the alpha in `rgba()` as 0–255; browsers read 0–1.** `rgba(27, 27, 27, 1)` is near
invisible in the fallback. The article stylesheet gets opaque hex colours, translucent tokens composited over the
article background first. Measured with `Shell_renders_with_an_article_open`.

**L11 — A control that only strokes a geometry is hit only on its strokes.** Avalonia hit-tests what a control
draws, so a 12 px chevron drawn with 2 px lines is nearly impossible to click. Put such an icon inside a `Border`
with `Background="Transparent"` sized to the target you want (the folder disclosure covers the 32 px indent left
of the title).

**L12 — `ClipToBounds` on a `Border` clips its own `BoxShadow`.** The shadow is drawn outside the border's bounds,
so a card that both casts a shadow and clips its content to the rounded corners renders with no shadow. A card is two
borders: the outer one carries margin, background and `BoxShadows.Card`, the inner one (`Border.cardClip`) the
corner radius and `ClipToBounds` (D20). Measured with `A_clipping_border_clips_its_own_shadow`.

**L13 — A `NativeMenuItem` that gets its `Menu` after the window loads stays a disabled leaf on macOS.** The native
menu is exported from the XAML tree as it stands; an item with neither a command nor a submenu at that point is
shown disabled, and setting `Menu` later does not turn it into a submenu. Declare the submenu in XAML (an empty
`<NativeMenu />`) and add to its `Items` from code; item additions are followed. Seen in the macOS menu bar's theme
submenus (D22); `Theme_items_in_the_menu_bar_are_enabled_and_switch_the_theme` guards the structure, the native
behaviour itself needs a check by hand.

**L14 — A `UserControl` clips to its own bounds.** A card that sits flush with its view's edge draws its ring or
shadow outside those bounds, and it was cut off: the list and article cards had no left edge in the ring palettes. The
two card views set `ClipToBounds="False"`. Measured with `The_card_ring_shows_on_every_edge`.

**P12 — A Debug Android APK installed with `adb install` closes at once.** Debug builds use Fast Deployment: the
assemblies are not in the APK but pushed separately by `dotnet build -t:Install`. Installed on its own, the APK
aborts on launch (`monodroid: No assemblies found … Assuming this is part of Fast Deployment`), visible only in
logcat. Install Debug with `-t:Install`; a Release APK carries its assemblies and installs with `adb`. Seen with the
platform spike on an API 35 emulator.

**P13 — The iOS SDK reports trim analysis warnings that the Android SDK hides.** Both heads trim with
`TrimMode=partial`: only assemblies marked `IsTrimmable` are trimmed, every other one is copied whole. Android sets
`SuppressTrimAnalysisWarnings=true` by default, iOS does not, so an iOS Release build (warnings as errors) fails on
IL2104 for AngleSharp, AngleSharp.Css, Microsoft.Data.Sqlite and Avalonia.DesignerSupport — none of which is trimmed:
each one in `obj/…/linked/` is byte for byte the size of its package copy, while `Avalonia.Base` shrinks from 2.36 MB
to 1.66 MB. ILLink also reports IL2037 for interface members `Avalonia.iOS` names (`AvaloniaAppDelegate`,
`TextInputResponder`), which is not our code. The iOS head sets `SuppressTrimAnalysisWarnings` and `NoWarn` IL2037.
Measured with the platform spike, which passes every step in a Release simulator build. A new dependency that is
marked trimmable and uses reflection would be trimmed silently: run the spike after adding one.
