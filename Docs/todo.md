# To do

`N` = need to have, `W` = nice to have. Ids are append-only (see CLAUDE.md).

**N1 — Check `ArticleHost` by hand on each OS.** The fallback is covered by tests (A9); the native path is not.
Confirm on macOS, Windows 11 (WebView2) and a Linux VM with WPE or
WebKitGTK and with neither (fallback): page background before first paint, links to the browser, shortcuts
forwarded from the page (L9), space scrolling, YouTube embeds. For the `.app` (N5),
WKWebView may need `NSAllowsArbitraryLoadsInWebContent` for http images.

**N2 — Spike: title bar on Windows and Linux.** Measure the caption-button width on Windows (P3) and decide the
Linux strip layout (P2).

**N5 — Packaging per OS.** macOS `.app` (ad-hoc signed), Windows zip, Linux tarball first; Velopack later.

**W1 — Sync services** (Feedbin, Fever, …). Deliberately out of scope for now.

**N9 — Settings window and article context menus.** General · Reading · Appearance · Feeds (TableView, D17);
article-list context menu (mark above/below as read, star, open in browser). Sidebar menus are done (A6). The
Settings button goes in the sidebar status bar.

**N12 — Check Automatic theme switching on each OS.** Switch the OS between light and dark while RSSmote runs, with
Automatic and with non-default palettes in both slots: the window repaints, and the macOS menu and the Windows/Linux
menu button show the checked items.

**N13 — Look at the cards and panels on each OS.** On macOS, Windows 11 and Linux: the article's web view sits inside
the card with its rounded corners intact; the 8 pt band above the cards and the card strips drag the window; on
Windows the caption buttons clear the article card's actions (P3, N2); panels show their shadow and scrim; and the ten
palettes switch from the menus.


**W2 — Preview an OPML import on desktop, as the mobile Import Feeds page does (D27).** Today "Import Feeds…"
imports the chosen file at once and only reports the count afterwards (`ShellViewModel.ImportOpmlAsync`). Instead,
show the file name and "N feeds in M folders", the folders with the feeds each one adds, how many feeds are already
added and will be skipped (`Store.FeedWithUrl`), and import only on "Import N Feeds"; "Change" picks another file.
Mobile and desktop share the view-model, so build it once in `RSSmote.App` and give each head its own view (a panel
on desktop, A6). Done when importing a file that partly overlaps the library shows the right counts before anything is
saved, and cancelling leaves the library untouched (a test in `RSSmote.App.Tests`).

**W3 — Tablet layout.** The mobile design covers the phone only (D23–D28). A tablet in landscape needs its own
answer (list and article side by side, or three columns as on desktop). Done when it is drawn and recorded as a
D entry.

**N16 — Native article view on iOS (P16).** Find why `NativeWebView` stays blank inside `MobileShell` (a page pushed
during a transition, a native view attached under a `NavigationPage`, or the WebView package's iOS support; the page's first layout happens a whole width off screen, P20) and switch
the iOS head back from the fallback renderer. The fallback also shows no remote images on iOS. Done when an article
with images and a YouTube embed shows in the simulator with the native view.
