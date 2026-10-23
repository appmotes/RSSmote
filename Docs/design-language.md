# Design language

Which shape to reach for, and why. Ids are append-only (see CLAUDE.md). Values are in device-independent
pixels (1 pt = 1 DIP).

## Look

**D1 — Three columns: the sidebar on a canvas, the list and the article as cards.** The window background is
the sidebar colour, and it shows in the 8 px gaps around the two cards (D20). Every surface is opaque; there is no
blur and no glass. Inside a card everything is flat: rows are tinted tiles, strips and status bars are separated by
hairlines (1 px). Chrome icons sit directly on their surface, borderless, in 28 px targets.

**D2 — One accent.** Amber (`Accent`) marks what is selected or actionable: the selected badge, the active filter
segment, the smart-stream icon, links, the starred mark, the focus ring, the primary button and the selected chip.
Never body text and never a large background: a selected row is a tint (`Selection.List`), and selected text in a
field is that tint with ordinary ink. Text on an accent fill is `Ink.OnAccent`; destructive actions use `Ink.Danger`
(D21).

**D3 — Read state is ink, not decoration.** Unread titles are `Ink.Primary` SemiBold; read titles drop to
`Ink.Read` Regular. No unread dots.

## Tokens

**D4 — Every palette answers the same key set.** Colour and shadow keys live in `Assets/Themes/*.axaml`, one file
per palette, and are read with `DynamicResource`. Neutral keys (type, spacing, radii, sizes) live in
`Assets/AppResources.axaml` and are read with `StaticResource`. A palette changes hue only; it never changes a size.

**D5 — Palettes.** Ten palettes, five per slot (D22). Sand is the light slot's default, Ember the dark slot's, and
Black the dark palette for OLED screens; the table gives these three, and the other seven are in their own files. Sand and Ember are the `Light` and `Dark` theme variants; every other palette is a `ThemeVariant` that
inherits from one of them.

| Key | Sand | Ember | Black | Used for |
|---|---|---|---|---|
| `Bg.Sidebar` | `#E9E4DA` | `#131211` | `#000000` | Sidebar and the window canvas between the cards |
| `Bg.List` | `#FBF9F5` | `#1D1C1A` | `#0E0E0D` | Article list card |
| `Bg.Article` | `#FFFEFB` | `#211F1D` | `#0E0E0D` | Article card and the article page |
| `Bg.Panel` | `#FFFEFB` | `#2A2826` | `#1A1918` | Overlay panels, sidebar badges, active filter segment |
| `Bg.PanelRow` | `#F4F0E8` | `#2E2B28` | `#1D1C1A` | Fields, row groups and chips in panels |
| `Bg.Pill` | `#EFE9DF` | `#2E2B28` | `#1D1C1A` | Filter box, segment track, list badge, kbd, secondary buttons |
| `Bg.Hover` | `#3A2A10` @ 6 % | `#FFFFFF` @ 6 % | = Ember | Pointer-over wash on icon buttons, rows and chips |
| `Bg.FaviconTile` | `#8C8276` | `#5F5850` | `#4A453F` | Letter tile when a feed has no favicon |
| `Selection.Sidebar` | `#FFFEFB` | `#2A2826` | `#1A1918` | Selected sidebar row, raised (D20) |
| `Selection.List` | `#F6EAD9` | `#3A2D1C` | `#2A2116` | Selected article, selected result, selected text |
| `Ink.Primary` | `#24201B` | `#EEE9E2` | = Ember | Titles, body |
| `Ink.Sidebar` | `#2E2923` | `#E6E0D8` | = Ember | Sidebar labels |
| `Ink.Secondary` | `#6B6257` | `#A39A8E` | = Ember | Feed names, times, summaries, labels, status |
| `Ink.Read` | `#83796D` | `#8F877C` | = Ember | Read article titles |
| `Ink.Icon` | `#5C544A` | `#D0C8BD` | = Ember | Chrome icons |
| `Ink.OnTile` | `#FFFFFF` | `#FFFFFF` | = Ember | Letter on the favicon tile |
| `Accent` | `#A85607` | `#F0A447` | = Ember | See D2 |
| `Line.Separator` | `#3A2A10` @ 9 % | `#FFFFFF` @ 8 % | `#FFFFFF` @ 10 % | Hairlines in strips and status bars, field edges |
| `Scrim` | `#2A1F10` @ 28 % | `#0A0807` @ 55 % | `#000000` @ 60 % | Behind every overlay panel |
| `Ink.OnAccent` | `#FFFFFF` | `#2A1A05` | = Ember | Text and icons on an accent fill (D21) |
| `Ink.Danger` | `#B3261E` | `#FF8A80` | = Ember | Remove Feed, Delete Folder (D21) |
| `Bg.Knob` | `#FFFFFF` | `#FFFFFF` | = Ember | A phone switch's knob, on the accent and off (D27); white in every palette |
| `BoxShadows.Card` | soft, 2 layers | soft, 2 layers | 1 px ring | List and article cards (D20) |
| `BoxShadows.Raised` | soft + hairline | soft + hairline | 1 px ring | Selected sidebar row, active filter segment |
| `BoxShadows.Panel` | deep + hairline | deep + hairline | 1 px ring | Overlay panels |

