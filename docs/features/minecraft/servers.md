# Minecraft servers

Create and run Java edition servers on this computer, in Docker, or on another machine over SSH.

![The Minecraft page](../../screenshots/control-minecraft.png)

## Create a server

Open **Minecraft servers** in the control centre. The **New server** card is a four-step guide with Back, Next and Cancel; the step you are on is kept if the page redraws, and Cancel discards the draft.

1. **Name and version.** Name the server and pick a version. The list comes straight from Mojang's official catalogue; choose the type and search the version picker. A version without a dedicated server is reported here.
2. **Where it runs.** Pick the target (see below). The Docker targets also offer the flavour: Vanilla, Paper or Fabric. SSH targets ask for the host and optionally a folder, with **Check host key**.
3. **Memory and port.** Choose the memory and a port from 1024 to 65535. A port already used by another server on the same target is refused.
4. **Review and accept the EULA.** Read the summary, accept the EULA and press **Create server**. Nothing is downloaded until then; progress and any failure are shown under the step.

## Where it can run

| Target | What it means |
| --- | --- |
| Local process | Runs with Java on this computer |
| Local Docker | Runs in a container on this computer |
| SSH process | Runs on another machine over SSH |
| SSH Docker | Runs in a container on another machine over SSH |

For SSH targets, **Check host key** shows the machine's key first so you can confirm you are connecting to the right one.

## Manage it

Each server card shows an icon tile, the name, chips for version, flavour, where it runs and the port, and a
status pill. While the server runs, the pill and tile turn mint and the dot breathes; a stopped server stays still,
and an unreachable or missing one turns amber. The page header counts the servers and how many are running.

Actions: Start, Stop, Restart, Console, Settings (local targets), Add plugin or mod, Open folder (local targets) and Remove.

- **Console** opens a terminal panel under the actions: the live log, then the command builder. Pick a known
  command from the chips (the prompt shows it), type its arguments and press Send. Commands are sent to the server,
  never to a shell.
- **Settings** opens a drawer with the `server.properties` controls (switches, choices and numbers). Changes are
  saved at once and applied on the next start. The drawer is closed by default so the card stays short.

With no servers yet, the page shows a short empty state above the New server guide.

## Failure modes

- A server that cannot start shows the error it printed.
- The EULA must be accepted before a server will run.

## Related

- [Docker actions](../files/docker-actions.md)
- [Game mode](../game/game-mode.md)
