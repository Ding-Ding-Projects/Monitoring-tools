# School mode

A switch for handing the computer to someone who should only see a quiet, English-only version of the app. The user chooses the mode's name; the default is "School mode".

## What it does while on

- Game mode stays on and cannot be switched off (`Modes.GameModeOn`).
- Sound effects are muted (`Modes.SoundsOn`).
- Quick commands that push to git are paused (`Modes.Blocks("push")`).
- The app shows English only. Cantonese, bilingual mode, humour levels, personal wording and the dim sum surprise are treated as not installed: their controls are left out of the page, not disabled (`Modes.Blocks` for `cantonese`, `bilingual`, `funny`, `vocabulary`, `dimsum`).
- Saved choices are kept and return when the mode is turned off.

## Name

Rename it on the Modes page, 1 to 60 characters. After a rename the Modes page uses only the new name. Other surfaces must read `Modes.SchoolName`.

## PIN

- The first time the mode is turned on, a PIN of 4 to 12 digits is required.
- It is stored only as a PBKDF2-SHA256 hash, 200,000 iterations, with a random 16-byte salt generated when the PIN is set. The PIN itself is never stored, logged or exported.
- Turning the mode off needs the PIN. Five wrong tries start a wait of 30 seconds that doubles with each further wrong try, capped at 15 minutes. There is no permanent lock. The counter is kept in this install's settings.

## Shared record and live updates

The mode, name and PIN hash live in `%LocalAppData%\DingDingShared\school-mode.json`, so every app that reads this record agrees. This app watches the file and applies changes immediately. If the file cannot be read, the mode is treated as off and the page says so, with a button to reset the record. If it cannot be watched, the page says that changes from another app appear only after a restart.

## Recovery and honesty

This is a user-experience lock, not a security boundary. A forgotten PIN cannot be recovered. Close the app and delete the shared record (the page shows its path and opens its folder). That turns the mode off and forgets the name and PIN. Anyone using the computer can do the same.

## Not covered here

Forcing English and removing language controls on other pages depends on those pages consulting `Modes.Blocks`. Passkey or password unlock is not implemented; only a PIN is offered.
