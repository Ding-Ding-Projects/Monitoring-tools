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

## In progress

- Minecraft servers: Bedrock edition, a world generator with seed preview, and a recorded acceptance check per target on real hardware

## Still to build from the shared app requirements

- Language modes: English, Hong Kong Cantonese and bilingual, persisted
- Independent humour-level sliders (1 to 5) for English and Cantonese copy
- A shared, renamable School mode across the maintainer's apps, with its unlock credential
- Show-emoji-in-dialogs toggle
- Regex builder parity: flavour comparison, a step debugger, benchmarks, file and folder search, test cases, editable code templates (see [the parity ledger](docs/features/search/regexbuddy-parity.md))
- Settings matches from other pages listed inline under a page search (today: Search every page opens the palette)
- Right-click menus with their own filter field
- Resizable, draggable panels with persisted size and position
- Right-click menus that show working keyboard shortcuts
- Localized wizard and palette copy
