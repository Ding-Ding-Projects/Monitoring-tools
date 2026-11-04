# Minecraft servers

Create and run Java edition servers on this computer, in Docker, or on another machine over SSH.

![The server manager](../../screenshots/mc-overview.png)

## The server list

The left side lists every server this app created. Each row shows a state dot, the name, the software and version,
the players online (or the state), and a badge for where it runs: **This PC**, **Docker**, **SSH** or **SSH Docker**.

- **New server** opens the [guided create flow](create.md).
- **Search servers** is the app's shared search field: plain words by default, `.*` for a regular expression with the
  regex builder, `Aa` for matching case. It searches the name, version, software, target and SSH host.
- The filter chips **All**, **Running**, **Stopped** and **Offline host** show how many servers each one holds.

Removing a server from the list never deletes its world, settings or backups.

## The header

The header shows the server's name, a state badge and a detail line with the software, version, build, where it runs and
how long it has been up. The actions change with the state:

| State | Meaning | Actions |
| --- | --- | --- |
| Running | The process or container is up | Stop, Restart |
| Starting | Started from here and not yet "Done" | Stop, Restart |
| Stopped | The host answered and the server is not running | Start, Restart |
| Host offline | Docker is not running here, or the SSH host could not be reached | Reconnect |
| Missing | The container is not there any more | Start (reports why it cannot) |

A host that cannot be reached is never shown as stopped: SSH reports a failed connection with exit code 255, and only an
answer from the host itself can say the server is stopped. **Copy join address** copies `localhost:port`, or the SSH
host's name and port. **Open folder** opens the server folder for servers on this computer.

## Where it can run

| Target | What it means | Software |
| --- | --- | --- |
| This computer | A Java process started by the app | Vanilla, Paper, Purpur |
| Docker on this computer | The `itzg/minecraft-server` image with the server folder as its volume | All six |
| Another computer over SSH | A Java process on the host, signed in with your SSH key | Vanilla |
| Docker over SSH | The same image on the host | All six |

Fabric, Forge and NeoForge need their own installers, so they run on the Docker targets. For SSH targets, **Check host
key** shows the machine's key first so you can confirm you are connecting to the right one; it is stored in the app's own
`known_hosts`, and connections use key authentication only.

## The sections

[Overview](overview.md), [Console](console.md), [Players](players.md), [Settings](settings.md), [Worlds](worlds.md),
[Plugins or Mods](plugins.md), [Backups](backups.md) and [Schedules](schedules.md). The tab bar is reachable with Tab and
the arrow keys. At narrow widths the list stacks above the workspace. Sections slide in with the app's shared motion,
which is skipped under reduced motion and low stimulation.

## Stored data

`servers.json` in the app's data folder keeps the list. Fields added with the server manager (build, backup folder, how
many backups to keep, schedules, recent commands) have defaults, so a list from an older version loads unchanged and gains
them the next time it is saved. A list that cannot be read is copied aside rather than overwritten.

## Failure modes

- A server that cannot start shows the error in the header and as "The last start failed" on the overview.
- A port already in use on this computer is refused before the server starts.
- The EULA must be accepted before a server will run.

## Verification

Self-test checks drive the list filter and regex search, the Host offline state of an unreachable SSH host, and an older
`servers.json`. The [live check](../tools/live-check.md) creates a real Vanilla 1.21.4 server in a throwaway folder,
starts it, asks it `list` and stops it.

## Related

- [Docker actions](../files/docker-actions.md)
- [Game mode](../game/game-mode.md)
