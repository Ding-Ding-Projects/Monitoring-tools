# Tab strip

Each open tab is one page of the control centre (the nineteen page ids such as `overview`, `history`, `modes`). Every page is open on a fresh install, in page order, with Overview pinned, so nothing hides behind New tab. A page the strip has never offered before opens once: after an update that is only the new pages, and an existing strip keeps its own order and gains them at the end. A tab you close stays closed (the list of pages already offered is saved as `KnownTabs`). **Open every page** in the strip menu (right-click the strip or Tab options) brings back every closed page at once. Opening any page, from the New tab catalog or from a link inside another page, opens its tab; selecting a page that already has a tab focuses that tab and never duplicates it.

## Docking

The strip is docked to the left by default (vertical, 40 px rows, up and down arrows). It can be docked to the top, right or bottom from the strip's own context menu (right-click on an empty part of the strip, or the Tab options button) or from the `TabStrip.DockChoice()` settings control. The choice is saved in `TabDock`. Labels are never rotated. At window widths below 1000 px a vertical strip collapses to icons; every icon tab keeps its full accessible name and tooltip. When the strip is on the top edge the window buttons stay in the top right corner and the strip leaves room for them.

## Tab behaviour

- **New tab** (button and Ctrl+T) opens a searchable catalog of every page with its icon. Its search field has the anchored regex builder. Cancelling returns focus to the New tab button.
- **Close**: close glyph, middle-click, Delete on a focused tab, Ctrl+W. Closing the active tab activates its neighbour; closing the last tab reopens Overview. Pages keep their drafts inside the window, so closing a tab never discards them.
- **Reorder**: drag a tab onto another tab, or Ctrl+Shift+Up/Down (Ctrl+Shift+Left/Right when docked top or bottom).
- **Pin / unpin**: context menu, Ctrl+Shift+P, or the search results. Pinned tabs form the first region, show a pin glyph, and are skipped by every bulk close unless explicitly included. Pinning removes a tab from its group.
- **Groups**: create (strip menu, or "New group with this tab"), rename (F2 or menu), pick a theme colour role from a dialog, collapse and expand (Enter, Space or click), remove (members stay open). Moving a tab uses **Move… into group…**, which opens a picker dialog with its own search field, a create-new-group path and an empty state; the menu never lists groups inline. A collapsed group stays collapsed when a tab moves into it; if its member is the active tab the group is shown temporarily without changing the saved collapsed state.
- **Overflow**: the strip scrolls with a visible scroll bar, and a **More tabs** button appears when tabs do not fit and lists the hidden ones.
- **Keyboard**: the strip reports itself as a tab list with the real orientation and each tab as a tab item with selection state. Arrow keys along the strip's axis move focus (manual activation with Enter or Space), Home and End jump to the edges, the Applications key or Shift+F10 opens the tab menu.

## Shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+T | New tab catalog |
| Ctrl+W | Close the active tab |
| Ctrl+Tab, Ctrl+Shift+Tab | Next, previous tab |
| Ctrl+Shift+P | Pin or unpin the focused or active tab |
| Ctrl+Shift+Up/Down (Left/Right when docked top or bottom) | Move the tab within its region |
| Ctrl+Shift+E | Search this tab strip |
| Ctrl+Shift+A | Search all open tabs |
| Ctrl+Shift+G | Search tab groups |
| F2 (on a group header) | Rename the group |
| Delete (on a tab) | Close the tab |

## The four tab searches

Each is a `SearchField` with its own state and anchored regex builder (`tabs:strip`, `tabs:group:<id>`, `tabs:groups`, `tabs:master`): the current strip, one group, group names, and the master search of every open tab. Results show pinned state, group and active state, can be activated with Enter, and offer Pin and Close without losing the query. Selecting a group-name result moves focus to the group header and never changes its collapsed state. There is one window and one strip in this app, so the master search shows the same set as the strip search plus the window and dock edge.

## Bulk close

**Close tabs containing text** and **Close tabs not containing text** (strip and tab menus). Each has its own search field and builder, matches only the visible tab label with one shared predicate (the inverse negates it), refuses an empty query or an invalid pattern, shows the match mode, a count and a preview before closing, and protects pinned tabs unless the include-pinned switch is on.

## Persistence

`Settings.Tabs.cs` stores `TabDock`, `OpenTabs` (order), `PinnedTabs`, `TabGroups` (name, colour role, collapsed), `TabGroupOf` (membership) and `ActiveTab`. Structure changes save immediately; the active tab is saved on window close and with the next structure change.

## Not done

- **Edit tab appearance** (per-tab typography and colour editor, Shift+right-click) and group appearance decoration are not implemented. The menu's generic Edit appearance entry opens the Appearance page.
- Tab and group search results are reached from dialogs rather than inline in the strip, because the vertical strip has no room for four search fields.
- The dock setting control exists (`TabStrip.DockChoice()`) but is not yet placed on the Appearance page.
- Localized (Cantonese and bilingual) strings are not wired for the strip and its dialogs yet; the labels are English.
- Verified by compiling only. No runtime, keyboard, screen-reader, 125/150/200% scale or narrow-width pass has been run.
