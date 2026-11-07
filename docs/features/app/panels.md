# Folding and resizable panels

## Folding

The search bar of every control-centre page, the filters of the Activity page, the Virtual disks library and the conversion formats, and the statistics on the overview fold away under their own header.

- The header is one keyboard stop with a visible focus ring. Enter or Space opens and closes it, and screen readers hear it expand and collapse.
- Whether each panel is open is kept between runs.
- A panel that only describes a collection, such as the statistics, starts closed. A closed panel that hides an active filter says so in its header, for example "1 filter on: Attached".
- `Ctrl+F` opens a folded search bar and puts the cursor in it.

## Resizing

The regex builder and the explanation that opens from an info icon can be resized from their right edge, bottom edge and corner. A floating panel can also be moved by its header.

- On the keyboard, with focus inside the panel: `Alt+Shift` with the arrow keys resizes, and `Alt` with the arrow keys moves a floating panel.
- The size and place are kept for each kind of panel, and a panel never grows past the screen.
- Double-clicking the corner, or `Alt+Shift+Home`, puts it back to its first size.
