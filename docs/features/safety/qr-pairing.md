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

- Versions 1 to 40 (up to 2,331 bytes at level M), error-correction levels L, M, Q and H, interleaved blocks, with Reed-Solomon repair of damaged modules. Versions 7 and above carry version information and several alignment patterns; the reader finds each alignment pattern near where the first fit predicts it and refits the perspective on all of them.
- Byte, alphanumeric and numeric segments (and an ECI marker is skipped; byte data is read as UTF-8, falling back to Latin-1).
- Pictures rotated by 90, 180 or 270 degrees, mildly tilted or with mild perspective, with light noise, and light-on-dark (inverted) codes.
- Mirrored codes: when a picture cannot be read, the reader transposes it (a mirror image is a transpose plus a turn, and turns are handled) and tries again.

### When it cannot read a code, it says why

| Message | Meaning |
| --- | --- |
| No finder patterns | No QR code was found: the three large corner squares are missing |
| Finder patterns were seen, but not three that form the corners of one code | Cropped or too small |
| Unsupported version | The code looks larger than version 40, so it was probably misjudged |
| Format information could not be read | Too blurred or damaged |
| Too many errors | More damage than the code's error correction can repair |
| Unsupported content | Kanji mode or another segment type |
| Bulk export (`otpauth-migration://`) | A multi-account export code; this build reads single-account setup codes only |
| Not an authenticator link | The code was read but holds other text; nothing is filled in |

### Known limits

- Perspective: a code is read through a homography fitted to the three corner squares and every alignment pattern found (refitted twice, with a finer search for each pattern, and the plain four-point fit kept as a last resort). Simulated tilts up to 15% (with an 8 degree turn) decode on a version 9 code, and all 16 simulated photos decode, including the version 24 ones (tilts of 6.5% and 8%). Two causes were fixed: a tidy-looking triple of stray ring patterns in the data could be picked as the three corner squares (finders are now weighed by how often each was seen, which is about 2.5 scan rows per pixel of module size for a real one and 1 or 2 for a stray), and an alignment pattern matched in the wrong place could drag the fitted map off, so patterns that disagree with the fit by more than a third of a module are dropped and the map refitted.
- A very small code in a very large screenshot (modules of about 2 pixels or less) may not be found. Crop the picture or enlarge it.
- The decoder is tested with generated images and with images from an independent encoder (see Verification). It has not been tested against a large set of real phone photographs.

## Drawing a QR

Each account row has **Show as QR**. It draws the account's `otpauth://` link as a QR code (level M, up to version 40) in a small window, so you can scan it into a phone.

The picture contains the account's **secret**, so it is guarded:

- **Show as QR** opens the two-key confirmation first. Nothing is drawn until both keys and the slider are done.
- If Windows Hello is available, **Show as QR (Windows Hello)** reveals it after Windows Hello confirms it is you instead (see [Authenticator](authenticator.md)).
- The window warns about the secret, is drawn black on white so every scanner reads it, and closes itself after 60 seconds. Revealing it is written to the change history by account name, never with the secret.

An account whose link is longer than a version 40 code can hold (2,331 bytes at level M) shows an honest "could not be drawn" message instead.

## Verification

Checked outside the app in a scratch project that compiles the three QR source files:

- 20 `otpauth` links of varied length (versions 7 to 10) encoded, drawn at four scales (3, 4, 5.5 and 7 pixels per module, inside a larger image at a random offset) with Gaussian noise, rotated 0, 90, 180 and 270 degrees, then decoded: 320 of 320 renders matched exactly.
- The encoder's output read by OpenCV's QR detector: 20 of 20.
- 40 reference images from a separate Python encoder (levels L, M, Q, H; versions 1 to 10) decoded by this decoder: 40 of 40.
- In the app's own self-test (`--selftest`): every version from 11 to 40 at levels L, M, Q and H encodes and decodes; the derived block layout equals the explicit tables for versions 1 to 10 and matches the standard's byte capacities at versions 15, 20 and 40; a mirrored code decodes at small and large versions; six codes from another encoder (versions 3, 13, 20, 27, 34, 38) decode; and 16 simulated phone photos of a screen (tilt, turn, blur, uneven light, noise) are run, all 16 of which must decode (16 of 16 in the last full run). The block table for versions 11 to 40 was also compared with the other encoder library's table for all 160 version and level pairs. A real phone camera was not used.
- Known vectors: the format word for L/mask 0, the Reed-Solomon codewords of the classic "HELLO WORLD" example, its alphanumeric codewords, a numeric segment.
- Random module flips are repaired; heavy damage, noise and a blank picture produce the messages above.

The full interactive flow in the running app (clipboard, file dialog, QR window) has been compiled but not clicked through in this change.
