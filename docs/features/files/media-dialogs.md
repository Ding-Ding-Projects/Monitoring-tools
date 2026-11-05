# Media file tool dialogs

Six separate windows open from the clipboard notice when media files are copied. Each one is its own window (not part of the
control centre), is named "tool: file name" for assistive technology, works off the UI thread, and never overwrites anything:
a name that exists gets a number added, output is written to a `.part` file and renamed only when the engine finished cleanly,
and Cancel (or Esc) stops the work and deletes the unfinished file. The design is in `design/file-tools/`.

| Notice button | Opens for | Dialog |
| --- | --- | --- |
| Trim… | `.mp4 .mkv .mov .avi .webm .m4v` | Trim and cut video |
| To GIF… | the same video types | Video to GIF or short clip |
| Edit audio… | `.wav .mp3 .flac .m4a .ogg .aac` | Trim, fade and loudness |
| Resize… | `.jpg .png .webp .bmp .gif .tif .heic`, one or many | Resize, crop and compress |
| Privacy… | `.jpg .png .tif .webp .heic`, one or many | Image privacy clean-up |
| Make icon… | `.png .jpg .bmp .gif .webp .svg` | Make an icon |

The buttons sit right after Convert… on the notice. Several copied files earn Resize… and Privacy… only when pictures are among them.

## Trim and cut video

FFmpeg and FFprobe from the bundled copy. A timeline of 16 frames, two focusable handles (Left and Right move one frame, Ctrl one second,
Shift ten frames, Home and End jump to the ends of the media), a playhead, and `I` and `O` to set the points at the playhead.
**Keep selected** makes the clip between the handles; **Remove selected** cuts that section out and joins the rest.

- **Lossless**: key frames come from the packet flags (no decoding). When the point the kept piece starts at sits on a key frame (and the end sits on one
  or at the end of the file) the cut is a stream copy: no re-encode, no quality loss. A removal is two copies joined with the concat demuxer.
- **Exact cut**: when a point is between key frames the banner says so and the cut re-encodes (libx264 CRF 18, audio copied; WebM uses VP9 and Opus; a removal
  uses one filter graph). The switch Exact cut instead forces it.
- **Failure modes**: a damaged file shows the error view with FFmpeg's own message and what was not changed; a stop in the middle reports the time it stopped at
  and offers Try again with exact cut. No output is left behind in any failure or cancel.

## Video to GIF or short clip

GIF is two passes (`palettegen` then `paletteuse` with Bayer dither), animated WebP uses libwebp, Short MP4 is libx264 without audio. Range, width and
frame rate are typed or set with the timeline handles; Up and Down in the width field change it by 16 and in the frame rate field by 1.
The estimate under the preview comes from a quick low-effort encode of the first seconds, scaled up.

**Fit a target file size** tries the settings as typed and then steps width and frame rate down together (480 px 15 fps, 400 12, 320 10, 240 8, 160 8) until the
result is under the limit. When even the smallest step is over, nothing is saved, the tried sizes are listed, and Switch to Short MP4 is offered.

## Trim, fade and loudness

The waveform is decoded in .NET from low-rate mono PCM. Fades are `afade`. Loudness is two passes of `loudnorm`: pass one measures the edited audio
(and the whole file is measured when the dialog opens, cached per file), pass two applies exactly the measured values with the chosen target and a true-peak
limit of -1.0 dB; the hint under the chips says how many decibels it raises or lowers and whether the limiter has to act. The result card reads the new file
back and shows loudness and true peak before and after. The format follows the file name (WAV, FLAC, MP3, M4A, OGG).

## Resize, crop and compress

WPF imaging with the Fant filter. Keep proportions fits each picture inside the box and never enlarges; without it the size is exact. Crop chips cut a centred
1:1, 4:3 or 16:9 area, and Free crops to the shape of the width and height typed. Quality sets the JPEG or WebP quality; Target size lowers it per picture to the
highest value that fits, never below 40. The before and after preview encodes the chosen quality for real on a reduced copy. Copies are named `name-small`, next to
the original or in the chosen folder; originals are never changed. One damaged picture does not stop the others and can be retried.

## Image privacy clean-up

Reads and removes location, camera and lens (with serial numbers and the maker note), dates, author and notes (author, copyright, software, comments, XMP, IPTC),
the colour profile and the embedded thumbnail. JPEG, PNG and WebP are edited inside the file, so the picture data is not re-compressed; TIFF is saved again losslessly.
Orientation is always kept so the picture looks the same. Every clean copy is read back and the chosen groups must really be gone, otherwise it is not kept.

- **Privacy rules**: nothing is uploaded; the "map" is a drawing and no map service is contacted; coordinates are shown only inside this window and are not logged, copied,
  or part of Copy details.
- **Keep the original** off replaces each file with the clean version and moves the original to the Recycle Bin.
- HEIC cannot be read or cleaned in this version and is listed as unsupported.

## Make an icon

An `.ico` built by hand in .NET: each size is rendered with a high-quality filter, stored as PNG, and written into the directory records (256 is stored as 0).
Options are the sizes (16 to 256), transparency, padding (0 to 40%) and shape (as is, rounded square, circle). A source smaller than a size flags it and offers to
turn 128 and 256 off; continuing is allowed. SVG sources are drawn by a small built-in renderer (rect, circle, ellipse, line, polyline, polygon, path, fill, stroke,
opacity, transforms, nested groups, viewBox) at 512 px; gradients use their first stop colour, and filters, masks, clip paths, text and CSS classes are not drawn.

## Verification

`MonitoringTools.exe --selftest <folder>` runs these on generated media (FFmpeg lavfi sources and pictures made in code, including JPEG, PNG and WebP files with
hand-built metadata), by filling the real dialogs and pressing their real primary buttons: lossless, exact and remove cuts checked by duration and codec; Esc during a
run leaves no files; GIF size and length from FFprobe; a target-size search that must step down and a target that cannot be met; loudness checked with the
`ebur128` filter and fades with `volumedetect`; resized pixel sizes, a quality search that fits a byte target, a damaged picture in a batch; metadata checked by raw
byte search, by WPF's own metadata reader and by pixel comparison; ICO directory, decoded frames and pixel values; the notice buttons; names, Close buttons and Esc;
and a Cantonese language audit of every state of every dialog. `MT_ONLY=media` runs only these. `--screenshots` captures `media-*` states.

## Not done

- HEIC pictures are not read or cleaned (no decoder in the box).
- The loudness limiter path (a gain that would clip) is covered by the hint arithmetic but not exercised by a clipping source.
- Animated GIF input to Resize is refused rather than resized frame by frame.
- Preview playback uses the Windows media element, so formats it cannot play show a message; the cut itself is unaffected.
- Only the Resize dialog accepts files dropped on it; the others open from the notice.
