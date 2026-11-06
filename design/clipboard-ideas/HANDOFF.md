# Clipboard ideas: design handoff

Concept canvas made on 2026-11-06 to choose the next clipboard features. Every feature on it was picked ("add all"), and it is the build reference. The toasts are
drawn in the app's own toast style (Mint seed Material 3 roles, Roboto and Roboto Mono, the 3 second bar along the
foot) on the site's dark ground. Sample values are made up and safe: 555 phone numbers, example addresses, the
documented UPS sample tracking number and a sample JWT.

| File | What it shows |
| --- | --- |
| `Main.dc.html` | Idea map of 22 ideas: 14 new kinds of copied content and 8 clipboard features. Each card names its first actions and whether it is offline, uses the internet or needs care. Tap to pick; filter by group or picks. |
| `Toasts1.dc.html` | Toasts for a phone number, a Unix timestamp, a sum or unit, a .NET stack trace (open the exact line in VS Code), cells copied from Excel, and a JWT token (kept out of history, cleared after 30 s). |
| `Toasts2.dc.html` | Toasts for a SHA-256 hash (check it against a file in Downloads), a private IP address, a Git clone URL, a picture with text or a QR code (read on the PC), a Windows error code (message from Windows itself) and a parcel tracking number. |
| `Power.dc.html` | Paste queue, snippets with fill-ins, transform recipes, clipboard undo, pin on screen and per-app rules, each working. |
| `DlgHistory.dc.html` | Clipboard history popup on Win+Shift+V (Windows keeps Win+V). Every row previews its kind: link card, picture thumbnail, video frame, mini table, colour swatch, tidy phone number, stack trace. The picked copy shows large on the right. |
| `DlgSnippets.dc.html` | Snippets with a trigger, an optional hotkey, fill-ins and a live preview. |
| `DlgRecipes.dc.html` | Transform recipes: a chain of steps, a live result and a switch to show the recipe on text toasts. |
| `DlgRules.dc.html` | Per-app rules: toast and history behaviour and an automatic action per app. |
| `DlgPicture.dc.html` | Text and QR codes found in a picture, chosen by tapping lines. |
| `DlgFloat.dc.html` | Paste queue beside a form, a picture or table pinned on screen, and clipboard undo. |
| `DlgSettings.dc.html` | Clipboard settings with search: automatic lookups and exactly what is sent where, kinds on or off, shortcuts. |
| `Actions.dc.html` | The action bar rule every dialog and toast follows. |
| `MoreIdeas.dc.html` | 28 further ideas, all picked. |
| `DlgConvert.dc.html` | Quick conversions: copy an amount, see it in every unit of its kind, copy any result. Weight, length, volume, temperature, area, speed, data size, time, energy, pressure, fuel use, angle, number bases and money. |
| `Extensions.dc.html` | Extensions manager, the permission prompt, and how an extension plugs in: an `extension.json` with patterns and actions, and a program that reads one JSON line and writes one back. |
| `frames/` | Local renders of each board. |

Ideas only on the map: street address, regex or cron rule, paste as plain text, and sending a copy to a phone.

Motion: toasts slide in staggered, the 3 second bar drains and pauses on hover, the error line and the OCR scan
line pulse, the ping bars beat, the token countdown runs, and reduce motion stops all of it.

Decided: build everything, automatic lookups allowed with a setting that shows what is sent where, never for secrets or password-manager copies. Parcel status only opens the carrier page, because live status needs carrier keys. The IP sample uses a documentation address.
