# Appearance

Look, theme, colour, text size, density, motion, name and logo.

- [Looks](looks.md): Graphite, Instrument, Blueprint and Pop
- [Theme](theme.md)
- [Seed colour](seed-colour.md)
- [High-contrast outlines](high-contrast-outlines.md)
- [Text size](text-size.md)
- [Density](density.md)
- [Reduce motion](reduce-motion.md)
- [Display name](display-name.md)
- [Logo tint](logo-tint.md)
- [Custom logo colour](custom-logo-colour.md)
- [Preview](preview.md)
- [Reset to defaults](reset.md)

## How changes reach the app

Every change calls `Appearance.Apply()`, which replaces the `C.*` colour and `B.*` brush resources, the look's fonts (`F.Display`, `F.Body`, `F.Mono`), title weight, shadows and card outlines in the application dictionary, reshapes the corners of open windows when the look changed, and raises `Appearance.Changed`. Pages built in code read brushes when they are built, so they show new colours after they reload. Surfaces defined in XAML with StaticResource keep the colours they were created with until their window opens again. Text size applies at once to open windows.
