# File tool dialogs: design handoff

Fifteen separate dialog windows, one per file format family. Each opens from the clipboard notice (toast) when files of
that type are copied. They are not part of the control centre and do not share a window. The boards are design
references, not shipped code: implement them natively in WPF with the existing `B.*` brushes, Roboto and the dialog
pattern already used by `ConvertDialog` and `FileToolsWindow`.

Open `canvas.json` in the design canvas, or open any `*.dc.html` file in a browser. Every board has a Light or dark
button. Rendered frames, light and dark, are in `frames/` (`<Board>-dark.png`, `<Board>-light.png`).

| # | Board file | Dialog |
|---|---|---|
| 1 | `VideoTrim.dc.html` | Trim and cut video |
| 2 | `VideoToGif.dc.html` | Video to GIF or short clip |
| 3 | `AudioEdit.dc.html` | Trim, fade and loudness |
| 4 | `ImagesResize.dc.html` | Resize, crop and compress |
| 5 | `ImagePrivacy.dc.html` | Image privacy clean-up |
| 6 | `MakeIcon.dc.html` | Make an icon |
| 7 | `ExtractArchive.dc.html` | Extract an archive |
| 8 | `HashVerify.dc.html` | Hash and verify |
| 9 | `WhoSigned.dc.html` | Who signed this? |
| 10 | `CertInspector.dc.html` | Certificate inspector |
| 11 | `DataViewer.dc.html` | Data viewer and converter |
| 12 | `EncodingLines.dc.html` | Encoding and line endings |
| 13 | `SubtitleTiming.dc.html` | Subtitle timing |
| 14 | `FontInstall.dc.html` | Font preview and install |
| 15 | `DiscImage.dc.html` | Disc image |

`tools/` holds the generator (`build.mjs`, `lib.mjs`, `tools1.mjs` to `tools3.mjs`) and the renderer (`render.mjs`).
Edit the generator and run `node tools/build.mjs`; the `.dc.html` files are its output. Render with
`node tools/render.mjs <path to playwright-core>` (headless Edge, no window is created).

## Shared window spec (applies to all fifteen)

- **Window**: borderless, transparent, `ResizeMode=NoResize`, `SizeToContent=Height`, centred on screen, Topmost like
  `ConvertDialog`. Outer margin 16, inner card `B.Raised`, corner radius 28, padding 24 left, 20 top, 20 right, 18 bottom,
  `Fx.Shadow`. Width per dialog is in each section below (720 to 860). `MaxHeight` = work area minus 48; the body scrolls,
  the header and footer do not.
- **Header**: 44 px rounded tile (radius 14, `B.TertiaryContainer` with `B.OnTertiaryContainer` icon), then the tool
  name at 20 px Medium (the window title and the automation name of the dialog), under it the file name (13 px Medium,
  trimmed in the middle if long) and a one-line summary (13 px `B.TextSoft`). A 40 px tonal close button sits at the right.
  The file name is always shown so the person can tell which file the dialog acts on when several are open.
- **Footer**: one status line at the left (13 px, live region, says the result of the current choices: size, count,
  or what Cancel does), then Cancel (tonal pill, 40 px) and the primary action (accent pill, 40 px, with icon). A
  secondary tonal action such as Choose folder may sit before the status line. Done and error states replace Cancel
  with a single accent Close.
- **Controls**: field 42 px high, radius 10, `B.Sunken` fill, 1 px `B.LineStrong` border, `B.Mint` border and ring on
  focus, `B.Danger` border for an error. Segmented control 40 px, pill, `B.SecondaryContainer` for the selected part.
  Chip 32 px, radius 8. Switch 52×32. Banner radius 16, card radius 20, list radius 16. Section labels 12 px Medium
  caps in `B.Mint`.
- **Roles and fonts**: the roles are the `B.*` brushes in `Theme.xaml`. The light values used on the boards are the mint
  scheme of `Appearance.cs`. Roboto comes from the repository's vendored fonts (`fonts/`, copied from `site/src/fonts`).
  Monospace (times, hashes, code) is `F.Mono` (Cascadia Mono), shown in the frames with Roboto Mono.
