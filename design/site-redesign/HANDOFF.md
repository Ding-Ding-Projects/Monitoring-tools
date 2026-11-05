# Site redesign: design handoff

Design canvas made on 2026-11-05 for a fresh redesign of the project site (the GitHub Pages site built by
`scripts/build-site.mjs`). The app itself is not part of this pass. This folder is the record of the design; the
hosted canvas is an extra copy. Nothing here is implemented, verified or shipped behaviour of the site until the
site build says so.

## Direction: "Night shift monitor"

- Deep ink ground (`#070B09`) with a faint 48 px grid, the app's Mint seed Material 3 roles for accents
  (primary `#6FDBB4`), amber for warnings and the dim sum code name, sky for explanations, violet for notifications.
  A light variant swaps to the app's light roles.
- Oversized, tight display type (Roboto 800, negative tracking) over Roboto Mono readouts that read like
  instrument labels: release, latency, counts, hashes.
- The app's pulse glyph becomes a heartbeat line that draws itself across the hero.
- Every picture of the app is a real capture from `docs/screenshots/`. No mockups of the app.
- Every board states the site/app boundary where it matters: the site explains and downloads the app, and its
  locks, codes and examples are toys kept in the browser.

## Files

| File | What it shows |
| --- | --- |
| `canvas.json` | Canvas index: eleven boards in six rows. |
| `Main.dc.html` | Home (interactive). Live readout with a breathing dot, heartbeat line, scanning sweep, download button with version and platform, boundary line, the control centre capture bleeding off the right with a toast sliding into its corner on a loop, count-up stats, clipboard and internet panels (toasts scroll; the probe ladder lights up and DNS turns amber), task filter with staggered cards, all 17 areas with a sweeping underline, diagram call-out. English, Cantonese, bilingual; light and dark; a nav pill that springs between sections. |
| `HomePhone.dc.html` | Home at 390 px: sheet menu that springs open, full-width download, link toast over the capture, scrolling filter chips. |
| `Docs.dc.html` | Docs hub: search with the regex builder unfolding under it (tokens pop in, matches highlight, explanation), a five-step guided start kept per browser with a springy progress bar, areas with a ring and glyph morph on hover. |
| `Article.dc.html` | Article reader: browser-style tabs with group colours and pins, a reading progress bar, a contents panel resized by dragging its divider (200 to 420 px), right-click tab menu with shortcut labels and its own filter, real capture, callout, suggested next articles, optional source link. |
| `Diagram.dc.html` | Interactive workflow: six steps of copying a file path with `:mp4`, auto-play with pause and replay, any step selectable, a "hover the toast" or "ignore it" branch that changes step 4, a live pulse on the current step, flowing connectors and a text version. Labelled as an illustration. |
| `Settings.dc.html` | Settings: language mode, separate English and Cantonese humour sliders with a live sample line, seed colours, colour picker and colour translator (hex, name, HSL, contrast), text size, reduce motion switch, live preview, presets, export, import, reset, and what the browser keeps. |
| `Palette.dc.html` | Command palette over the dimmed site: typed query, filter chips, results that stagger in, and the jump to the exact element with a pulsing ring. |
| `Tabs.dc.html` | Tab strip with pins, collapsible colour groups, an overflow button and a tab being dragged while the next one makes room; dock on top or on the left; four ways to find a tab; a right-click menu with shortcuts and filter; resizable panels. |
| `Gallery.dc.html` | Real captures in a masonry with filters and counts, hover lift, and a lightbox that zooms in. |
| `Releases.dc.html` | Latest release v1.0.57 "Steamed Custard Bao", workflow start, end and duration, the four downloads with sizes and SHA-256 and a copy button, line counts with growing bars, the human-time estimate with its method, and a changelog timeline. Figures are copied from the v1.0.57 release and README. |
| `Safety.dc.html` | A toy PIN lock that shakes on a wrong code and springs open on the right one (example code 2468), the unlock ladder with the app's real rungs, an authenticator ring with an example code, non-blocking notifications sliding in, and the dim sum surprise with steam. |
| `frames/` | Local renders of every board at its design width, and Home, Docs, Article and Settings at 390 px. |
| `REHEARSAL.md` | What the review of the frames found and what was fixed. |

Board files reference the real captures as `../../docs/screenshots/<name>.png`, so the canvas reads back from the
repository alone (the hosted canvas uses uploaded copies of the same files).

## Motion system

| Motion | Timing | Why it exists |
| --- | --- | --- |
| Rise in, staggered | 700 to 900 ms, `cubic-bezier(.2,.8,.2,1)`, 60 to 90 ms per item | Content arrives in reading order. |
| Spring | `cubic-bezier(.3,1.4,.5,1)`, 350 to 500 ms | Pills, chips, tabs, keys and the nav indicator: things you press answer you. |
| Shape morph | pill to 14 to 18 px radius on hover, 12 px on press | Material 3 expressive feedback without colour noise. |
| Heartbeat draw | 2.6 s once | Ties the brand glyph to the product: it watches. |
| Toast slide | 7 s loop, 3 s hold | Shows what a toast does instead of describing it. |
| Count up | 2.2 s once | Stats feel live; ends on the true number. |
| Breathing dot | 2.4 s loop | Something is running. |
| Unfold | 450 ms | Builders and menus grow out of what opened them. |
| Shake / pop | 500 ms / 450 ms | Wrong code, done step. |

Reduce motion (system or site setting) stops every animation and transition and shows each element in its final
state.

## Decisions for the site build

- Replace `site/src/style.css` and the page templates in `scripts/build-site.mjs` with this direction; keep the
  build's guards (fonts, categories, links) and its deterministic output.
- Keep Roboto and Roboto Mono from the vendored `site/src/fonts/`; the boards load them from Google Fonts only as
  design files. Weight 800 is used for display type: the vendored variable Roboto covers 400 to 700, so the build
  either vendors the 800 range through `scripts/vendor-site-fonts.mjs` or uses 700.
- Every number on a page comes from the build inputs (release metadata, line-count figures in the README, the
  article inventory), never typed into a template.
- The download button is shown only when the release manifest confirms the installer asset.

## Stageshow

Stageshow came from the pinned cache at commit `299140fafd08df55a9c234e610c3f64af3f45531`, because the fresh install
was refused in the design session. Rules applied: restraint (real words only where the eye lands, filler elsewhere),
one hero with one satellite straddling its edge, crops that bleed off one edge, one gutter, no dead corners, every
motion with a cause, a poster frame that tells the story, filming at the shown width and at 390 px, and a review of
the frames. Rules that did not apply: the faux-UI kit (this site uses real captures, not drawn product UI), the
cursor choreography (the site is used directly, not watched), and the film script's gate (the design session could
not install the kit's dependencies; frames were rendered locally with Chrome instead).

## Not checked

- The boards were rendered with the canvas runtime in a local Chrome, not inside the hosted editor.
- No board has been tested with a screen reader, at 200 % text size, or on a real touch device.
- The light variant is drawn for Home only.
