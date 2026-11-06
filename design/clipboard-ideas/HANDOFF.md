# Clipboard ideas: design handoff

Concept canvas made on 2026-11-06 to choose the next clipboard features. Nothing here is built. The toasts are
drawn in the app's own toast style (Mint seed Material 3 roles, Roboto and Roboto Mono, the 3 second bar along the
foot) on the site's dark ground. Sample values are made up and safe: 555 phone numbers, example addresses, the
documented UPS sample tracking number and a sample JWT.

| File | What it shows |
| --- | --- |
| `Main.dc.html` | Idea map of 22 ideas: 14 new kinds of copied content and 8 clipboard features. Each card names its first actions and whether it is offline, uses the internet or needs care. Tap to pick; filter by group or picks. |
| `Toasts1.dc.html` | Toasts for a phone number, a Unix timestamp, a sum or unit, a .NET stack trace (open the exact line in VS Code), cells copied from Excel, and a JWT token (kept out of history, cleared after 30 s). |
| `Toasts2.dc.html` | Toasts for a SHA-256 hash (check it against a file in Downloads), a private IP address, a Git clone URL, a picture with text or a QR code (read on the PC), a Windows error code (message from Windows itself) and a parcel tracking number. |
| `Power.dc.html` | Paste queue, snippets with fill-ins, transform recipes, clipboard undo, pin on screen and per-app rules, each working. |
| `frames/` | Local renders of each board. |

Ideas only on the map: street address, regex or cron rule, paste as plain text, and sending a copy to a phone.

Motion: toasts slide in staggered, the 3 second bar drains and pauses on hover, the error line and the OCR scan
line pulse, the ping bars beat, the token countdown runs, and reduce motion stops all of it.

Open decisions are which ideas to build, whether any action may use the internet, and the order.