- **Banner colours**: success `MintWash` on `OnPrimaryContainer`, info `TertiaryContainer`, warning `AmberWash` on `Amber`,
  error `DangerWash` on its on-colour. Light-theme amber and danger roles are not in `Appearance.cs` today:
  the boards use amber `#855300` on `#FFDDB8` and danger `#BA1A1A` on `#FFDAD6`. The implementer should add these two role
  pairs to the light scheme.
- **Disabled primary** uses the Material 3 disabled treatment: 12% of `B.Text` as fill, 38% as label.
- **Keyboard order** (all dialogs): content controls in reading order, then the footer secondary action, Cancel, the
  primary action, and the close button last. Esc closes (and cancels running work, see each Working state). Enter runs the
  primary action unless focus is in a list, a multi-line preview or a field that uses Enter itself. The primary action is the
  default button. Arrow keys move inside segmented controls, chip groups, lists and trees; Space toggles checks and
  switches; Tab never gets trapped inside a list or the timeline.
- **Accessible names**: the dialog is named "<tool name>: <file name>". Icon-only buttons are named (Close, Show password,
  Play). Progress bars are named and expose their value, or "working" when the length is unknown. Banners are live
  regions (polite for success and info, assertive for errors). Segmented controls are radio groups, chips with a
  state are toggle buttons, trees expose expanded and checked state. Colour is never the only carrier: every state
  also has an icon and a word.
- **Running work** (Working states): every tool runs off the UI thread, reports progress, and Cancel stops it and
  removes any unfinished output (`.part` files). Source files are never modified in place unless the dialog says so
  in plain words and keeps a backup.
- **Errors** (Error states): the banner says what stopped and the engine's own message in plain words; the card under
  it says what was not changed; there is always a safe next action and Copy details (which copies the engine message
  without file contents). No dialog ever claims success after a failure and none leaves a partial output behind.
- **Names that exist**: a result name that exists gets a number added, never an overwrite (same rule as Convert).
- **Languages**: every string is localized, English and Cantonese, through the existing string tables.
- **Not in these designs, on purpose**: the clipboard notice itself (designed in `design/clipboard-actions/`) and the
  catalogue of which notice button opens which dialog.

## Icons

All icons are the app's own stroke icons (24 px grid, 1.8 stroke, round caps) from the `Icons` class where one exists.
The implementer must add these to `Icons` (same grid and stroke, geometry is in `tools/lib.mjs`, object `P`):
Scissors, Hash, Pin, Cert, Disc, Font, Table, Warn, Info, Copy, Lock, Eject, Eye, Wave, Subtitle, Chevron, Down,
Key, Stamp, Sliders, Crop, Refresh. The ones already present and reused: Folder, File, Image, Video, Audio, Check, Close,
Clock, Restart, Lines, Trash, Shield, Box, Code, Plus, Play, Archive, Search, Text, Braces, Palette, Bolt.

---

## 1. Trim and cut video (`VideoTrim`)

- **Size**: 760 wide. **Opens for**: `.mp4 .mkv .mov .avi .webm .m4v`.
- **Engine**: FFmpeg from `.deps\ffmpeg` (already bundled) plus ffprobe. Lossless cut is `-ss/-to -c copy` and must snap to
  keyframes found with ffprobe (`-skip_frame nokey`, packet flags). Exact cut re-encodes with libx264 CRF 18 and copies
  the audio when possible. Remove-selected cuts two segments and joins them with the concat demuxer (lossless if both cuts
  are on keyframes). The thumbnail strip is 16 frames from `-vf fps=...,scale` into memory; preview frame by a seek.
- **Ready controls**: preview frame with play and the playhead time; segmented Keep selected / Remove selected; timeline
  (16-frame strip, mint start and end handles, white playhead, dimmed area outside the kept part; ruler under it);
  fields Start, End, New length (read-only); a banner that says Lossless or Exact cut needed; Save as field; switch Exact
  cut instead; Choose folder; Cancel; Trim.
- **Keyboard**: handles are sliders (Left and Right move one frame, Ctrl moves one second, Home and End jump to ends); I and
  O set start and end at the playhead; Space plays.
