# Colour picker

A reusable control (`ColorPicker`) used by the appearance editor.

## What it has

- A saturation and brightness field, a hue bar and an opacity bar. Drag, or focus a control and use the arrow keys (Shift for larger steps).
- Direct entry that translates between **HEX / HEX8, RGB, HSL, HSV, HWB, CMYK, OKLCH, CIELAB (LAB) and LCH**. Edit any field and press Enter; the others follow. An unreadable value is flagged and the colour is left alone. Each field has a copy button. Leaving a field without changing it does not count as a new choice.
- An OKLCH, LAB or LCH value outside sRGB is clipped to the nearest displayable colour and the picker says so.
- Original and new colour side by side, with a checkerboard under translucent colours.
- The theme's 17 colour roles as swatches, and the 12 most recent choices (saved in settings).
- A contrast ratio against the text or background colour, with an explicit pass or fail against 4.5:1. The result is written in words, not only colour.
- A screen eyedropper: **Pick from screen** covers the screen with an almost invisible window, so the click is taken and never reaches the app underneath. Esc cancels.
- A name when the colour exactly matches a standard named colour.

## CIELAB and LCH

LAB and LCH use CIELAB with the D65 white point (the sRGB white), the same space CSS `lab()` and `lch()` use. Lightness runs 0 to 100, LAB a and b about -128 to 128, LCH chroma about 0 to 150. The self-test checks the red and white reference values and a round trip through both spaces.

## Not built

OKLab as a separate field, a colour wheel, the animated rainbow colour, a gamut warning before editing, and a named-colour entry field. CMYK is a plain mathematical conversion with no printer profile.

## Verification

The colour-space maths is covered by an in-app self-test check. The picker control itself is compile-checked only. The eyedropper reads pixels with the Windows screen device context and has not been exercised on multi-monitor or mixed-scale setups.
