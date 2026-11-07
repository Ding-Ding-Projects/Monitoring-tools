# Handoff

Factual state of the project for the next person or agent. Claims here are limited to what was
checked; anything unchecked is labelled.

## Current state

- Windows tray app (.NET 8, WPF, single-file) with clipboard toasts, quick commands, internet watch,
  control centre, regex builder, Minecraft server manager and the other features listed in
  [README.md](README.md). The checklist of shipped and open items is [ROADMAP.md](ROADMAP.md).
- Every push to `main` builds through `build-installer.bat`, renders screenshots of the real app,
  publishes the next normal release and deploys the site (`.github/workflows/build.yml`).
- Latest release at the time of writing: v1.0.36 (see [CHANGELOG.md](CHANGELOG.md)).

## Virtual disks, Quick pack and the smaller features beside them (2026-11-07)

- **Design:** [design/features](design/features/HANDOFF.md), five interactive boards made before the build: the page,
  New virtual disk, Quick pack, the live chart and the menus with live controls.
- **Virtual disks page:** `UI/Pages/VirtualDisksPage.cs` (routes row, library, one disk, action panels, New virtual
  disk) and `VirtualDisksPage.QuickPack.cs`. The page is a section of the control centre (`"vdisks"`), and a disk file
  dropped anywhere on the control centre joins the library (`Settings.DiskLibrary`).
- **Engine:** `Core/Disks/VirtDisk.cs` calls `virtdisk.dll` directly, writing each parameter block at byte offsets.
  It opens with the version 2 parameters and no access mask, and falls back to version 1 with an access mask when
  Windows answers 87, 5 or 50. `Core/Disks/DiskJobRules.cs` holds the checks before anything is written and the
  PowerShell and DiskPart scripts (window-free, unit-checked); `Core/Disks/DiskJob.cs` runs one job (`Execute`) and
  the elevated route (`RunElevatedAsync`, `--vdisk-job <request> <sha256>`, one job per run, exit 1223 means the
  prompt was declined).
- **Quick pack:** `Core/Disks/QuickPackPlan.cs` (name, clash numbering, sizing, growth sizes) and
  `Core/Disks/PackCopy.cs` (copy, streams, times, attributes, optional permissions, SHA-256 read back, the grow
  callback). The disk is made by the existing elevated helper (`VhdHelper`, `--vhd-create`), which now also takes
  `grow <MB>` (detach, grow, attach, extend, same letter) and `fit` (shrink the volume and the VHDX, then detach)
  while it stays running, so a pack asks for permission once. New NTFS and ReFS volumes are opened up for the
  account that asked.
- **Checks:** `UI/SelfTest.VirtualDisks.cs`, run with `MT_ONLY=vdisk` by the build step "Check virtual disks" on
  every push, makes and changes real disks in a scratch folder and reads every state of the page in Cantonese and
  in both languages through `LanguageAudit.Begin`/`End`. The page has test hooks (`VdTestShow`, `VdTestPackAsync`,
  `VdTestNew`) that the captures in `UI/ScreenshotRunner.VirtualDisks.cs` use too.
- **Cantonese:** `Core/Strings.yue.VirtualDisks.cs`. Its patterns are registered first, because some general
  patterns elsewhere would otherwise take whole disk messages; a pattern that joins two sentences translates each
  part with `Language.Yue`.
