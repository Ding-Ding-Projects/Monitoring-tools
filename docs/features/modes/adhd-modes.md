# ADHD modes

Five independent switches, all off by default. Any mix can be on. They are aids for attention, not medical advice, and the copy is plain and factual: no streaks, scores or congratulations.

| Mode | What it does in this app | Flag |
| --- | --- | --- |
| Focus | Messages that are not urgent (a copied item, an address change) go quietly to History instead of showing. Failures and lost connections still show. Nothing is hidden for good. | `Modes.Focus`, `Modes.ShouldHoldToast(urgent)` |
| Low stimulation | No fade or slide motion; only warnings and failures make a sound. It adds to Windows' animation setting and never overrides it: if Windows has animation off, the flag is on regardless. | `Modes.LowStimulation`, `Modes.ReduceMotion`, `Modes.SoundAllowed(essential)` |
| Time awareness | Running jobs show elapsed time, and a plain reminder toast appears every 15, 30 (default), 45, 60 or 90 minutes. The reminder is implemented in `Modes`; the elapsed-time display on jobs is for the toast code to add. | `Modes.TimeAwareness` |
| One thing at a time | At most one popup is on screen. The rest wait and also appear in History. | `Modes.OneThing` |
| Momentum | The window reopens on the page last used (`Settings.LastPage`), so work can resume where it stopped. | `Modes.Momentum` |

## Interaction with School mode

The modes keep their saved state while School mode is on. School mode adds its own limits and does not change them.

## Not applicable or not wired yet

The tray app has no canvas, document or list to spotlight, so Focus is expressed as holding non-urgent popups. Command-palette entries, local history and export of these choices are not implemented. The consuming call sites (toast manager, sounds, window navigation) must still read the flags.
