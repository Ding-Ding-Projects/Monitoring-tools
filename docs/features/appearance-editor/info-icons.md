# Info icons

Every switch and every row of choices built with the shared `Switch` and `Choice` helpers now carries a small information button (`InfoIcon`).

## What opens

A non-modal panel beside the button with:

- what the setting does, in plain words;
- the current value and the real default, and whether the value has been changed from it;
- a sample control to try. The sample is isolated and changes nothing real.

The button has an accessible name ("About ..."), a tooltip, keyboard activation, an expanded or collapsed state, Esc to close, and focus returns to the button.

## Where the text comes from

A hand-written table in `InfoIcon.cs` holds the explanation and the default for every setting the app currently has. Defaults are read from a fresh `Settings` object where the setting is stored there, so they cannot drift from the code. A label that is reused (for example "Check every") is told apart by its first option.

A setting with no table entry shows its subtitle, or a generic sentence, and says plainly that no default is recorded. Minecraft server property rows currently fall into this case.

## Known limits

- The button is 36 by 36, not 44 by 44, and sits beside the label rather than above the control, to keep dense settings rows compact.
- There is no completeness test yet that fails when a setting is missing from the table.
- The panel has no step-by-step guide beyond the sample; it is an explanation panel, not a wizard.
- The command palette's inline choices do not have the button.
