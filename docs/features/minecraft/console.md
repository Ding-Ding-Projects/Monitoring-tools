# Console

![Console](../../screenshots/mc-console.png)

## The log

The live log, with each line split into its time, level and message. Chips show or hide **Info**, **Warnings**,
**Chat** and **Commands**; **Follow** keeps the newest line in view. The log search is the shared search field with the
regex builder. For a process on this computer the lines arrive as the server prints them; for Docker and SSH servers the
app reads the newest 400 lines every few seconds. The last 2,000 lines are kept.

## Run a command

There is no free shell field. The command builder uses the server's own command tree, the `reports/commands.json` that
the version's jar writes when asked (the world generator reads the same file). If it has not been read yet, **Read this
version's command list** downloads the official jar once and asks it.

1. Find a command: the shared search field filters the top-level commands.
2. Each step offers what can come next: the literal words the command takes, or an argument.
3. Arguments get a control for their type: a number field with the jar's own minimum and maximum, true or false, or a
   text field with a hint such as "a player name or a selector such as @a". Online players are offered for player
   arguments.
4. The exact line it will send is shown, and it is checked before **Run**: missing arguments, numbers out of range,
   and commands that need more are refused with the reason.

The last eight commands are kept per server as **Recent** chips that run again. For Paper, Purpur and modded servers
the list holds the game's own commands; commands added by plugins or mods are not in it, and the page says so.

## Verification

A self-test check parses a sample `commands.json`, builds `/time set 1000` and `/give Ava minecraft:diamond 5`,
refuses a count below the jar's minimum, and drives the builder in the Console section to show `/time set day`.
Capture `mc-console`.
