# To do

`N` = need to have, `W` = nice to have. Ids are append-only (see CLAUDE.md).

**N1 — Check `ArticleHost` by hand in each browser.** The fallback is covered by tests (A9); the iframe is not
(A23). In Safari on macOS and iOS, Chrome and Firefox: page background before first paint, links to a new tab,
shortcuts forwarded from the page (L9), space scrolling, YouTube embeds, and an article with an http image on an
https server, which the browser blocks or upgrades (A19 covers favicons and thumbnails, not the article's own images).
Seen so far in the in-app browser (Chromium): the page renders in an `<iframe srcdoc>`, `j` inside the page moves to
the next article.

**W1 — Sync services** (Feedbin, Fever, …). Deliberately out of scope for now.

**N12 — Check Automatic theme switching in each browser.** Switch the OS between light and dark while RSSmote runs,
with Automatic and with non-default palettes in both slots: the page repaints, and the menu button shows the checked
items.

**W3 — Tablet layout.** Below 900 wide `MainView` shows the phone stack (A22), so a tablet in portrait gets the
phone's layout at a larger size, and from 900 the three desktop columns. A tablet needs its own answer between the
two (list and article side by side). Done when it is drawn and recorded as a D entry.

**W4 — Check the phone stack on a phone.** Served with `build/run-local.sh --lan` and opened in Safari on an iPhone:
safe-area insets (P15) and the home-screen icon, the back gesture and the browser's own back (P18), the list keeping
its top row across a refresh (P25), the article page's bars hiding on scroll (D26), and the event stream coming back
after the phone sleeps (A20). Done when each is seen.

**W7 — Conditional GET on refresh.** `Refresher` downloads every feed in full on each pass. Store `ETag` and
`Last-Modified` per feed (schema version 2, two nullable columns on `feeds`, A8), send `If-None-Match` and
`If-Modified-Since`, and skip parsing on 304. Done when a test server sees both headers on the second refresh and a
304 leaves the feed's items and `last_error` untouched.

**N21 — Check text sizes by hand (D31).** In each browser: rows at 85 % and 175 % are sharp, not blurred by
`Scaled`; on the desktop columns the Aa popover opens above the gear over the list, and ⌘/Ctrl + changes its pane
while the slider has the keyboard; on the phone stack the article's iframe ends at the top of the text sheet instead
of covering it (P29). Done when each is seen.

**N22 — Sign in by hand in each browser.** The sign-in field could not be typed into by the test driver (P30); signing
in was checked through `fetch('api/login')`, and a wrong key through events dispatched by hand. In Safari (macOS,
iOS), Chrome and Firefox: type the key, Enter signs in, a wrong key says so, and the password manager does not offer
to save a site password it cannot use. Done when each is seen.

**N23 — Build and run the Docker image.** The Dockerfile is written but not built: the Docker daemon was not running.
Done when `docker build -t rssmote .` succeeds, `docker run -p 8080:8080 -e RSSMOTE_KEY=… -v rssmote:/data rssmote`
serves the app, and the library survives a container restart.

**N24 — Import and Export in Safari and Firefox.** `StorageFilePicker` uses Avalonia's browser storage provider, which
builds on the File System Access API that only Chromium has. Check that Import opens a file and Export saves one in
Safari and Firefox; if not, Export can link to `GET /api/opml` (A17) and Import take a plain file input. Done when
both work in all three.

**W8 — Install as an app.** A web manifest and a service worker would let the phone keep RSSmote on its home screen
as a standalone app, with its own icon sizes. Today `index.html` names `icon.png` as the touch icon only.
