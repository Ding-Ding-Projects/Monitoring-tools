# Closeout prompt

Copy the block below to continue this work in a fresh session. It reflects the repository state at
the commit that contains this file.

```
Project: Monitoring Tools, a Windows tray app (.NET 8, WPF, single-file). Repository:
Ding-Ding-Projects/Monitoring-tools. Read README.md, ROADMAP.md, HANDOFF.md, AGENTS.md and
docs/features/required-features.md first.

State (2026-11-04 evening): main builds; full self-test 180 of 180; every push to main ships a
normal release (latest v1.0.36, installed on the maintainer's computer).

Done and checked this session: background updates (download, SHA-256 digest and version check,
install at a quiet moment, a game left idle 10 minutes no longer blocks); "game detected" and
"game closed" notices; Minecraft bulk player actions on a searchable multi-select list, SSH
players, plugins, backups and worlds, Bedrock on Docker and SSH, item and NBT editors, live plugin
and mod commands, peak history; pre-generation stall fixed (empty servers pause after 60 s since
1.21.2; staging now sets pause-when-empty-seconds=0); language, humour and regex explanations fully
translated; encrypted local history; QR up to version 40; real pointer-click checks.

Next steps:
0. New request, not started: when a copied image holds an otpauth:// QR code, offer to register it
   for TOTP codes (secret encrypted for this Windows account, current code with countdown, copy).
   The QR decoder already exists (QR pairing); a TOTP routine exists for element locks.
1. Branch worktree-agent-aa24b41c13dfee312 holds unfinished work: SSH world import, reset and
   replace from the app, and Forge and NeoForge live runs. Read its last commit message for what is
   finished, finish it, run the full self-test (--selftest on a hidden desktop), and merge to main.
2. Confirm a real background update: the release after v1.0.36 should install itself on the
   maintainer's computer; check the log in %LOCALAPPDATA%\MonitoringTools\logs for "Update:" lines.
3. The history retention self-test check failed twice in other runs; it now names the refused save.
   If it fails again, fix the cause it reports.
Needs the maintainer: native Cantonese review, FIDO2 key, pen or touch device, phone camera, real
Windows Hello prompt, approval to download glab and tea for the forge checks.
```