Colours are named `Color.<Key>`, brushes `Brush.<Key>`; shadows are `BoxShadows.<Key>` resources with their colour
inside, so a palette swap reaches them (L1). The palette files are generated from one table; `PaletteTests` fails
when their key lists differ and `ContrastTests` when a pair falls under its floor (D22). Fluent's accent
keys (`SystemAccentColor` and its Dark1–3 / Light1–3) are mapped in each palette so stock controls match: Dark1–3
are the accent × 0.88 / 0.76 / 0.64, Light1–3 the accent blended 15 / 30 / 45 % towards white. Selected text in a
field is styled once, in `Styles/Inputs.axaml` (D2).

**D6 — Type roles** (style classes on `TextBlock`; roles never set `FontFamily` — Inter comes from
`WithInterFont()`, which ships Thin, Light, Regular, Medium, SemiBold and Bold and no italic, so roles use 400–700):

| Class | Size / weight | Used for |
|---|---|---|
| `largeTitle` | 20 Bold | Panel titles ("Add Feed") |
| `listTitle` | 15 SemiBold | List title in the card strip, edit-panel headers |
| `sidebarSmart` | 13 Medium | Smart stream row |
| `sidebarSection` | 12 SemiBold, `Ink.Secondary` | "Folders", "Feeds" |
| `sidebarRow` | 13 Regular | Folder and feed names |
| `itemTitle` | 13.5 SemiBold (unread) / Regular (read), one line | Article row title |
| `itemSummary` | 12.5 Regular, `Ink.Secondary`, two lines | "Feed — summary" under the title |
| `meta` | 11.5 Regular, `Ink.Secondary`, +tnum | Row time, status bars |
| `caps` | 11 SemiBold, +0.66 tracking, upper-case in the string | "TODAY", panel sections |
| `panelRow` | 13.5 Regular | Fields and rows in overlay panels |
| `count` | 11 Medium, `Ink.Secondary`, +tnum, in a badge | Sidebar and list counts |
| `kbd` | 10.5 Medium | Shortcut chips |
| `emptyTitle` / `emptySubtitle` | 15 SemiBold / 12.5 Regular | Empty states |

Article body typography is CSS in the HTML template (D14), not a type role.

**D7 — Metrics** (`AppResources.axaml`):

| Key | Value | Notes |
|---|---|---|
| `Size.Column.Sidebar` | 232 (min 200, max 320) | |
| `Size.Column.List` | 428 (min 328, max 608) | The card is 420 plus the 8 pt gap to the article card |
| `Size.Column.ArticleMin` | 360 | Article card takes the rest |
| `Size.TitleBar.Height` | 52 | Sidebar strip; in a card, 8 of canvas + `Size.CardStrip.Height` 44 |
| `Size.StatusBar.Height` | 28 | Status bars under the list and the article |
| `Size.SidebarRow.Height` | 28 | Section rows 38 |
| `Size.DateHeader.Height` | 28 | |
| `Size.Thumb` | 44 | Square, `CornerRadius.Thumb` 7 |
| `Size.Favicon` | 14 | Rounded 3 |
| `Size.Badge.Height` | 18 | `CornerRadius.Badge` 5 |
| `Size.Panel.Width` | 400 (narrow 360) × content, max 600 | Overlay panels, centred in the window |
| `Size.PanelRow.Height` | 36 (48 with subtitle) | |
| `Size.FormLabel.Width` | 64 | Label column in edit panels |
| `Thickness.Card.Margin` | 0,8,8,8 | `CornerRadius.Card` 12 |
| `Thickness.Row.Inset` | 6 | Tile inset in the list; 8 in the sidebar |
| `Double.Gutter` | 16 | |
| `Double.ListTextLeft` | 37 | Favicon at 16, title at 37 |
| `Double.ItemLineHeight` / `Double.SummaryLineHeight` | 18 / 17 | Title line / summary lines |
| `Double.IconStroke` | 1.5 | Icons 15 in 28 px targets |
| `CornerRadius.Row.Sidebar` / `.List` | 7 / 8 | |
| `CornerRadius.Panel` / `.Group` / `.Field` / `.Chip` | 16 / 10 / 7 / 7 | |
| `CornerRadius.IconButton` / `.Pill` | 7 / 7 | |

