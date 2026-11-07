# New features: design handoff

Five interactive boards, designed on one canvas on 2026-11-07 before the features were built: the Virtual disks page,
New virtual disk, Quick pack, the live response-time chart, and right-click menus with live controls. Each board
works in the browser (click through the states) and has a Theme tweak for light and dark.

| File | What it shows |
| --- | --- |
| `canvas.json` | The canvas index: five boards and their titles. |
| `Main.dc.html` | The Virtual disks page: the routes this computer has, the library with search and filters, one VHDX chosen with its figures and what is inside it, and the action buttons, each one explaining when it cannot be used. |
| `Create.dc.html` | New virtual disk: presets, every option, the checks before anything is written, and the same steps as a PowerShell and a DiskPart script. |
| `QuickPack.dc.html` | Quick pack from drop to result: the plan with the size worked out line by line, packing with the disk growing by itself once, and the checked result beside the originals. |
| `LiveChart.dc.html` | The response-time chart: an easing line with a live dot, the value badge, a crosshair under the pointer and the arrow keys, and the honest waiting, paused and stale states. |
| `Menus.dc.html` | Right-click menus with live switches, steppers and choices that keep the menu open, and a dropdown with its own filter. |

## Decisions carried into the app

- **Virtual disks** became `UI/Pages/VirtualDisksPage.cs`. The board's library row (format badge, name, folder, type,
  sizes, state) and the detail card's figures were kept. The board stops at the action buttons; the page adds a panel
  under the disk for each action, with its options, the checks, the script and Run, so the result stays next to the
  disk it belongs to. The board's line that names the route ("Attached through the Windows virtual disk service")
  became the result line every action writes.
- **New virtual disk** kept the presets, the checks list and the script tabs. The checks use a mark beside each
  sentence, so the sentence is translated as a whole.
- **Quick pack** kept the three stages. The size breakdown is the board's, line for line; the progress figures are
  bytes, files, speed, time left and how often the disk grew.
- **The live chart** became `UI/LiveChart.cs` on the overview, with the same states and the reduced-motion path
  (it snaps instead of easing and does not pulse).
- **Menus** became the live items in `UI/ElementMenu.cs` and the filter in every dropdown (`UI/Dialogs/DialogParts.cs`).

## Carried onto the project site

The site's right-click menu got the same live settings (theme, text size, language, reduce motion) and its filter a
small regex builder. The site cannot touch disks or run probes, so it documents the Virtual disks page and the chart
instead of copying them.

## Not built yet

What the full virtual disk manager still lacks is listed at the end of
[the Virtual disks article](../../docs/features/files/virtual-disks.md#not-built-yet).
