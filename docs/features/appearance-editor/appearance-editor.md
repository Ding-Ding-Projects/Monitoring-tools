# Per-element appearance editor

Right-click (or Shift+F10) any button, card or marked row, then choose **Edit appearance…**. A non-modal window opens beside that element.

## What it edits

- **Font:** family (every installed font, filtered by a search field with its regex builder, each name drawn in its own face), size 8 to 72, weight Light to Black, italic.
- **Colour:** text colour, background and border colour, through the [colour picker](color-picker.md). A contrast readout compares text and background against the 4.5:1 minimum.
- **Shape:** corner radius (cards and panels), padding, border thickness.

Changes show at once and are saved. A live sample strip in the editor mirrors the combined result.

## Resetting

- **Reset** beside each property returns just that property to the theme.
- **Reset element** clears everything for the element.
- **Reset all…** clears every saved look and asks for the two-key confirmation first.

## Persistence

Styles live in `settings.json` under `ElementStyles`, keyed by `pageId|element text` (text capped at 120 characters). Empty styles are removed. `ElementStyles.ApplyAll(root, pageId)` re-applies them after a page is rebuilt.

## Known limits

- Elements that share the same text on one page share one look, because the key is page plus text. An element whose text changes (a live counter) loses its look.
- Controls whose shape comes from a theme template (buttons) cannot have their corner radius edited; cards and panels can.
- Cards repaint their background on pointer enter and leave, so a background set on a card may be overwritten by that hover until the card helper is changed.
- Not built: layers and blend modes, per-state (hover, pressed, focus) editing, the animation and transition tab, letter spacing, line height, underline and strikethrough, grouped font families, named presets, copy and paste style, undo and redo, history, import and export.
- Compile-checked only. It has not been run or captured.
