# Required features: delivery inventory

The maintainer's shared app requirements, checked against this app on 2026-11-04. Status words:
**done** (built and rendered from the real app by `--screenshots`), **partial** (works, with the gaps listed),
**missing**, **n/a** (does not apply, with the reason). Nothing is marked done from reading source alone.

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
| Material 3 conformance | partial | Colour roles, Roboto (pinned and embedded), pill buttons with state layers, 24 corner cards, Material 3 switch, 56 high search bar, pill drawer indicator; design canvas in `design/search-and-regex-builder`. Gaps: some older windows (toasts, offline dialog, setup wizard) still use the first design's shapes. |
| Motion with a full reduced-motion path | partial | Spring and decelerate curves; Reduce motion setting and low-stimulation mode skip entrance motion and the builder's container transform. Gap: toast slide-in is not yet gated. |
| Appearance editor (theme, seed, text size, density, contrast) | partial | Appearance page; capture `theme-light-violet`; "Edit appearance…" on any element opens a per-element editor (font, size, weight, colours, radius, padding, border) that persists per page. Gaps: per-state styles, style import and export; density is stored but not applied to rows yet. |
| Colour picker | partial | HSV square, hue and alpha, hex, RGB, HSL and OKLCH fields, theme swatches, recent colours, eyedropper and a contrast readout. Gap: no LAB or LCH fields. |
| Logo customization | partial | Logo colour from the seed or a custom hex. |
| Accessibility and sizing | partial | Text size 90 to 150 %, keyboard paths for search, menus and the confirmation gate, live regions. Gap: not checked at 200 % display scale. |

## Navigation, search and commands

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Onboarding wizard | done | Existing setup wizard. |
| Regex builder on every search | partial | Every page, palette, tray, version picker, notifications, changelog, docs, schedule rules and element menus; see [search](search/README.md) and its parity ledger. |
| Command palette | done | Existing; now on the shared search field. |
| Context menu on every element | partial | Right-click, Shift+F10 or Menu key in the control centre and tray: press, copy, search the page, edit appearance, lock; each menu has its own filter. Gaps: toasts and dialogs have no element menu; no per-element appearance editor behind "Edit appearance…". |
| Menus show working shortcuts | done | Shortcut hints in element menus; Ctrl+C and Ctrl+F work. |
| Browser-style tabs | partial | Tab strip docked left by default (top, right or bottom from its menu or Appearance), New tab catalog with search, pinning, groups with a move-into-group picker, overflow list, four tab searches, close tabs containing or not containing text, keyboard shortcuts, persistence; capture `control-*`. Gaps: per-tab appearance editor, Cantonese strings for the strip. |
| Settings explain themselves | partial | New pages state what each control does and its default; older pages mostly do. |
| Guided forms, info icons, blank-editor presets | partial | Every switch and choice has an info button; New shortcut is a guided form; a blank script offers presets. Gap: other editors have no guided flow. |

## Safety and records

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Two-key super confirmation | done | Every destructive dialog; captures `super-confirm-start`, `super-confirm-armed`. |
| Toy locks on every element | partial | Persistent locks from any element menu, a lock wizard, a veil and chip on locked elements, listed on the Safety and locks page. Gap: tabs and appearance values cannot be locked; the palette does not respect locks yet. |
| Unlock ladder, support tickets, authenticator | partial | Four-rung unlock ladder, a local Support Tickets desk, and a TOTP authenticator (RFC 6238 self-check passes 9 of 9 published vectors) with secrets protected by Windows for the current user. Gap: no passkey route. |
| QR pairing | missing | No QR decoder ships with Windows for this app; pasting an otpauth link or a Base32 secret is the supported route, and the page says so. |
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
| Ollama suite manager | partial | Detect, install with consent, start and stop, list, pull with progress, delete, loaded models and a chat test, all on localhost. Gaps: model catalogue, hardware fit, attachments. |
| Minecraft world generator | partial | Seed, level type, structures, single biome from the real jar, a bounded pre-generation with the server's own commands, folder or ZIP output. Gaps: no layer, noise or rule editors; overworld only. |
| Drawing and annotation | partial | Native Windows ink board (the named JavaScript library needs a web view this app does not have); pens, shapes, text, undo, PNG and SVG export. |
| Publishing to a forge | partial | Publish a folder to GitHub with bundled Git and an optional GitHub CLI, explicit consent, no stored tokens, recovery steps. Gap: github.com only. |
| External editor hand-off | done | VS Code, Notepad++, the default app or a custom program, for scripts and copied file paths. |

## Not applicable here

| Feature | Reason |
| --- | --- |
| Browser-extension download hand-off | The app is not a browser extension. |
| Roblox models and realism | The app is not a game. |
| Encrypted public builder | The repository is public. |
