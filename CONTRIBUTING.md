# Contributing

1. Run `download-dependencies.bat` once, then `build.bat` to build `out\MonitoringTools.exe`.
2. Keep changes focused. Update the matching page under `docs/features/` and tick or add items in
   [ROADMAP.md](ROADMAP.md) only when the work is finished and verified.
3. New search or filter fields use the shared search control so they get the regex builder.
4. Write plain professional English in code, comments, documentation and commit messages.
5. Run `MonitoringTools.exe --selftest <folder>` on a non-visible desktop before sending a change.
6. Every push to `main` publishes a release, so merge only builds you would be happy to ship.

Generated files: [CHANGELOG.md](CHANGELOG.md) (`node scripts/changelog.mjs`) and the README figures
block (written by the release workflow) should not be edited by hand.
