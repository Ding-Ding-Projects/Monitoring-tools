# Monitoring Tools

A small Windows background service with a tray icon and a modern, animated UI. It watches your clipboard and your internet connection, and turns copied paths into one-step commands.

## Features

**Clipboard toasts**
- A toast slides into the bottom-right corner for every copy, stays for 3 seconds and holds while you hover.
- Hovering reveals actions tailored to what you copied:
  - **Text and Markdown**: rendered Markdown preview, copy as plain text, case changes, tidy spaces, join lines, search, translate, save as a file.
  - **Links**: open, open in a private window, copy domain, strip tracking parameters, copy as a Markdown link.
  - **Files and folders**: open, show in folder, copy name, forward slashes, terminal here. Paths copied with quotes (Explorer "Copy as path") offer **Remove quotes**; unquoted ones offer **Add quotes**.
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
```

Formats: `mp4 mov mkv webm avi gif mp3 wav flac m4a ogg opus png jpg webp bmp tiff avif ico`. Existing files are never overwritten. Conversion uses FFmpeg; if it is missing, the toast offers to install it with winget.

The first `:push` asks for a default commit message, with snippets such as `{date}`, `{time}`, `{branch}`, `{repo}` and `{files}`.

**Custom shortcuts**

Create your own `:name` commands in the control centre. Each one is a batch or PowerShell script, edited in the built-in script editor, and receives whatever came before the colon as its first argument (also in `MT_INPUT`).

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

Download `MonitoringTools.exe` from the latest release and run it. It copies itself to `%LOCALAPPDATA%\Programs\MonitoringTools`, registers start-up and appears in the tray. Settings, logs and scripts live in `%LOCALAPPDATA%\MonitoringTools`.

To remove it, right-click the tray icon and choose **Uninstall service**.

## Build

Requires the .NET 8 SDK.

```
dotnet publish src/MonitoringTools/MonitoringTools.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The design for every surface lives in the project's design canvas; the app implements the same "Graphite Signal" look.