- **Copy**: "Lossless: no re-encode. Both points sit on keyframes, so the cut copies the video as it is. It is instant and
  loses no quality." Not on a keyframe: "Exact cut needed. The start is between keyframes, so the first seconds are
  re-encoded. Slower, with a tiny quality cost at the cut." Footer: "0:32.440 kept · about 690 MB".
- **Working**: percentage, "Copying video and audio, no re-encode", bytes and speed, `.part` name. Cancel deletes the part.
- **Done**: banner "Saved <name>", Length, Size against the original, Method; Show in folder, Play, Trim another section.
- **Error**: the stop point and FFmpeg's message; "No output file was kept"; Try again with exact cut, Copy details.
- **Implementer builds**: keyframe scan, the timeline control with thumbnail strip, estimated output size (bytes per second
  of the kept range), concat for Remove selected.

## 2. Video to GIF or short clip (`VideoToGif`)

- **Size**: 780. **Opens for**: the same video types.
- **Engine**: FFmpeg. GIF: two-pass `palettegen` then `paletteuse` (dither bayer), `fps`, `scale=<w>:-1:flags=lanczos`,
  `-loop 0`. Animated WebP: libwebp. Short MP4: libx264, no audio by default. Target size: loop that steps width
  (480, 400, 320, 240, 160) and fps (15, 12, 10, 8) until the output is under the limit; the estimate comes from a quick
  low-effort first pass.
- **Controls**: preview with play; segmented GIF / Animated WebP / Short MP4; From and To (also draggable on the preview);
  Width, Frames per second; switch Loop forever; card with switch Fit a target file size, Largest size (MB) and "Result will
  be" (read-only, the settings the search chose); Save as; Cancel; Make GIF.
- **States**: Working (pass 1 of 2, pass 2 of 2, frame x of y); Done (size against the limit, settings used);
  **Target too small** (warning, the table of tried settings, Switch to Short MP4); no file is written in that state.
- **Keyboard**: range fields accept `m:ss.s`; Up and Down change width by 16 and fps by 1.

## 3. Trim, fade and loudness (`AudioEdit`)

- **Size**: 780. **Opens for**: `.wav .mp3 .flac .m4a .ogg .aac`.
- **Engine**: FFmpeg. Waveform from `showwavespic` or decoded peaks computed in .NET. Trim `atrim`, fades `afade`,
  loudness two-pass `loudnorm` (first pass `print_format=json` gives the measured LUFS and true peak, the second pass
  applies `measured_*` values with the chosen target, true peak limit −1.0 dB). Output format follows the source
  (WAV, FLAC, MP3 re-encoded at the source bitrate, M4A via AAC).
- **Controls**: waveform with start and end markers and the selected part highlighted; Start, End, New length; sliders
  Fade in and Fade out (seconds); loudness card with the measured value and chips Leave as it is, Podcast −16, Streaming −14,
  Broadcast −23, Custom (a LUFS field); Save as; Cancel; Save.
- **States**: Working (step 2 of 2 after the measuring step), Done (before and after LUFS and true peak), Error (cannot decode).
- **Notes for the implementer**: the measured loudness is computed when the dialog opens (background, cached per file).
  Never apply a gain that would clip: the hint under the chips shows the gain and says when the limiter acts.

## 4. Resize, crop and compress (`ImagesResize`)

- **Size**: 840. **Opens for**: `.jpg .png .webp .bmp .gif .tif .heic`.
- **Engine**: WPF imaging (`BitmapDecoder`, `TransformedBitmap`, `CroppedBitmap`, `JpegBitmapEncoder`,
  `PngBitmapEncoder`); WebP through FFmpeg. "Target size" is a binary search of JPEG quality per picture with a floor of 40.
  Metadata is preserved unless Image privacy clean-up is used. Original is always kept; copies get the suffix `-small`
  (or the chosen folder).
- **Controls**: batch list (check, thumbnail, name and size, result size), before and after split preview with a drag
  handle, Width and Height, switch Keep proportions, crop chips (None, 1:1, 4:3, 16:9, Free), segmented Quality / Target
  size with its field, Save in… (folder), Cancel, Save N copies.
- **States**: Working (picture n of m, list shows done and working), Done with a failure (7 of 8 saved, per-file reason,
  Retry the failed one), Empty (no pictures; Add pictures).