## Components

**D8 — Control preference order:** stock control + style class → small templated control in
`Controls/Templates/` → code-only control in `Controls/Headless/`. Never a commercial package (no TreeDataGrid) and never a deprecated one (no DataGrid, no ItemsRepeater).

**D9 — Component inventory.**

| Need | Shape | Built on |
|---|---|---|
| Column surfaces | Sidebar on the canvas; list and article as cards: `Border.card` (margin, shadow) around `Border.cardClip` (corners) (D20, L12) | `Border` styles |
| Window chrome per column | `TitleStrip` (templated): left/right slots, `ElementRole="TitleBar"`, optional bottom hairline; `.card` makes it 44 inside a card | `Border` + `DockPanel` |
| Chrome buttons | `IconButton` (templated): borderless, `Ink.Icon`, optional filled variant | `Button` |
| Icons | `Icon` (headless): strokes a geometry from `Assets/Icons.axaml` (D12) | `Control` + `Path` |
| Sidebar | Flattened rows (smart, section, folder, feed) with indent + expand state | `ListBox`, row templates by type |
| Sidebar row | `SidebarRow`: smart icon or chevron or favicon · label · count badge; raised when selected, washed on hover | `Grid` |
| Status bar | `StatusBar` (templated): status text left, controls right; hairline above, or none on the canvas (`.canvas`) with the refresh dot | `Border` + `DockPanel` |
| Article list | Card: title + count badge and filter in the strip, filter box, date headers with counts, article tiles, status bar; virtualized | `ListBox` + `VirtualizingStackPanel` |
| Article row | `ArticleRow`: favicon · title · time, then "Feed — summary" (2 lines) · 44 pt thumbnail (D11) | `Grid` + two `TextBlock`s |
| Filter bar | `FilterBar` (templated): starred · unread · all as icon segments in a track, the active one raised; looks in `Button.filter-segment` | `Border` + 3 buttons |
| Filter box | Always visible under the list strip: search icon, placeholder "Filter", a ⌘F / Ctrl+F chip or a clear button; ⌘/Ctrl+F focuses it, Escape clears it and returns to the list | `TextBox.filter` with inner content |
| Article | `ArticleHost`: `NativeWebView` with HTML template; falls back to `HtmlPanel` (A9) | WebView / HtmlRenderer |
| Empty states | `EmptyState` (templated): a soft box with title, subtitle and shortcut chips in the article card; `.compact` an icon tile, title and subtitle in an empty list | `Panel` + `StackPanel` |
| Overlays | `PanelFrame` (templated): scrim that catches clicks + a sheet as tall as its content (`.narrow` for confirmations); pages use `sheetHeader` · `sheetBody` · `sheetFooter` | `ContentControl` over sidebar and list (A6) |
| Grouped rows | `PanelGroup` + `PanelRow` (text / subtitle / chevron or round check); fields are `TextBox.panel-field` on their own, labelled with `TextBlock.formLabel` | `ItemsControl` + `Button` |
| Buttons | `Button.primary` (accent), `.secondary` (pill), `.danger` (ink), `.dangerSecondary` (danger ink on the pill); a `TextBlock.keyHint` names the key ("esc", "↵") | stock `Button` |
| Folder choice | Chips: `Button.chip` in a `WrapPanel`, the chosen one `.on` in the accent; clicking it again chooses no folder | `ItemsControl` + `Button` |
| Shortcut chip | `Border.kbd` + `TextBlock.kbd` | `Border` |
| Context menus | Stock `ContextMenu` / `MenuFlyout` | — |
| Settings | Separate `Window`, `TabControl` (General · Reading · Appearance · Feeds), form grid | stock |
| Real tables | `TableView` (D17), e.g. the Feeds tab | stock, Avalonia.Controls |
| Everything else that repeats | `ListBox` (virtualizing, selection) or `ItemsControl` with a virtualizing panel (D18) | stock |

