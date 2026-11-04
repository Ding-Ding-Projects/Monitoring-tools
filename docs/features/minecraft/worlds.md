# Worlds

![Worlds](../../screenshots/mc-worlds.png)

Each dimension of the server's world with its size: the Overworld (the `level-name` folder), the Nether and the End,
whether they sit beside the world (`world_nether`, `world_the_end`, as Paper writes them) or inside it (`DIM-1`, `DIM1`,
as Vanilla writes them). The seed is read from the world's own `level.dat`, or from `level-seed` before the world exists.

- **Download ZIP** writes one dimension as a ZIP, skipping the lock file a running server holds.
- **Import a world** takes a ZIP or a folder. It is checked for a `level.dat` (also one folder down), copied in, and never
  moved. A ZIP entry that points outside its folder is refused.
- **Reset the world** removes the world folders so the server makes a new world at its next start. When a world already
  exists, an import becomes **Replace**. Both stop the server, take a backup first, and ask for the two-key confirmation.
- **Generate a new world, or pre-generate chunks** opens the [world generator](world-generator.md) here, aimed at this
  server when it is on this computer.

SSH servers keep their world on the host, so import, download and reset happen there.

## Verification

Capture `mc-worlds`. The reset and import code paths share the backup, which the backup self-test check drives; they were
not driven against a real world in the self-test.
