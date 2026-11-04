# Roadmap

What is built, what is in progress, and what the maintainer's shared app requirements still ask
for. Items move up as they ship; nothing here is claimed as done until it is in a release.

## Shipped

- Clipboard toasts with tailored actions, rich file previews and drag-out
- Quick commands (`:mp4`, `:gif`, `:push`, folder batch conversion, custom script shortcuts)
- Internet watch with probe ladder, release and renew, DNS profiles
- Public IP watch with a git-backed history (first and last seen)
- Game mode, file tools (move, copy, Recycle Bin delete, Base64, AES-256-GCM encryption)
- Control centre with live latency, outages, activity, command playground, update check
- Bundled FFmpeg, FFprobe and portable Git, pinned and digest-verified
- First-run setup wizard (real setup actions, Back/Next, save and resume, optional steps)
- `Ctrl+Shift+F` command palette in the app and on the project site
- Action sound effects (Windows sounds) with an off switch and volume, quiet during quiet time and games
- Personal wording file: Settings upload and drop on any window, validated in full, with a celebration
- Portable desktop companion of the project site and docs (`MonitoringTools-Site.exe`)
- Portable desktop build tool: Build, Build and run, Build installer over the batch entrypoints (`MonitoringTools-BuildTool.exe`)
- `build.bat`, `build-installer.bat`, `download-dependencies.bat`/`.sh`, CI screenshots and releases
- Minecraft servers, first slice: Java edition from the official version catalogue (releases, snapshots, beta, alpha), SHA-1 verified server jars, a verified Temurin runtime, explicit EULA consent, four targets (this computer, Docker here, SSH host, Docker over SSH) with host-key approval, start, stop and restart, console, a typed command builder, server.properties controls, plugin and mod install

- A search bar on every control-centre page, the palette, the tray menu and the Minecraft version picker, each with its own anchored regex builder (Build, Test, Replace, Code, Library) on the .NET engine
- Running version and its build time (local time with seconds and time zone) on the control centre's front screen

- Material 3 throughout: colour roles from five seeds, light, dark and system themes, Roboto, pill shapes, spring motion, reduce motion
- Language modes (English, Cantonese, bilingual), two humour sliders, emoji switch and narrator
- Modes: School mode, ADHD helpers, dim sum surprise, scheduled and file-driven settings
- Records: notification centre, settings history with restore, exports in five formats, changelog viewer
- Offline Help and docs page, right-click menus on every element, two-key destructive confirmation, bulk actions in history

- Browser-style tabs, a per-element appearance editor and colour picker, info icons and a guided new-shortcut form
- Element locks, an unlock ladder, Support Tickets and a TOTP authenticator
- A local AI models (Ollama) manager, a Minecraft world generator, drawing, publishing to GitHub and editor hand-off

## In progress

- Minecraft servers: Bedrock edition, a world generator with seed preview, and a recorded acceptance check per target on real hardware

## Still to build from the shared app requirements

- Regex builder parity: flavour comparison, a step debugger, benchmarks, file and folder search, test cases, editable code templates (see [the parity ledger](docs/features/search/regexbuddy-parity.md))
- Settings matches from other pages listed inline under a page search (today: Search every page opens the palette)
- Deeper Ollama, world generator and drawing features (see their docs), QR pairing, a passkey route
- A native-speaker review of the Cantonese copy
- Resizable, draggable panels with persisted size and position
- Right-click menus that show working keyboard shortcuts
