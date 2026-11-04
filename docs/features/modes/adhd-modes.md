# ADHD modes

Five independent switches and a Focus spotlight, all off by default. Any mix can be on. They are aids for attention, not medical advice, and the copy is plain and factual: no streaks, scores or congratulations.

| Mode | What it does in this app | Flag |
| --- | --- | --- |
| Focus | Messages that are not urgent (a copied item, an address change) go quietly to History instead of showing. Failures and lost connections still show. Nothing is hidden for good. | `Modes.Focus`, `Modes.ShouldHoldToast(urgent)` |
| Low stimulation | No fade or slide motion; only warnings and failures make a sound. It adds to Windows' animation setting and never overrides it: if Windows has animation off, the flag is on regardless. | `Modes.LowStimulation`, `Modes.ReduceMotion`, `Modes.SoundAllowed(essential)` |
| Time awareness | A running background task toast shows `N s elapsed` and, when it finishes, `Took N s`. A plain reminder toast appears every 15, 30 (default), 45, 60 or 90 minutes; it is exempt from Focus. | `Modes.TimeAwareness` |
| One thing at a time | At most one popup is on screen. The rest wait their turn, in order, and are written to History at once. A waiting popup whose time runs out is dropped. | `Modes.OneThing` |
| Momentum | The control centre reopens on the page last used (`Settings.LastPage`, saved when you change page while the mode is on), so work can resume where it stopped. When a background task finishes, its toast also offers **Next: open Activity** while the mode is on. | `Modes.Momentum` |

## Focus spotlight

Dims everything in the window except the card under keyboard focus or the mouse, so one thing stands out. Switch it on from the Modes page ("Focus spotlight") or press Ctrl+Shift+Space in any window of the app; the page switch follows the shortcut. Default off, and the choice is saved (`Settings.AdhdSpotlight`).

- It never blocks clicks or typing; the dim layer is not hit-testable.
- The highlight moves with a short slide; with Low stimulation, Windows animation off or Reduce motion it jumps without animation.
- It finds the card as the nearest rounded container around the focused or hovered element, and follows scrolling and resizing.
- Only normal application windows are dimmed. Toasts, the palette and the offline dialog are left alone. A window without an adorner layer is skipped.
- It is separate from the Focus switch above, which holds back popups.

Implementation: `UI/FocusSpotlight.cs`. Not yet exercised in a running build by its author.

## Command palette

Every switch has a palette entry (Ctrl+Shift+F): Focus spotlight, Focus, Low stimulation, Time awareness, Reminder interval, One thing at a time and Momentum. Each toggles the same setting as the page.

## Interaction with School mode

The modes keep their saved state while School mode is on. School mode adds its own limits and does not change them.

## Not applicable or not wired yet

The tray app has no canvas or document, so the Focus switch is expressed as holding non-urgent popups and the Focus spotlight dims cards instead. Local history and export of these choices are not implemented.

## Verification
The self-test reads each effect in a running window: Focus holds a non-urgent toast into the notification history while a warning shows; One thing at a time queues the second and third toast and shows them in order; Low stimulation makes a fade finish at once and refuses ordinary sounds while warnings still play; Time awareness shows `N s elapsed` then `Took N s` on a task toast and starts and stops the reminder timer; Momentum reopens the window on the remembered page and not with the mode off. The reminder toast itself is waited for using a test-only one-second interval (`Modes.ReminderIntervalForTest`; the real shortest interval stays 5 minutes) and is shown even with Focus on. Not covered: listening to a sound. Checking the effects found that Focus, One thing at a time, the elapsed time and Momentum had no code reading their switches; each now has one (`ToastManager`, the task toast, `ControlCentreWindow`).
