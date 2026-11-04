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

## Password

A password of 8 to 128 characters can be set instead of a PIN, or as well. It is stored the same way (salted PBKDF2-SHA256, 200,000 iterations, its own random salt) in separate fields of the shared record, so apps that only know PINs still read the record. When turning the mode on the first time, at least one of the two is required; either one turns the mode off afterwards, with one box that says which it accepts. The wrong-try counter, the wait and the recovery steps are shared with the PIN. A password-only record shows no PIN to an app that expects one, so those apps cannot turn the mode off with a PIN; treat the lock as a user-experience lock.

## Shared record and live updates

The mode, name and PIN hash live in `%LocalAppData%\DingDingShared\school-mode.json`, so every app that reads this record agrees. This app watches the file and applies changes immediately. If the file cannot be read, the mode is treated as off and the page says so, with a button to reset the record. If it cannot be watched, the page says that changes from another app appear only after a restart.

## Recovery and honesty

This is a user-experience lock, not a security boundary. A forgotten PIN cannot be recovered. Close the app and delete the shared record (the page shows its path and opens its folder). That turns the mode off and forgets the name and PIN. Anyone using the computer can do the same.

## What it removes from this app

- Language: the app is English only (`Language.Mode` returns English, even if a schedule rule asks for another language). The Language and voice page shows a short note naming the mode instead of the language choice, hides both humour sliders, and hides the Cantonese voice. Headlines use the plain level.
- Personal wording: the Service page card shows a note instead of the controls, the palette entries are left out, a dropped file is refused, and a loaded file stays stored but is not applied until the mode is off (`PersonalWording.Active` is false).
- The dim sum surprise, sounds, and quick commands that push are removed or paused as listed above.

## Not covered here

Windows Hello is offered as an optional third way to turn the mode off (see below). It is Windows Hello user verification through the Windows consent verifier, not a FIDO2 or WebAuthn credential: no key pair is created and Windows only answers yes or no. A true FIDO2 passkey is not implemented. Other Ding-Ding apps that share the record decide for themselves what they remove.

## Windows Hello as a third way to turn it off

Once a PIN or password exists, the Modes page shows a Windows Hello row with Windows' own availability text (available, no capable device, not set up for this account, turned off by policy, busy). While the mode is off, **Allow Windows Hello** asks Hello once to confirm the user and then stores a single flag (`HelloUnlock`) in the shared record, which holds no secret. While the mode is on, **Turn off with Windows Hello** asks Hello and, only on a confirmed yes, turns the mode off and clears the wrong-try count. A cancelled, failed or unavailable check changes nothing, and the button is shown disabled with the reason when Hello cannot be used. Hello cannot be added or removed while the mode is on, and the PIN and password keep working throughout. Code: `Core/Modes.Hello.cs`, `UI/Pages/ModesPage.Hello.cs`.
