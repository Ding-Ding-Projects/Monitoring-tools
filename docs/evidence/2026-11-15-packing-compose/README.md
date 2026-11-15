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

## Limits

The complete display-scale and minimum-layout matrix, assistive-technology interaction, native
consent cancellation, automatic VHDX growth under actual low-space conditions, crash recovery and
every live Compose failure/repair scenario were not run. Generated announcement images are private
presentation material and are excluded from this evidence inventory.

The existing installer entrypoint produces the project's self-installing application and ZIP.
It is not genuine Squirrel.Windows packaging; no Squirrel installation/update claim is made here.