- **Not built yet:** listed at the end of [the article](docs/features/files/virtual-disks.md#not-built-yet).
- **Beside it on the same branch:** the dim sum switch that springs back (`ModesPage.DimSumSwitch`), Base64 beside
  the file (`Core/Base64Files.cs`, `UI/Pages/ToolsPage.Base64.cs`), the append-only record of actions and discarded
  changes (`Core/LocalHistory.cs`, `UI/Pages/RecordsPage.Actions.cs`), one Markdown renderer (`UI/Markdown.cs`,
  `Core/MarkdownText.cs`), live controls in the element menu and a filter in every dropdown (`UI/ElementMenu.cs`,
  `UI/Dialogs/DialogParts.cs`), the live response-time chart (`UI/LiveChart.cs`), folding panels
  (`UI/Collapsible.cs`) and resizable panels (`UI/PanelSizer.cs`). Their docs say what is checked and what is not.
- **Capture fix:** the guided Make a look flow starts with the look step since the four looks; its capture in
  `ScreenshotRunner.Wave6.cs` now steps past it. Before the fix the screenshot run stopped there, wrote no index, and
  the release then failed in the site build on pictures that were never made.

## Looks, every tab open and the every-surface sweep (2026-11-07)

- **Every tab open:** `Core/TabDefaults.cs` opens each page the strip has not offered before (`KnownTabs`), so a fresh
  install shows all nineteen and an update adds only new pages; a closed tab stays closed. Close buttons show only on
  the open tab and on hover (`UI/Tabs/StripTab.cs`). Checked by `tests/SmartTests/AppStartChecks.cs`.
- **Updates no longer show the welcome:** `Core/InstallNote.cs` decides what a hand-started copy leaves behind: the
  welcome on a genuine first install, the quiet "Updated from X to Y." toast on an upgrade by hand, and just the control
  centre when the same version is started again. Checked by unit checks in `AppStartChecks.cs`; not yet
  watched on a real install.
- **Cut or copied:** `Clip/Smart/CopyOrigin.cs`; files use Explorer's Preferred DropEffect, everything else the shortcut
  held at the moment of the change. Toast chip in `UI/ToastWindow.xaml(.cs)`, history line in `UI/HistoryPopup.cs`.
- **Looks:** designs in [design/looks](design/looks/HANDOFF.md). `Core/Looks.cs` (definitions), `Core/Appearance.cs`
  (`ApplyLook`: fonts, title weight, shadows, card outlines), `UI/LookShape.cs` (corner mapping on each border's first
  layout and a reshape of open windows on change), `UI/LookParts.cs` (page label, card outline, readout window, grid,
  picker previews), the picker in `UI/Pages/AppearancePage.Looks.cs`, a setup step, the Make a look guide and palette
  entries. WPF only delivers `Loaded` to elements with a Loaded handler of their own, so class handlers that must see
  every element hook `SizeChanged` (the first layout) instead; corner mapping and the compact density for buttons both
  do.
  Typefaces come from the npm registry, pinned with sha256 in `deps/manifest.json` (`download-dependencies.ps1` now
  reads npm tarballs); their OFL texts are embedded under `licenses/fonts/`.
- **Sweep:** `UI/SurfaceSweep.cs`, run with `MT_ONLY=sweep` through the screenshot entry point; the build step "Sweep
  every surface" fails the build on any finding except trimmed text, which `sweep.md` lists for review. The sweep fakes
  the screen through `Ui.WorkArea`, so every window sizes and places itself from `Ui.WorkArea`, never from
  `SystemParameters.WorkArea`. Dialogs built on `ToolDialogWindow` mark their scrolling part with `Flex(...)`, which
  shrinks on a short screen so the window and its buttons fit.
- **Layout building blocks:** `UI/CardGrid.cs` lays cards in equal columns with each row as tall as its tallest card,
  always at the full width it is offered (so it is arranged at the width it was measured at) and with fewer columns
  when `MinColumnWidth` would be broken; use it instead of `UniformGrid` for cards with wrapping text. `UI/FieldBox.cs`
  is a text box with a preferred width that narrows when there is no room; put it, and the buttons beside it, in a
  `WrapPanel`, not a horizontal `StackPanel`, which gives its children unlimited width and so never wraps or narrows.
  Code that changes a corner after a box appeared calls `LookShape.Set`.
- **How the sweep reads a cut:** WPF arranges an element that got less room than it asked for at its full size and
  clips it to its slot (a layout clip); the sweep reads `LayoutInformation.GetLayoutClip` for that, plus `ClipToBounds`
  and scroll viewports. It measures text again only to list trimmed text and text held to a set height, because a
  second measurement can disagree with the window by a pixel or two. Each element is checked against two boxes: what
  is visible, and what boxes that cannot scroll allow. A scroll viewer resets the second box on the axes it scrolls, so
  text scrolled out of view is still checked, and only a box that cannot scroll can cut it. An early version stopped at
  the first box that was empty and so skipped everything scrolled out of view; fixing that found ten more cuts.
- **Labels:** section and field labels are in sentence case everywhere; helpers no longer turn them into capitals.

## Smart clipboard (2026-11-06)

- **Design:** [design/clipboard-ideas](design/clipboard-ideas/HANDOFF.md), every idea picked, plus boards for each dialog,
  quick conversions, extensions and the one action bar rule.
- **Detectors:** `src/MonitoringTools/Clip/Smart/` holds pure code with no WPF: 30 kinds of copied text (`SmartDetect`
  orders them), quick conversions (`Units`), snippets, recipes and per-app rules (`Power`), and the extension host
  (`Extension`, `ExtensionHost`). `tests/SmartTests` checks them on any machine (`dotnet run --project tests/SmartTests`);
  the build workflow runs it before building.
- **App side:** `UI/Actions/SmartToast.cs` (tiles), `SmartActions.cs` (buttons), `SmartLookups.cs` (automatic lookups),
  `UI/ClipPower.cs` (shortcuts, plain paste, undo, collect, type it, snippets, rules), `HistoryPopup.cs`, `PasteQueue.cs`,
  `PinWindow.cs`, and the dialogs in `UI/Dialogs/` (conversions, extensions, clipboard settings, snippets, recipes, rules,
  picture text, ask). Settings live in `Core/Settings.Clipboard.cs`; Cantonese in `Core/Strings.yue.Smart.cs`.
- **Shortcuts:** history is `Win+Shift+V`; the rest are `Ctrl+Shift+Alt+letter` so no editor shortcut is taken. Global
  shortcuts and the keyboard hook start only in the normal run, never in captures or self-tests.
- **Extensions:** `extension.json` plus a program that reads one JSON line and writes one; samples in `samples/extensions`,
  guide in [docs/features/clipboard/extensions.md](docs/features/clipboard/extensions.md).
- **Checked:** detector and extension checks pass on Linux (152) and in CI; the Windows screenshot run renders the smart
  toasts, quick conversions and the extensions manager. Unchecked on a real desktop: global shortcuts, the keyboard hook
  (snippets, paste queue), pasting into another app, OCR, face blurring and automatic lookups against the live services.
- **Not built:** sync between your own PCs.

## Site redesign (2026-11-05, evening)

- **Design:** [design/site-redesign](design/site-redesign/HANDOFF.md), eleven boards in the "Night shift monitor"
  direction, with local renders and review notes.
- **Built:** `site/src/style.css`, `site/src/app.js` and the page templates in `scripts/build-site.mjs` were replaced.
  New pages: `diagram/` (interactive workflow) and `settings/` (language, humour, colour, text size, motion, tab
  dock, presets, export and import, toy locks, toy authenticator, notification centre). New across every page:
  browser-style tabs, right-click menus with working shortcuts, a command palette that jumps to elements, the
  regex builder with tokens and an explanation, notifications and the language switch.
- **Release facts:** the version, the installer URL (the immutable asset of that release) and the line counts are
  read from the README's release-figures block, which the release workflow writes.
- **Guard:** the build fails if a shared site feature is missing from the pages that must carry it (`SITE_FEATURES`).
- **Article:** [docs/features/app/project-site.md](docs/features/app/project-site.md) documents the site and what a
  static site cannot do.
- **Checked:** the build passes its guards; Chrome renders of every page in dark and light at 1440 px and at 360 px
  with no sideways scroll and no script errors; palette, right-click menu, toy lock with a wrong and a right code,
  Alt+T, settings language and accent, tab grouping and the diagram were driven in Chrome.
- **Not checked:** a screen reader, a real phone or touch screen, and the deployed site until the next Pages run.

## State on 2026-11-05 (afternoon)

- **main:** 1946faf, pushed. Releases v1.0.51, v1.0.52 and v1.0.53 published green as normal
  releases; the release for 1946faf had not been checked when this was written.
- **Merged and checked today:** document, archive and disk, data and security, and media dialogs
  (21 file tool windows); the accessibility, Material 3, QR and world generator audit; dialogs moved
  onto the type and shape scales; regex builder parity (13 of 13 families); 7z, xz, bz2 and AES zip
  extraction, an Ask rule for existing files, every face of a .ttc and tray Eject for mounted images;
  RAR4 and RAR5 through the full 7-Zip 26.05 (pinned by SHA-256, unRAR licence: decoding only);
  per-element locks with radio buttons, drop-down lists, tabs and menu items; Bedrock Download ZIP
  and folder import driven through the real dialogs; a Hold button on toasts.
- **Fixes found while checking:** the updater now retries soon after a failed or rate-limited check
  (was a whole interval); the Records page uses its own translated words for GitHub errors; the
  sound playback check clears schedule rules (it failed in daytime runs only); the regex help and
  editable-text checks no longer depend on timing.
- **Last full self-tests:** 365 of 365 on the archive merge; 365 of 367 after the locks merge (one
  language mode switch check and one Minecraft item editor check failed in the full order but pass
  on their own: 59 of 59 and 1 of 1; cause not yet found). A second full run of that build passed
  367 of 367, so both are intermittent; cause not found. The Hold button: toasts group 22 of 22 and the held capture inspected. RAR: document dialog
  group 30 of 30.
- **Installed copy:** still 1.0.40 on the maintainer's computer at the time of writing; it checks
  every 6 hours in that version, and its one check today was refused by GitHub's rate limit.
- **Open:** the intermittent full-run failures above; only ISO 9660 and Joliet images list
  without mounting; colour picker, context menu and drawing rows need a physical mouse or pen;
  Bedrock has no publisher checksum and its Docker image is a community image.
- **Virtual disk, later the same day:** the first real run (live check `MT_LIVE_ONLY=vhd`, consent
  prompt approved by the maintainer) found that diskpart rejected the helper's UTF-16 scripts
  (help text, exit 0x80070057), so no real disk could ever be made. Fixed by writing scripts in the
  console code page; the live check then created, formatted NTFS, mounted, filled from the
  unelevated app, verified, detached and deleted a 64 MB VHDX. Archive and disk checks 31 of 31.
- **Clean-up:** every merged task branch and worktree was removed after an archive of the
  repository was written and verified; only `main` remains locally and on GitHub.

## State on 2026-11-04 (evening)

- **Checked:** full self-test 180 of 180 on `main` at 823d9a8; releases v1.0.17 to v1.0.36 all green
  and published as normal releases; v1.0.36 installed on the maintainer's computer and running.
- **Background updates:** the installed copy checks 90 s after start and every 6 h, downloads,
  verifies the SHA-256 digest and the file's own version, and installs at a quiet moment. Seen working
  in the real app: v1.0.31 found, downloaded and verified v1.0.32 in 10 s. A game left open with no
  input for 10 minutes no longer blocks it (fixed in v1.0.36). **Seen:** the swap itself
  completing on its own (v1.0.36 to v1.0.37 at 23:23, then v1.0.40 at 23:25, back in seconds).
- **Game notices:** "Roblox detected" and "Roblox closed", checked by the self-test.
- **Minecraft:** bulk player actions on a searchable multi-select list; SSH players, plugins,
  backups and worlds; Bedrock on Docker and over SSH; item and NBT editors; live plugin commands;
  peak history. The pre-generation stall's cause is proven (an empty 1.21.2+ server pauses after
  60 s) and fixed with `pause-when-empty-seconds=0`.
