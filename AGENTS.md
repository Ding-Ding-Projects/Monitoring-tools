# Agent guide

## Agent conversation vocabulary

Agents working in this repository use the shared private conversation vocabulary defined in
the maintainer's canonical agent instructions for every reply, including chat prose, visible
reasoning, status lines, and task or session titles. Read those instructions and apply them
in full. They are deliberately not restated here, because this file is part of this
repository's public record.

That vocabulary never appears in anything published. Keep it out of commit messages, branch
names, code, code comments, test names, documentation, this file, issues, pull requests,
discussions, release notes, published sites, logs, and every other file in this repository.
Published writing uses ordinary professional English, and exact technical identifiers always
keep their literal spelling. The single documented public-safe exception is named in those
canonical instructions; do not infer any other.

Scan any text bound for a public surface against that vocabulary before publishing it. A
reviewer cannot tell a correct release note from a leaking one by reading it, so the scan is
a step, not a habit.

## Working in this repository

- `download-dependencies.bat` (or `.sh`) fetches every pinned, digest-verified dependency into `.deps/`.
- `build.bat` builds the single-file app with every dependency bundled; `build-installer.bat` produces `dist/MonitoringTools-Setup.exe`.
- `scripts/check-vocabulary.ps1` runs first in `build.bat` and in the `pre-push` hook (`git config core.hooksPath .githooks`).
- Every push to `main` builds on Windows, renders screenshots of the real app, commits them back, publishes the next release and deploys the project site.
- Commits use the author `Claude Fable 5.1 <noreply@anthropic.com>` with the matching `Co-Authored-By` trailer, and bilingual English and Cantonese messages.
