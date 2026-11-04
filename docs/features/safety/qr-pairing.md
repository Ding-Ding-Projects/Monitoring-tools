# QR pairing

Read an authenticator setup QR code from a picture, and draw an account as a QR code to move it to a phone. Both are written from the QR specification inside this app: no library, no web service, no network request. Pictures are decoded on this computer and are not stored.

## Reading a QR

On the **Safety** page, in the Authenticator card, under **Add an account**:

| Button | What it does |
| --- | --- |
| **Read a QR from the clipboard** | Decodes a picture on the clipboard (for example a region copied with Win+Shift+S) |
| **Read a QR from a file…** | Decodes a PNG, JPEG, BMP, GIF or TIFF file, up to 30 MB |

When the code holds an `otpauth://` link, the link fills the box above, the status line names the account, and you press **Add account** yourself. Nothing is added automatically.

### What the reader supports

- Versions 1 to 10 (up to roughly 200 characters), error-correction levels L, M, Q and H, interleaved blocks, with Reed-Solomon repair of damaged modules.
- Byte, alphanumeric and numeric segments (and an ECI marker is skipped; byte data is read as UTF-8, falling back to Latin-1).
- Pictures rotated by 90, 180 or 270 degrees, mildly tilted or with mild perspective, with light noise, and light-on-dark (inverted) codes.

### When it cannot read a code, it says why

| Message | Meaning |
| --- | --- |
| No finder patterns | No QR code was found: the three large corner squares are missing |
| Finder patterns were seen, but not three that form the corners of one code | Cropped, too small, or mirrored |
| Unsupported version | The code is version 11 or larger |
| Format information could not be read | Too blurred or damaged |
| Too many errors | More damage than the code's error correction can repair |
| Unsupported content | Kanji mode or another segment type |
| Bulk export (`otpauth-migration://`) | A multi-account export code; this build reads single-account setup codes only |
| Not an authenticator link | The code was read but holds other text; nothing is filled in |

### Known limits

- Mirrored codes are not read.
- A very small code in a very large screenshot (modules of about 2 pixels or less) may not be found. Crop the picture or enlarge it.
- The decoder is tested with generated images and with images from an independent encoder (see Verification). It has not been tested against a large set of real phone photographs.

## Drawing a QR

Each account row has **Show as QR**. It draws the account's `otpauth://` link as a QR code (level M, up to version 10) in a small window, so you can scan it into a phone.

The picture contains the account's **secret**, so it is guarded:

- **Show as QR** opens the two-key confirmation first. Nothing is drawn until both keys and the slider are done.
- If Windows Hello is available, **Show as QR (Windows Hello)** reveals it after Windows Hello confirms it is you instead (see [Authenticator](authenticator.md)).
- The window warns about the secret, is drawn black on white so every scanner reads it, and closes itself after 60 seconds. Revealing it is written to the change history by account name, never with the secret.

An account whose link is longer than a version 10 code can hold (213 bytes) shows an honest "could not be drawn" message instead.

## Verification

Checked outside the app in a scratch project that compiles the three QR source files:

- 20 `otpauth` links of varied length (versions 7 to 10) encoded, drawn at four scales (3, 4, 5.5 and 7 pixels per module, inside a larger image at a random offset) with Gaussian noise, rotated 0, 90, 180 and 270 degrees, then decoded: 320 of 320 renders matched exactly.
- The encoder's output read by OpenCV's QR detector: 20 of 20.
- 40 reference images from a separate Python encoder (levels L, M, Q, H; versions 1 to 10) decoded by this decoder: 40 of 40.
- Known vectors: the format word for L/mask 0, the Reed-Solomon codewords of the classic "HELLO WORLD" example, its alphanumeric codewords, a numeric segment.
- Random module flips are repaired; heavy damage, noise and a blank picture produce the messages above.

The full interactive flow in the running app (clipboard, file dialog, QR window) has been compiled but not clicked through in this change.
