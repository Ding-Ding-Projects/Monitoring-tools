# Guided forms

`GuidedForm` is a reusable step-by-step form: a segmented progress bar, "Step n of N", one step at a time, Back, Next, Cancel (and Esc), a validation message that blocks leaving an invalid step, and a final action that returns either success or a message to show and stay. Steps are built once, so Back keeps what was typed.

## The new-shortcut guide

`ShortcutGuide.Build(created, cancelled)` returns the guided flow for creating a shortcut:

1. **Pick a starting point:** blank or a preset. Each states what it does.
2. **Name it:** validated against the existing rules (letters, digits, - and _, not reserved, not already used).
3. **Choose how it runs:** console window and administrator switches, each with an info button.
4. **Review:** states the exact shortcut, file name and options that will be created, and previews the script. Nothing runs.

Finishing writes the script file, adds the shortcut and saves settings.

## Wiring

The guide is a self-contained control; the shortcuts page must show it in place of the immediate create. The integration lines are in the task report and are not applied by this change.

## Not built

Save and resume of a half-finished guide, a command palette entry for the guide, and a search over the steps.
