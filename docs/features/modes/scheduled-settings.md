# Scheduled settings

A rule says: between two times on chosen days, set one value. Rules are checked every minute by a dispatcher timer and immediately after any edit.

## Rule fields

`id` (short stable id), `label` (optional), `enabled`, `start` and `end` (24-hour `HH:mm`), `days` (list of weekday numbers, 0 = Sunday; empty = every day), `fromDate` and `toDate` (optional `yyyy-MM-dd`; empty = no limit on that side), `setting`, `value`.

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
- Optional dates limit the rule to whole calendar days, both ends included. Fill only one for an open-ended range. A window that crosses midnight follows the day it starts on, so with a last date of the 1st, a window starting at 22:00 on the 1st still runs until 02:00 on the 2nd.
- Invalid or disabled rules never match, and so does a rule whose end date is before its start date. Invalid time or date input is refused when adding or saving.
- **Edit** on a rule loads it into the form above the list; **Save changes** updates that same rule in place (keeping its position and its enabled state), and **Cancel edit** leaves it untouched.
- If several rules match the same setting, the one lower in the list wins. Use Move earlier and Move later to reorder.

## Overlay, not rewrite

A rule never changes your saved choice. The effective value is an overlay, so when the rule ends the saved choice is back. Precedence from lowest to highest: saved setting, settings file, scheduled rule, School mode.

## Not implemented

Native date and time pickers (the editor uses validated text boxes; WPF has none built in), rules whose value comes from an HTTPS API or Home Assistant, and recording schedule edits in local history (this app has no local history).