- **Keyboard**: list rows are toggles (Space), the preview split is a slider (Left and Right), crop chips are a radio group.

## 5. Image privacy clean-up (`ImagePrivacy`)

- **Size**: 860. **Opens for**: `.jpg .png .tif .heic .webp`.
- **Engine**: WPF `BitmapMetadata` read; removal by `InPlaceBitmapMetadataWriter` when the format allows (JPEG, TIFF, PNG)
  so the pixels are not re-compressed; otherwise copy the file and strip with FFmpeg `-map_metadata -1 -c copy` when
  possible. PNG text chunks removed in .NET. Orientation and the colour profile are kept by default so the picture looks the
  same.
- **Controls**: file list with a badge per picture (Location, Clean) and a summary card; right pane with a warning banner,
  the location shown as coordinates on a **placeholder map** (a static drawing; the app contacts no map service), metadata
  groups each with a check (Location, Camera and lens, Date and time, Colour profile, Embedded thumbnail), switch Apply the
  same choices to all selected pictures, switch Keep the original, Select all, Cancel, Remove selected.
- **States**: Working, Done (tags removed per picture and what was kept), Empty ("Nothing to clean", Close only).
- **Privacy rules**: the dialog never uploads anything; coordinates are shown only in this window and are not logged or copied
  to history; Copy details never includes them.

## 6. Make an icon (`MakeIcon`)

- **Size**: 820. **Opens for**: `.png .jpg .bmp .gif .webp .svg`.
- **Engine**: .NET only. Build the ICO container by hand: for each size render with `RenderTargetOptions`/`TransformedBitmap`
  (high quality, `BitmapScalingMode.Fant`), encode each as PNG (all sizes, Windows Vista and later), write the
  `ICONDIR` and `ICONDIRENTRY` records (size 256 is stored as 0). SVG is rasterised at 512 first.
- **Controls**: source preview on a checkerboard, switch Keep transparency, slider Padding; size chips (16, 24, 32, 48, 64,
  128, 256, all on by default); preview of the sizes on a light and a dark tile (128 and 256 shown smaller; the label
  says so); Shape field (Keep the picture as it is, Rounded square, Circle); Save as; Cancel; Make icon.
- **States**: Working (size n of 7), Done (sizes and file size), **Small source picture** (warning, sizes above the source
  size are flagged and Turn off 128 and 256 is offered; continuing is allowed).

## 7. Extract an archive (`ExtractArchive`)

- **Size**: 820. **Opens for**: `.zip .7z .rar .tar .gz .tgz`.
- **Engine**: bundled 7-Zip (`7z.exe`) for 7z, rar, tar, gz and as the zip fallback; `System.IO.Compression` for plain zip
  listing. Listing with `7z l -slt`. **Safety**: reject any entry whose resolved path leaves the destination (zip-slip), absolute
  paths, drive letters and `..`; count them in the Done card ("Refused"). Password with `-p` passed through a pipe or
  response file, never on a command line that shows in a process list.
- **Controls**: search field (plain text with the regex button, like every search in the app), tree with checkboxes (name,
  size, modified; a tri-state check on folders), Select all and None, Extract to (default: a new folder named after the archive),
  segmented overwrite rule (Ask, Skip, Replace, Keep both), Cancel, Extract N files.
- **States**: **Password needed** (info banner, password field with show switch, wrong-password error under the field,
  nothing extracted), Working (file n of m, current file, bytes), Done (extracted count, skipped, renamed, refused,
  Open folder, Delete the archive as a separate red action that needs the usual two-key confirmation).
- **Keyboard**: Up and Down move in the tree, Right and Left expand and collapse, Space toggles the check, `/` or Ctrl+F
  focuses the search.

## 8. Hash and verify (`HashVerify`)

- **Size**: 760. **Opens for**: any file.
- **Engine**: `System.Security.Cryptography` (`SHA256`, `SHA1`, `MD5`, `SHA512`) with `IncrementalHash`, read with a 1 MB
  buffer on a background thread, cancellable. Several algorithms in one pass.
