# Minecraft server manager remake: design handoff

The current Minecraft page stacks a world generator, a server list and every form on one long scroll, so it is hard to
tell which server a control acts on. This design turns it into a working manager: a server list on the left, and the
selected server's workspace on the right with its own sections.

The artboards (`*.dc.html`) are interactive design references, not shipped code. Implement them natively in WPF with the
app's existing Material 3 theme roles (`B.*` brushes), Roboto, the shared `SearchField` with its regex builder, the
element menus, the two-key confirmation and the guided form.

## Layout

- **Server list pane** (Main): New server (opens the guided create flow), a search field with the regex builder, filter
  chips (All, Running, Stopped, Offline host) with counts, one row per server with a state dot, name, version and
  software, player count, and a target badge (This PC, Docker, SSH, SSH Docker). A note offers to adopt servers found on
  the computer; discovery is read-only until the person adopts one.
- **Workspace header**: server name, state badge (Running, Stopped, Starting, Host offline), a detail line (software,
  version, build, Java, uptime, world), Copy join address, Restart, and one main action that changes with the state
  (Start, Stop, Reconnect). A host that cannot be reached is shown as Host offline, never as Stopped.
- **Sections** (a tab bar, keyboard reachable): Overview, Console, Players, Settings, Worlds, Plugins, Backups,
  Schedules.

## Sections

1. **Overview**: live tiles (players x of max, ticks per second, memory used of the allowance, uptime, port), the last console
   lines with a link to the console, players online with a menu each, and a "Needs your attention" list (available
   updates with backup-first and rollback, last backup age, failed starts, port conflicts).
2. **Console**: the live log with level filter chips (Info, Warnings, Chat, Commands), follow-tail, a log search with the
   regex builder, and a typed command builder: pick a command from the server's own command list for its version, fill
   its arguments with proper controls, see the exact command line it will send, Run. No free shell field. Recent commands
   as chips.
3. **Players**: everyone known (online, whitelisted, operators, banned) in a table with Operator and Whitelist switches,
   status, and a menu (kick, ban, pardon, teleport, game mode). Add a player by name. Bans and operator changes use the
   two-key confirmation. Changes apply live through the console when the server runs, and through the JSON files when it
   is stopped.
4. **Settings**: groups (Gameplay, World, Players and access, Network, Performance, Game rules, Management, Other keys)
   with a count each and a search with the regex builder. Every row shows the label, what it does, the real key and its
   default, a badge for when it applies (Applies now, Needs restart, New worlds only, Not on this version), and the right
   control: a switch for true/false, a typed number field with a stepper, unit and the version's real bounds, a list for
   enumerations. Unknown keys are kept exactly as written. An unsaved-changes bar offers Discard, Save (apply at next
   start) and Save and restart.
5. **Worlds**: each dimension with its size, seed and state; Import a world (folder or ZIP, copied and checked), Download
   as ZIP, Reset or Replace (stop, back up first, two-key confirmation), Generate a new world (the existing world
   generator, moved here), and Pre-generate chunks with a typed radius, progress, pause and cancel.
6. **Plugins** (or Mods for Fabric, Forge, NeoForge): installed list with version, compatibility badge (Compatible,
   Update, May not work), an enable switch and a menu; Install from a file; Update all; a catalogue search (Modrinth and
   Hangar) filtered to the server's version and software; downloads checked against the catalogue hash; the plugins
   folder is backed up so an install can be rolled back.
7. **Backups**: list with time, reason (scheduled, before an update, by hand with a note), size and contents; Back up now;
   Restore (stops the server, keeps the current world as one more backup, then swaps; two-key confirmation); how many to
   keep (typed number); destination folder. A backup uses save-off, copy, save-on.
8. **Schedules**: restart (with a chat warning), backup, message and update-check schedules with the next run and an on
   switch; a guided "New schedule" form; missed runs happen once on wake.

## Create flow (Create)

A six-step guided form with a step rail that shows completed answers, a progress bar, Back and Next, and a draft that is
kept: Edition and version (Java or Bedrock; the full Mojang catalogue, searchable, with snapshot and old alpha/beta
toggles and the Java each version needs), Server software (only software with a build for that version: Vanilla, Paper,
Purpur, Fabric, Forge, NeoForge), Where it runs (This computer, Docker on this computer, Another computer over SSH,
Docker over SSH, each with a live readiness check), Name and resources (typed numbers for memory, port and maximum
players, with a free-port and memory check), World (new, existing folder or ZIP, or from a backup; seed), Review with
the EULA consent.

## Rules carried over

- Numbers are typed fields with steppers and real bounds, never a row of preset chips.
- Every control explains itself and its default; every label has Cantonese; accessible names on icon buttons.
- Motion: sections and steps slide in, selected rows morph their corner radius; all of it is skipped under reduced
  motion and low stimulation.
- Narrow widths: the list pane stacks above the workspace.
- Nothing destructive happens without the two-key confirmation, and nothing outside the app's own servers is ever
  stopped.

## Command builder (Commands)

The console's chip-only builder is replaced by a real form, opened from the Console section (and from a Commands
button in the header). Two modes:

- **Tasks**: a searchable gallery grouped by what people want to do (Players: give an item, teleport, give an effect,
  change game mode, kick; World: set the time, change the weather, summon a mob; Rules: set a game rule; more from the
  same pattern). Each task is a form with the controls its arguments need:
  - **Who**: online players as chips (from `list`), plus the target selectors `@p @a @r @s` with a plain explanation of
    each; a selector builder for `@a[distance=..10]` style filters is a follow-up.
  - **Item, block, effect, enchantment, entity**: searchable lists from the selected version's own registries
    (the jar's `reports/registries.json`), showing the readable name and the id.
  - **Numbers**: typed number fields with the argument's real bounds (from `commands.json` parser properties).
  - **Positions**: three axes, each with a `~` relative toggle, plus "use a player's position" (from `data get entity`).
  - **Enumerations and booleans**: chips or a switch.
- **Every command**: walks the server's command tree node by node; each argument type gets the matching control
  (`brigadier:integer`/`double` with min and max, `brigadier:bool`, `minecraft:entity`/`game_profile`,
  `minecraft:block_pos`/`vec3`, `minecraft:item_stack`, `minecraft:block_state`, `minecraft:resource`/`resource_key`
  with its registry, `brigadier:string` and `minecraft:message`).

Below the form, the command preview colours each part by kind (command, target, value, number); clicking a part focuses
its field. A status line says whether the command is complete and valid for the server's version. Actions: Run on this
server, Copy, Save as a favourite (favourites list per server), Schedule it (opens a schedule with this command).
