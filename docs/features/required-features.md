# Required features: delivery inventory

The maintainer's shared app requirements, checked against this app on 2026-11-04. Status words:
**done** (built and rendered from the real app by `--screenshots`), **partial** (works, with the gaps listed),
**missing**, **n/a** (does not apply, with the reason). Nothing is marked done from reading source alone. `MonitoringTools.exe --selftest <folder>` drives 45 checks inside the built app in a throwaway data folder and writes a pass or fail report.

## Language, voice and personalization

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Language modes (English, Hong Kong Cantonese, bilingual) | partial | Language and voice page; about 580 table entries and 60 patterns; captures `lang-cantonese-*`, `lang-bilingual-*`. Accessible names and help text are translated and restored like visible text. Gaps: wording not reviewed by a native speaker; a text block changed after it has loaded needs `Language.Refresh`. |
| Independent English and Cantonese humour levels (1 to 5) | partial | Sliders on the Language page; about 45 headline keys with five variants each; every headline in the toast manager, the offline dialog and the app start-up path uses them, and the notice titles follow. Gap: the welcome, game mode, snooze, empty history, no results and update available keys still have no call site outside these files. |
| Emoji in dialogs switch | done | Language page; toast titles go through `Language.Title`. |
| Personal wording file (upload, drop and copy) | done | Service page, drop on any window, or copy the file or its path; unrelated JSON files are ignored. Self-test applies a copied file, ignores a `package.json` and respects the switch. |
| School mode | partial | Modes page: renamable, PIN with PBKDF2-SHA256, back-off, shared record. PIN or password (separate PBKDF2 hashes, 8 or more characters), the Language page hides the language choices, humour sliders and personal wording while it is on, and English is forced. Windows Hello is an optional third way to turn the mode off, set up on the Modes page (self-test checks drive setup, refusals, unlock and the unavailable states; capture `school-hello`). It is Windows Hello user verification, not a FIDO2 credential. Gap: no FIDO2 passkey; the real Hello prompt was not answered on this machine (the checks stub the Hello answer, the real availability check ran). |
| Narrator | partial | Windows voices, separate English and Cantonese pickers, queue, test button. Gap: not exercised with real voices on this machine. |
| Action sound effects | done | Existing; now governed by Modes. |
| Scheduled and external settings | partial | Weekday and time rules with optional start and end dates, in-place rule editing, external JSON file with validation and watching; the date-range rule evaluation is covered by a self-test check. Built but not yet driven or captured on screen. Date and time are validated text boxes, not native pickers. |
| App rename | done | Appearance → display name drives the drawer title. |
| ADHD modes | partial | Five switches and reminders, a Focus spotlight (Modes page and Ctrl+Shift+Space) and palette entries for every switch. Gap: the spotlight has not been exercised in a running build by its author. |
| Dim sum surprise | partial | Modes page; reads the public photo catalogue, with an honest unavailable state. Gap: not exercised against the live catalogue. |

## Material 3, appearance and layout

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Material 3 conformance | partial | Colour roles, Roboto (pinned and embedded), pill buttons with state layers, 24 to 28 corner containers, Material 3 switch, 56 high search bar, tab strip; toasts, the offline dialog, setup wizard, welcome tour, file tools, commit dialog, tray menu and confirmation all read theme roles live.  |
| Motion with a full reduced-motion path | done | Spring and decelerate curves; Reduce motion and low-stimulation mode skip entrance motion, toast slides, the builder container transform and menus. |
| Appearance editor (theme, seed, text size, density, contrast) | partial | Appearance page; per-element editor from any element menu; per-tab and per-group looks (Shift+right-click); density applied to rows, cards, tabs and buttons; self-test checks the light Violet surface and compact rows. Per-state styles, letter spacing, underline, presets, copy and paste, undo and redo, history and JSON import and export are built (see appearance-editor.md); not driven or captured yet. |
| Colour picker | partial | HSV square, hue and alpha, hex, RGB, HSL, HSV, HWB, CMYK, OKLCH, CIELAB and LCH fields with gamut clipping notes, theme swatches, recent colours, eyedropper and a contrast readout. Gap: not every field combination is captured. |
| Logo customization | done | Logo colour from the seed or a custom hex, plus twelve glyphs from the icon set or one custom PNG or ICO (256 KB, 16 to 512 px, signature and decode checked, copied into the data folder) for the drawer logo and the tray icon, with reset. Self-test checks validation, import, glyph choice, reset, the drawer header and the tray bitmap; capture `logo-custom`. The tray icon itself was not photographed (it lives outside the app windows); its bitmap is shown in the preview and compared in the check. |
| Accessibility and sizing | partial | Text size 90 to 150 %, keyboard paths for search, menus and the confirmation gate, live regions. Captures `scale200-*` render overview, history, appearance, safety and Ollama at 960 by 540 (1920 by 1080 at 200 %) in English and bilingual with no clipped content. Gap: bilingual text is incomplete on the Ollama and Safety pages. |

