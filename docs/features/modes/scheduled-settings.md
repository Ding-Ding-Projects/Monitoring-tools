# Scheduled settings

A rule says: between two times on chosen days, set one value. Rules are checked every minute by a dispatcher timer and immediately after any edit.

## Rule fields

`id` (short stable id), `label` (optional), `enabled`, `start` and `end` (24-hour `HH:mm`), `days` (list of weekday numbers, 0 = Sunday; empty = every day), `setting`, `value`.

| Setting | Values | How it takes effect |
| --- | --- | --- |
| `language` | `en`, `yue`, `bilingual` | Offered through `Modes.ScheduledLanguage`; the language code must apply it. |
| `theme` | `system`, `light`, `dark` | Offered through `Modes.ScheduledTheme`; the appearance code must apply it. |
| `sounds` | `on`, `off` | `Modes.SoundsOn` |
| `toasts` (clipboard toasts) | `on`, `off` | `Modes.ClipboardToastsOn` |
| `gamemode` | `on`, `off` | `Modes.GameModeOn` |

Up to 64 rules are stored in `settings.json`. Density, colour, fonts, motion and display-name presentation are not scheduled because this app does not have those settings yet.

## Semantics

- Times are the computer's local clock. The page states the time zone.
- A window is start inclusive, end exclusive.
- Start later than end crosses midnight and belongs to the day it starts on. For example, Friday 22:00 to 06:00 covers early Saturday.
- Start equal to end means all day.
- Daylight-saving changes follow the wall clock; a window that falls inside a skipped hour does not happen that day.
- Invalid or disabled rules never match. Invalid time input is refused when adding.
- If several rules match the same setting, the one lower in the list wins. Use Move earlier and Move later to reorder.

## Overlay, not rewrite

A rule never changes your saved choice. The effective value is an overlay, so when the rule ends the saved choice is back. Precedence from lowest to highest: saved setting, settings file, scheduled rule, School mode.

## Not implemented

Native date and time pickers (the editor uses validated text boxes; WPF has none built in), start and end dates, rules whose value comes from an HTTPS API or Home Assistant, editing a rule in place (delete and add instead), and recording schedule edits in local history (this app has no local history).