**D10 — Sidebar is a flattened `ListBox`, not a `TreeView`.** The rows need a custom indent, a
chevron on the left for folders, a chevron on the right for sections, and filter-driven hiding; a flat list of
row view-models with `Level` and `IsExpanded` gives that with one template per row type and keeps selection
styling in one place. Collapsing a folder or section that hides the selected feed moves the selection to the
folder, or to the top stream when the folder is hidden too.

**D11 — One line of title, two of source and summary.** The title is one `TextBlock` with `MaxLines=1` and an
ellipsis (the full title is its tooltip), so every title starts at the same place and the eye can run down the
column. Under it one `TextBlock` with two `Run`s — the feed name in Medium, then " — " and the summary — has
`MaxLines=2` at `Double.SummaryLineHeight`. Row height is therefore content-driven but bounded; no measuring code.

## Icons and assets

**D12 — Icons are stroked geometry, not glyphs or fonts.** Lucide (ISC) path data is copied into
`Assets/Icons.axaml` as `StreamGeometry` resources and rendered by the `Icon` control with
`StrokeThickness` 1.5 (`Double.IconStroke`), round caps/joins. Filled variants (star, circle) set `Fill` to the stroke brush.
Licence notice in `Assets/Icons/LICENSE-lucide.txt`.

| Use | Key | Lucide name |
|---|---|---|
| Refresh | `Icon.Refresh` | rotate-cw |
| Add | `Icon.Plus` | plus |
| Mark all read | `Icon.CheckCircle` | circle-check |
| Filter/search | `Icon.Search` | search |
| Close | `Icon.X` | x |
| Read toggle | `Icon.Circle` / filled | circle |
| Unread filter, smart row | `Icon.CircleDot` | circle-dot |
| Star | `Icon.Star` / filled | star |
| Web page | `Icon.AlignLeft` | text-align-start |
| Share | `Icon.Share` | share |
| Chevrons | `Icon.Chevron{Left,Right,Down}` | chevron-* |
| Folder | `Icon.Folder` | folder |
| Feed fallback | `Icon.Rss` | rss |
| Next article | `Icon.ChevronDown` | chevron-down |
| Previous article (phone) | `Icon.ChevronUp` | chevron-up |
| Feed error (phone) | `Icon.CircleAlert` | circle-alert |
| Settings | `Icon.Settings` | settings |
| Menu (Win/Linux) | `Icon.Menu` | ellipsis |
| Open website | `Icon.Globe`, `Icon.ExternalLink` | globe, external-link |
| Checked row | `Icon.Check` | check |
| Import, Export (phone) | `Icon.Download`, `Icon.Upload` | download, upload |
| Appearance (phone) | `Icon.Palette` | palette |
| Read when opened (phone) | `Icon.Eye` | eye |
| Newest first (phone) | `Icon.SortDown` | arrow-down-wide-narrow |
| Group by date (phone) | `Icon.Calendar` | calendar |
| Refresh interval, Keep read (phone) | `Icon.Clock`, `Icon.Archive` | clock, archive |
| Picker (phone) | `Icon.ChevronsUpDown` | chevrons-up-down |

**D13 — Other assets.**
- App icon: the Sand accent as a rounded square with a white Lucide star; master SVG in
  `Assets/AppIcon/AppIcon.svg`. The `.icns`, `.ico` and Linux PNGs are exported from it when packaging (N5).
- Empty states use Lucide icons and type, not an illustration (D9).
- Favicons: fetched at runtime, shown greyscale when "Grayscale Favicons" is on (BGRA on desktop, RGBA on the
  phones); a letter tile when missing.
- Font: Inter via `Avalonia.Fonts.Inter` on every OS, so the measured metrics hold everywhere.

