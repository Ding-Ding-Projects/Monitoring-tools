# Progress and recovery

`OperationCard` is a reusable card that shows a long operation where it was started.

- Named steps with a text state (done, skipped, failed) as well as a glyph, a progress bar, and a polite live message for screen readers.
- Cancel while running. The card refuses a second run in its handler as well as disabling the submit control, so a keyboard submit cannot start a duplicate.
- On failure it shows a plain message with Retry, Copy details and any operation-specific recovery action beside the control that failed.
- Optional slow phases are offered as a choice by the feature (for publishing, "Upload after committing") with the consequence of declining stated.

Used by publishing. Status: compile-checked only.
