# Local history for scripts and the commit message

Every save of a shortcut script keeps a versioned copy, and so does every save of the default commit message template. Nothing is uploaded.

## What is kept
- Where: the app data folder, under `history`, one subfolder per item (`script-<name>` for each shortcut script, and `commit-template`).
- Each save writes one file named by the time it was kept. A save that is identical to the newest copy is not stored again.
- At most 50 versions are kept per item by default, and the oldest is dropped first. The limit (5 to 500) and an optional age limit in days are set on the Records page, in the **Local history** card. The newest version of an item always stays.
- Versions are plain text files (`.txt`), or encrypted files (`.enc`) when protection is on.
- Renaming a script starts a new history under the new name; the old name's versions stay in their folder.

## Looking at it
- In the script editor, the **History** button opens the list below the buttons. In the commit message window, **History** opens it above the buttons.
- The list shows when each copy was kept and its size. Its search bar (with the regex builder) filters versions by their text or time.
- Picking a version shows a line comparison against the text now in the editor. A line starting with `+` would be added by restoring, and a line starting with `-` would be removed. A short sentence above it counts both.

## Restore
**Restore this version** is off until a version is picked. It asks for the two-key confirmation, then:
1. keeps the text currently in the editor as a new version, so nothing is lost;
2. puts the chosen version in the editor and saves it (a script is written to disk, the template is saved to settings). Restoring a commit template does not start a push.

## Protection and the manager
The **Local history** card on the Records page chooses how saved versions are stored:
- **Off**: plain text files.
- **This Windows account**: each version is encrypted (AES-256-GCM) with a random key that Windows protects for this account. Another Windows account or another computer cannot open it; malware running as you could.
- **Password**: the key comes from a password you choose (8 or more characters; PBKDF2-SHA256, 600,000 rounds, a random salt). The password is not stored and cannot be recovered. The history is locked until you type it (**Unlock history**) and is locked again by **Lock history now** or when the app closes. While it is locked, saved versions cannot be read, new versions are not kept, and the History panels say so.

**Apply protection** moves every saved version to the chosen protection. Every version is read first and written under the new key before the old file is removed; if one cannot be read nothing is changed. Changing a password, or turning protection off, needs the history unlocked first. The card also lists each saved history with **Delete history** (two-key confirmation) and holds the two retention numbers.

## Not done
- Settings snapshots on the Records page follow the same protection: with it on they are written as `.jsonenc` files, listed only while the history is unlocked, and not kept while it is locked (the settings themselves are still saved). Apply protection converts them with the saved versions.
- A forgotten history password cannot be recovered. That is by design.
- A flipped bit in an encrypted file makes that version unreadable (it is detected, never shown wrong).
- The comparison is by whole lines, so a small change inside a long line shows as one removed and one added line.
- Other editable text, such as appearance edits, is not versioned here. Settings have their own history on the Records page.
- Verified by the self-test (save, duplicate skip, the 50 version cap, restore and the comparison; encryption for the Windows account and for a password, locking, a wrong password, a changed file, conversion back; retention by count and by age; and the manager card through its real buttons). Capture `history-manager`.
