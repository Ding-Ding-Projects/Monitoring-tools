# Drawing and annotation

Open it from Tools: a blank board, an image file, or the newest picture in clipboard history. Annotating never changes the original image.

## What it offers
- Seven pens: Pencil (P), Pen (N), Fineliner (F), Marker (M), Highlighter (H), Brush (B), Fountain pen (U).
- Eraser (E) that removes only the part of a stroke you rub over.
- Select (S) to move, resize and delete strokes and text; double-click placed text to edit it.
- Line (L), Arrow (A), Rectangle (R), Ellipse (O), Text (T). Shift gives 45 degree lines, squares and circles.
- Colour (C) turns the toolbar into a palette of the app's theme colours plus a custom red, green, blue or hex colour. Size (Z) turns it into a width slider.
- Undo (Ctrl+Z), redo (Ctrl+Y), clear (asks for the two-key confirmation).
- Save PNG next to the original (Ctrl+S) at 1x, 2x or 3x, save SVG, copy to the clipboard (Ctrl+Shift+C).
- The toolbar can sit on any edge and can be minimised to a disc (Esc). Position, minimised state, pen, width, colour and PNG scale persist.

## How it is built, and what differs from Drawesome
Drawesome is a JavaScript package. This app is a native .NET program with no web view and no package download step, so the package is not used. The board is built on the WPF ink canvas instead and offers an equivalent toolset.

Differences to know about:
- Pencil, Pen and Brush thin with speed, and the Fountain pen follows stroke direction, but that shaping is applied when you lift the pen. While drawing, the live line is drawn at the width in force at each moment: the keys [ and ] (Shift for 5) change the width with the pen down, the next segment takes it, and the finished stroke takes the latest width.
- Speed is estimated from the spacing of input samples, not from timestamps.
- The toolbar cannot be dragged freely; use the Dock button to move it to an edge.
- SVG strokes are filled outlines of exactly what is drawn, so erased parts are gone; text is exported as text.
- Per-element appearance editing and element locks follow the rest of the app through the shared right-click menu; the drawing toolbar has no separate appearance editor.

## Saving and privacy
Strokes and text are saved in the app's data folder (`drawings`) after each change and restored when you open the same image or the blank board again. Nothing is uploaded.

## Status
A self-test check renders segments of one stroke before and after a width change and measures them. Not yet exercised with a pen or touch device (the segments are fed to the renderer directly), and not compared against Drawesome side by side.
