# Archive, subtitle, font and disc image dialogs

Four separate windows that open from the clipboard notice when one file of the matching type is copied. Each shows the
file name in its header, never changes the original file, runs its work off the interface thread and says plainly what it did
not change when something fails. They follow the design in `design/file-tools/` (boards 7, 13, 14 and 15).

| Copied file | Notice button | Window |
| --- | --- | --- |
| `.zip .7z .rar .tar .gz .tgz` | Extract… | Extract an archive |
| `.srt .vtt` | Subtitle timing… | Subtitle timing |
| `.ttf .otf .ttc .woff` | Preview font… | Font preview and install |
| `.iso .img .vhd .vhdx` | Disc image… | Disc image |

## Extract an archive

* **Formats.** Zip, tar, tar.gz, tgz and gz are read with the .NET libraries. 7z, rar, xz and bz2 need 7-Zip, which this build
  does not bundle, so those open a "cannot be opened here" state that says so and changes nothing. If a later build bundles 7-Zip
  the dialog can list and extract them through it.
* **Contents.** A tree with check marks (a folder's check selects everything under it and shows a mixed state), sizes and dates,
  Select all and None, and a search with the same plain text and regex builder as every other search. Up and Down move between rows,
  Right and Left expand and collapse, Space toggles, `/` or Ctrl+F focuses the search. Only the first 1,000 visible rows are drawn;
  the list says when more exist and the search narrows it.
* **Destination and names that exist.** The default is a new folder named after the archive. The rule is Skip, Replace or Keep both
  (a number is added). The "Ask" rule of the design is not built.
* **Safety.** Every entry path is checked against the destination first. Absolute paths, drive letters and `..` are refused
  (zip-slip), marked in the list, and counted in the Done card. Each file is written under a `.part` name and renamed when whole, so
  Cancel never leaves a half file under a real name.
* **Passwords.** Zip entries protected with the traditional zip password scheme are decrypted by the app. The password is checked on
  the first locked file before anything is written, is used once and is not stored. AES-encrypted zip entries need 7-Zip and are reported as such.

## Subtitle timing

SRT and WebVTT are read by the app's own parser; times are whole milliseconds.

* **Shift** adds a signed number of seconds to every cue. A move that would put a cue before 00:00:00 is blocked and names the cue.
* **Stretch** maps the first and last cue to two chosen times (`new = a + (t - t1) * (b - a) / (t2 - t1)`) and shows the speed factor.
  Frame rate presets (23.976 to 25, 25 to 23.976, 24 to 25, 25 to 24, 29.97 to 25, 25 to 29.97) fill the two times.
* **Convert** writes SRT or WebVTT. Converting to SRT drops WebVTT cue settings and NOTE, STYLE and REGION blocks and lists what it dropped.
* **Problem cues.** Overlaps and unreadable times are listed. Fix overlaps ends the earlier cue one millisecond before the next
  starts; Skip broken cue removes cues whose time cannot be read. A file with problems can still be shifted.
* The result is saved next to the original under a new name (`.shifted`, `.stretched` or the new extension); an existing name gets a number.

## Font preview and install

* The preview loads the file with WPF `GlyphTypeface` and shows it at 72, 36, 18 and 12 px with editable sample text. It installs
  nothing. Family, face, weight, glyph count, scripts, licence text and version come from the font's own name table.
* **Install** is for this user only: the file is copied to `%LOCALAPPDATA%\Microsoft\Windows\Fonts`, its name is written under
  `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Fonts`, then it is added with `AddFontResource` and a font-change message is
  broadcast. No administrator rights and no system folder. **Uninstall** removes the same file and value.
* An already installed copy (same bytes, same registry value) opens in an "already installed" state with Uninstall. A damaged file, or a WOFF
  web font, opens in an error state and can never reach the installer. A font collection (`.ttc`) shows its first face only; there is no face picker yet.

## Disc image

* **Browse and extract without mounting** uses a built-in ISO 9660 reader with Joliet names. It lists folders and files, and copies the chosen
  files out byte for byte. Images that are not plain ISO (`.img`, `.vhd`, `.vhdx`, or a UDF-only ISO) cannot be listed; they can still be mounted.
* **Mount as a drive** runs `Mount-DiskImage -Access ReadOnly` for the current user (no elevation) and reads the drive letter. Eject runs
  `Dismount-DiskImage`. Closing the window never ejects. An image that is already attached opens as mounted.
* A mount Windows refuses shows "Could not mount" with Windows' own reason and a Browse without mounting action.
* Not built: the tray menu does not yet list images this app mounted (the app keeps the list in `DiscImageTools.MountedByApp`).

## Verification

The self-test group `MT_ONLY=docsys` drives each window on files it creates: a zip with zip-slip entries, a password zip, tar, tar.gz, gz and 7z;
SRT and VTT samples for shift, stretch, convert and problem cues; a font file copied from the system fonts folder (the install step is replaced
by a recorder, so no font is installed); and an ISO written by the check, which is read back, extracted, and mounted for real through Windows
and ejected again. It also checks that every visible string in the four windows has a Cantonese entry. Captures: `docsys-extract-*`,
`docsys-subtitle-*`, `docsys-font-*` and `docsys-disc-*` in [screenshots](../../screenshots).

![Extract an archive](../../screenshots/docsys-extract-ready.png)
![Subtitle timing](../../screenshots/docsys-subtitle-stretch.png)
![Font preview and install](../../screenshots/docsys-font-ready.png)
![Disc image](../../screenshots/docsys-disc-mounted.png)
