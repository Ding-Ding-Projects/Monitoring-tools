# Players

![Players](../../screenshots/mc-players.png)

Everyone the server knows: the names in `ops.json`, `whitelist.json` and `banned-players.json`, and the players the last
`list` answer named. Chips filter **Everyone**, **Online**, **Operators** and **Banned**, with counts.

Each player has an **Operator** switch, a **Whitelist** switch, a status line (online, offline, or banned with the reason),
and a menu with **Kick**, **Ban** or **Pardon**, and the game modes while they are online. **Add a player by name** checks
the name (1 to 16 letters, digits or underscores) and offers **Whitelist** and **Make operator**.

## How changes apply

- **While the server runs**, changes go through its console (`op`, `deop`, `whitelist add`, `whitelist remove`, `ban`,
  `pardon`, `kick`), so the server applies them and saves its own files.
- **While it is stopped**, the JSON files are edited directly. Every entry the app does not change is kept, a player
  keeps the UUID the server already recorded for them (from the other files or `usercache.json`), and a new ban is written
  in the server's own format.
- **SSH servers** keep their files on the host, so while stopped they can only be changed by starting the server.

Making someone an operator, removing an operator and banning ask for the app's two-key confirmation. A kick is only
possible while the server runs.

## Verification

A self-test check writes and reads the three files in a temporary folder: whitelist and op with a known UUID, a ban with
its reason, then pardon, deop and whitelist remove, an invalid name and a kick while stopped are refused, and the Players
section of a sample server lists its five players with their switches. Capture `mc-players`.
