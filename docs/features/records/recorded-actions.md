# Actions and discarded changes

The **Actions and discarded changes** card on the Records page lists, newest first, what the app did that you might want to see or take back:

- **Discarded unsaved changes**, from a guided form, the copied text editor and the Minecraft server settings, with the text that was discarded and a **Copy it back** button.
- **Base64 files** written beside a file, with **Show** and **Undo**.
- **Virtual disk actions** and **Quick packs** from the Virtual disks page, with the route that did the work.

## How it is kept

Each action is one line of JSON added to the end of a file in the app's data folder. Nothing is rewritten or removed: an undo or a copy back is a new line that points at the line it reverses, and the card shows the first one as reversed. Discarded text is sealed with the clipboard history key whenever clipboard history protection is on; while that history is locked, **Copy it back** asks for it to be unlocked first. The card shows the newest 30 actions; the file keeps every line.
