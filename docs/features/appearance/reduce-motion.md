# Reduce motion

Follow Windows uses the Windows animation setting. On skips animation. Off always animates.

**Default:** Follow Windows

**Stored as:** `MotionMode (follow, on, off)` in settings.json.

**Notes:** Exposed as Appearance.ReduceMotion. Code that animates should check it first.

## What moves, and why

The motion spec lives in `design/motion-and-pages/Motion.dc.html`; the helpers are in `UI/Ui.cs`.

| Motion | Timing | Cause |
| --- | --- | --- |
| Page change (shared axis) | 420 ms, emphasized decelerate, 28 px from the side of the tab you moved toward | Shows which way you travelled in the tab strip |
| Cards and rows arrive | 420 ms rise on a gentle spring, 45 ms stagger capped at 12 items | Reading order follows the order things land |
| Press | scale 0.95 in 80 ms, back on a spring in 350 ms | The button answers before the work does |
| Live status dot | a ring breathes out every 1.8 s, only while something runs | Running things look alive; stopped things stay still |
| Open and close (console, settings drawer, pull progress) | 360 ms fade with a scale from 96 % at the top edge | The panel grows out of the button that opened it |
| Progress and hover | progress glides 500 ms; cards tint in 200 ms and lift 2 px | Numbers that jump look broken |

With reduce motion on, slides, springs, lifts and the breathing ring are skipped; content appears at once and colours change without a glide.
