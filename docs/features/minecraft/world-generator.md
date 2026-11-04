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
| Also pre-generate the Nether / the End | Off | Runs the same bounded procedure in those dimensions with `execute in minecraft:the_nether` / `the_end`, using the same chunk radius. Needs the jar's command list to include `execute in`. |
| Game rules | Server defaults | Listed from the jar's own `gamerule` command (name and type); only rules you change are set. |
| Superflat layers | Version default | Block and height per layer, plus biome and preset-string import and export, for the Flat type on Java 1.19 and newer. |
| Output | Folder | Folder or ZIP, with a Browse dialog. |

## How generation works

1. The version's server jar is downloaded once into a cache and checked against Mojang's SHA-1. A Temurin runtime is obtained through the app's existing Java route.
2. A private staging server is created on a free loopback port with `online-mode=false`, `max-tick-time=-1` and the chosen seed, type and structures. For Single biome, a data pack is written whose world preset is the jar's own `single_biome_surface` preset with the chosen biome swapped in; the pack format comes from the jar's `version.json`.
3. The server runs once. This requires the user to tick the EULA box in the card; the app never accepts it on anyone's behalf.
4. If an area, game rules or custom superflat layers are requested, the jar's own data generator is run once per version (no server start, cached) to read `reports/commands.json` and `reports/registries.json`. Game rules are then set with `gamerule`, each checked against the jar's rule list and type and confirmed by the server's reply. Pre-generation proceeds only when `execute if loaded` exists, because that is the only honest way to know a chunk is done. Then rectangles of at most 16 x 16 chunks (256, the forceload limit) are force-loaded one at a time, every chunk is confirmed loaded, and `forceload remove all` runs before the next batch. When Nether or End pre-generation is switched on, the same rectangles are force-loaded and confirmed in each dimension with `execute in <dimension> run forceload ...` and `execute in <dimension> if loaded`; a dimension is skipped, with a note in the result, when the jar has no `execute in`. If the version has no such command, pre-generation is skipped, the card says so, and the world simply generates as it is explored.
5. `save-all flush`, then `stop`. The `world` folder, including its data packs, is copied out.
6. Folder output creates a new folder and never overwrites; every file is verified by length and SHA-256. ZIP output is written to a `.part` file, checked entry by entry and renamed; an existing ZIP is never replaced. The staging folder is deleted afterwards, including on cancel or failure.

"Write to server settings" updates `server.properties` of a local server (and writes the single-biome pack for a local-process server). It only affects a world that does not exist yet, and says so when the world folder already contains `level.dat`.

## Game rule editor

"Read this version's jar" downloads the jar (cached, SHA-1 checked) and runs its data generator. The rule list comes from `gamerule` in `commands.json`: a rule whose value parser is `brigadier:bool` gets Server default / true / false, `brigadier:integer` gets a number box, and anything else is listed but disabled with its parser type. The command list does not contain defaults, so the editor records overrides only and says so. Rules apply to a generated world only, not to an existing server's world.

## Superflat layer editor

Visible for the Flat type. Each layer has a block id and a height; layers can be added, removed and moved. Validation: ids must look like `namespace:name`, heights are 1 to 384 and total at most 384 (the overworld height), the biome must be in the jar's biome list (checked when the data pack is written), and once the jar has been read every block must exist in its block registry. The preset string is `layers;biome`, for example `minecraft:bedrock,2*minecraft:dirt,minecraft:grass_block;minecraft:plains`. A leading numeric version and trailing structure options are accepted on import and ignored; numeric biome ids are kept at the default with a note, and block data values or states are refused. The layers are written into a data pack by copying the jar's own `flat` world preset and replacing its layers and biome, and `level-type` is set to the generated preset. Custom layers need Java 1.19 or newer; older versions keep their default layers and the editor says so. Writing to an existing server's settings supports layers for a local process server only.

## Limits and honest gaps

- Nether and End pre-generation use the same chunk radius as the Overworld, centred on 0, 0 in each dimension without coordinate scaling.
- There is no preview image. Terrain cannot be shown truthfully without running the game's generator, so none is faked.
- There is no noise settings or dimension-type editor and no drawing canvas for the area; the area is a numeric radius. Structure sets, lakes and features of the flat generator keep the jar's defaults. Game rules are not applied to an existing server's world.
- Single biome needs Java 1.19 or newer and a local-process server (or a new world).
- Docker and SSH servers: the card can write `server.properties` for a local Docker server's folder, but cannot read its jar, so Single biome and custom layers are unavailable there. SSH servers are not supported by this tool.
- The version boundaries (1.19 for presets, 1.13.1 for forceload) come from release dates in the catalogue. The data generator's output shapes were only studied for 1.21.x; a jar that changes the `commands.json` shape shows an unavailable state.
- Bedrock has no equivalent route and is not covered.

## Verification

Compiled only. A real generation run (download, data generator, staging server, game rules, Nether and End forceload batches, custom superflat world, folder and ZIP output) has not been executed, so none of these paths has been proven against a real jar.
