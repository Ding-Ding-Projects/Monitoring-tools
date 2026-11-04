# Settings history

Each time settings are saved and the result differs from the last snapshot, a snapshot is written to `%LOCALAPPDATA%\MonitoringTools\settings-history\` as a JSON file. It records the time, a one-line summary (for example `Toast seconds: 3 → 5; +1 more`) and the full settings. At most 200 snapshots are kept; the oldest are deleted first.

- **View diff** lists every property that changed compared with the snapshot before it.
- **Restore** replaces the current settings with a snapshot after a confirmation, saves them, and reloads the page. The settings you replaced are kept as a new snapshot, so a restore can be undone.

Snapshots contain your full settings, including folder paths and the git history repository. Delete the folder to clear them.
