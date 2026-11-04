# Typed number fields

Every numeric setting is typed, never picked from a row of presets. One reusable control, `UI/NumberField.cs`, is used on every page and window.

## What it does

- A label, a subtitle saying what the setting does and its default, a text box, minus and plus steppers and an optional unit (s, min, %, items).
- Type a value and press Enter, or move focus away, to commit. Up and Down step by one step, Page Up and Page Down by ten, Escape puts the last good value back.
- A value that is not a number, is not whole where a whole number is needed, or is outside the allowed range is refused. A message under the box says why (for example "Not changed: enter a whole number from 5 to 1000.") and the stored value stays as it was.
- A setting whose consumer rounds anyway (Text size, steps of 10) rounds a typed value to the nearest step and says so.
- Where a slider remains (humour levels, speaking rate, stroke width) a compact typed box sits beside it and each follows the other.

## Settings converted

| Setting | Where | Bounds | Step |
| --- | --- | --- | --- |
| Toast duration | Clipboard, setup wizard | 1 to 600 s | 0.5 |
| History size | Clipboard | 5 to 1000 items | 5 |
| Sound volume | Clipboard | 0 to 100 % | 5 |
| Network check every | Network | 1 to 3600 s | 1 |
| Alert after | Network | 1 to 20 checks | 1 |
| Public IP check every | Network | 1 to 1440 min | 1 |
| Text size | Appearance, Make a look | 90 to 150 % | 10 (rounds) |
| Reminder interval | Modes | 5 to 1440 min | 5 |
| Ollama keep-alive | Ollama chat | 0 to 1440 min (0 unloads at once) | 1 |
| PNG export size | Tools | 1 to 3 times | 1 |
| New locks stay unlocked for | Safety | 0 to 1440 min (0 until the app closes) | 1 |
| Stay unlocked for | Lock wizard | 0 to 1440 min | 1 |
| Authenticator digits | Safety | 6 to 8 | 1 |
| Authenticator period | Safety | 1 to 3600 s | 1 |
| English and Cantonese humour | Language (beside slider) | 1 to 5 | 1 |
| Speaking rate | Language (beside slider) | 0.5 to 2.0 times | 0.1 |
| Stroke width | Drawing board (beside slider) | 1 to 48 px | 1 |

The command palette entries for toast duration, network check interval, volume and reminder interval open the page where the field sits.

## Not converted

- The Minecraft page memory choice (another worker owns that page).
- Tray snooze buttons (15 m, 1 h, 4 h): an action, not a stored setting.
- Red, green and blue sliders in the drawing board colour dialog: a hex box sits beside them.
- The appearance editor steppers already take typed numbers.

## Verification

Self-test checks "number fields: ..." walk every page and setup step (no choice offers only numeric presets), drive the history size by typing, steppers and keys, and check decimals, rounding and slider pairs.
