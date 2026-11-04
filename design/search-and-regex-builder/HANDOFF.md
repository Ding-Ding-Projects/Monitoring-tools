# Monitoring Tools in Material 3 Expressive: design handoff

The design canvas for the whole app, remade in Material 3 Expressive on 2026-11-04 after the first pass was judged not modern enough. No external design system was used; the tokens are Material 3 roles generated from a Mint seed, with Violet, Amber, Ocean and Rose seeds for the theme picker.

## Files

| File | What it shows |
| --- | --- |
| `canvas.json` | Canvas index: four artboards; the canvas opens focused on the prototype. |
| `Main.dc.html` | Interactive prototype. Navigation rail with a springy pill indicator; Overview hero container with a drawn sparkline; Clipboard history with the 56 high search bar that opens the regex builder as a container transform, selection mode with a contextual bulk-action bar; Appearance with live light and dark, five seeds, text size and reduce motion; Language with English, Cantonese and bilingual plus two humour sliders and a live preview; Records with notifications, exports and settings history. The demo filters with the browser's regex engine; the app uses the .NET engine. |
| `Tokens.dc.html` | Colour roles, the Roboto type scale, shape that morphs, and the four motion curves animated, each with its reason. |
| `BuilderTabs.dc.html` | The regex builder's Test, Replace, Code and Library views with primary tabs and an animated indicator. |
| `SearchEverywhere.dc.html` | Settings search with matches on other pages, the tray menu filter, a row menu with its own filter and shortcuts, and an honest empty result. |

## Decisions carried into the app

- Every colour is a Material 3 role; the app's Theme.xaml maps its brush keys onto those roles and the Appearance page swaps the whole set at run time.
- Roboto is vendored through `deps/manifest.json` with its SHA-256 and embedded in the app.
- Buttons are pills with hover and press state layers; containers use 24 to 28 corners; chips use 8; fields use 12.
- Motion has a reason per curve: decelerate for arrivals, accelerate for exits, a gentle spring for shape and selection. Reduce motion removes slides and springs.
- The regex builder belongs to its search bar and grows out of it.

## Differences between the design and the build

- The app uses a tab strip docked on the left instead of the navigation rail, because the shared requirements ask for browser-style tabs.
- Settings matches on other pages are reached with Search every page (the command palette), not listed inline.
- The FAB shown in the design is not used in the app; New tab and page actions take its place.

## Review

Frames came from the real built app (`MonitoringTools.exe --screenshots`), because the hosted canvas needs a sign-in the review browser did not have. Findings fixed in the app: drawer overflow hiding the last section, duplicated section icons, nav icons keeping their dark colour in the light theme, an unstyled confirmation slider, unlabelled PIN fields, profile paths shown with the user name, and untranslated labels in Cantonese mode.

Stageshow skill guidance (v1.1.0, upstream commit `ccb6a38`) was applied for restraint, hierarchy, motion with a cause, and the fresh-eye review. Its marketing-specific rules (cursor choreography, crops, plates, loop timing) do not apply to real app screens and were not used.
