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
0. Done since: a copied otpauth:// QR picture offers "Register for codes" (built-in authenticator),
   and SSH world import, reset and replace from the app are merged and checked.
1. Forge (2,781 s) and NeoForge (1,529 s) both passed live through the command builder.
2. Background updates are confirmed on the maintainer's computer (v1.0.36 to v1.0.37 to v1.0.40 by itself).
3. The history retention check failed in 3 of ~9 full runs before the millisecond naming change and 0 of 4 since;
   not proven fixed. If it fails again, its message lists every kept version.
Needs the maintainer: native Cantonese review, FIDO2 key, pen or touch device, phone camera, real
Windows Hello prompt, approval to download glab and tea for the forge checks.
```
