# Blank-editor presets

A new script no longer starts as an empty canvas.

## Presets

Built from the app's own templates: the two **blank** presets use `Shortcuts.Template` exactly, so they can never disagree with the defaults the app ships.

- **Batch file:** open folder, copy path, zip it.
- **PowerShell:** resize image (a copy no wider than 1600 px, original untouched), rename by date (prefix the file with its last-modified date), hash file (SHA-256, printed and copied).

Each preset states what it does before it is chosen, and the review step of the [guide](guided-forms.md) shows the exact script that will be written.

## Empty editors

`ScriptPresets.Chips(shell, insert)` returns a row of preset chips for an editor whose script is empty or untouched. The caller decides how the chosen body enters the editor, so nothing is overwritten silently. It is not yet placed in the shortcut editor; see the task report for the line.

## Not built

User-saved presets, and a record in the local history (the app has no per-shortcut history yet).
