# Search and regex builder: design handoff

Design canvas for the search bars and the anchored regex builder, in the app's existing "Graphite Signal" look (colours and fonts from `src/MonitoringTools/UI/Theme.xaml`). No external design system was used.

## Files

| File | What it shows |
| --- | --- |
| `canvas.json` | Canvas index: three artboards and their layout |
| `Main.dc.html` | Clipboard history with its own search bar and the builder open underneath it. Interactive: typing, the `.*` and `Aa` toggles, tokens and options work. The demo filters with the browser's regex engine; the app uses the .NET engine. |
| `BuilderTabs.dc.html` | The Test, Replace and Code views and the Library |
| `SearchEverywhere.dc.html` | A settings page searched for one option with matches on other pages, the tray menu filter, a row menu filter, and an honest empty result |

## Decisions carried into the app

- The builder is attached to its own search bar, opens under it, and returns focus to it on close.
- Plain text stays the default. The bar shows `Aa`, `.*` and **Builder**; the count sits beside them.
- The engine, version, backtracking mode and time limit are always shown in the builder header.
- An invalid pattern turns the bar amber and filters nothing; a risky pattern gets a plain warning.
- Every list and page has its own bar and its own state.

## Differences between the design and the build

- Settings matches on other pages: the design lists them inline. The build offers **Search every page**, which opens the command palette with the same query, because the palette already indexes every page's settings. Inline cross-page results are still to do.
- Row right-click menus with their own filter (design, right column) are not built: the app has no context menus yet.
- The design's "Save to library" footer button lives in the Library view in the build.

## Review

The design was reviewed against frames of the real built app (`MonitoringTools.exe --screenshots`) rather than canvas renders, because the canvas runtime is hosted. Findings and fixes:

1. Test sample text included button labels, and plain search matched them. Fixed: button content inside a result is no longer searched or used as sample text.
2. The group table's position column touched the scrollbar. Fixed with a right margin.
3. On short pages the search bar shrank to the content width. Fixed with a minimum width.
4. A search with no matches on the current page gave no route to other pages. Fixed with **Search every page**.

Stageshow skill guidance (v1.1.0, upstream commit `ccb6a38`) was applied for restraint, hierarchy and the fresh-eye review. Its marketing-specific rules (cursor choreography, crops, plates, loop timing) do not apply to real app screens and were not used.