**D14 — Article typography is CSS.** The HTML template sets the date at 12.5 px with tabular figures, the title at
28 px SemiBold, and the byline on one line, "Author · Source", with the source in the accent; the column is 620 px
wide and the body 16 px / 1.58 by default. No `text-transform`: the fallback renderer cannot do it, and a byline in
sentence case reads better. It reads the same colour values as D5: `Bg.Article`, `Ink.Primary`, `Ink.Secondary`,
`Accent`, `Line.Separator` (rules), `Bg.Hover` (code) and `Selection.List` (selected text), written into the
stylesheet as opaque hex (L10). The font family is user-selectable (System, Serif, Rounded, Mono).

## Interaction

**D15 — Keyboard first.** Single-key shortcuts as in the original: `j`/`k` next/previous, `space` scroll or
next, `m` read, `s` star, `b` browser, `v` web page, `r` refresh, `a` mark all read, `n`/`p` next/previous
feed, `.` go back. They are ignored while a text box has focus. Modified shortcuts: ⌘/Ctrl+1/2/3
filter, ⌘/Ctrl+N add feed, ⌘/Ctrl+F filter box, ⌘/Ctrl+, settings.

**D16 — Menus per OS.** macOS gets a full `NativeMenu` (app, File, Edit, View, Feeds, Article, Window).
Windows and Linux get no menu bar; the same commands are reachable from shortcuts and a menu button
(`Icon.Menu`) in the sidebar's title strip.

## Lists and tables

**D17 — Tables are `TableView`.** Read-only, column-based data uses the core `TableView` (Avalonia 12.1+): it is a
`ListBox` with `TableViewColumn`s, row virtualization, resizable columns, and `CellTemplate` for rich cells. First
use: the Feeds tab in Settings (Title · Folder · Unread · Last updated · Status). Keep the column count
small; columns are not virtualized (P5).

**D18 — The sidebar and the article list are virtualized `ListBox`es, not tables.** They mix row types (date
header + article, section + folder + feed), have no column header, and render one rich cell per row — none of
which `TableView` offers. `ListBox` virtualizes by default and gives keyboard selection; non-selectable rows
(date headers, sections) opt out through their container. A list without selection uses `ItemsControl` with a
`VirtualizingStackPanel` items panel inside a `ScrollViewer` template, per the Avalonia how-to
("virtualized scrollable items"). If a list outgrows this, the next step is a custom virtualizing panel in
`Controls/Headless/`, never `ItemsRepeater` (P6).

## Developer page

**D19 — The component gallery shows every token, icon and component with sample data.** `Gallery/ComponentGallery`
is the reference for how a component looks in each palette; `GalleryRenderTests` renders it headless to
`tests/RSSmote.App.Tests/bin/Debug/net10.0/snapshots/gallery-{Light,Dark,Black}.png`, and `ShellRenderTests` (in `RSSmote.Desktop.Tests`)
renders the real shell over a seeded library to `shell-{Light,Dark}.png`. In a Debug build Ctrl/⌘+Shift+G opens
the gallery in its own window. A new component gets a
gallery entry in the same change as its `templates.html` article.


## Elevation and themes

**D20 — Cards rise from the canvas by a palette shadow.** The list and the article are cards
(`CornerRadius.Card`, `Thickness.Card.Margin`, `BoxShadows.Card`); the sidebar sits on the canvas. Elevation is a
palette key, not a neutral one, because a shadow that reads on sand reads as nothing on black: Sand and Ember use a
soft two-layer shadow, Black a 1 px light ring. Only the two cards, the selected sidebar row and the active filter
segment (`BoxShadows.Raised`) and the panels (`BoxShadows.Panel`) are raised — never a list row. The native web view
sits between the card's strip and its status bar, so the card's rounded corners are always drawn by Avalonia (L9).
`BoxShadows` resources resolve through `DynamicResource` on `Border.BoxShadow`; measured with `ElevationTests`.

**D21 — Ink on the accent, and ink for danger.** `Ink.OnAccent` is chosen against `Accent`, not against a page:
white on Sand's amber, dark brown on Ember's and Black's, because an accent light enough to carry on a dark page
cannot hold white text. The accent is not red, so Remove Feed and Delete Folder… get their own ink, `Ink.Danger` —
as text on a panel or on `Bg.Pill`, never as a fill under white.

