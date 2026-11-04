# Search bars

## Where they are

| Surface | Field id | What one result is |
| --- | --- | --- |
| Control centre: every page (Overview, Activity, Clipboard history, Clipboard, Shortcuts and scripts, Network and DNS, Game mode, Conversion, Minecraft servers, Service, About and updates) | `page:<page id>` | A history row, an activity entry, a setting (switch or choice), a tile, a shortcut, a watched game, or a whole card when it has none of those |
| Minecraft servers: version picker | `minecraft:versions` | One official version id of the selected type |
| Command palette (`Ctrl+Shift+F`) | `palette` | One command, page or setting |
| Tray menu | `tray` | One menu row |

Each field keeps its own query, mode, case, regex options, replacement, test sample and recent patterns. Typing in one never changes another. The state lives in memory while the app runs; only patterns saved on purpose in the builder's Library are written to `settings.json`, plus the recent patterns of each box if the opt-in **Remember across restarts** switch (default off) is turned on.

## How matching works

- **Plain text (default):** every space-separated word must appear, case-insensitive unless `Aa` is on.
- **Regex:** the pattern runs in the real .NET engine (`System.Text.RegularExpressions`) with the selected options and a 200 ms limit per item. An item that hits the limit is left out and the bar says so.
- **Invalid pattern:** nothing is filtered, the bar turns amber and the reason is shown under it.
- **No matches:** an honest "No … match" line, plus **Search every page** on control-centre pages, which opens the command palette with the same query, mode and options.
- Button labels inside a row ("Copy again", "Actions") are not searched, so a word like "copy" finds copies, not every row.

## Keyboard

| Keys | Action |
| --- | --- |
| `Ctrl+F` | Focus the current page's search |
| `Alt+R` / `Alt+C` / `Alt+B` | Regex mode / match case / open the builder |
| `Esc` in the bar | Clear the query |
| `Esc` in the builder | Close it and return focus to its bar |

The result count is a polite live region, so screen readers hear "3 of 12 copies" as it changes.

## Security and privacy

Patterns and sample text never leave the computer. Patterns are capped at 4 096 characters and samples at 256 KB; every evaluation has a 200 ms timeout, so a catastrophic pattern cannot hang the app.

## Verification

`MonitoringTools.exe --screenshots <folder>` renders `search-history-plain`, `search-builder-*`, `search-settings`, `search-tray-filter` and `search-palette-regex` from the real built app. There is no automated test project yet; see the parity ledger for what is still unverified.
