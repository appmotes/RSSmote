# To do

`N` = need to have, `W` = nice to have. Ids are append-only (see CLAUDE.md).

**N1 — Check `ArticleHost` by hand on each OS.** The fallback is covered by tests (A9); the native path is not.
Confirm on macOS, Windows 11 (WebView2) and a Linux VM with WPE or
WebKitGTK and with neither (fallback): page background before first paint, links to the browser, shortcuts
forwarded from the page (L9), space scrolling, YouTube embeds, and http images in the packaged `.app`, which allows
them in web content (A2).

**N2 — Spike: title bar on Windows and Linux.** Measure the caption-button width on Windows (P3) and decide the
Linux strip layout (P2).

**N5 — Try the packages by hand (A2).** `build/package-*.sh` make them. The macOS arm64 app, unpacked from its zip
without a download mark, opened with its window, its Dock icon and an article with a web image. Still to see: a
downloaded copy opening after Gatekeeper's prompt, the x64 app on an Intel Mac, and an article with an http image.
On Windows 11 (x64, arm64): `RSSmote.exe` starts after SmartScreen and shows its icon in the taskbar. On Linux (x64,
with WebKitGTK 4.1): the tarball in `/opt` starts from the desktop entry. Done when each is seen.

**W1 — Sync services** (Feedbin, Fever, …). Deliberately out of scope for now.

**N12 — Check Automatic theme switching on each OS.** Switch the OS between light and dark while RSSmote runs, with
Automatic and with non-default palettes in both slots: the window repaints, and the macOS menu and the Windows/Linux
menu button show the checked items.

**N13 — Look at the cards and panels on each OS.** On macOS, Windows 11 and Linux: the article's web view sits inside
the card with its rounded corners intact; the 8 pt band above the cards and the card strips drag the window; on
Windows the caption buttons clear the article card's actions (P3, N2); the Settings question shows its shadow and
scrim; and the ten palettes switch from the menus.


**W3 — Tablet layout.** The mobile design covers the phone only (D23–D28). A tablet in landscape needs its own
answer (list and article side by side, or three columns as on desktop). Done when it is drawn and recorded as a
D entry.

**N18 — An Android Debug build stalls its UI thread during the first refresh.** On a freshly booted API 35 emulator
(`-memory 4096`, host load ~10) a Debug build (`-t:Install`) takes 17.6 s to start and then blocks the main thread
for 22 s and 6 s while the startup refresh runs (`Choreographer: Skipped 1307 frames`); input in that window ends in
an ANR, and the dialog returns after every Wait. Offline there is no long stall; Release on the same emulator starts
in 2.0 s and skips at most 42 frames. Stack samples show the main thread in Mono's type loader and interpreted code,
not waiting on the store's lock. Find which refresh work runs on the UI thread (the store-change handlers and their
reloads, A16) and whether Debug runs interpreted. Done when a Debug start on a fresh emulator skips under 120 frames
during the first refresh.

**W4 — Check refresh on resume on a device.** `ShellViewModel.RefreshIfStale` runs when the app comes back from the
background and the last pass is older than the interval; it is tested headless only. The list keeping its top row
across a refresh (P25) was seen on the iPhone 17 Pro simulator, not yet on the API 35 emulator. Done when a resume
after the interval starts a refresh on both, and the list keeps its place on Android.

**N20 — Check desktop Settings on each OS (D30).** On macOS: "Settings…" with ⌘, sits in the application menu and
opens Settings (the item is added from code after the window is built, which the headless tests cannot see exported;
L13), and the traffic lights clear the Settings title. On Windows: the caption buttons clear New Folder and Save
(P3). On all three: the settings sidebar's strip and the band above the bar drag the window, the article's native
view stays hidden while Settings show (L9), and Import and Export open the system's file dialogs. Done when each is
seen.

**W6 — Installers and updates.** Velopack (MIT) for an installer and updates on all three OSes, a Developer ID
signature with notarisation on macOS, and Authenticode on Windows, so the first-start warnings go (A2).

**W7 — Conditional GET on refresh.** `Refresher` downloads every feed in full on each pass. Store `ETag` and
`Last-Modified` per feed (schema version 2, two nullable columns on `feeds`, A8), send `If-None-Match` and
`If-Modified-Since`, and skip parsing on 304. Done when a test server sees both headers on the second refresh and a
304 leaves the feed's items and `last_error` untouched.

**N21 — Check text sizes by hand (D31).** On macOS, Windows and Linux, the iPhone simulator and the API 35 emulator:
rows at 85 % and 175 % are sharp, not blurred by `Scaled`; on desktop the Aa popover opens above the gear over the
list, and ⌘/Ctrl + changes its pane while the slider has the keyboard; on the phones the article's native view ends
at the top of the text sheet instead of covering it (P29), and changing Larger Text (iOS) or the font size (Android)
while RSSmote runs resizes the panes. Done when each is seen.
