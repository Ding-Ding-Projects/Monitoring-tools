# Bulk actions

Every list that holds many items shares one select mode, so choosing several and acting on them works the same everywhere.

## How it works

1. Press **Select** above the list. Every row gets a checkbox and a bar appears with a live count ("3 selected").
2. Tick rows one by one, or press **Select all shown**. That selects only the rows the search currently shows, so a filtered list never selects hidden rows by accident. **Select none** clears the choice.
3. Use an action on the bar. Actions stay disabled until at least one row is chosen. Destructive actions ask for the two-key confirmation first.
4. Press **Done selecting** to leave select mode. Leaving clears the choice.

The count is announced politely to screen readers, every checkbox is named after its row, and everything works from the keyboard.

## Where it is available

| List | Actions |
| --- | --- |
| Clipboard history | Copy, Save as text, Remove from history |
| Activity | Copy, Export as text |
| Records, notification centre | Copy, Export as text, Remove (only the chosen ones) |
| Shortcuts | Export as JSON (with each script), Duplicate (names end in -copy), Delete |
| Game mode, watched games | Remove selected |
| Regex builder, Library tab | Export selected (same file format as Export JSON), Delete selected |
| Local AI models, installed models | Delete selected |
| Settings history | Copy, Export as JSON, Delete (the newest snapshot is the current state and is never selectable) |
| Changelog | Copy notes, Export as Markdown (releases come from GitHub, so nothing is deleted) |
| Outages (Activity page) | Copy, Export as text, Remove (an ongoing outage is kept) |
| Schedule rules | Enable, Disable, Delete |

## Notes and limits

- Selection is kept while the list refreshes, matched by the item itself (models by name), and an item that disappears is dropped from the choice.
- The regex Library has no search of its own, so "all shown" there means every saved pattern. Because that builder is a pop-up that closes when another window takes focus, the Library may have closed by the time a confirmation finishes; the change is still made.
- Deleting models runs one after another and stops at the first failure, saying how many were deleted.
- Lists with no bulk actions on purpose: the overview page's four-item outage summary (the full list is on Activity), and single-item panels such as lock records, which are managed one at a time behind their own credentials. The regex Library still has no search of its own.

## For developers

`UI/BulkSelect.cs` holds `BulkSelect<T>`: `Begin`, `Wrap(item, row, name)`, `Bar(actions)`, `SelectAllShown`, `SelectNone`, `Prune`, `ConfirmThen`, plus shared copy and save helpers. The self-test covers select all shown against a filter.