- **Since then, done:** SSH world import, reset and replace from this app (self-test and a live
  OpenSSH run), and Forge and NeoForge live runs through the command builder (both passed).
  The history retention self-test check failed in 3 of about 9 full runs before history names were
  compared at millisecond precision, and in 0 of 4 full runs since (184 of 184 each); not proven fixed, and it lists every kept version if it fails again.
- **Done:** register a TOTP account when a copied image holds an `otpauth://` QR
  code (see ROADMAP.md).
- **Needs the maintainer:** native review of the Cantonese, a FIDO2 key, a real pen or touch screen,
  a real phone camera, a real Windows Hello prompt, and approval to download the `glab` and `tea`
  command-line tools for the forge checks.

## Motion and page redesign (2026-11-04)

- Design canvas and its handoff: [design/motion-and-pages](design/motion-and-pages/HANDOFF.md).
- Shared motion helpers in `UI/Ui.cs`; shared-axis page changes in `ControlCentreWindow.Navigate`.
- Redesigned Ollama, Minecraft servers and Conversion pages; Conversion is now a searchable list of 39 formats.
- **Checked:** compiles with `EnableWindowsTargeting` on Linux (0 errors). **Not checked:** running the app, and the
  new FFmpeg encoders against the bundled build. The first Windows build and its screenshots are the real test.

