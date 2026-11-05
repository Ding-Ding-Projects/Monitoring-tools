# Accessibility audit

A named checklist for keyboard use, accessible names, target size and contrast, measured on the built app. It runs on the same tour as the [Material 3 audit](material3-audit.md): every page, window and state, in the dark and the light theme (about 340 scans, 16,000 control sightings and 39,000 text sightings per run). Findings go to `audit-ui.json`.

Code: `UI/UiAudit.cs`, `UI/SelfTest.Audit.cs`, `UI/AccessibleLabels.cs`.

## Checklist

| Row | How it is measured | Result |
| --- | --- | --- |
| Every control has an accessible name | The automation peer's name of every visible button, check box, text box, combo box, slider, tab and menu item must not be empty. A name of only symbols is allowed when a text tooltip explains it (the regex token buttons). | Pass after two fixes. 431 buttons and 21 inputs had no name. A button whose content is a panel now takes its name from the text it shows; an icon-only button takes its tooltip; an input takes the label before it or its tooltip. The names follow the text, so the language and personal wording reach them. |
| Keyboard reach: static | Each enabled control must be focusable, a tab stop and have no ancestor that turns Tab off. | Pass. A list is entered through its items. The command palette is excluded, see below. |
| Keyboard reach: real traversal | Tab is pressed from the first element through the whole window (326 traversals, about 19,000 steps per run) and every enabled control must receive focus. | Pass after one real fix: committing the Display name or logo colour field rebuilt the Appearance page, so tabbing out of the field lost focus. The page now keeps its place (`Appearance.KeepPage`). The Minecraft players list now lets Tab reach the switches inside each row. |
| Target size | Every enabled target at least 24 by 24 (WCAG 2.2, 2.5.8), or a 24 diameter circle on its centre touches no other target. | Pass. About 800 small targets pass by the spacing exception; the rest are 32 or larger. Most buttons are 40 high; the 48 by 48 recommendation is not required. |
| Text contrast | Text colour against the real backdrop (the nearest opaque fills, including template borders beneath the content) at least 4.5:1, or 3:1 for large text, in both themes. Disabled text and text in the middle of a fade are skipped. | Pass. Fixed: muted text on tonal surfaces (the strongest surface now reaches 4.9:1), warning, accent and error text in the light theme, text on selected rows and chips, the date picker box in the dark theme. |
| Palette contrast | Every text role on every surface and every filled container pair, in all five seeds, light and dark. | Pass. |
| Focus visible | Every interactive style has a keyboard focus ring. | Pass (see the state layer row of the Material 3 audit). |
| Reduced motion, live regions, text size 90 to 150 percent, 200 percent display scale | Existing features, covered by their own checks and captures. | Unchanged. |

## Accepted exceptions

- The command palette rows hold a switch that is not a tab stop on purpose: the palette is driven by Up, Down, Enter and, new in this audit, Ctrl+Space to operate the selected row's switch or Unlock button.
- A list or combo box with no items has nothing to focus and is not required to be reached.
- Text in the middle of a fade (opacity under 0.98) is skipped, because its settled colour is what a person reads.

## What the audit does not prove

- Real assistive technology (Narrator, NVDA) was not run. The names are the automation peer's, which is what they read.
- Contrast is measured on solid fills. A gradient or picture behind text would be skipped; none was met.
- Target size does not account for text size scaling beyond the scale factor of the window.

## Verification

`MonitoringTools.exe --selftest <folder>` runs the checks named `accessibility: ...`. The quick loop is `MT_ONLY=audit`.
