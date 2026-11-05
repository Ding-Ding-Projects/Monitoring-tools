# Roadmap

What is built, what is in progress, and what the maintainer's shared app requirements still ask
for. This is a checklist: `- [x]` means shipped in a release, `- [ ]` means not finished. An item is
ticked only when it is implemented and in a release; nothing is deleted to make the list look
finished (a dropped item would be struck through or moved to the last section with its reason).

## Shipped

- [x] Clipboard toasts with tailored actions, rich file previews and drag-out
- [x] Quick commands (`:mp4`, `:gif`, `:push`, folder batch conversion, custom script shortcuts)
- [x] Internet watch with probe ladder, release and renew, DNS profiles
- [x] Public IP watch with a git-backed history (first and last seen)
- [x] Game mode, file tools (move, copy, Recycle Bin delete, Base64, AES-256-GCM encryption)
- [x] Control centre with live latency, outages, activity, command playground, update check
- [x] Bundled FFmpeg, FFprobe and portable Git, pinned and digest-verified
- [x] First-run setup wizard (real setup actions, Back/Next, save and resume, optional steps)
- [x] `Ctrl+Shift+F` command palette in the app and on the project site
- [x] Action sound effects (Windows sounds) with an off switch and volume, quiet during quiet time and games
- [x] Personal wording file: Settings upload and drop on any window, validated in full, with a celebration
- [x] Portable desktop companion of the project site and docs (`MonitoringTools-Site.exe`)
- [x] Portable desktop build tool: Build, Build and run, Build installer over the batch entrypoints (`MonitoringTools-BuildTool.exe`)
- [x] `build.bat`, `build-installer.bat`, `download-dependencies.bat`/`.sh`, CI screenshots and releases
- [x] Minecraft servers, first slice: Java edition from the official version catalogue (releases, snapshots, beta, alpha), SHA-1 verified server jars, a verified Temurin runtime, explicit EULA consent, four targets (this computer, Docker here, SSH host, Docker over SSH) with host-key approval, start, stop and restart, console, a typed command builder, server.properties controls, plugin and mod install

- [x] A search bar on every control-centre page, the palette, the tray menu and the Minecraft version picker, each with its own anchored regex builder (Build, Test, Replace, Code, Library) on the .NET engine
- [x] Running version and its build time (local time with seconds and time zone) on the control centre's front screen

- [x] Material 3 throughout: colour roles from five seeds, light, dark and system themes, Roboto, pill shapes, spring motion, reduce motion
- [x] Language modes (English, Cantonese, bilingual), two humour sliders, emoji switch and narrator
- [x] Modes: School mode, ADHD helpers, dim sum surprise, scheduled and file-driven settings
- [x] Records: notification centre, settings history with restore, exports in five formats, changelog viewer
- [x] Offline Help and docs page, right-click menus on every element, two-key destructive confirmation, bulk actions in history

- [x] Browser-style tabs, a per-element appearance editor and colour picker, info icons and a guided new-shortcut form
- [x] Element locks, an unlock ladder, Support Tickets and a TOTP authenticator
- [x] A local AI models (Ollama) manager, a Minecraft world generator, drawing, publishing to GitHub and editor hand-off

## In progress

- [ ] Minecraft servers: Bedrock edition, a world generator with seed preview, and a recorded acceptance check per target on real hardware
- [x] Register a TOTP account when a copied image holds an `otpauth://` QR code: "Register for codes" on the image toast adds it to the built-in authenticator (encrypted for this Windows account, codes on the Safety page); checked by the self-test
- [ ] SSH world import, reset and replace from the app, and Forge and NeoForge live runs (on branch `worktree-agent-aa24b41c13dfee312`, unfinished)

## Still to build from the shared app requirements

- [ ] Regex builder parity: flavour comparison, a step debugger, benchmarks, file and folder search, test cases, editable code templates (see [the parity ledger](docs/features/search/regexbuddy-parity.md))
- [ ] Settings matches from other pages listed inline under a page search (today: Search every page opens the palette)
- [ ] Deeper Ollama, world generator and drawing features (see their docs), QR pairing, a passkey route
- [ ] A native-speaker review of the Cantonese copy
- [ ] Resizable, draggable panels with persisted size and position
- [ ] Right-click menus that show working keyboard shortcuts

## Release pipeline and repository records (written, first workflow run pending)

- [ ] Release notes with workflow start, end and duration measured by the workflow (`scripts/release-notes.mjs`)
- [ ] Line counts per category and language, with surviving-line authorship, counted by the workflow (`scripts/line-counts.mjs`)
- [ ] Dim sum code name per release from the public catalogue, with a linked public photo when it is confirmed published
- [ ] README figures and human-time estimate refreshed from the release that produced them
- [ ] README organised into collapsible sections with the site and download links first
- [ ] `ROADMAP.md` checklist, `HANDOFF.md`, `CLOSEOUT_PROMPT.md`, `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`
- [ ] A `LICENSE` file (needs the owner's choice of licence; not added by the pipeline work)

## Deliberately not doing

- Nothing has been dropped so far.
