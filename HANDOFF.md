# Handoff

Factual state of the project for the next person or agent. Claims here are limited to what was
checked; anything unchecked is labelled.

## Current state

- Windows tray app (.NET 8, WPF, single-file) with clipboard toasts, quick commands, internet watch,
  control centre, regex builder, Minecraft server manager and the other features listed in
  [README.md](README.md). The checklist of shipped and open items is [ROADMAP.md](ROADMAP.md).
- Every push to `main` builds through `build-installer.bat`, renders screenshots of the real app,
  publishes the next normal release and deploys the site (`.github/workflows/build.yml`).
- Latest release at the time of writing: v1.0.12 (see [CHANGELOG.md](CHANGELOG.md)).

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
