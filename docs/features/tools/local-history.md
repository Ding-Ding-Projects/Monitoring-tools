# Local history for scripts and the commit message

Every save of a shortcut script keeps a versioned copy, and so does every save of the default commit message template. Nothing is uploaded.

## What is kept
- Where: the app data folder, under `history`, one subfolder per item (`script-<name>` for each shortcut script, and `commit-template`).
- Each save writes one file named by the time it was kept. A save that is identical to the newest copy is not stored again.
- At most 50 versions are kept per item. The oldest is dropped first. There is no setting for this yet.
- Renaming a script starts a new history under the new name; the old name's versions stay in their folder.

## Looking at it
- In the script editor, the **History** button opens the list below the buttons. In the commit message window, **History** opens it above the buttons.
- The list shows when each copy was kept and its size. Its search bar (with the regex builder) filters versions by their text or time.
- Picking a version shows a line comparison against the text now in the editor. A line starting with `+` would be added by restoring, and a line starting with `-` would be removed. A short sentence above it counts both.

## Restore
**Restore this version** is off until a version is picked. It asks for the two-key confirmation, then:
1. keeps the text currently in the editor as a new version, so nothing is lost;
2. puts the chosen version in the editor and saves it (a script is written to disk, the template is saved to settings). Restoring a commit template does not start a push.

## Not done
- No encrypted snapshots, no password-protected manager and no retention setting.
- The comparison is by whole lines, so a small change inside a long line shows as one removed and one added line.
- Other editable text, such as appearance edits, is not versioned here. Settings have their own history on the Records page.
- Verified by the self-test (save, duplicate skip, the 50 version cap, restore and the comparison) and by compiling; the buttons have not been exercised on a screen.
