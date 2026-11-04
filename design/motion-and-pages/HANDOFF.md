# Motion and page redesign: design handoff

Design canvas made on 2026-11-04 for three pages that had fallen behind the rest of the app (Local AI models,
Minecraft servers, Conversion) and for one motion system used everywhere. Tokens are the app's existing Material 3
roles from the Mint seed in `src/MonitoringTools/UI/Theme.xaml`; no external design system was used.

## Files

| File | What it shows |
| --- | --- |
| `canvas.json` | Canvas index: four artboards, opening focused on the Ollama page. |
| `Main.dc.html` | Ollama page (interactive). Status hero in the primary container with a breathing status dot and three numbers, Pull with a live progress bar (press Pull), installed models as rows with an initial tile and chips, and the loaded model's GPU and CPU share as one split bar. |
| `Minecraft.dc.html` | Minecraft servers (interactive). Header counts, a server card with icon tile, metadata chips and a status pill, a terminal console with command chips and a prompt (press Console), and a settings drawer (press Settings). |
| `Conversion.dc.html` | Conversion formats (interactive). One searchable list of all 39 formats with Video, Audio and Image filters that show counts; each row has a kind badge, the `:command`, name and extension, when to pick it, what it can be made from, and Convert files. |
| `Motion.dc.html` | The motion spec: six motions, each with timing, curve and the reason it exists. Replay all restarts them. |

## Decisions carried into the app

- `UI/Ui.cs` gains the motion helpers: `SharedAxis` (page change), `RiseAll` (staggered rows, capped at 12),
  `Expand` (panels that grow out of their button), `Glide` (progress values), `Liftable` (2 px hover lift),
  `LiveDot` (a status dot whose ring breathes only while something runs) and `Pill` (metadata chips).
- `ControlCentreWindow.Navigate` slides the new page in from the side of the tab you moved toward, then the cards rise.
- Cards tint on hover with a colour glide instead of a snap, and lift 2 px on a spring.
- Ollama: the status card became the hero; numbers fill in from the same server calls as before.
- Minecraft: the command builder moved inside the console panel, and `server.properties` moved behind a Settings
  drawer, so a card is short until you ask for more. The four-step New server guide is unchanged.
- Conversion: the chip cloud became the list; `Media/Formats.cs` grew from 19 to 39 formats with a note per format,
  and the recognised source extensions grew for video, audio and images.
- Reduce motion (Appearance or a mode) skips slides, springs, lifts and the breathing ring.

## Differences between the design and the build

- The design shows a navigation rail; the app keeps its tab strip, as in the earlier design pass.
- The Ollama hero in the app keeps the port field and the install and download-page buttons, which the design leaves out.
- Row hover in the app is a colour glide only; the lift is kept for cards so long lists do not shimmer.

## Not checked

- The app was compiled on Linux with `EnableWindowsTargeting`; it was not run here. The first build on the Windows
  runner renders the real screenshots and is the real test of the motion and layout.
- The new FFmpeg encoders (`libx265`, `libsvtav1`, `libjxl`, `qoi`, `jpeg2000`, `wmv2`, `ac3`) are expected in the bundled
  BtbN GPL build but were not run against it here.
- The Stageshow skill could not be installed or read in this session, so its checklist was not applied; the
  restraint, hierarchy and motion-with-a-cause notes from the earlier handoff were followed instead.
