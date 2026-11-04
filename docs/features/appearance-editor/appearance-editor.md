# Per-element appearance editor

Right-click (or Shift+F10) any button, card or marked row, then choose **Edit appearance…**. A non-modal window opens beside that element. It has four tabs: Font, Colour, Shape and Library.

## What it edits

- **Font:** family (every installed font, filtered by a search field with its regex builder, each name drawn in its own face), size 8 to 72, weight Light to Black, italic, **letter spacing** (0 to 12 pixels, default none) and **underline** (default off).
- **Colour:** text colour, background and border colour, through the [colour picker](color-picker.md). A contrast readout compares text and background against the 4.5:1 minimum.
- **Per-state colours:** on the Colour tab, a State row switches between Normal, Hover, Pressed, Focused and Disabled. A state holds its own text, background and border colour. A state you leave empty keeps the normal look. The preview strip shows the chosen state.
- **Shape:** corner radius (cards and panels), padding, border thickness.

Changes show at once and are saved. A live sample strip in the editor mirrors the combined result.

## How states work

State colours are applied with the element's own mouse, keyboard-focus and enabled-changed events, not by replacing its template. Priority is Disabled, then Pressed, then Focused, then Hover; each channel (text, background, border) falls through to the next state, then to the normal look. Buttons keep their theme state layer on top of the colour you choose.

## Letter spacing and underline

WPF text has no letter-spacing property, so spacing is made by putting hair spaces (U+200A) between the letters. This can only widen text, never tighten it, and it only touches plain text: text bound to data, or made of several styled runs, is left alone. Keys for appearance edits, locks and the element menu ignore hair spaces, so a spaced label keeps the same key. The page search does not ignore them, so a letter-spaced label may not match a search for its words. Underline is drawn on the text blocks inside the element and removed again when the look is reset.

## Library tab

- **Copy and paste:** copy an element's whole look, open another element's editor and paste it. The copy lives in memory until the app closes.
- **Presets:** save the current look under a name (up to 40 characters, at most 50 presets), apply it to any other element, or delete it behind the two-key confirmation. Presets are stored in `settings.json` under `StylePresets`.
- **History:** the last 100 changes with their time and a summary of what changed. **Restore** puts that look back on its element. History is stored under `StyleHistory`; **Clear history** asks for confirmation.
- **Import and export:** **Export all looks…** writes every look and preset to one JSON file (format `monitoringtools.element-styles`, version 1). **Import looks…** reads at most 1 MB, checks the whole file first (format, version, at most 2000 looks and 50 presets, value ranges, real colours, no locked look touched) and applies nothing if any check fails. A good file is merged over the saved looks.
- **Search:** one search field with its regex builder filters presets and history together.
- **Lock this look:** see below.

## Undo and redo

The header has Undo and Redo buttons (Ctrl+Z and Ctrl+Y when focus is not in a text box). They cover the changes made in the current editor session for that element, up to 100 steps. Closing the editor ends the session; the History list on the Library tab is the longer record.

## Appearance locks

On the Library tab, **Lock this look…** opens the same lock wizard used for any element, keyed to the element's look rather than to the element. While the lock is closed, the look cannot be changed, reset, pasted over, undone, restored or replaced by an import, and **Reset all…** keeps it. A banner in the editor says so. **Unlock…** opens the normal unlock prompt (PIN, password or code, with the usual waits). Once open, the lock can be closed again or removed. As with every lock in the app, this is a self-imposed speed bump, not security.

## Resetting

- **Reset** beside each property returns just that property to the theme.
- **Reset element** clears everything for the element.
- **Reset all…** clears every saved look that is not locked and asks for the two-key confirmation first.

## Persistence

Styles live in `settings.json` under `ElementStyles`, keyed by `pageId|element text` (text capped at 120 characters). Empty styles are removed. `ElementStyles.ApplyAll(root, pageId)` re-applies them after a page is rebuilt.

## Known limits

- Elements that share the same text on one page share one look, because the key is page plus text. An element whose text changes (a live counter) loses its look.
- Controls whose shape comes from a theme template (buttons) cannot have their corner radius edited; cards and panels can.
- Cards repaint their background on pointer enter and leave, so a background set on a card may be overwritten by that hover until the card helper is changed.
- Letter spacing cannot be negative and does not apply to bound or mixed-style text (see above).
- State colours change colours only, not size or opacity.
- Not built: layers and blend modes, the animation and transition tab, line height, strikethrough, grouped font families, a per-element lock list view.
- Verified by the in-app self-test: preset save and apply, letter spacing and underline apply and restore, state resolution, export and import round trip with rejection of bad, oversized and wrong-format files, appearance locks refusing change until opened with the PIN. The editor window itself has been compile-checked only; it has not been driven or captured.
