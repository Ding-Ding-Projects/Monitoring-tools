# Material 3 Expressive windows

Every secondary window follows the same Material 3 Expressive treatment as the control centre, so the Appearance theme (light or dark, seed colours) repaints all of them.

## Covered windows

Toast, offline dialog, setup wizard, welcome tour, file tools, default commit message dialog, tray menu and confirmation dialog.

## What changed

- **Theme roles only.** Every `B.*` brush and `C.*` colour in these windows' XAML is a `DynamicResource`, so a theme change repaints open windows. Hard-coded hex colours for borders, tracks and tiles were replaced by theme roles (`B.Line`, `B.Sunken`, `B.SecondaryContainer`, `B.DangerWash`, `B.AmberWash`, `B.VioletWash`).
- **Shapes.** Window containers use 24 to 28 corner radii on `B.Raised` or `B.Surface`, with no hex borders. Tiles and segmented controls use 16 to 20.
- **Toast motion.** Toasts slide in with an ease-out (decelerate) curve. When Reduce motion is on in Appearance or Modes, toasts appear and close without animating.
- **Offline dialog.** The probe ladder tiles use `B.SecondaryContainer` (passed) or `B.DangerWash` (failed) with their matching on-colours; warnings use `B.AmberWash`. The DNS chip row now wraps inside its own column, so the last chip is never clipped.
- **Setup wizard.** The progress bar is a pill track in `B.SecondaryContainer`, the step markers are larger circles, and navigation uses the filled (Next) and tonal (Back, Skip) pill buttons. Progress still saves at every step. The animated fill is skipped under Reduce motion.
- **Welcome tour.** Cards use 24 radius containers; the two tinted cards use `B.AmberWash` and `B.VioletWash` instead of hex borders. The entrance animation is skipped under Reduce motion.

## Not done

- The welcome tour has no step indicator because it is a single card grid, not a stepped flow.
- Brushes assigned in code (for example ladder chips built at runtime in the setup wizard) read the current theme when created and do not repaint until the content is rebuilt. Offline ladder tiles use resource references and do repaint.
- Verified by compile only; no screenshots of the themed variants were captured.