- **Controls**: algorithm chips (SHA-256 default, SHA-1, MD5, SHA-512) with the note that MD5 and SHA-1 do not prove
  authenticity; the value in groups of eight characters; Copy hash, Copy as file line (`<hash> *<name>`); Expected value field
  (ignores spaces, dashes, case; accepts a whole `hash  filename` line); Cancel; Verify (off until a value is pasted or while
  computing).
- **States**: Computing (percentage, MB/s, time left), **Match** (success banner, the whole hash highlighted), **Mismatch**
  (error banner "Do not run or install this file", both values side by side with the differing group highlighted and the
  first differing character number). A length that fits no algorithm says so ("This is 40 characters, which is SHA-1").
- **Never** auto-run, open or move the file from this dialog.

## 9. Who signed this? (`WhoSigned`)

- **Size**: 780. **Opens for**: `.exe .msi .dll .sys .cab .msix`.
- **Engine**: `WinVerifyTrust` with `WINTRUST_ACTION_GENERIC_VERIFY_V2` via P/Invoke for the verdict (also catalog-signed system
  files), `X509Certificate2` and `X509Chain` for the publisher and chain, `CryptQueryObject` or `SignedCms` for the timestamp,
  the file hash with SHA-256. Revocation checking is off-line friendly: report "not checked" instead of waiting.
- **Controls**: verdict banner, card (Publisher, Signed on, Certificate valid, Digest, Thumbprint), chain list with a badge
  per level, Open certificate (opens the Certificate inspector on that certificate), Copy details, Close.
- **States**: signed and valid; **Not signed** (warning, hash, Hash and verify, Show in folder); **Broken signature**
  (error, never run it, Delete file as a red action with the usual confirmation); **Untrusted issuer** (signed but the root
  is not trusted).
- **Wording rule**: a valid signature says who made the file and that it was not changed, never that it is safe. The
  dialog never launches the file and never claims anything about its behaviour.

## 10. Certificate inspector (`CertInspector`)

- **Size**: 780. **Opens for**: `.cer .crt .pem .pfx .p12 .der`.
- **Engine**: `X509Certificate2` / `X509Certificate2Collection.Import`; `ExportCertificatePem`, `Export(X509ContentType.Cert)` and
  `Export(Pfx, password)` for conversion; key usage and SAN from the extensions.
- **Controls**: a big expiry countdown ("214 days left"), a validity bar with the dates, card (Subject, Issuer, Alternative
  names, Key, Serial, SHA-256), allowed-use chips, Convert…, Copy details, Close.
- **States**: valid; **Expiring soon** (warning banner under 30 days, amber bar); expired (error colour, "Expired 3 days ago");
  **Password needed** for `.pfx` (the password is used once, never stored or logged, the private key is never shown);
  **Convert format** (segmented PEM / DER / PKCS#12, switch Include the private key that is only available when the source has
  one and then requires a new password, Save as).
- **Rule**: a public-only conversion is safe to share; any output that contains a private key is created only with the
  switch on and a password set, and the confirmation says so.

## 11. Data viewer and converter (`DataViewer`)

- **Size**: 800. **Opens for**: `.csv .tsv .json .yaml .yml`.
- **Engine**: `System.Text.Json` (`JsonDocument`, `Utf8JsonWriter` with indentation) for JSON; **YamlDotNet** for YAML (a new
  dependency: pin its version and hash in `deps/manifest.json`); an own RFC 4180 CSV reader (quotes, embedded newlines, BOM,
  delimiter detection). Only the first 500 rows are shown, the conversion uses the whole file with streaming.
- **Controls**: validity banner, segmented Table / Text, separator chips (CSV), table (header row, selected row), Convert to
  (JSON, YAML, CSV) with Pretty print, Save as, Format in place (rewrites this file, after a backup), Cancel, Convert.
- **States**: valid table, **Invalid file** (line and column, three lines of context with the offending line highlighted, Go to the
  error, Copy message; Convert off), Text view with YAML, Done (counts and how numbers were typed).
- **Keyboard**: the table is a read-only grid (arrows, Ctrl+C copies a cell); Ctrl+F searches.

## 12. Encoding and line endings (`EncodingLines`)

