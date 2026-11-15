# Packing and Compose verification

## Source and build

- Application source: `0536b2489faac68410c11a04f960bb42d8dbced2`.
- Built through `build-installer.bat 1.0.75` on the selected physical Windows host.
- Executable SHA-256: `8632a4c54984cb0cb120e47b2b22dd4cbc39f0e38329acfd2ac91ece5aaa5655`.
- Executable size: 214,866,612 bytes. Product version: 1.0.75.
- Route: installed direct cheap Lowlevel CLI, native background window capture, named isolated desktops.
- Actual DPI: 144 (150%). Dark theme, English. Packing window: 828 × 679; Compose: 1308 × 1191.
- Exact image-capture instants are unavailable. Live-run receipts and result reports record their own
  actual times. No date is inferred from an image filename or filesystem timestamp.
- Captures were inspected. Profile prefixes are masked by the application; generated test files and
  an isolated test service are shown. No personal clipboard content or credentials are included.

## Results

- [Packing: 14 passed, 0 failed](packing-checks.json), including direct ZIP readback, overlap and empty-folder
  handling, collision races, repeated action activation, live language switching, diagnostic path
  display, missing and locked inputs, cancellation and declined-consent simulation.
- The same run created a real dynamic VHDX, verified 2 of 2 source files by SHA-256, preserved their
  original hashes and confirmed the final disk was detached through two independent observations.
- [Real Compose startup: 1 passed, 0 failed](compose-live-checks.json). The built dialog and controller
  used the real Docker CLI against an isolated Alpine service. It became healthy; a separate CLI
  query agreed. The test project was then stopped and removed without touching unrelated projects.
- [Dialog fixture checks: 4 passed, 0 failed](compose-fixture-checks.json) on the earlier built source
  `cd2c2fdf1840c56d465d02e63372534ea416f6d3`. This specifically proves live language switching,
  dialog cancellation and redaction behavior against a deterministic process fixture. It is not
  live Docker evidence and is not relabelled as evidence for later revisions.
- Focused console Compose controller checks passed for preflight ordering, service-health verdicts,
  contextual diagnostics, redaction, scoped repair validation, nonzero exit, timeout and cancellation.

## Existing-disk addition

`packing-append.png` is a genuine reopened result from source
`4bf231a37104fc70b1432a5f240a9ac65b2a2b39`, executable SHA-256
`46792337685d54c0881e696b76ffe534d512ee79ba35a3e4a952bfd12505f3f0`.
It is 828 × 854 pixels, dark English, 144 DPI, captured through the same native background route.
The test explicitly reopened the completed result for inspection; production success dismissal
was not disabled. Its validated result-hold interval began at 2026-11-15T05:27:03.7666649Z and
lasted 120 seconds. The exact capture instant within that interval is unavailable.

The native transaction succeeded: 2 existing user files and 2 newly added files verified,
the colliding incoming root was numbered, cancellation during staging preserved the original,
and the retained rollback copy matched the original disk bytes. A test-only ordering error in
an unrelated overlap fixture caused that run's overall report to remain 22 passed, 1 failed.
The unchanged engine verdict is not a claim that the whole suite passed. The fixture ordering
was corrected afterward and its final rerun is pending.

Earlier source-bound captures are listed in [the archive](ARCHIVE.md).

## Verification limits

The complete display-scale and minimum-layout matrix, assistive-technology interaction, native
consent cancellation, automatic VHDX growth under actual low-space conditions, crash recovery and
every live Compose failure/repair scenario were not run. Generated announcement images are private
presentation material and are excluded from this evidence inventory.

The existing installer entrypoint produces the project's self-installing application and ZIP.
It is not genuine Squirrel.Windows packaging; no Squirrel installation/update claim is made here.
