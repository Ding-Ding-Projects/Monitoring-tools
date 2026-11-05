# World generator

A card on the Minecraft page that builds a Java edition world with the real server jar, or writes the same choices into one of your local servers.

Code: `Minecraft/WorldGenerator.cs` (engine), `Minecraft/WorldgenEditor.cs` (noise settings and dimension type editing, data pack writer), `UI/ControlCentreWindow.WorldGen.cs` (card), `UI/ControlCentreWindow.WorldGenEditors.cs` (worldgen editor panel), `UI/ChunkAreaPicker.cs` (area canvas).

## Controls

| Control | Default | Effect |
| --- | --- | --- |
| Which server | New world only | A local-process or local-Docker server supplies its version and receives the settings; otherwise type a version. |
| Version | Newest release | Any version in Mojang's catalogue that publishes a dedicated server. |
| Seed | Empty (random) | `level-seed`. A Random seed button fills in a reusable number. |
| World type | Normal | Normal, Flat, Large biomes, Amplified, and Single biome where the version supports it (Java 1.19 and newer). |
| Biome | `minecraft:plains` | Listed from the exact jar's `data/minecraft/worldgen/biome` folder, searchable. |
| Generate structures | On | `generate-structures`. |
| Area | Radius 50 chunks, centre 0, 0 | A square of 2r x 2r chunks (default 100 x 100) around a chosen centre chunk. Draw it on the area canvas or type radius and centre. 0 skips pre-generation. Radius capped at 100. See the area canvas section. |
| Noise settings and dimension types | Jar defaults | Optional single-value edits, written as a data pack of only the changed files. See the worldgen editor section. |
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
   While pre-generating, **Pause pre-generation** holds the build at the next batch boundary (the batch being built, at
   most 256 chunks, finishes first and nothing is force-loaded while it waits) and **Resume pre-generation** carries on
   from the next batch. Cancel works while paused. The server reply to each `forceload add` is read, so a refusal is
   reported in the server's words. The square is cut into batches of nearly equal width (17 chunks each side is 12, 11
   and 11 wide, never 16 and a strip of 1), the world is saved after every batch, and when no chunk of a batch has loaded
   for 45 seconds the staging server is restarted (up to three times per batch) and the batch is asked for again. This
   restart is now only a fallback: the staging `server.properties` sets `pause-when-empty-seconds=0` (see below), which removed the stall.
5. `save-all flush`, then `stop`. The `world` folder, including its data packs, is copied out.
6. Folder output creates a new folder and never overwrites; every file is verified by length and SHA-256. ZIP output is written to a `.part` file, checked entry by entry and renamed; an existing ZIP is never replaced. The staging folder is deleted afterwards, including on cancel or failure.

"Write to server settings" updates `server.properties` of a local server (and writes the single-biome pack for a local-process server). It only affects a world that does not exist yet, and says so when the world folder already contains `level.dat`.

## Game rule editor

"Read this version's jar" downloads the jar (cached, SHA-1 checked) and runs its data generator. The rule list comes from `gamerule` in `commands.json`: a rule whose value parser is `brigadier:bool` gets Server default / true / false, `brigadier:integer` gets a number box, and anything else is listed but disabled with its parser type. The command list does not contain defaults, so the editor records overrides only and says so. Rules apply to a generated world only, not to an existing server's world.

## Superflat layer editor

Visible for the Flat type. Each layer has a block id and a height; layers can be added, removed and moved. Validation: ids must look like `namespace:name`, heights are 1 to 384 and total at most 384 (the overworld height), the biome must be in the jar's biome list (checked when the data pack is written), and once the jar has been read every block must exist in its block registry. The preset string is `layers;biome`, for example `minecraft:bedrock,2*minecraft:dirt,minecraft:grass_block;minecraft:plains`. A leading numeric version and trailing structure options are accepted on import and ignored; numeric biome ids are kept at the default with a note, and block data values or states are refused. The layers are written into a data pack by copying the jar's own `flat` world preset and replacing its layers and biome, and `level-type` is set to the generated preset. Custom layers need Java 1.19 or newer; older versions keep their default layers and the editor says so. Writing to an existing server's settings supports layers for a local process server only.