- **Size**: 840. **Opens for**: text files (`.txt .csv .log .sh .md .json .xml` and similar).
- **Engine**: .NET `Encoding` classes. Detection order: BOM; strict UTF-8 validation; UTF-16 heuristics (NUL pattern);
  fall back to the system ANSI code page and say it is a guess. Line endings counted as CRLF, LF and CR separately.
  Conversion decodes strictly: a byte that is not valid in the detected encoding stops that file, nothing is written.
- **Controls**: file list (check, name, encoding found, line ends, result badge), segmented Text encoding (UTF-8, UTF-8 with
  BOM, UTF-16) and Line endings (Keep, LF, CRLF), a preview of the first lines with visible line-end marks, switch Keep a
  backup (`.bak`), Cancel, Convert N files.
- **States**: **Mixed line endings** (warning, counts, Show mixed lines); **Cannot decode** (error, byte and position, choose the
  source encoding by hand); Done (per-file result, Undo from backups).

## 13. Subtitle timing (`SubtitleTiming`)

- **Size**: 800. **Opens for**: `.srt .vtt`.
- **Engine**: own parser and writer, no external tool. Times are integers in milliseconds; shift adds, stretch is a linear map
  from two anchor cues (`new = a + (t - t1) * (b - a) / (t2 - t1)`), convert changes the time separator, header and escapes.
- **Controls**: cue table (number, start, end, text), segmented Shift / Stretch / Convert; Shift: Move all by (seconds,
  signed, millisecond precision) and the result for the first and last cue; Stretch: where the first and last cue should
  start, with the resulting speed factor; Convert: From and To (SRT, WebVTT) with what changes; Save as.
- **States**: shift (main), Stretch, Convert, **Problem cues** (overlaps and unreadable times listed; Fix overlaps, Skip broken
  cue). A shift that would push a cue before 00:00:00 is blocked with the cue named.

## 14. Font preview and install (`FontInstall`)

- **Size**: 780. **Opens for**: `.ttf .otf .ttc .woff`.
- **Engine**: WPF `GlyphTypeface` loaded from the file URI for the preview and metadata (the preview installs nothing).
  Install for the current user only: copy to `%LOCALAPPDATA%\Microsoft\Windows\Fonts`, write the name under
  `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Fonts`, then `AddFontResource` and a `WM_FONTCHANGE` broadcast. No
  administrator rights, no system folder. Uninstall removes the same file and value. A collection (`.ttc`) shows a face picker.
- **Controls**: editable sample text, specimens at 72, 36, 18 and 12 px, card (Family, Glyphs and scripts, Licence from the
  name table), switch Install for this user only (on, and the only option offered), Cancel, Install.
- **States**: Done (location, Uninstall), **Already installed** (same file; Uninstall), Unreadable file (error).

## 15. Disc image (`DiscImage`)

- **Size**: 780. **Opens for**: `.iso` (and `.img .vhd .vhdx` where the OS can mount them).
- **Engine**: Windows disk image mount through the Storage management API (`MSFT_DiskImage` `Mount` and `Dismount`, read-only)
  with no elevation for ISO; the drive letter is read from the volume. Browse and extract without mounting uses the bundled
  7-Zip listing. Mounting from a network share or an unsupported image fails and the dialog offers Browse without mounting.
- **Controls**: state badge (Not mounted, Mounted as E:), contents tree (chevrons on folders, checks on files), Mount as a
  drive, Extract selected (destination chosen like the archive dialog), Browse without mounting, Eject, Open drive, Close.
- **States**: Ready, Mounting (indeterminate bar), Mounted ("Leaving this window open does not unmount it"; Eject is a separate
  action), **Could not mount** (error with the reason and Browse without mounting).
- **Rule**: closing the dialog never ejects; a mounted image the app mounted is listed in the tray menu until ejected.

---

## Process record

- **Stageshow**: commit `299140f` of the Stageshow skill, as named in the brief (the installed copy has no version metadata to confirm it). Read in full: `SKILL.md`,
  `references/craft.md`, `references/framing.md`, `references/rehearse.md`, `references/review.md`.
- **Plan first**: each dialog got a written plan (focal object, states, what is real text) before the generator was written.
  Real words only where the story needs them; the Ready state carries the working controls, the other states reuse one shell.
