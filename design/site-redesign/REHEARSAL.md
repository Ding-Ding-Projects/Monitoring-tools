# Rehearsal: site redesign boards

Frames were rendered locally with Chrome at each board's design width and at 390 px, read, fixed and rendered
again. The files in `frames/` are the final renders.

## First pass and the restart

The first Home board was a light Material 3 page with no motion. Reviewed against the brief it could have been any
app's site, so the direction was replaced with "Night shift monitor" before the other boards were drawn.

## Findings fixed

| Board | Finding | Fix |
| --- | --- | --- |
| Home | The heartbeat line crossed the hero paragraph. | Moved to the hero's foot, under the buttons. |
| Home | The toast stack in the clipboard panel faded out for most of its loop, leaving an empty panel. | Replaced the fade with a slide between two toasts that are always visible. |
| Home | The toast stack covered the panel's copy. | Copy column capped at 300 px; toasts sized to 300 px on the right. |
| Home | One empty cell after 17 areas in a three-column grid. | Added "Browse all 107 articles" as the eighteenth cell. |
| Phone home | The heartbeat crossed the boundary line. | Lowered the line. |
| Docs | "All 17" was pressed while eight areas showed. | "Most used" is the default filter, with a button for the other nine. |
| Article | The right-click menu, open by default, hid the title. | Closed by default with a hint; the Tabs board shows the open menu. |
| Article | A template value inside the stylesheet stopped the rules after it from applying. | Moved it to an inline style. |
| Diagram | A band of empty page under the content. | Root fills the viewport; board height matches content. |
| Settings | Contrast and HSL values were estimates. | Replaced with computed values (for example Mint 11.8:1 on the ink ground). |
| Palette | The spotlight shadow darkened the card it was spotlighting. | Ring pulse only. |
| Palette | One result named a setting the site does not have. | Replaced with a real article. |
| Tabs | Highlighted filter letters split the label into separate flex items. | Label wrapped in one span with a mark. |
| Safety | Unlock ladder rungs did not match the app. | Copied the four real rungs from `docs/features/safety/unlock-ladder.md`. |
| Safety | Empty PIN dots were nearly invisible. | Brighter empty dots. |
| All pages | At 390 px the desktop boards kept their columns and the page scrolled sideways. | A phone layer: one column, smaller display type, scrolling tab and nav rows, hidden side panels. |
| Home, Docs, phone home | The boards said 107 articles and per-area counts that included each area's index page. | Corrected to the 91 articles and the real per-area counts the site build reports. |
| Releases | At 390 px the duration wrapped onto two lines in its readout. | Readouts stay on one line at a smaller size on phones. |

## Still open

- Nothing fixable in the design. The colour translator stacks under the picker on a phone; that is long but
  readable and is the intended phone layout.
