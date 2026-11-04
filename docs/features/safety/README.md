# Safety

Toy locks, the unlock ladder, a local recovery desk, a built-in authenticator and a change history. All of it lives on the **Safety** page and works offline.

| Feature | What it is | Status |
| --- | --- | --- |
| [Element locks](element-locks.md) | Lock any button, setting or card behind a PIN, password or authenticator code | Implemented, partly (see the page) |
| [Unlock ladder](unlock-ladder.md) | Three small games that can end a wait early | Implemented |
| [Support Tickets](support-tickets.md) | A local make-believe support desk that leads to the app data folder | Implemented |
| [Authenticator](authenticator.md) | Live one-time codes, RFC 6238 | Implemented |
| [QR pairing](qr-pairing.md) | Reading a QR picture into the add form, and drawing an account as a QR for a phone | Implemented (built-in decoder and encoder, versions 1 to 10) |
| Windows Hello route | Unlock a toy lock, or reveal an authenticator secret, with Windows Hello | Implemented where Windows Hello is set up; see [Element locks](element-locks.md) |
| [Change history](history.md) | What changed in locks, accounts and the display name | Implemented, simpler than the full requirement |

## One thing to know first

The locks are **just for fun**. They are not security, they do not encrypt anything, and the recovery is always to delete files in the app data folder. Every lock prompt says so.

## Where things are stored

Everything is under the app data folder (`%LocalAppData%\MonitoringTools`):

| File | Holds |
| --- | --- |
| `safety\locks.dat` | Lock list, salted hashes of PINs and passwords, authenticator secrets for locks |
| `safety\lockstate.dat` | Attempt counts, wait times, ladder use |
| `safety\authenticator.dat` | Authenticator accounts and their secrets |
| `safety-history.json` | Change history, no secrets |
| `tickets.json` | Local support tickets |

The `.dat` files are protected with the Windows data-protection API for the current user. Another Windows account or another computer cannot read them. This is **not** the Windows Credential Manager vault, and it does not stop software running as you.
