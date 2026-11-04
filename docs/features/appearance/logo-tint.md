# Logo tint

Chooses whether the tray and header glyph follows the seed colour or uses a custom colour.

**Default:** Match seed colour

**Stored as:** `LogoSource (seed, custom)` in settings.json.

**Notes:** Exposed through Appearance.LogoColor.

## Logo glyph and custom image

Beyond tint, the Appearance page offers twelve glyphs from the app's icon set and one custom PNG or ICO image for the drawer logo and the tray icon (the status dot is still drawn on top). A custom image is limited to 256 KB and 16 to 512 pixels per side, is checked by file signature and decoded before it is accepted, and is copied into the app's `logo` data folder so the original is never read again. SVG is not accepted. Choosing a glyph removes any custom image; **Reset logo** restores the Pulse glyph. Stored as `LogoGlyph` and `LogoImageFile`. Code: `Core/LogoKit.cs`.
