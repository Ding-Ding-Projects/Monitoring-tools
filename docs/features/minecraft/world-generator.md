# World generator

A card on the Minecraft page that builds a Java edition world with the real server jar, or writes the same choices into one of your local servers.

Code: `Minecraft/WorldGenerator.cs` (engine), `UI/ControlCentreWindow.WorldGen.cs` (card).

## Controls

| Control | Default | Effect |
| --- | --- | --- |
| Which server | New world only | A local-process or local-Docker server supplies its version and receives the settings; otherwise type a version. |
| Version | Newest release | Any version in Mojang's catalogue that publishes a dedicated server. |
| Seed | Empty (random) | `level-seed`. A Random seed button fills in a reusable number. |
| World type | Normal | Normal, Flat, Large biomes, Amplified, and Single biome where the version supports it (Java 1.19 and newer). |
| Biome | `minecraft:plains` | Listed from the exact jar's `data/minecraft/worldgen/biome` folder, searchable. |
| Generate structures | On | `generate-structures`. |
| Area radius | 50 chunks | A square centred on 0, 0 of 2r x 2r chunks (default 100 x 100). 0 skips pre-generation. Capped at 100. |
| World border | Off | Optionally runs `worldborder set` to match the area. |
| Output | Folder | Folder or ZIP, with a Browse dialog. |

## How generation works

1. The version's server jar is downloaded once into a cache and checked against Mojang's SHA-1. A Temurin runtime is obtained through the app's existing Java route.
2. A private staging server is created on a free loopback port with `online-mode=false`, `max-tick-time=-1` and the chosen seed, type and structures. For Single biome, a data pack is written whose world preset is the jar's own `single_biome_surface` preset with the chosen biome swapped in; the pack format comes from the jar's `version.json`.
3. The server runs once. This requires the user to tick the EULA box in the card; the app never accepts it on anyone's behalf.
4. If an area is requested, the jar's data generator is asked for its command list. Pre-generation proceeds only when `execute if loaded` exists, because that is the only honest way to know a chunk is done. Then rectangles of at most 16 x 16 chunks (256, the forceload limit) are force-loaded one at a time, every chunk is confirmed loaded, and `forceload remove all` runs before the next batch. If the version has no such command, pre-generation is skipped, the card says so, and the world simply generates as it is explored.
5. `save-all flush`, then `stop`. The `world` folder, including its data packs, is copied out.
6. Folder output creates a new folder and never overwrites; every file is verified by length and SHA-256. ZIP output is written to a `.part` file, checked entry by entry and renamed; an existing ZIP is never replaced. The staging folder is deleted afterwards, including on cancel or failure.

"Write to server settings" updates `server.properties` of a local server (and writes the single-biome pack for a local-process server). It only affects a world that does not exist yet, and says so when the world folder already contains `level.dat`.

## Limits and honest gaps

- Overworld only is pre-generated; the Nether and the End are not.
- There is no preview image. Terrain cannot be shown truthfully without running the game's generator, so none is faked.
- Flat worlds use the version's default layers. There is no layer editor, no noise or dimension-type editor, no game-rule editor and no drawing canvas for the area; the area is a numeric radius. These parts of the wider contract are not built.
- Single biome needs Java 1.19 or newer and a local-process server (or a new world).
- Docker and SSH servers: the card can write `server.properties` for a local Docker server's folder, but cannot read its jar, so Single biome is unavailable there. SSH servers are not supported by this tool.
- The version boundaries (1.19 for presets, 1.13.1 for forceload) come from release dates in the catalogue.
- Bedrock has no equivalent route and is not covered.

## Verification

Compiled only. A real generation run (download, staging server, forceload batches, folder and ZIP output) has not been executed.
