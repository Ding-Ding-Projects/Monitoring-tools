# Material 3 audit

A named checklist for Material 3 conformance, measured on the built app rather than read from source. The self-test tours every control-centre page, every other window and every state the language audit visits (about 340 scans, each repeated in the dark and the light theme) and judges the live controls against the rows below. The full list of findings is written to `audit-ui.json` beside `selftest.json`.

Code: `UI/UiAudit.cs` (measurements), `UI/SelfTest.Audit.cs` (the checks), `UI/AccessibleLabels.cs`, `Core/Appearance.cs` (theme roles).

## Checklist

| Row | How it is measured | Result |
| --- | --- | --- |
| Colour roles | Every brush set directly on an element is compared with the theme's role colours (`C.*`). Anything else is a literal colour. | Pass. The tint edges of the cards, the Overview chart series and the Game page chips now use theme roles. Warning, accent and error colours (Amber, Violet, Danger and their washes) have a dark set and a light set, set in `Appearance.Apply`. The colour picker paints the colour a person is choosing, which is data, and is the one exception. |
| Type scale | Font size of every visible text block and text box against 11, 12, 14, 16, 22, 24, 28, 32, 36, 45, 57. | Pass. About 80 files were moved onto the scale (13 became 12, 15 became 14, 18 became 16, 20 became 22, and so on). A font picker that shows each font's name in that font at its own size is exempt. |
| Typeface | Roboto (or the monospace stack for code). A window with no typeface of its own inherits the body typeface. | Pass. |
| Shape scale | Integer corner radii of borders against 0, 4, 8, 12, 16, 20, 28, 32, 48 or a full pill. Focus rings follow their container and animated radii are not authored. | Pass. 24 became 28, 19 became 20, 18 and 14 became 16 and 12, and small radii 5, 6, 7 and 10 moved to 4, 8 and 12. |
| State layers | The templates of Btn.Ghost, Accent, Alert, Danger, Chip, Icon, Row, Switch and Input are read for hover, pressed, keyboard focus and disabled triggers. | Pass by the template check. Added in this audit: the switch hover and pressed halo, the disabled state of switches, rows and text fields. **Not seen:** the capture runner cannot hover or press, so only the resting switch was looked at in a capture; the halo and disabled looks are unchecked by eye. |
| Elevation | Drop shadows found on live elements. | Recorded, not judged. 70 of 74 shadows use one setting (blur 32, depth 10, opacity 0.42, used by toasts and dialogs); 4 are a 3 px edge. Tonal surface steps (Window, Surface, Raised, Sunken) carry the rest of the elevation. There is no per-level shadow scale. |
| Component anatomy | Button height 40 (chip 32), icon button 40 by 40, switch track 52 by 32 with a thumb that grows from 16 to 24, text field 40 with a 12 radius. | Matches by construction in `Theme.xaml`; the audit records the rendered sizes (for example 1,120 buttons at 79 by 39) in `audit-ui.json`. The 56 high search bar and the 24 corner containers moved to 28. |
| Contrast of roles | Every text-on-surface pair of the palette in all five seeds, light and dark (see the accessibility audit). | Pass. Muted text is nudged until it reaches 4.9:1 on the strongest surface. |

## What the audit does not prove

- The date fields of the Modes schedule form are not themed: the picker's inner text box still draws the system's light box and blue calendar button in the dark theme (capture `modes-schedule-edit`). The text is readable, so the contrast check passes, but it is not Material 3. A style set on the inner text box did not take effect; the picker needs its own template.

- Captures: before and after the font-size and corner-radius change the app was captured (167 captures each) and 8 pages compared side by side by eye (toasts, Appearance, Minecraft settings, the appearance editor, welcome, the 200 percent Overview and Ollama). No clipping or overflow came from the change. The other pages were not compared by eye.
- The audit reads live properties. It cannot tell whether a layout looks right. The built-app captures and the existing no-clipping checks do that.
- Elevation has no check that fails, because the app uses tonal surfaces and one shared shadow.
- The Material 3 Expressive motion curves are used in the templates; motion is not measured.

## Verification

`MonitoringTools.exe --selftest <folder>` runs the checks named `material 3: ...`. The quick loop is `MT_ONLY=audit`.