**D22 — A theme is a palette, and the app holds two.** Settings keep `Theme` (Automatic, Light or Dark) and one
palette id per slot (`LightTheme`, `DarkTheme`); Automatic shows the slot the OS asks for and follows it when it
changes (`PlatformSettings.ColorValuesChanged`). Picking a palette also shows it: choosing a light palette sets
`Theme` to Light, a dark one to Dark, so a choice is never invisible; Automatic is chosen again from Appearance and
then uses both slots. Two slots rather than one list, so Automatic never has to guess what dark means. `Theming/ThemeCatalogue` lists the palettes; an id this build does not know resolves to the slot's
default, and a stored `Black` from older settings becomes Dark with the Black palette. Adding a palette is a file
under `Assets/Themes/`, its `ThemeDictionaries` entry and one catalogue line.

Light: Sand (default; warm), Newsprint (sepia paper and ink-blue links, for long reading), Fern (green-leaning),
Harbor (blue accent and brown danger, apart in lightness as well as hue, so they hold under colour blindness) and
Contrast (black on white). Dark: Ember (default; Sand turned over), Black (OLED), Fjord (arctic blue-grey),
Phosphor (green on an unlit tube; danger stays red) and Contrast Dark (white on black). In Black, the contrast pair
and Phosphor a card is marked by a 1 px ring instead of a shadow (D20). The menus offer View ▸ Theme ▸
Appearance, Light Theme and Dark Theme as radio items (macOS menu bar and the Windows/Linux menu button).

Every palette clears these floors (WCAG 2, translucent ink composited first; `ContrastTests`):

| Pair | Floor |
|---|---|
| `Ink.Primary` on `Bg.List`, `Bg.Article`, `Bg.Panel`, `Selection.List`; `Ink.Sidebar` on `Bg.Sidebar` | 7:1 |
| `Ink.Secondary` on `Bg.List`, `Selection.List`, `Bg.Sidebar`, `Bg.Panel`, `Bg.Pill` | 4.5:1 |
| `Ink.Read` on `Bg.List` | 4:1 |
| `Accent` on `Bg.Article`; `Ink.OnAccent` on `Accent`; `Ink.Danger` on `Bg.Panel` and `Bg.Pill`; `Ink.Icon` on `Bg.List` | 4.5:1 |
| `Ink.OnTile` on `Bg.FaviconTile` | 3:1 |

## Mobile (phone)

