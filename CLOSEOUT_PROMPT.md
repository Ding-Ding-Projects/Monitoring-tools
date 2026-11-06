# Closeout prompt

Copy the block below to continue this work in a fresh session. It reflects the repository state at
the commit that contains this file.

```
Project: Monitoring Tools, a Windows tray app (.NET 8, WPF, single-file). Repository:
Ding-Ding-Projects/Monitoring-tools. Read README.md, ROADMAP.md, HANDOFF.md, AGENTS.md and
docs/features/required-features.md first.

State (2026-11-05 afternoon): main at 1946faf plus this file, pushed; only main exists locally and
on GitHub. Every push to main ships a normal release (v1.0.53 green; later ones not yet checked).

Done and checked this session: 21 file tool dialogs (documents, archive and disk, data and
security, media); accessibility and Material 3 audit with a themed date picker and captured control
states; type and shape scales across the dialogs; regex builder parity 13 of 13 families; 7z, xz,
bz2, AES zip and RAR4/RAR5 extraction (full 7-Zip 26.05, pinned, unRAR licence: decoding only), an
Ask rule, all faces of a .ttc and tray Eject; per-element locks including radio buttons, drop-down
lists, tabs and menu items; Bedrock Download ZIP and folder import through the real dialogs; a Hold
button that keeps a toast until it is closed (toasts group 22 of 22, capture inspected); updater
retries soon after a failed or rate-limited check; Records page errors in its own translated words.

Later (same day): the real virtual disk route was run for the first time (live check
MT_LIVE_ONLY=vhd, consent prompt approved by the maintainer). It found that diskpart rejected the
helper's UTF-16 scripts, so no real disk could ever be made; fixed (console code page), and the live
check then created, formatted, filled, verified, detached and deleted a 64 MB VHDX. Archive and
disk checks 31 of 31. The row "Archive and virtual disk windows" is now done.

Next steps:
1. Intermittent full-run failures (seen once after the locks merge; a second full run passed 367/367): "language: switching mode
   rewrites an open window" and "minecraft commands: the item component editor builds a give
   line..." (lacks max_stack_size). Both pass alone and in the second full run; watch for repeats.
2. Confirm the release for the latest main is green and that the installed copy (1.0.40 at the time
   of writing, 6-hour checks in that version) updates itself.
3. Only ISO 9660 and Joliet images can be listed without mounting (UDF is open).
Needs the maintainer: a physical mouse or pen for the colour picker, context menu and drawing rows; native Cantonese review.
```
