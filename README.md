# Monitoring Tools

**[Project site and screenshots](https://ding-ding-projects.github.io/Monitoring-tools/)** · **[Download](https://github.com/Ding-Ding-Projects/Monitoring-tools/releases/latest/download/MonitoringTools-Setup.exe)**

A small Windows background service with a tray icon and a modern, animated UI. It watches your clipboard and your internet connection, and turns copied paths into one-step commands.

![Clipboard toast](docs/screenshots/toast-link.png)
![Control centre](docs/screenshots/control-overview.png)

## Features

**Clipboard toasts**
- A toast slides into the bottom-right corner for every copy, stays for 3 seconds and holds while you hover. It shows four actions up front and keeps the rest behind **More**.
- Hovering reveals actions tailored to what you copied:
  - **Text and Markdown**: rendered Markdown preview, copy as plain text, case changes, tidy spaces, join lines, search, translate, save as a file.
  - **Links**: open, open in a private window, copy domain, strip tracking parameters, copy as a Markdown link.
  - **Files and folders**: rich previews (video frames, PDF and document thumbnails, album art, the first lines of text and code, rendered Markdown, CSV tables, folder and zip listings, plus duration, resolution and codecs for media). **Open in** the file's default app, **Open with…**, show in folder, copy name, forward slashes, terminal here. Paths copied with quotes (Explorer "Copy as path") offer **Remove quotes**; unquoted ones offer **Add quotes**.
  - **Several files at once**: move to, copy to, delete to the Recycle Bin, and batch conversion chips.
  - **File tools sheet**: one tab each for move and copy, batch Base64 encode and decode (plus copy as Base64 text or a data URI), and password encryption (AES-256-GCM, `.mtenc` files next to the originals).
  - **Base64 text** on the clipboard offers decode; any text offers encode.
  - **Images, video and audio**: save, convert and quick command chips such as `:mp4`, `:gif`, `:mp3`, `:webp`. Drag the preview out of the toast into any drop target.
  - **Docker**: a copied `compose.yaml` / `docker-compose.yml` offers up, down, pull, restart and logs; a `Dockerfile` offers build and build-and-run. Each button explains what it will do.
  - **JSON, colors, email addresses** get their own previews and actions.
- Copies that password managers mark as private are skipped.
- History of the last copies, kept in memory only.

**Quick commands**

Copy a path with a command on the end and it runs as soon as it lands on the clipboard:

```
D:\Footage\clip.mov:mp4        converts next to the original, same name
"C:\Music\take 3.wav":mp3      quotes are fine
C:\Code\my-repo:push            stages, commits with your default message and pushes
D:\Footage:mp4                  converts every compatible file in the folder
```

Formats: `mp4 mov mkv webm avi gif mp3 wav flac m4a ogg opus png jpg webp bmp tiff avif ico`. Existing files are never overwritten. Conversion uses FFmpeg; if it is missing, the toast offers to install it with winget.

The first `:push` asks for a default commit message, with snippets such as `{date}`, `{time}`, `{branch}`, `{repo}` and `{files}`.

**Custom shortcuts**

Create your own `:name` commands in the control centre. Each one is a batch or PowerShell script, edited in the built-in script editor, and receives whatever came before the colon as its first argument (also in `MT_INPUT`).

**Search and regex builder**
- Every page, list, the command palette, the tray menu and the Minecraft version picker has its own search bar. Plain text by default; `.*` switches to regular expressions and `Aa` matches case. `Ctrl+F` jumps to the page's search.
- **Builder** opens a regex builder attached to that bar: a token palette, all .NET options, a token-by-token explanation, a backtracking warning, a Test view with matches and capture groups, Replace and Split previews, generated C#, PowerShell and Python, and a saved library. It runs the real .NET engine with a 200 ms limit, locally. Details and the list of what is not built yet: [docs/features/search](docs/features/search/README.md).

![Regex builder](docs/screenshots/search-builder-build.png)

**Internet watch**
- When the connection drops, an always-on-top dialog explains where it broke (adapter, IP address, gateway, DNS, upstream, captive portal) with live details, and stays until the connection returns or you dismiss it.
- One-press **Release & renew** (`ipconfig /release`, `/renew`, `/flushdns`), DNS flush, sign-in page for captive portals, copyable report.
- **DNS profiles**: switch between Automatic, Google, Cloudflare, Quad9, OpenDNS or your own saved servers from the dialog, the tray menu or the control centre.

**Game mode**

On by default. Popups are held back while Roblox, Minecraft, League of Legends, Valorant, Fortnite, CS2 and other listed games run, or while any app is fullscreen. The list is editable.

**Service and tray**
- Installs itself per user, starts at sign-in and runs under a supervisor that restarts it after a crash.
- Tray icon with a status dot: mint online, amber offline, violet game mode, grey paused.
- Right-click menu: control centre, history, shortcuts, connection test, toggles, snooze, DNS, administrator rights, restart, logs, **uninstall**, quit.
- **Administrator rights** are off by default. Turning them on asks once through UAC, then the service starts elevated at every sign-in until you turn it off.

## Install

Every push to `main` builds on Windows and publishes the next release automatically. Download `MonitoringTools-Setup.exe` from the latest release and run it. FFmpeg, FFprobe and portable Git are bundled inside, so nothing else needs installing and conversion works offline. It copies itself to `%LOCALAPPDATA%\Programs\MonitoringTools`, registers start-up and appears in the tray. Settings, logs and scripts live in `%LOCALAPPDATA%\MonitoringTools`.

To remove it, right-click the tray icon and choose **Uninstall service**.

## Control centre

Overview with live latency, uptime and outage history; an activity feed of conversions, pushes, shortcuts and file jobs; clipboard history; a command playground that explains what `path:format` will do before it runs; settings for every feature; and an about page with an update check. A welcome tour with **Try it** buttons opens on first run and from the tray menu.

## Screenshots

Every build renders the real app on a Windows runner (`MonitoringTools.exe --screenshots <folder>`), attaches the images to the release and publishes them on the project site.

## Build

```
download-dependencies.bat      fetch pinned, digest-verified dependencies into .deps\ (also installs a .NET 8 SDK if missing)
build.bat [version]            build out\MonitoringTools.exe with every dependency bundled
build-installer.bat [version]  build dist\MonitoringTools-Setup.exe and a zip
```

Prefer a window? `MonitoringTools-BuildTool.exe` (in every release, source in `apps/build-tool`) runs the same entrypoints with Build, Build and run, and Build installer, showing each phase, the live log, the real exit code and a cancel button. `MonitoringTools-Site.exe` (source in `apps/site-companion`) is this project's site and documentation as an offline desktop app.

Dependencies are pinned in `deps/manifest.json` with SHA-256 digests. On Linux or macOS, `download-dependencies.sh` fetches the same files for cross-building.

## Public IP history

When the public IP address changes, a dialog shows the old and new address, and the change is committed to a local git repository (`%LOCALAPPDATA%\MonitoringTools\ip-history`) as `ip-history.md` and `history.json`, with first seen and last seen times for every address. Set a remote in the control centre to push the history automatically. Lookups use ipinfo.io, with api.ipify.org as a fallback.

## Licenses of bundled tools

FFmpeg (GPL build from BtbN/FFmpeg-Builds, includes libx264) and Git for Windows MinGit (GPL-2.0) ship inside the app unmodified; their sources are available from those projects.

The design for every surface lives in the project's design canvas; the app implements the same "Graphite Signal" look.