The phone screens are drawn as HTML recreations, not screenshots, on the design canvas
(https://claude.ai/artifact/JXWBKf8qYCoCGWSKPKx7BJ): M1–M11 in Sand and M1D–M11D in Ember, at 402 × 874 pt. A
tablet layout is not designed yet (W3).

**D23 — One navigation stack, two roots.** Feeds → article list → article is one `NavigationPage`. Its root page
shows a bottom tab bar, Feeds | Settings (`Icon.Rss`, `Icon.Settings`, the active one in `Accent`); the bar belongs to
the root page, so it is gone as soon as a list is pushed. Settings is reachable only from the root: reading pages
keep their bars for reading, and there is no drawer. One stack rather than a tab control with a stack per tab, so
there is one back history and one place that owns the selection.

**D24 — Feeds root.** A large title "Feeds" over "Updated 09.29 · 10 feeds", which takes the place of the desktop
status bar. Grouped cards on `Bg.Sidebar`, each `Bg.List` with `CornerRadius.Card`: a stream card (Unread, Starred,
All Items, with `Icon.CircleDot`, `Icon.Star` and `Icon.AlignLeft` in `Accent`), then Folders and Feeds, each under
a collapsible section header. A folder's chevron expands it in place; the row itself opens the folder's list. Counts
are plain `Ink.Secondary` figures, not badges, and always count unread articles, because the root has no filter. A
feed whose last refresh failed shows an `Ink.Danger` alert icon in its row.

**D25 — Article list: three lines of title.** A large title (the stream's name) over "3 unread · newest first".
Each row is a source line (favicon · feed name · star · time), the title in up to three lines, one line of summary,
and a 64 pt thumbnail on the right. This departs from D11 on purpose: a phone column is about 370 pt, where one line
would cut most titles short, so the title wraps and the summary gives up a line instead. A bottom bar holds Mark All
as Read · the filter (Starred, Unread, All Items; the active one a labelled pill on `Bg.Pill`, the others icons) ·
search, which opens the filter box.

**D26 — Article: bars that step aside.** A top bar with back, the position ("1 of 3") and Aa; a bottom bar with Read,
Star, Open in Browser, Previous and Next, icons only. Both bars slide away while the reader scrolls down and come
back on a scroll up or a tap, leaving a 3 pt reading-progress line under the status bar. Previous and next are
buttons, never a horizontal swipe: a swipe from the edge is back (`NavigationPage`), and the two would fight. Aa opens
a sheet with text size (11–32, `AppSettings`), font (System, Serif, Rounded, Mono) and Automatic / Light / Dark as a
shortcut to the same setting as Appearance.

**D27 — Settings are plain pushed pages.** The Settings root lists Feeds (Add Feed, Manage Feeds, Import Feeds,
Export Feeds), Display (Appearance), Articles (Read when opened, Newest first, Group by date as switches) and Refresh
(interval and how long read articles are kept, as pickers), then the version. Every row that opens a page has a
chevron; pages have a back button and a centred title, no large title.
- Appearance: Mode (Automatic, Light, Dark), then the light and the dark palettes as rows with a swatch (`Bg.Article`
  with an `Accent` dot) and a check, then the favicon switches. Choosing a palette follows D22.
- Manage Feeds: one group per folder (with Edit) and one for feeds without a folder; each feed shows its host, or its
  error in `Ink.Danger`; New Folder sits in the bar. A feed opens Edit Feed: name, read-only address, folder chips
  (as D9), Open site, Reload Icon, and Remove Feed… in `Ink.Danger` with the line that it deletes the feed and its
  articles for good (A14).
- Add Feed: the address field, the feeds found as radio rows, folder chips and one primary button. No OPML here.
- Edit Folder (no mockup) follows Edit Feed: the name, the folder's feeds (each opens Edit Feed), and Delete Folder…
  in `Ink.Danger` with the line that its feeds stay. New Folder is the name alone; Save sits in the bar on all three.
- The two Refresh pickers open a menu of radio items over the row (`MenuFlyout`, rows at 44 pt on `Bg.Panel`), as
  M5's up-down chevron says; every other row that opens something pushes a page.
- Import Feeds shows what a file will add before anything is saved: the file, its folders with their feed counts,
  how many feeds already exist and are skipped, then "Import N Feeds". Export Feeds shows what the file holds and hands
  it to the system's save or share sheet.

**D28 — Touch metrics.** Rows are at least 44 pt (56 with a subtitle, 60 for radio rows with one); icon buttons
are 44 pt targets around 22 pt icons; bars are 44 pt at the top and 49 pt plus the home-indicator inset at the
bottom. Type: large titles 34 Bold, page titles 17 SemiBold, row text 16, secondary text 13–15. Cards keep 16 pt
from the screen edges and reuse `CornerRadius.Card`. The keys in `Assets/AppResources.axaml`:

| Key | Value | Notes |
|---|---|---|
| `Size.Phone.Row` / `.RowSubtitle` | 44 / 56 | |
| `Size.Phone.Target` / `.Icon` | 44 / 22 | Tab bar icons are 22 too |
| `Size.Phone.TopBar` / `.BottomBar` | 44 / 49 | The bottom bar without the home-indicator inset |
| `Size.Phone.Thumb` | 64 | Article list thumbnail (D25) |
| `Thickness.Phone.CardMargin` | 16,0 | |
| `TextTheme.Phone.LargeTitle` | 34 Bold | Class `phoneLargeTitle` |
| `TextTheme.Phone.PageTitle` | 17 SemiBold | Class `phonePageTitle` |
| `TextTheme.Phone.Row` | 16 Regular | Class `phoneRow` |
| `TextTheme.Phone.Secondary` | 15 Regular, `Ink.Secondary` | Class `phoneSecondary`; 13 and 14 have no key yet |

The tab bar (`Button.tab`) is `Bg.Sidebar` under a hairline, two 22 pt icons over 11 pt labels
(`TextTheme.Count.Size`): Medium in `Ink.Secondary`, the selected one SemiBold in `Accent`.

**D29 — The phone asks in a bottom sheet.** Remove Feed… and Delete Folder… open a sheet from the bottom over the
`Scrim`: the question as a page title, the line of what happens, the action in `Ink.Danger`, then Cancel, both as
44 pt rows on `Bg.Pill` (the pair D21 holds at 4.5:1); a tap on the scrim or the system back cancels. A sheet rather
than a centred dialog because it sits under the thumb and leaves the page it asks about in view. It reuses
`ConfirmViewModel`, which the desktop shows as a panel (A6).
