# Text size

Scales text and spacing together from 90 to 150 percent in steps of 10, using a layout transform on the root element of each window (`Appearance.ScaleFor`).

**Default:** 100 %

**Stored as:** `TextScalePercent` in settings.json.

**How windows make room:**

- Windows the person sizes (the control centre) keep their size; their pages reflow and scroll.
- Windows of a set size (setup, toasts, the tray menu, the offline dialog, confirmations, the welcome tour, the command palette and the tool dialogs) grow with the text, so their content keeps the room it was laid out for. They never grow past the screen: there, their long parts scroll and their buttons stay in view. Such a window records the size it was designed at with `Appearance.SetDesignSize`.
- Text fields keep their width while there is room and narrow when there is not, and the buttons beside them move to the next line first.

**Notes:** Open windows are scaled when the choice changes. A new window is scaled when it opens. The every-surface sweep checks every surface at 150 percent on each build.
