# Live check

`MonitoringTools.exe --live-check <folder>` runs the app's own code against real services on the computer and writes
`<folder>/livecheck.json`. The self-test (`--selftest`) proves logic and windows with no network; the live check proves
the parts that only mean something against the real thing. It uses a throwaway data folder, so the real settings are
never read or written.

Each check ends **passed**, **failed** or **skipped**. A check is skipped, never passed, when its service is not present,
and the reason is written beside it.

| Check | What it does |
| --- | --- |
| Narrator, English | Synthesises a sentence with the voice the narrator would pick and saves `voice-english.wav`. |
| Narrator, Cantonese | The same with a Cantonese (zh-HK) voice. Skipped when none is installed. |
| Ollama models | Asks the local server for its version, model list and a model's capabilities. |
| Ollama text attachment | Sends a text file as an attachment and checks the model reads a code word out of it. |
| Ollama image attachment | Sends a red square as an image attachment to a vision model and checks it names the colour. |
| Dim sum | Loads the live public dish catalogue and picks a dish with both names. |
| Forge | Publishes a folder to a test Gitea/Forgejo or GitLab server with a one-time token, then reads the pushed file back through the server's API. |
| Minecraft | Downloads the real server jar and Java, lists game rules, blocks and biomes from the jar's own reports, and writes a noise-settings and dimension-type edit from the jar's default files. |
| Minecraft server | Creates a real Vanilla server with the server manager in the throwaway data folder (the EULA is accepted for that test server only), starts it, waits for "Done", sends `list` and reads the answer, then stops it gracefully. `MT_LIVE_ONLY=minecraft-server` runs only this check. |
| Minecraft command builder | Reads the 1.21.4 jar's command tree, registries and enchantments, starts a throwaway Vanilla server, builds `time set day`, `give @a minecraft:diamond 2` and `gamerule keepInventory true` with the command builder's own task code, sends them through the Run route and checks each reply in the console ("Set the time to 1000", "No player was found", "Gamerule keepInventory is now set to: true"), then stops the server and removes its folder. `MT_LIVE_ONLY=minecraft-commands` runs only this check. |
| Minecraft SSH files | Starts a throwaway OpenSSH container and drives the server file store over real ssh and scp: text with quotes, a dollar sign, a backtick and non-ASCII letters, binary and folder transfers, copy, move, size, backup and delete, players (op, whitelist, ban and a two-player bulk change), plugins (install, switch, remove), a world backed up to this computer and restored with a safety backup, and the Linux Bedrock zip downloaded and unpacked on the host. `MT_LIVE_ONLY=minecraft-ssh-files`. |
| Minecraft Bedrock add-ons and allow list | Creates a real Windows Bedrock server, starts it once so it makes its world, installs an add-on from a .mcaddon and writes an allow-list entry and an operator through the app's file code, starts it again and reads the `Pack Stack` line naming the add-on and the `allowlist list` reply naming the player, then sends the app's own `allowlist add` for a name with a space. `MT_LIVE_ONLY=minecraft-bedrock-files`. |
| Minecraft Bedrock in Docker | Creates, starts, sends a console command to, and stops a real `itzg/minecraft-bedrock-server` container, then removes it. `MT_LIVE_ONLY=minecraft-bedrock-docker`. |
| Minecraft lane (`MT_LIVE_ONLY=minecraft-lane`) | Eight checks, picked with `MT_LIVE_LANE` (`paper,docker,ssh,pregen,updates,fabric,sshdocker,gen`). **Paper with a bot:** a real Paper 1.21.4 server, `tps` through the Overview's poll, the update check against the real Paper API, `help` read into plugin and mod commands, a headless mineflayer bot (npm in a throwaway folder, offline mode) whose position equals the server's `data get entity` reply, an item with seven components given to it and read back from its inventory, and a summon with NBT from the tree editor. **Docker, SSH and SSH Docker targets:** the builder's `time`, `give` (with components) and `gamerule` lines answered. **Updates:** Paper, Purpur, Fabric, Forge and NeoForge build lists and update checks against their real APIs. **Fabric:** a real Fabric server in Docker with Fabric API and the spark mod, `help` read into a `/spark` form. **Pre-generation:** bounded pre-generation with a pause, a resume, restarts of a stalled staging server and the region files counted (`MT_LIVE_RADIUS`, a comma list such as `17,23`, default 16). **Generation:** Nether and End regions, game rules in `level.dat`, a custom superflat ZIP and a Single biome world, each read from the files written. |
| Minecraft Bedrock over SSH | Builds a Debian bookworm container with openssh-server, installs the Linux Bedrock server through the app's SSH route, starts it, reads `list`, backs the running server up to this computer and on the host (list read back from the host), restores the on-host backup with a safety backup, and stops it. `MT_LIVE_ONLY=minecraft-bedrock-ssh`. |
| Minecraft catalogues over SSH | On a throwaway OpenSSH host: Hangar and Modrinth installs with the hash matched after the copy, badges from hashes computed on the host, Update all, and Undo. `MT_LIVE_ONLY=minecraft-ssh-catalogue`. |
| Minecraft bulk actions on a real player | Installs mineflayer in a throwaway folder, starts a Vanilla server with online-mode=false, joins a headless bot, and runs the bulk teleport, kick and ban on it, reading the server log and the bot's own events (the ban is refused again at login; Pardon lets it back in). Needs node and npm. `MT_LIVE_ONLY=minecraft-bulk-bot`. |
| Minecraft catalogues | Searches Modrinth and Hangar for real, installs the newest Hangar file and an older Modrinth file into a throwaway Paper server folder (each hash checked against the catalogue), reads the badges (the older file must be **Update**, the Hangar one **Compatible**) and runs **Update all**. |
| Minecraft Bedrock | Creates a real Bedrock Dedicated Server from Mojang's current Windows link (signature checked), starts it, waits for "Server started.", reads `0 of 7` from `list`, takes a backup while it runs, and stops it. |
| Minecraft discovery, process | Starts a real Java server the way a person would (not through the app) with RCON on, discovers it (command line and folder read from the process), adopts it, sends `list` over RCON and stops it over RCON. |
| Minecraft discovery, container | Starts an `itzg/minecraft-server` container without the app's label, discovers and adopts it, reads its state and `server.properties` through Docker, confirms the app refuses to remove it, then removes it. |
| Minecraft Docker target | Creates a Vanilla server on the local Docker target, starts it, waits for "Done", sends `list`, stops it and removes the container after checking the app's label. |
| Minecraft SSH target | Starts a throwaway `linuxserver/openssh-server` container on 127.0.0.1 with a generated key pair, adds Java 21 to it, approves its host key through the app's own flow, then creates, starts, asks `list` and stops a Vanilla server over SSH. |
| Minecraft SSH Docker target | Starts a throwaway privileged `docker:dind` container (its own Docker engine, separate from this computer's) with an SSH server on 127.0.0.1, approves its host key, then creates, starts, asks `list` and stops a server in Docker over SSH. |

`MT_LIVE_ONLY=minecraft-wave8` runs only the last seven checks. Every container, process and folder they make is removed
afterwards, whether the check passed or not.

## Settings

All optional, as environment variables for that one run:

| Variable | Meaning |
| --- | --- |
| `MT_LIVE_OLLAMA_MODEL` | Model to chat with. Default: the smallest installed model. |
| `MT_LIVE_MC` | Minecraft version. Default `1.21.4`. |
| `MT_LIVE_FORGE_URL`, `MT_LIVE_FORGE_KIND`, `MT_LIVE_FORGE_OWNER`, `MT_LIVE_FORGE_TOKEN` | A disposable test forge server (`gitea` or `gitlab`). The token is used for one push and never written to disk; it is masked in the step log. |

Use only a throwaway forge server and token. A local Gitea in a container works:
`docker run -d -p 127.0.0.1:3300:3000 -e GITEA__security__INSTALL_LOCK=true gitea/gitea:1.22.6`, then create a user and
token with `gitea admin user create` and `gitea admin user generate-access-token`.

## Last run on the maintainer's computer (2026-11-04)

| Check | Result |
| --- | --- |
| Narrator, English | passed (Microsoft David, 0.5 s) |
| Narrator, Cantonese | skipped: no Cantonese voice installed |
| Ollama models | passed (Ollama 0.33.3) |
| Ollama text attachment | passed (gemma4:12b, 43 s) |
| Ollama image attachment | passed (gemma4:12b, 45 s) |
| Dim sum | passed |
| Forge, Gitea | passed (Gitea 1.22.6 in a local container; repository created, commit pushed, README read back) |
| Forge, GitLab | passed (GitLab CE 19.5.3 in a local `gitlab/gitlab-ce` container, a one-time personal access token; repository created, commit pushed, README read back; 4.4 s once the server was up, which took about 20 minutes on the first start) |
| Minecraft | passed (1.21.4 jar: game rules, blocks, biomes, noise and dimension edits) |
| Minecraft server | passed (1.21.4 Vanilla: created, started, `list` answered 0 of 7, stopped; 46 s) |
| Minecraft catalogues | passed (Modrinth 6 hits, Hangar 10 hits; ViaVersion 5.12.2 from Hangar, SHA-256 matched; Chunky 1.4.36 from Modrinth, SHA-512 matched; badges Update to 1.4.40 and Compatible; Update all updated 1; 3.9 s) |
| Minecraft Bedrock | passed (1.26.61.1 installed in 23 s with its signature checked; "Server started." after 15.1 s; `list` 0 of 7; a running backup held `worlds`; 44.6 s) |
| Minecraft discovery, process | passed (process found with jar, version 1.21.4 and port read from it; adopted; `list` over RCON; stopped over RCON, exit code 0; 38.8 s) |
| Minecraft discovery, container | passed (found, adopted, Running, `server.properties` read through `docker exec`, removal refused; 18.4 s) |
| Minecraft Docker target | passed (created in 3 s, Done after 129 s, `list` 0 of 7, stopped, removed by label; 142.5 s) |
| Minecraft SSH target | passed (throwaway account, host key approved, created in 3 s, Done after 22 s, `list` 0 of 7, stopped; 56.5 s) |
| Minecraft SSH Docker target | passed (Docker-in-Docker host with a throwaway account ready in 5 s, created in 24 s, Done after 33 s, `list` 0 of 7, stopped; 72.5 s) |

Run only the forge with `MT_LIVE_ONLY=forge`. A GitLab container needs `nginx['listen_port'] = 80` when its external URL has another port, and a token made with `gitlab-rails runner`; remove the container afterwards.
