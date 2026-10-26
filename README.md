<img src="Docs/images/app-icon.png" alt="The RSSmote icon: a spark sending out two feed arcs" width="96">

# RSSmote

**English** · [Dansk](README.da.md)

An RSS reader for macOS, Windows and Linux, and for iPhone and Android. Three columns on the desktop, keyboard first,
and everything stored locally.

**This app was written by a machine intelligence.** All the code, the comments, the design, the ten colour themes, the
app icon, the tests, the documentation and this README were made by Claude Opus 5.5 (Anthropic, with *reasoning
effort* set to high) from a human's description. No human has reviewed the code line by line. Read the
[Disclaimer](#disclaimer) before you trust it with anything you care about.

![RSSmote with an article open, light theme](Docs/images/reading-light.png)

On the left are your feeds, gathered in folders. In the middle are the articles, grouped by day. On the right is the
article you are reading. There is no account, no sync and no cloud: your feeds and articles live in one SQLite file on
your own machine.

Built with .NET 10 and [Avalonia](https://avaloniaui.net) 12. Only free components with MIT or similar licences; see
[Docs/dependencies.md](Docs/dependencies.md).

## What it does

- **Add a feed** from a feed address or just the address of a website. RSSmote finds the site's feeds itself and
  shows them before anything is saved.
- **RSS 2.0, Atom, RDF and JSON Feed.** Feeds with invalid XML (such as `&nbsp;` in the middle of a title) are read
  anyway.
- **Folders** that fold in and out, with unread counts on every folder and feed.
- **Filters**: Starred, Unread and All, plus a filter box above the list that searches titles, summaries and feed
  names.
- **Articles in the OS's own web engine** (WebKit on macOS, WebView2 on Windows, WebKitGTK on Linux) with the app's
  own typography. Scripts, forms and inline styles are removed before the page is shown. Where there is no web engine,
  a built-in HTML renderer shows the same page.
- **OPML import and export**, so you can move your feeds in and out.
- **Refresh** every half hour in the background, and with `r` whenever you like. Read, unstarred articles are cleared
  after 30 days.
- **Ten colour themes**, five light and five dark. See [Themes](#themes).

![The same window in the default dark theme](Docs/images/reading-dark.png)

## The keyboard

The whole app can be driven without a mouse. The keys do nothing while you type in a field.

| Key | Does |
| --- | --- |
| `j` / `k` | Next / previous article |
| `space` | Scroll down the article; at the bottom it moves on to the next (`shift+space` scrolls up) |
| `m` | Mark as read / unread |
| `s` | Star |
| `b` | Open the article in the browser |
| `n` / `p` | Next / previous feed |
| `r` | Refresh all feeds |
| `a` | Mark everything in the list as read |
| `.` or `esc` | Close the article |
| `⌘1` `⌘2` `⌘3` | Starred / Unread / All (`Ctrl` on Windows and Linux) |
| `⌘F` | Go to the filter box; `esc` clears it |
| `⌘,` | Settings: add, edit and remove feeds, import, export, appearance; `esc` goes back |
| `⌘N` | Add a feed (in Settings) |
| `⌘+` `⌘−` `⌘0` | Larger / smaller / standard text in the article, or in the pane chosen under **Aa** while it is open |

With no article selected, the right side shows the most important shortcuts:

![The shortcuts when no article is selected, in the Fjord theme](Docs/images/no-article.png)

## Screenshots

| | |
| --- | --- |
| ![The filter box in use](Docs/images/filter-box.png) | ![Editing a feed](Docs/images/edit-feed.png) |
| The filter box searches the list as you type | Rename a feed and move it between folders |
| ![Adding a feed](Docs/images/add-feed.png) | ![The Phosphor theme](Docs/images/theme-phosphor.png) |
| Add a feed from an address, in Settings | Phosphor: green on an unlit tube |

Every screenshot is taken by the app itself, without a screen, over a made-up demo library: the feeds, articles,
favicons and images are invented for the purpose (see `ReadmeCaptureTests.cs` in `tests/RSSmote.Desktop.Tests` and
`tests/RSSmote.Mobile.Tests`).

## On the phone

RSSmote also runs on iPhone and Android, built from the same code as the desktop app: the same library, the same
settings and the same ten themes. The screens below show the iPhone.

| | | | |
| --- | --- | --- | --- |
| ![Feeds](Docs/images/phone-feeds.png) | ![The unread list](Docs/images/phone-list.png) | ![An article](Docs/images/phone-article.png) | ![Settings](Docs/images/phone-settings.png) |
| Feeds: streams, folders and feeds with unread counts | The list: titles over three lines, thumbnails, the filter in the bottom bar | The article: the bars step aside while you scroll down | Settings: feeds, appearance, reading and refresh |

- **One stack:** Feeds → list → article. Swipe from the edge or tap back to return. Settings is the second tab.
- **Reading:** previous and next are buttons in the bottom bar, not swipes. **Aa** on the Feeds page, the list and the
  article sets that page's text size. Settings ▸ Text sets all three and can
  follow the system's text size.
- **Feeds** are added, edited, removed, imported and exported under Settings, as on the desktop. Removing asks in a
  sheet from the bottom.
- **Refresh:** pull down on Feeds, or come back to the app after the refresh interval has passed.
- **No sync.** The phone keeps its own library. Move your feeds between devices with OPML export and import.

Articles are shown in the phone's own web engine (WebKit on iOS, the system WebView on Android), with images and
video. The screenshots are drawn without a screen at iPhone 17 Pro size (402 × 874 pt), with the built-in HTML
renderer, so they have no status bar.

## Themes

You choose one light and one dark theme under **View ▸ Theme**. Picking a theme switches the app to it at once. Under
**Appearance** you can set it to follow the system; it then switches by itself between your light and your dark theme
when macOS, Windows or Linux goes from light to dark.

| Light | Dark |
| --- | --- |
| ![Sand](Docs/images/theme-sand.png) **Sand** — warm sand and amber. The default. | ![Ember](Docs/images/theme-ember.png) **Ember** — Sand turned over. The default. |
| ![Newsprint](Docs/images/theme-newsprint.png) **Newsprint** — sepia paper and ink-blue links, for long reads. | ![Black](Docs/images/theme-black.png) **Black** — pure black, for OLED screens. |
| ![Fern](Docs/images/theme-fern.png) **Fern** — green-leaning tones under a forest-green accent. | ![Fjord](Docs/images/theme-fjord.png) **Fjord** — arctic blue-grey with an ice-blue accent. |
| ![Harbor](Docs/images/theme-harbor.png) **Harbor** — a blue accent and a brown warning colour that stay apart under colour blindness. | ![Phosphor](Docs/images/theme-phosphor.png) **Phosphor** — green phosphor; only warnings are red. |
| ![Contrast](Docs/images/theme-contrast.png) **Contrast** — black on white with clear edges. | ![Contrast Dark](Docs/images/theme-contrast-dark.png) **Contrast Dark** — white on black with clear edges. |

Each theme is only a palette: sizes, type and spacing are the same everywhere. All ten must meet the same contrast
requirements (WCAG 2) for text, links and buttons, and a test measures them directly in the theme files. The typeface
is [Inter](https://rsms.me/inter/) on all three operating systems, and the icons are [Lucide](https://lucide.dev).

## Build and run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build RSSmote.slnx
```

```bash
dotnet run --project src/RSSmote.Desktop
```

Run the tests:

```bash
dotnet test RSSmote.slnx
```

The tests start the real app without a screen (Avalonia's headless platform) and draw it with Skia, so they also catch
mistakes in layout, themes and contrast.

### Packages

`build/` makes a package per OS and architecture in `artifacts/packages/`, each with its own .NET runtime:

```bash
build/package-macos.sh
```

```bash
build/package-windows.sh
```

```bash
build/package-linux.sh
```

The macOS script needs a Mac; the other two run on macOS or Linux. None of the packages are signed by a known
developer, so the first start asks:

- **macOS** (14 or later): unzip, move `RSSmote.app` to Applications, then right-click it and choose Open. Or clear the
  download mark with `xattr -dr com.apple.quarantine /Applications/RSSmote.app`.
- **Windows:** unzip and run `RSSmote\RSSmote.exe`; SmartScreen asks once (More info ▸ Run anyway). Articles need the
  WebView2 runtime, which Windows 11 has.
- **Linux:** unpack to `/opt` (`sudo tar -xzf RSSmote-…-linux-x64.tar.gz -C /opt`) and copy `/opt/RSSmote/rssmote.desktop`
  to `~/.local/share/applications/`. Unpacked elsewhere, change the two paths in that file.

**iPhone and Android:** the phone apps live in `RSSmote.Mobile.slnx` and need the `ios` or `android` workload
(`dotnet workload install ios android`). iOS also needs the Xcode version the workload names; Android needs the API 36
platform and JDK 17 or later. `RSSmote.slnx` builds without any of that.

```bash
dotnet build src/RSSmote.iOS/RSSmote.iOS.csproj -r iossimulator-arm64
```

```bash
dotnet build src/RSSmote.Android/RSSmote.Android.csproj
```

**Linux:** articles are shown in WebKitGTK 4.1 with libsoup 3. Without them the app uses the built-in HTML renderer
instead, which cannot show video.

### Where your data lives

The library is called `rssmote.sqlite` and the preferences `settings.json`. They sit side by side:

| System | Folder |
| --- | --- |
| macOS | `~/Library/Application Support/com.ahrentlov.rssmote/` |
| Windows | `%LOCALAPPDATA%\RSSmote\` |
| Linux | `$XDG_DATA_HOME/rssmote/` (otherwise `~/.local/share/rssmote/`) |
| iOS | `<app container>/Library/Application Support/` |
| Android | The app's private files folder (`/data/user/0/com.ahrentlov.rssmote/files/`) |

Set the environment variable `RSSMOTE_DATA_DIR` to use another folder, for example for a test library.

## What the app fetches from the network

- **Your feeds**, from the addresses you have added.
- **Favicons**, through Google's favicon service (`google.com/s2/favicons`), with the site's domain as a parameter.
  Google can therefore see which domains you follow, but not what you read.
- **Images in articles**, when you open them, from the servers the article points to.

There are no analytics, no tracking and no account. Links open in your browser, not in the app.

## Documentation

The project is described in `Docs/`, with an id on every rule, so code and documents can point to each other:

| File | Contents |
| --- | --- |
| [Docs/architecture.md](Docs/architecture.md) | How the app is put together: projects, shell, storage, refresh, article rendering |
| [Docs/design-language.md](Docs/design-language.md) | The design: tokens, themes, typography, components, icons |
| [Docs/pitfalls.md](Docs/pitfalls.md) | Failures that say nothing, with the fix and the measurement that proves it |
| [Docs/todo.md](Docs/todo.md) | What is missing, and how to check it |
| [Docs/dependencies.md](Docs/dependencies.md) | Every package with its version and licence |
| [src/RSSmote.App/Controls/Templates/templates.html](src/RSSmote.App/Controls/Templates/templates.html) | A catalogue of the app's own controls |

## Disclaimer

**Written by a machine.** The whole project was generated by Claude Opus 5.5, a language model from Anthropic, with
*reasoning effort* set to high. The code is exercised by about 240 automated tests that run the app without a screen,
but it has not been reviewed line by line by a human, nor audited by anyone with security expertise. The native article
view has not yet been tried by hand on Windows and Linux (see [Docs/todo.md](Docs/todo.md)).

**No warranty.** The app is provided as is, without warranty of any kind. Use it at your own risk. Neither the author,
Anthropic nor anyone else is liable for lost data or anything else that may follow from using it.

**Removed feeds are gone.** When you remove a feed, it is deleted together with all its articles, starred ones too.
The app asks first, but keeps no copy. Export your feeds as OPML if you want a backup of the list.

**The content belongs to others.** Articles belong to those who wrote them. RSSmote fetches feeds as they are
published and shows them to you; it passes nothing on.

**Not signed.** There are no signed or notarised packages. If you build it yourself, macOS and Windows may warn the
first time the app is opened.

The app is made for private use and is not affiliated with any company or product.

## Licence

[MIT](LICENSE). You may use, change and pass on the code, commercially too, as long as the copyright line and the
licence text go with it. The packages, the typeface and the icons have their own licences (MIT, Apache 2.0, OFL and
ISC); see [Docs/dependencies.md](Docs/dependencies.md).
