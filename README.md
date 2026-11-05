<img src="Docs/images/app-icon.png" alt="The RSSmote icon: a spark sending out two feed arcs" width="96">

# RSSmote

**English** · [Dansk](README.da.md)

An RSS reader you host yourself and read in the browser: on the desktop, the tablet and the phone. Three columns on a
wide screen, one stack on a phone, keyboard first, and one library on your own server that every device shares.

**This app was written by a machine intelligence.** All the code, the comments, the design, the ten colour themes, the
app icon, the tests, the documentation and this README were made by Claude Opus 5.5 (Anthropic, with *reasoning
effort* set to high) from a human's description. No human has reviewed the code line by line. Read the
[Disclaimer](#disclaimer) before you trust it with anything you care about.

![RSSmote with an article open, light theme](Docs/images/reading-light.png)

On the left are your feeds, gathered in folders. In the middle are the articles, grouped by day. On the right is the
article you are reading. A small server keeps your feeds and articles in one SQLite file, refreshes them, and serves the
app to your browser. There is one key and no accounts: whoever has the key reads the library, from as many devices as
they like, and what is read on one is read on all of them.

Built with .NET 10, ASP.NET Core and [Avalonia](https://avaloniaui.net) 12, which runs in the browser as WebAssembly. Only free components with MIT or similar licences; see
[Docs/dependencies.md](Docs/dependencies.md).

## What it does

- **Add a feed** from a feed address or just the address of a website. RSSmote finds the site's feeds itself and
  shows them before anything is saved.
- **RSS 2.0, Atom, RDF and JSON Feed.** Feeds with invalid XML (such as `&nbsp;` in the middle of a title) are read
  anyway.
- **Folders** that fold in and out, with unread counts on every folder and feed.
- **Filters**: Starred, Unread and All, plus a filter box above the list that searches titles, summaries and feed
  names.
- **Articles in the browser's own engine**, with the app's own typography. Scripts, forms and inline styles are
  removed before the page is shown.
- **One library, every device.** Read, starred, folders and feeds follow you from the desktop to the phone at once;
  each device keeps its own theme and text sizes.
- **OPML import and export**, so you can move your feeds in and out.
- **Refresh** every half hour on the server, also while no device is open, and with `r` whenever you like. Read, unstarred articles are cleared
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
| `⌘1` `⌘2` `⌘3` | Starred / Unread / All (`Ctrl` instead of `⌘` off a Mac) |
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
`tests/RSSmote.Mobile.Tests`). They are drawn with the built-in HTML renderer, so an article looks slightly different
from the browser's.

## On the phone

Open the same address on the phone. Below 900 pixels wide RSSmote is one stack instead of three columns, and a
desktop window narrowed that far turns into the same. The screens below show an iPhone-sized window.

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
- **Refresh:** the server refreshes on its own; pull down on Feeds for a refresh now.
- **The same library** as on the desktop: what you read on the phone is read everywhere.

The screenshots are drawn without a screen at iPhone 17 Pro size (402 × 874 pt), so they have no status bar.

## Themes

You choose one light and one dark theme in the menu (**Theme**) or under Settings ▸ Appearance. Picking a theme
switches the app to it at once. You can set it to follow the system; it then switches by itself between your light and
your dark theme when the device goes from light to dark. Each device keeps its own choice.

| Light | Dark |
| --- | --- |
| ![Sand](Docs/images/theme-sand.png) **Sand** — warm sand and amber. The default. | ![Ember](Docs/images/theme-ember.png) **Ember** — Sand turned over. The default. |
| ![Newsprint](Docs/images/theme-newsprint.png) **Newsprint** — sepia paper and ink-blue links, for long reads. | ![Black](Docs/images/theme-black.png) **Black** — pure black, for OLED screens. |
| ![Fern](Docs/images/theme-fern.png) **Fern** — green-leaning tones under a forest-green accent. | ![Fjord](Docs/images/theme-fjord.png) **Fjord** — arctic blue-grey with an ice-blue accent. |
| ![Harbor](Docs/images/theme-harbor.png) **Harbor** — a blue accent and a brown warning colour that stay apart under colour blindness. | ![Phosphor](Docs/images/theme-phosphor.png) **Phosphor** — green phosphor; only warnings are red. |
| ![Contrast](Docs/images/theme-contrast.png) **Contrast** — black on white with clear edges. | ![Contrast Dark](Docs/images/theme-contrast-dark.png) **Contrast Dark** — white on black with clear edges. |

Each theme is only a palette: sizes, type and spacing are the same everywhere. All ten must meet the same contrast
requirements (WCAG 2) for text, links and buttons, and a test measures them directly in the theme files. The typeface
is [Inter](https://rsms.me/inter/) in every browser, and the icons are [Lucide](https://lucide.dev).

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Pick a key of at least 12 characters: it is the only
thing that signs a device in.

**On this machine:**

```bash
RSSMOTE_KEY='a long key of your own' build/run-local.sh
```

Open <http://localhost:5000> and enter the key. With `--lan` the server listens on every address, so a phone on the
same network can open `http://<this machine>:5000`.

**On a server**, with Docker:

```bash
docker build -t rssmote .
```

```bash
docker run -d -p 8080:8080 -e RSSMOTE_KEY='a long key of your own' -v rssmote:/data rssmote
```

Or without Docker: `build/publish.sh` writes the server with the app beside it to `artifacts/server`
(`build/publish.sh linux-x64` brings its own .NET runtime). Run it with `RSSMOTE_KEY` set.

Put it behind a reverse proxy with HTTPS (Caddy, nginx, Traefik) before it is reachable from the internet, and set
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the server sees the reader's address. Sign-in attempts are limited to
five a minute per address. A new key signs every device out.

**Tests:**

```bash
dotnet test RSSmote.slnx
```

The tests start the real app without a screen (Avalonia's headless platform) and draw it with Skia, so they also catch
mistakes in layout, themes and contrast. The server's tests run it in process and let two devices sync through it.

### Where your data lives

On the server: the library is called `rssmote.sqlite`, the library's settings `settings.json`, and the sign-in keys
sit in `keys/`, side by side:

| System | Folder |
| --- | --- |
| Docker | the `/data` volume |
| macOS | `~/Library/Application Support/com.ahrentlov.rssmote/` |
| Windows | `%LOCALAPPDATA%\RSSmote\` |
| Linux | `$XDG_DATA_HOME/rssmote/` (otherwise `~/.local/share/rssmote/`) |

Set `RSSMOTE_DATA_DIR` to use another folder. A library from the earlier desktop app can be copied in as it is. Each
device keeps its theme and text sizes in the browser's own storage.

## What the app fetches from the network

The server fetches:

- **Your feeds**, from the addresses you have added.
- **Favicons**, through Google's favicon service (`google.com/s2/favicons`), with the site's domain as a parameter.
  Google can therefore see which domains you follow, but not what you read.
- **Thumbnails** in the list, on behalf of the browser.

The browser fetches **images in articles** when you open them, from the servers the article points to. There are no
analytics and no tracking. Links open in a new tab.

## Documentation

The project is described in `Docs/`, with an id on every rule, so code and documents can point to each other:

| File | Contents |
| --- | --- |
| [Docs/architecture.md](Docs/architecture.md) | How the app is put together: projects, shell, storage, refresh, article rendering |
| [Docs/architecture.html](Docs/architecture.html) | The same as diagrams, in English and Danish: projects, sync, start-up, the server |
| [Docs/design-language.md](Docs/design-language.md) | The design: tokens, themes, typography, components, icons |
| [Docs/pitfalls.md](Docs/pitfalls.md) | Failures that say nothing, with the fix and the measurement that proves it |
| [Docs/todo.md](Docs/todo.md) | What is missing, and how to check it |
| [Docs/dependencies.md](Docs/dependencies.md) | Every package with its version and licence |
| [src/RSSmote.App/Controls/Templates/templates.html](src/RSSmote.App/Controls/Templates/templates.html) | A catalogue of the app's own controls |

## Disclaimer

**Written by a machine.** The whole project was generated by Claude Opus 5.5, a language model from Anthropic, with
*reasoning effort* set to high. The code is exercised by about 280 automated tests that run the app without a screen,
but it has not been reviewed line by line by a human, nor audited by anyone with security expertise. That matters more
now that it is a server: keep the key long, and keep the server behind HTTPS. It has not yet been tried by hand in
every browser (see [Docs/todo.md](Docs/todo.md)).

**No warranty.** The app is provided as is, without warranty of any kind. Use it at your own risk. Neither the author,
Anthropic nor anyone else is liable for lost data or anything else that may follow from using it.

**Removed feeds are gone.** When you remove a feed, it is deleted together with all its articles, starred ones too.
The app asks first, but keeps no copy. Export your feeds as OPML if you want a backup of the list.

**The content belongs to others.** Articles belong to those who wrote them. RSSmote fetches feeds as they are
published and shows them to you; it passes nothing on.

The app is made for private use and is not affiliated with any company or product.

## Licence

[MIT](LICENSE). You may use, change and pass on the code, commercially too, as long as the copyright line and the
licence text go with it. The packages, the typeface and the icons have their own licences (MIT, Apache 2.0, OFL and
ISC); see [Docs/dependencies.md](Docs/dependencies.md).
