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
