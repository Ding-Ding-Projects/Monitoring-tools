# Required features: delivery inventory

The maintainer's shared app requirements, checked against this app on 2026-11-04. Status words:
**done** (built and rendered from the real app by `--screenshots`), **partial** (works, with the gaps listed),
**missing**, **n/a** (does not apply, with the reason). Nothing is marked done from reading source alone. `MonitoringTools.exe --selftest <folder>` drives 28 checks inside the built app in a throwaway data folder and writes a pass or fail report.

## Language, voice and personalization

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Language modes (English, Hong Kong Cantonese, bilingual) | partial | Language and voice page; about 580 table entries and 60 patterns; captures `lang-cantonese-*`, `lang-bilingual-*`. Gaps: wording not reviewed by a native speaker; text set after a window loads needs `Language.Refresh`; accessible names are not translated. |
| Independent English and Cantonese humour levels (1 to 5) | partial | Sliders on the Language page; 14 headline keys with five variants each. Gap: not every toast call site uses the headline keys yet. |
| Emoji in dialogs switch | done | Language page; toast titles go through `Language.Title`. |
| Personal wording file (upload and drop) | done | Service page and drop on any window (existing). |
| School mode | partial | Modes page: renamable, PIN with PBKDF2-SHA256, back-off, shared record. Gaps: PIN only (no passkey or password), Cantonese and humour controls are not hidden yet while it is on. |
| Narrator | partial | Windows voices, separate English and Cantonese pickers, queue, test button. Gap: not exercised with real voices on this machine. |
| Action sound effects | done | Existing; now governed by Modes. |
| Scheduled and external settings | partial | Weekday and time rules, external JSON file with validation and watching. Gaps: no date ranges, no in-place rule editing. |
| App rename | done | Appearance → display name drives the drawer title. |
| ADHD modes | partial | Five switches and reminders. Gap: Focus spotlight and palette entries. |
| Dim sum surprise | partial | Modes page; reads the public photo catalogue, with an honest unavailable state. Gap: not exercised against the live catalogue. |

## Material 3, appearance and layout

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Material 3 conformance | partial | Colour roles, Roboto (pinned and embedded), pill buttons with state layers, 24 to 28 corner containers, Material 3 switch, 56 high search bar, tab strip; toasts, the offline dialog, setup wizard, welcome tour, file tools, commit dialog, tray menu and confirmation all read theme roles live.  |
| Motion with a full reduced-motion path | done | Spring and decelerate curves; Reduce motion and low-stimulation mode skip entrance motion, toast slides, the builder container transform and menus. |
| Appearance editor (theme, seed, text size, density, contrast) | partial | Appearance page; per-element editor from any element menu; per-tab and per-group looks (Shift+right-click); density applied to rows, cards, tabs and buttons; self-test checks the light Violet surface and compact rows. Per-state styles, letter spacing, underline, presets, copy and paste, undo and redo, history and JSON import and export are built (see appearance-editor.md); not driven or captured yet. |
| Colour picker | partial | HSV square, hue and alpha, hex, RGB, HSL and OKLCH fields, theme swatches, recent colours, eyedropper and a contrast readout. LAB and LCH fields translate with the others. |
| Logo customization | partial | Logo colour from the seed or a custom hex. |
| Accessibility and sizing | partial | Text size 90 to 150 %, keyboard paths for search, menus and the confirmation gate, live regions. Gap: not checked at 200 % display scale. |

## Navigation, search and commands

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Onboarding wizard | done | Existing setup wizard. |
| Regex builder on every search | partial | Every page, palette, tray, version picker, lists and menus; Build, Test (with expected outcomes), Debug (traced and checked against .NET), Compare (.NET engine modes), Benchmark (growth), Replace, Files (search and replace with backups), Code and Library (opt-in history) views; `--regex` command line. See the parity ledger for what remains. |
| Command palette | done | Existing; now on the shared search field. |
| Context menu on every element | partial | Right-click, Shift+F10 or Menu key in the control centre and tray: press, copy, search the page, edit appearance, lock; each menu has its own filter. Gaps: toasts and dialogs have no element menu; no per-element appearance editor behind "Edit appearance…". |
| Menus show working shortcuts | done | Shortcut hints in element menus; Ctrl+C and Ctrl+F work. |
| Browser-style tabs | done | Tab strip docked left by default and movable to any edge, New tab catalog, pinning, groups with a picker, overflow, four tab searches, bulk close by text, per-tab and per-group appearance, shortcuts, Cantonese, persistence. |
| Settings explain themselves | partial | New pages state what each control does and its default; older pages mostly do. |
| Guided forms, info icons, blank-editor presets | partial | Every switch and choice has an info button; New shortcut is a guided form; a blank script offers presets. Gap: other editors have no guided flow. |

## Safety and records

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Two-key super confirmation | done | Every destructive dialog; captures `super-confirm-start`, `super-confirm-armed`. |
| Toy locks on every element | partial | Persistent locks with a wizard and veil, respected by the command palette; self-test locks, refuses removal while locked, unlocks with the PIN and removes. Appearance values can be locked from the editor (Library tab). |
| Unlock ladder, support tickets, authenticator | done | Four-rung ladder, Support Tickets desk, TOTP authenticator (RFC 6238 vectors pass in the self-test) with Windows-protected secrets, and Windows Hello as an extra unlock route where available. |
| QR pairing | done | A built-in QR decoder and encoder (versions 1 to 10): read a code from the clipboard or a file, show an account as a code behind the two-key confirmation. Round-trip in the self-test; 320 of 320 rotated and noisy test images decoded. Gaps: versions above 10, mirrored codes, phone photos untested. |
| Local history | partial | Settings history with restore and diff (Records page). |
| Export everything | partial | History, activity, settings, notifications and settings history as JSON, CSV (formula-safe), Markdown, HTML and text. |
| Bulk actions | partial | Clipboard history select mode: copy, save, remove. Gap: other lists. |
| Changelog viewer | done | Records page, cached releases rendered as Markdown. |
| Offline documentation browser | done | Help and docs page: task-led landing, article reader, search with the regex builder. |
| Notification centre | done | Records page; toasts and offline alerts are logged. |

## Built-in tools

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| File converter, Base64 toolkit, Minecraft server manager | done | Existing pages and file tools. |
| Ollama suite manager | partial | Detect, install with consent, start and stop, a dated model catalogue with a hardware-fit verdict from real RAM, VRAM and disk, a pull cart, model details, loaded models, a chat test and a troubleshooter. Gap: chat attachments, not exercised against a live server here. |
| Minecraft world generator | partial | Seed, level type, structures, single biome, game-rule editor and superflat layers from the real jar, bounded pre-generation in the overworld, Nether and End, folder or ZIP output. Gaps: noise and dimension-type editors, not exercised against a real jar here. |
| Drawing and annotation | partial | Native Windows ink board (the named JavaScript library needs a web view this app does not have); pens, shapes, text, undo, PNG and SVG export. |
| Publishing to a forge | partial | Publish a folder to GitHub with bundled Git and an optional GitHub CLI, explicit consent, no stored tokens, recovery steps. Gap: github.com only. |
| External editor hand-off | done | VS Code, Notepad++, the default app or a custom program, for scripts and copied file paths. |

## Not applicable here

| Feature | Reason |
| --- | --- |
| Browser-extension download hand-off | The app is not a browser extension. |
| Roblox models and realism | The app is not a game. |
| Encrypted public builder | The repository is public. |
