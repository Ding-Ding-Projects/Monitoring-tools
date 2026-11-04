# Colour picker

A reusable control (`ColorPicker`) used by the appearance editor.

## What it has

- A saturation and brightness field, a hue bar and an opacity bar. Drag, or focus a control and use the arrow keys (Shift for larger steps).
- Direct entry that translates between **HEX / HEX8, RGB, HSL, HSV, HWB, CMYK and OKLCH**. Edit any field and press Enter; the others follow. An unreadable value is flagged and the colour is left alone. Each field has a copy button.
- An OKLCH value outside sRGB is clipped to the nearest displayable colour and the picker says so.
- Original and new colour side by side, with a checkerboard under translucent colours.
- The theme's 17 colour roles as swatches, and the 12 most recent choices (saved in settings).
- A contrast ratio against the text or background colour, with an explicit pass or fail against 4.5:1. The result is written in words, not only colour.
- A screen eyedropper: **Pick from screen** covers the screen with an almost invisible window, so the click is taken and never reaches the app underneath. Esc cancels.
- A name when the colour exactly matches a standard named colour.

## Not built

CIELAB and LCH, OKLab as a separate field, a colour wheel, the animated rainbow colour, a gamut warning before editing, and a named-colour entry field. CMYK is a plain mathematical conversion with no printer profile.

## Verification

Compile-checked only. The eyedropper reads pixels with the Windows screen device context and has not been exercised on multi-monitor or mixed-scale setups.
