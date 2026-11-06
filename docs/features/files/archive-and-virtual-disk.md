# Archive and virtual disk windows

Two separate windows open from the clipboard toast when files or folders are copied: **Archive…** and **Put in a virtual disk…**. Both are built from the design in `design/clipboard-actions/` (boards 5 and 6).

## Archive

A zip, 7z or tar.gz of the copied items, with every option on the board.

| Option | Behaviour |
| --- | --- |
| Name, Save in | The ending follows the format. A name that already exists is never overwritten; a number is added. |
| Format | Zip, 7z, tar.gz. |
| Compression level | Six named stops: Store, Fastest, Fast, Normal, Maximum, Ultra (default Normal). |
| Method | Zip: Deflate, Deflate64, BZip2, LZMA, Store. 7z: LZMA2, LZMA, PPMd, BZip2, Deflate. |
| Dictionary size, Solid block, CPU threads | 7z only. Dictionary and solid block apply to LZMA and LZMA2 and are off for other methods. |
| Password | AES-256, through the bundled 7-Zip (7z and zip). A strength bar and a show toggle. |
| Encrypt file names | 7z only, and only with a password. |
| Split into volumes | Size in MB or GB, with presets 700 MB, FAT32 (4,095 MB, because a FAT32 file stops one byte short of 4 GiB) and DVD 4.4 GB. Volumes end in `.001`, `.002`. |
| Include the parent folder | Keeps the folder the items are in as the top folder inside the archive. |
| Exclude patterns | Chips. Glob (`node_modules`, `*.tmp`, `build/**`) or regex tested against the relative path. Regex mode shows the app's full regex builder. |
| Test after creating | On by default. |
| Delete the originals | Off by default. After the test passes, the app's two-key confirmation opens; the originals go to the Recycle Bin and the archive stays. It holds the test switch on while it is on. |

The summary strip above the buttons shows the files, size, an **estimate** of the result (from the file types and the level), the settings, the number of volumes, the free space needed and a time. It is labelled as an estimate everywhere.

### Which code writes what

- **Zip** without a password, volumes or an extra method is written by `System.IO.Compression`. Levels collapse to four there (Fast is Fastest, Ultra is Maximum); the summary says so.
- **tar.gz** is written by `System.Formats.Tar` and `GZipStream`. gzip has no store mode, so Store becomes the fastest level.
- **Everything else** (7z, encrypted zip, volumes, the extra zip methods) is written by the bundled `7za.exe`.
- The **test step** for an archive written by the app reads every entry back and compares its SHA-256 with the source file. For 7-Zip output it runs `7za t` (with the password) and requires "Everything is Ok".
- **Cancel** stops the work and removes the partial archive, including 7-Zip's `.tmp` volume.

### Security notes

- 7-Zip receives the password on its command line for the seconds it runs; another process of the same Windows account could read it then. It is never saved, logged or shown, and the logged argument list never contains it.
- Windows Explorer cannot open an AES-256 zip or an LZMA zip. 7-Zip can. The summary says so when a zip password is set.

## Virtual disk

A VHD or VHDX file, mounted and formatted, with the items copied or moved in.

Creating and attaching needs administrator rights. The window states this first. When **Create disk** is pressed the app starts **itself** once, elevated, with `--vhd-create <request.json> <sha256>`, behind the Windows consent prompt. The app never answers that prompt. The helper:

1. refuses the request unless its SHA-256 equals the one on its command line (which cannot change after launch) and unless the request passes the same strict validation as the window (extension, file system, size, label, drive letter or folder, account identifier, status files inside the app's own folders). The diskpart scripts are written from validated values only;
2. runs diskpart to create and attach the disk, then to make a GPT partition, quick format it and assign the drive letter or folder;
3. for NTFS, grants the account that asked full control of the new volume;
4. reports each phase in a status file and stays running, so **Cancel**, **Detach** and **After: Detach** need no second prompt. When the window's process disappears the volume is left mounted as it is.

The normal app then copies the items onto the volume itself (as the normal user), checks every file's size, and for **Move** asks for the two-key confirmation before the originals go to the Recycle Bin. Every failure detaches the disk and deletes the file the run made.

FAT32 is disabled above 32 GB (Windows formats FAT32 only up to 32 GB here) and when a selected file is over 4 GB. VHD is limited to 2,040 GB, VHDX to 64 TB. A fixed disk is refused when the drive does not have the room. A declined prompt shows exit code 1223 and says nothing was created.

## Bundled 7-Zip

7-Zip 26.05 (`7za.exe` from the official extra package, LGPL, contains no unRAR code) is pinned in `deps/manifest.json` by SHA-256 and fetched by `download-dependencies` like FFmpeg. The package is a `.7z` file, so the pinned `7zr.exe` (also digest-pinned, a build tool that is not bundled) opens it. A build without `.deps\7zip` keeps zip and tar.gz and switches the 7-Zip-only options off with the reason.

The full 7-Zip 26.05 command line (`7z.exe` with `7z.dll`, taken from the official `7z2605-x64.exe` package, which `7zr.exe` also opens) is pinned the same way as `7zip-full` and bundled only to read RAR archives in the Extract an archive window and in archive search. Its licence is LGPL-2.1-or-later with the unRAR licence restriction: the unRAR code in `7z.dll` is used only to decode RAR archives and may not be used to create a RAR compressor; the app never writes RAR. Both licence texts (`License.txt`) travel inside the app next to the programs they cover.

## Verification

Self-test checks (run with `MT_ONLY=archivedisk`, or as part of the full self-test) drive the real windows and read the results back independently. Nothing in the self-test creates, attaches or formats a real virtual disk: `VhdHelper.Diskpart` and the elevation are replaced by test hooks, so the request file, the scripts, the status and command files, the copy, the confirmation and the cleanup run for real and diskpart does not.

The real route is checked by the live check `MonitoringTools.exe --live-check <folder>` with `MT_LIVE_ONLY=vhd`, which shows the Windows consent prompt for the person to answer. On 2026-11-05 it created a 64 MB VHDX, formatted it NTFS, mounted it as Z:, copied 3 files into it from the unelevated app (all identical), then detached and deleted it, in 6.4 s. Its first run found that diskpart does not read UTF-16 scripts: it printed its command list and exited with 0x80070057, so no real disk could be made. Scripts are now written in the console (OEM) code page, diskpart's output is read in the same code page, and a disk path, folder or label with a character that code page cannot carry is refused in the window before the consent prompt.

Captures: `archive-dialog`, `archive-dialog-targz`, `archive-dialog-7z`, `archive-progress`, `archive-done`, `archive-error`, `disk-dialog`, `disk-dialog-fat32`, `disk-dialog-folder`, `disk-progress`, `disk-done`, `disk-error`. The progress, done and error captures are the real views filled with fixed numbers.