## Navigation, search and commands

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Onboarding wizard | done | Existing setup wizard. |
| Regex builder on every search | partial | Every page, palette, tray, version picker, lists and menus; Build, Test (with expected outcomes), Debug (traced and checked against .NET), Compare (.NET engine modes), Benchmark (growth), Replace, Files (search and replace with backups), Code and Library (opt-in history) views; `--regex` command line. See the parity ledger for what remains. |
| Command palette | done | Existing; now on the shared search field. |
| Context menu on every element | partial | Right-click, Shift+F10 or Menu key in the control centre and tray: press, copy, search the page, edit appearance, lock; each menu has its own filter. "Edit appearance…" opens the per-element appearance editor. Windows now carrying the menu: control centre, tray, drawing board, toasts, offline dialog, setup, welcome, file tools, commit message, dim sum and the appearance editor, confirmation and lock cards (see [element menu](app/element-menu.md)). Confirmation and lock windows offer Copy text only, so no entry can step around a gate. Gaps: not yet checked by a capture; the command palette overlay has no menu; toasts take the menu by pointer only. |
| Menus show working shortcuts | done | Shortcut hints in element menus; Ctrl+C and Ctrl+F work. |
| Browser-style tabs | done | Tab strip docked left by default and movable to any edge, New tab catalog, pinning, groups with a picker, overflow, four tab searches, bulk close by text, per-tab and per-group appearance, shortcuts, Cantonese, persistence. |
| Settings explain themselves | partial | Every switch and choice on the older Clipboard, Network, Game, Service and Shortcuts pages now carries a visible subtitle with what it does and its default, beside the information button; newer pages already did. Built but not yet captured on screen. |
| Guided forms, info icons, blank-editor presets | done | Every switch and choice has an info button; New shortcut, Add a DNS profile, New Minecraft server, Add a schedule rule, Make a look (theme, seed, contrast, density, text size, review) and New chat profile are guided forms with Back, Next, validation and a review. The single forms remain beside them. Self-test checks drive the schedule, look and profile forms through their real buttons; capture `guided-look`. A blank script offers presets. The per-element appearance editor window stays one form by design (it edits many properties at once). See [guided forms](app/guided-forms.md). |

## Safety and records

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Two-key super confirmation | done | Every destructive dialog; captures `super-confirm-start`, `super-confirm-armed`. |
| Toy locks on every element | partial | Persistent locks with a wizard and veil, respected by the command palette; self-test locks, refuses removal while locked, unlocks with the PIN and removes. Appearance values can be locked from the editor (Library tab). |
| Unlock ladder, support tickets, authenticator | done | Four-rung ladder, Support Tickets desk, TOTP authenticator (RFC 6238 vectors pass in the self-test) with Windows-protected secrets, and Windows Hello as an extra unlock route where available. |
| QR pairing | done | A built-in QR decoder and encoder (versions 1 to 10): read a code from the clipboard or a file, show an account as a code behind the two-key confirmation. Round-trip in the self-test; 320 of 320 rotated and noisy test images decoded. Gaps: versions above 10, mirrored codes, phone photos untested. |
| Local history | partial | Settings history with restore and diff (Records page). Shortcut scripts and the commit message template also keep up to 50 versions each, with a version list, line comparison and a two-key Restore (see [local history](tools/local-history.md)). Gaps: no encrypted snapshots, no password-protected manager, no retention setting. |
| Export everything | done | One Export everything action writes a zip holding settings, activity, notifications, settings history, shortcuts, the regex library, appearance and looks, schedule rules, lock records without secrets, and DNS profiles in JSON, CSV (formula-safe), Markdown, HTML and text; clipboard history is opt-in. Self-test opens the zip and checks files, formats, no lock hash or secret, and the Records page action; capture `export-everything`. The save dialog itself was not driven. |
| Bulk actions | done | One shared select mode ([bulk actions](app/bulk-actions.md)) on clipboard history, activity, notifications, shortcuts, watched games, the regex Library, installed models, settings history, the changelog, the outage list and schedule rules. Self-test checks settings history delete (newest kept), release export, outage removal (ongoing kept) and select mode on the rules and activity pages. Lists without bulk actions are named in the document with reasons. The regex Library still has no search of its own. |
| Changelog viewer | done | Records page, cached releases rendered as Markdown. |
| Offline documentation browser | done | Help and docs page: task-led landing, article reader, search with the regex builder. |
| Notification centre | done | Records page; toasts and offline alerts are logged. |

## Built-in tools

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| File converter, Base64 toolkit, Minecraft server manager | done | Existing pages and file tools. |
| Ollama suite manager | partial | Detect, install with consent, start and stop, a dated model catalogue with a hardware-fit verdict from real RAM, VRAM and disk, a pull cart, model details, loaded models, a chat test and a troubleshooter. Chat takes up to six image or text attachments. Gap: not exercised against a live server here. |
| Minecraft world generator | partial | Seed, level type, structures, single biome, game-rule editor and superflat layers from the real jar, bounded pre-generation in the overworld, Nether and End, folder or ZIP output. Gaps: noise and dimension-type editors, not exercised against a real jar here. |
| Drawing and annotation | partial | Native Windows ink board (the named JavaScript library needs a web view this app does not have); pens, shapes, text, undo, PNG and SVG export. A self-test check now opens a real board window and drives stroke, undo, redo, SVG and PNG render. Gap: not exercised with a real pen or touch device; width is applied when the stroke ends; the library itself is deliberately not used. |
| Publishing to a forge | partial | Publish a folder to GitHub with bundled Git and an optional GitHub CLI, explicit consent, no stored tokens, recovery steps. Also GitLab (gitlab.com or self-hosted) and Gitea/Forgejo, with `glab` and `tea` preferred when signed in and a one-time pasted token otherwise (see [publishing](tools/publishing.md)). Gap: the GitLab and Gitea routes have not been run against real servers. |
| External editor hand-off | done | VS Code, Notepad++, the default app or a custom program, for scripts and copied file paths. |

## Not applicable here

| Feature | Reason |
| --- | --- |
| Browser-extension download hand-off | The app is not a browser extension. |
| Roblox models and realism | The app is not a game. |
| Encrypted public builder | The repository is public. |
