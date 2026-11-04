# Overview

The first section of a server's workspace.

![Overview](../../screenshots/mc-overview.png)

## Live tiles

| Tile | Where the value comes from |
| --- | --- |
| Players | The server's own answer to the `list` command, asked every 20 seconds while it runs |
| Ticks per second | Paper and Purpur answer the `tps` command; other software does not report it, and the tile says so |
| Memory | The Java process's working set on this computer, `docker stats` for a container, or `ps` on an SSH host, against the memory allowance |
| Uptime | The process start time, the container's start time, or the elapsed time `ps` reports on the host |
| Port | Whether it is open, free, in use by another program, or shared with another server here |

A value that is not known is shown as "–" or "Unavailable" with the reason underneath, never as an estimate.

## Recent console and players online

The last six console lines, with a link to the [console](console.md), and the players the last `list` answer named,
each marked as operator or whitelisted from the server's files and with a menu for kick, ban and game mode.

## Needs your attention

- **Host offline**, with Reconnect.
- **The last start failed**, with the reason.
- **Port in use** by another program on this computer, for a stopped server.
- **A newer build** of Paper, Purpur, Fabric, Forge or NeoForge, compared with the build the server was created with.
  It is checked once per session, and by an update-check schedule. The suggestion is to take a backup first.
- **Last backup** age and the next scheduled backup, or that there is no backup yet.

## Verification

Capture `mc-overview` uses the sample servers of the capture run; its players, memory and uptime are sample values.
