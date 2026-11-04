# Closeout prompt

Copy the block below to continue this work in a fresh session. It reflects the repository state at
the commit that contains this file.

```
Project: Monitoring Tools, a Windows tray app (.NET 8, WPF, single-file). Repository:
Ding-Ding-Projects/Monitoring-tools. Read README.md, ROADMAP.md, HANDOFF.md and AGENTS.md first.

Latest change (2026-11-04): motion and page redesign. Design canvas in design/motion-and-pages
(HANDOFF.md there lists every decision and what was not checked).
- UI/Ui.cs: motion helpers SharedAxis, RiseAll, Expand, Glide, Liftable, LiveDot, Pill; all respect
  reduce motion.
- ControlCentreWindow.Navigate: shared-axis page change toward the tab you moved to; cards rise.
- Ollama page: status hero with a breathing dot and three numbers; model rows with chips; GPU/CPU
  split bar for loaded models; pull progress glides.
- Minecraft page: server cards with status pill, metadata chips, console panel holding the command
  builder, settings drawer; header counts; empty state. New server guide unchanged.
- Conversion page: one searchable list of 39 formats with kind filters and Convert files per row
  (UI/ControlCentreWindow.Conversion.cs); Media/Formats.cs grew from 19 to 39 formats; more source
  extensions recognised in Clip/ClipItem.cs.

Verified: compiles on Linux with EnableWindowsTargeting (0 errors); vocabulary scan of every added
line is clean.
Not verified: the app was not run; the new FFmpeg encoders (libx265, libsvtav1, libjxl, qoi,
jpeg2000, wmv2, ac3) were not exercised against the bundled build.

Next steps:
1. Check the push-to-main run: build, screenshots of the Ollama, Minecraft and Conversion pages,
   release notes.
2. Convert one sample file to each new format on Windows and fix any encoder the bundle lacks.
3. Continue the open items in ROADMAP.md.

Rules: ordinary professional English in all repository text; no secrets; bilingual commit messages
with the Co-Authored-By trailer; builds go through build.bat and build-installer.bat.
```
