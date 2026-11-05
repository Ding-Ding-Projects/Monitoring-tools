# Handoff

Factual state of the project for the next person or agent. Claims here are limited to what was
checked; anything unchecked is labelled.

## Current state

- Windows tray app (.NET 8, WPF, single-file) with clipboard toasts, quick commands, internet watch,
  control centre, regex builder, Minecraft server manager and the other features listed in
  [README.md](README.md). The checklist of shipped and open items is [ROADMAP.md](ROADMAP.md).
- Every push to `main` builds through `build-installer.bat`, renders screenshots of the real app,
  publishes the next normal release and deploys the site (`.github/workflows/build.yml`).
- Latest release at the time of writing: v1.0.36 (see [CHANGELOG.md](CHANGELOG.md)).

## State on 2026-11-04 (evening)

- **Checked:** full self-test 180 of 180 on `main` at 823d9a8; releases v1.0.17 to v1.0.36 all green
  and published as normal releases; v1.0.36 installed on the maintainer's computer and running.
- **Background updates:** the installed copy checks 90 s after start and every 6 h, downloads,
  verifies the SHA-256 digest and the file's own version, and installs at a quiet moment. Seen working
  in the real app: v1.0.31 found, downloaded and verified v1.0.32 in 10 s. A game left open with no
  input for 10 minutes no longer blocks it (fixed in v1.0.36). **Not yet seen:** the swap itself
  completing on its own; the next release after v1.0.36 is the first chance.
- **Game notices:** "Roblox detected" and "Roblox closed", checked by the self-test.
- **Minecraft:** bulk player actions on a searchable multi-select list; SSH players, plugins,
  backups and worlds; Bedrock on Docker and over SSH; item and NBT editors; live plugin commands;
  peak history. The pre-generation stall's cause is proven (an empty 1.21.2+ server pauses after
  60 s) and fixed with `pause-when-empty-seconds=0`.
- **Unfinished:** SSH world import, reset and replace from this app, and Forge and NeoForge live
  runs, were in progress on branch `worktree-agent-aa24b41c13dfee312` when work stopped (see that
  branch's last commit for its state). The history retention self-test check failed twice in other
  runs and never in four runs here; it now reports the exact refused save if it fails again.
- **Requested, not started:** register a TOTP account when a copied image holds an `otpauth://` QR
  code (see ROADMAP.md).
- **Needs the maintainer:** native review of the Cantonese, a FIDO2 key, a real pen or touch screen,
  a real phone camera, a real Windows Hello prompt, and approval to download the `glab` and `tea`
  command-line tools for the forge checks.

## Motion and page redesign (2026-11-04)

- Design canvas and its handoff: [design/motion-and-pages](design/motion-and-pages/HANDOFF.md).
- Shared motion helpers in `UI/Ui.cs`; shared-axis page changes in `ControlCentreWindow.Navigate`.
- Redesigned Ollama, Minecraft servers and Conversion pages; Conversion is now a searchable list of 39 formats.
- **Checked:** compiles with `EnableWindowsTargeting` on Linux (0 errors). **Not checked:** running the app, and the
  new FFmpeg encoders against the bundled build. The first Windows build and its screenshots are the real test.

## Release pipeline additions (written, not yet run on the build machine)

- `scripts/line-counts.mjs` counts lines per category and language at a commit, with surviving-line
  authorship from `git blame`. Run locally: it printed 36,460 project lines at commit `e8933df`, and
  its blame total matched. Runtime on this machine: about 45 seconds.
- `scripts/release-notes.mjs` builds the release notes: dim sum code name with a linked public photo
  (only if a HEAD request confirms the photo is published), workflow start, end and duration, the
  checks that actually ran, the line-count table and the human-time estimate with its arithmetic.
  It also refreshes the README figures block. Run locally with sample timing: output looked correct.
- `scripts/dim-sum-code-names.json` lists 120 dishes (exact English and Traditional Chinese names)
  whose photos were published in the public catalogue when the list was written.
- `scripts/changelog.mjs` regenerates [CHANGELOG.md](CHANGELOG.md) from tags and commits.
- The workflow now publishes with these notes, edits the release once more with the final end time,
  then commits the README figures and changelog back to `main`.

**Not verified:** the workflow changes have been syntax-checked (YAML parse, PowerShell parse of every
step) but have not run on a GitHub runner. The first push to `main` is the real test. Watch the
"Publish release" and "Record release figures" steps.

## Remaining work

- Confirm the first workflow run publishes notes with timing, counts, code name and estimate, and
  that the README figures commit lands.
- Add a `LICENSE` file once the owner picks a licence (the repository has none).
- Items still open in [ROADMAP.md](ROADMAP.md).

## Blockers and external state

- GitHub Pages must be enabled once in repository settings (Source: GitHub Actions) for the `pages`
  job; that job is `continue-on-error`.
- No handoff section has been posted on a GitHub issue by this change; do that when the work is
  merged.
