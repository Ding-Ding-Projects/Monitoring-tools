# Authenticator

Live one-time codes for any account, calculated on this computer.

## Adding an account

Paste an `otpauth://totp/…` link or a base32 secret and press **Add account**. A link brings its issuer, algorithm (SHA-1, SHA-256 or SHA-512), digits (6 to 8) and period (1 to 3600 seconds), and these are honoured. A bare secret uses the choices on screen, defaulting to SHA-1, 6 digits, 30 seconds. **Paste from clipboard** fills the field from copied text. **Read a QR from the clipboard** and **Read a QR from a file…** decode a setup QR picture into the same field (see [QR pairing](qr-pairing.md)).

## The list

Each account shows its code in large grouped digits, a countdown ring with the seconds as a number, an optional peek at the next code (on by default, a switch turns it off), and buttons to copy, move up or down, edit the label, show as a QR code, and remove (after a two-key confirmation). **Show as QR** reveals the secret, so it needs the two-key confirmation; when Windows Hello is available, **Show as QR (Windows Hello)** reveals it after Windows Hello confirms it is you instead. The list is searchable.

The code region changes once per period, so screen readers are told about the change and not about every second.

## Standards

RFC 6238 over RFC 4226. The page runs the published test vectors for SHA-1, SHA-256 and SHA-512 at 8 digits every time it opens (9 of 9 match at the time of writing).

## Storage and exports

Accounts and secrets are in `safety\authenticator.dat`, protected with Windows data protection for the current user, not in settings, logs or history. The export writes issuers, accounts and settings only, and says secrets are left out. A secrets export is not offered.

## What is not done

- QR reading and drawing exist but have limits (versions 1 to 40, mirrored codes read, no bulk-export codes): see [QR pairing](qr-pairing.md).
- Camera scanning is not available.
- Grouping and bulk actions on the list are not offered. Reordering is.
- The clock cannot be checked offline. The page says so and tells you where to sync it. It does not detect skew.
- If an account is used for a lock inside this same app, the key then sits inside the box it opens. The lock wizard says so and lets you do it anyway.
- Changing the stored secret of an existing account is not offered. Remove and add again.