## Release pipeline additions (written, not yet run on the build machine)

- `scripts/line-counts.mjs` counts lines per category and language at a commit, with surviving-line
  authorship from `git blame`. Run locally: it printed 36,460 project lines at commit `e8933df`, and
  its blame total matched. Runtime on this machine: about 45 seconds.
- `scripts/release-notes.mjs` builds the release notes: dim sum code name with a linked public photo
  (only if a HEAD request confirms the photo is published), workflow start, end and duration, the
  checks that actually ran, the line-count table and the human-time estimate with its arithmetic.
  It also refreshes the README figures block. Run locally with sample timing: output looked correct.
- `scripts/dim-sum-code-names.json` lists 120 dishes (exact English and Traditional Chinese names)
  whose photos were published in the public catalogue when the list was written.
- `scripts/changelog.mjs` regenerates [CHANGELOG.md](CHANGELOG.md) from tags and commits.
- The workflow now publishes with these notes, edits the release once more with the final end time,
  then commits the README figures and changelog back to `main`.

**Not verified:** the workflow changes have been syntax-checked (YAML parse, PowerShell parse of every
step) but have not run on a GitHub runner. The first push to `main` is the real test. Watch the
"Publish release" and "Record release figures" steps.

## Remaining work

- Confirm the first workflow run publishes notes with timing, counts, code name and estimate, and
  that the README figures commit lands.
- Add a `LICENSE` file once the owner picks a licence (the repository has none).
- Items still open in [ROADMAP.md](ROADMAP.md).

## Blockers and external state

- GitHub Pages must be enabled once in repository settings (Source: GitHub Actions) for the `pages`
  job; that job is `continue-on-error`.
- No handoff section has been posted on a GitHub issue by this change; do that when the work is
  merged.