## Area canvas

A chunk grid (one chunk is 16 x 16 blocks) on which the pre-generation square is drawn.

- **Draw:** drag across the grid; the square is made even-sided (rounded up) so it stays centred on a chunk edge, and is capped at radius 100. **Pan:** right or middle drag, or Shift and drag. **Zoom:** the mouse wheel, around the pointer. **Keyboard:** the canvas takes focus; arrow keys move the square by one chunk (Shift: 8), plus and minus resize it, Home returns the view to 0, 0.
- **Coordinates:** ruler labels in chunks, thin lines per chunk (coarser when zoomed out), stronger lines at the 32-chunk region boundaries, a dashed origin cross, and a readout of the square's chunk and block ranges and the chunk under the pointer.
- The radius and centre boxes below the grid stay in step with it; typing in them redraws the square. The centre is a chunk coordinate (default 0, 0) and applies to every chosen dimension with no coordinate scaling, so the Nether area is the same chunk numbers, not the Overworld's divided by eight. The optional world border is centred on the same block.
- The grid shows **no terrain**. Terrain cannot be known without running the game's generator, so none is drawn.

## Worldgen editor (noise settings and dimension types)

Unavailable until a version is chosen, and for versions before Java 1.19, where these files are not data pack content.

1. **Read this version's worldgen files** opens the exact server jar (cached, SHA-1 checked) and lists its default `worldgen/noise_settings` and `dimension_type` files. When the jar carries none (some versions build them in code), the jar's own data generator is run once with `--server`, without starting a server, and its output is cached next to the jar. If neither yields files the panel says so and stays unavailable.
2. Pick a file type and an id such as `minecraft:overworld`. The editor lists every single value in that default file with its default and JSON type, searchable with the standard search field and regex builder. Surface rules, the noise router and spawn targets are kept exactly as the jar defines them and are not listed.
3. Type a new value (booleans use Default / true / false). Leaving a value empty or equal to the default removes the change. Each changed row is marked, and a summary lists every pending change.
4. Validation runs as you edit and again when the pack is written. Every value must exist in the jar's own default and have the same JSON type (whole number, number, true/false, text). Block ids for `default_block.Name` and `default_fluid.Name` are checked against the jar's block list once the jar has been read. A short set of world height limits is applied: for dimension types `min_y` (-2032 to 2031) and `height` (16 to 4064) in multiples of 16, `logical_height` (0 to 4064, not above `height`) and `coordinate_scale` (0.00001 to 30,000,000); for noise settings `noise.min_y`, `noise.height`, `noise.size_horizontal` and `noise.size_vertical` likewise; and `min_y` plus height not above 2032. **The jar publishes no schema.** These limits were written by hand from the game's documented height rules for 1.18 and newer, not read from the jar, and the game itself has the final say.
5. On Generate (and on Write to server settings for a local process server), the changes are written to a data pack named `monitoring-tools-worldgen-edits` inside the world's `datapacks` folder. A data pack file replaces a whole entry, so each written file is the jar's default with only your values different, and untouched files are not written. Nothing is written when there are no changes.

## Versions run live

The live check (`MT_LIVE_ONLY=minecraft-lane`, `MT_LIVE_LANE=gen`, version from `MT_LIVE_MC`) was run on three versions, reading the region files and the stored game rules back from the files:

| Version | Result | Notes |
| --- | --- | --- |
| 1.20.6 | Passed in 362 s | 4 overworld, 4 Nether and 4 End region files; game rules in `level.dat`; the normal and superflat ZIPs; single biome cherry grove. Nothing version-specific was needed. |
| 1.21.4 | Passed (earlier runs) | The version the other live checks use. |
| 26.3 (newest release in Mojang's manifest) | Passed in 337 s | Two version differences, both in the check and not the engine: game rules are snake case (`keep_inventory`, `random_tick_speed`; the engine refuses an unknown name and says so, and the check now takes the names from the jar's own list), and a world stores them in `data/minecraft/game_rules.dat` as typed tags and keeps its dimensions under `dimensions/minecraft/...` instead of `DIM-1` and `DIM1`. |