- **Closest gold example**: `finder` (a desktop window with a file list, sidebar and real chrome) for the list and window
  anatomy, with `controls` and `parts` for the control specimens. Kept from them: one icon family at one stroke width, 12 px
  inset, one grid per list, status colours that mean status, quiet neutral surfaces with one accent.
- **Rules that do not apply to real app dialogs** (recorded, not followed): the hero crop and bleed rules, one gutter measured
  against the stage edge, "a window must run off an edge", satellites straddling the hero, the plate rules, loop and cursor
  choreography, the `Stage` and `Act` runtime, the 390 px phone `compact` design, reduced-motion posters, and the rule against
  real headings (these boards use real headings and landmark roles because they document windows). Windows are shown whole at
  1:1 with air around them because the artboard shows several states of one dialog; this is the specimen-sheet case, not a
  product shot.
- **Rendering**: `tools/render.mjs` uses Playwright Core with the installed Microsoft Edge in headless mode, which creates no
  window and never touches a visible or hidden desktop. The lowlevel hidden-desktop route was not needed for static pages.
  Frames are 1.0 scale, light and dark, 30 files in `frames/`. The render script also checks overflow of text inside every window
  (`no overflow problems` is its last line) and writes `heights.json` for the canvas sizes.
- **Fonts**: Roboto and Roboto Mono, latin and latin-ext subsets, copied from the repository's `site/src/fonts`. The render
  confirms both families loaded.

### Fresh-eye review (independent reviewer, frames only) and what happened

The reviewer saw all fifteen dark frames and seven light frames and returned 37 items. Fixed:

- Voids under left columns in Image privacy (more pictures and a summary card), Video to GIF (preview grows with the settings column),
  Make an icon (preview grows, padding slider added), Resize (preview grows).
- Disabled primary buttons were pale tinted primaries with unreadable labels: now the Material 3 disabled treatment.
- Highlight chips for hash and error marks were low contrast: now filled `Danger`, `Mint` and `Amber` with dark labels.
- Archive and disc tree columns did not line up with their headers (row and header grid mismatch): fixed.
- Destructive actions were styled differently (Delete the archive, Uninstall): both now use the red tonal style, like Delete file.
- Tertiary text buttons looked orphaned: they now have an outline.
- Mounting showed a 28 px word instead of a percentage: smaller label for the indeterminate case.
- Disc tree showed checkboxes on folders: folders now have chevrons, files have checks.
- Audio fade sliders read as one phrase: larger gap.
- Font size labels too small: 12 px, full contrast. Inconsistent truncation of the thumbprint: made consistent.
- Format in place contradicted "original is never changed": the footer now says which action rewrites the file.
- Make an icon's "real size" label was untrue for 128 and 256: relabelled.

Left as is, with the reason: the Working and Done states are shorter than Ready, so the 2×2 board grid has uneven rows (board layout,
not a dialog fault); the Disc image footer keeps its action row in the body because Mount, Extract and Browse are the primary
choices there and Close is the only footer button; amber in the light theme is a darker brown-orange by design of the token pair;
the board header pills are board chrome; small hint text stays at 12 px, the app's own hint size.

### Not verified

No WPF implementation exists for any of these dialogs: nothing here proves an engine works, that a keyframe cut is lossless on a
given file, or that the Windows APIs behave as described. Contrast was judged by eye and by the reviewer, not measured. Light-theme
amber and danger roles are proposals. The reviewer did not view the light frames of Audio, Resize, Make an icon, Extract, Data
viewer, Subtitle, Font and Disc image; the generator uses the same tokens for both themes, and the render script found no overflow
in any of them.

### Cross-state consistency pass

Every board was checked so that all four states describe the same file or batch, with matching counts, sizes and times: Resize is one batch of 8 (27.4 MB to about 5.1 MB) in every state; Image privacy is one batch of 5 pictures, 4 with hidden data; the video trim file is 1:12 and 340 MB so the timeline ruler, kept length and sizes agree; the audio gain note now says a limiter holds the peak; the archive Done card matches the Keep both rule; the expiring certificate is its own file (mail.example.org.cer) with its own dates; the encoding batch counts match the chosen target; the subtitle stretch factor and last-cue result were recomputed.
