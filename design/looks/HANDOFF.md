# Four looks: design handoff

Four complete looks for the app, each designed on its own canvas on 2026-11-07, then built into the app as the Look
setting (Appearance page, setup, Make a look and the command palette). Graphite is the look the app already had,
tidied up; Instrument, Blueprint and Pop are new. Every canvas has the same two artboards so the looks can be compared
like for like, and each artboard has a Theme tweak for light and dark.

| Folder | Look | Idea |
| --- | --- | --- |
| `graphite/` | Graphite | Material 3 tonal surfaces on the Mint seed, Roboto, soft shadows only on what floats. Polish: the three overview tiles become one status band, sentence-case page labels instead of capitals, counts written out ("9 text, 2 links, 1 file"), distinct icons for Local AI models and Service, a shorter footer. |
| `instrument/` | Instrument | The app as a piece of test gear: molded panels with fine edges, a lit indicator on the open page, big figures in dark readout windows, the probe chart on a scope screen. Barlow and Barlow Semi Condensed, Amber. |
| `blueprint/` | Blueprint | The network, drawn: fine ink outlines on a faint grid, square corners, no shadows, and the pages on one wire in the side rail with the open page as the filled node. IBM Plex Sans, Plex Sans Condensed and Plex Mono, Ocean. |
| `pop/` | Pop | Everything is a sticker: ink outlines, hard offset shadows, bright fills from three seeds, and each page name on label tape. Bricolage Grotesque, Rose. |

Each folder holds:

| File | What it shows |
| --- | --- |
| `canvas.json` | The canvas index: two artboards and a note on the idea. |
| `Main.dc.html` | The control centre with every page open in the side rail (click a page to move the selection) and the Overview: status, search with the regex builder, the live probe chart with its gap, outages and latest activity. |
| `Floating.dc.html` | The look picker as it appears on the Appearance page (all four looks, each drawn in its own style), the tray menu with working switches, and three toasts: the quiet "updated" notice, two files that were cut ("pasting moves these files") and a copied link. |
| `frames/` | Local renders of the artboards. |

## Decisions carried into the app

- `Core/Looks.cs` holds the four looks: typefaces, title weight, how corners map, card outlines, Pop's hard shadow, the
  shadow under floating windows, the side rail marker, the page label style, readout windows, the grid, and the
  neutral colours for light and dark. Accents still come from the colour seed; picking a look also picks its colour.
- `Appearance.Apply` swaps the fonts, weight, shadows and outlines and reshapes every open window's corners when the
  look changes. Corners are written once for Graphite and mapped by `UI/LookShape.cs` when each box first appears.
- `UI/LookParts.cs` draws the parts that differ beyond tokens: the page label (crumb, plate, rule, tape), card outlines,
  readout windows, the Blueprint grid and the picker previews. The side rail marker is drawn in `UI/Tabs/StripTab.cs`.
- Typefaces are embedded (SIL Open Font License, pinned in `deps/manifest.json` with their licence texts).
- Every page starts open in the side rail; close buttons show on the open page and on hover.

Sample values are made up and safe: example.com links, harbour photos as colour blocks, made-up probe figures.