Bedrock has no equivalent route; that is a limit, not a gap.

## Limits and honest gaps

- Nether and End pre-generation use the same chunk radius and centre as the Overworld, without coordinate scaling.
- There is no preview image. Terrain cannot be shown truthfully without running the game's generator, so none is faked.
- The worldgen editor changes single values only. Whether the running game applies a replaced `minecraft:overworld` dimension type or noise settings entry exactly as expected has not been tested here, and structure sets, lakes and features of the flat generator keep the jar's defaults. Game rules are not applied to an existing server's world.
- Single biome needs Java 1.19 or newer and a local-process server (or a new world).
- Docker and SSH servers: the card can write `server.properties` for a local Docker server's folder, but cannot read its jar, so Single biome and custom layers are unavailable there. SSH servers are not supported by this tool.
- The version boundaries (1.19 for presets, 1.13.1 for forceload) come from release dates in the catalogue. The data generator's output shapes were studied for 1.21.x and read live on 1.20.6 and 26.3; a jar that changes the `commands.json` shape shows an unavailable state.
- Bedrock has no equivalent route and is not covered.

## Verification

Compiled, plus self-test checks that need no network or jar: changing one noise value writes a data pack whose file differs from the default only in that value, invalid values (wrong type, a height that is not a multiple of 16, min_y plus height out of range, an excluded subtree, an unknown block) are refused with a reason, the editor lists typed values and skips the large subtrees, and a drawn square converts to a centre and radius within the cap. The canvas and the editor panel were not driven in the running app, and the data generator route for versions whose jar holds no worldgen files was not run. The live check (`MT_LIVE_ONLY=minecraft-lane`) runs real generations of the 1.21.4 jar and reads the result back from the files. **Pre-generation** (group `pregen`): radius 16 (four batches) paused after two, showed no progress for eight seconds, resumed and finished with non-empty region files for all four regions; radius 17 (nine batches, pause checked) and radius 23 (nine batches) both finished with every region file present (4 and 12 regions, 21,280 and 32,356 KB). **Generation** (group `gen`): a normal world with the Nether and the End pre-generated wrote region files in `region`, `DIM-1/region` and `DIM1/region` (4 each, 12,736 KB) and its `level.dat` holds `keepInventory=true` and `randomTickSpeed=7` (the defaults are false and 3); a normal world written as a ZIP holds 4 overworld, 4 Nether and 4 End region files and a `level.dat`; a custom superflat world (bedrock 1, emerald block 2, gold block 1) with the Nether and the End on, written as a ZIP, holds those blocks in its finished chunks plus 4 Nether and 4 End region files; a Single biome world of cherry grove with the Nether and the End on holds only `minecraft:cherry_grove` in its finished chunks and 4 Nether and 4 End region files (chunks saved part way as neighbours of the area carry a placeholder plains palette and are not counted). **Root cause (proven):** since 1.21.2 a dedicated server with no players pauses ticking after `pause-when-empty-seconds` (default 60); its log says `Server empty for 60 seconds, pausing`. Force-loaded chunks only generate while the server ticks, so a batch that was still loading after the first minute stopped with the server answering commands and `forceload query` normally and nothing else in the log. The staging server has no players, so every run hit it. Experiment on the real 1.21.4 jar with the restart recovery switched off: `pause-when-empty-seconds=60` stalled radius 17 at batch 4 of 9 (132 chunks never loaded, 5 minute timeout, the pausing line in the log); `pause-when-empty-seconds=0` finished radius 17 (170 s) and radius 23 (221 s), nine batches each, with zero restarts and every region file present. The staging properties now set 0; the restart stays as a fallback only. Not run: Bedrock.
