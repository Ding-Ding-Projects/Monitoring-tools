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
| Appearance editor (theme, seed, text size, density, contrast) | partial | Appearance page; capture `theme-light-violet`. Gaps: per-element appearance editing beyond the global theme; density is stored but not applied to rows yet. |
| Colour picker | partial | Custom logo colour by hex. Gap: no full picker. |
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
| Browser-style tabs | missing | The control centre uses a navigation drawer. Tabs with docking, pinning, grouping and the four tab searches are not built. |
| Settings explain themselves | partial | New pages state what each control does and its default; older pages mostly do. |
| Guided forms, info icons, blank-editor presets | missing | |

## Safety and records

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Two-key super confirmation | done | Every destructive dialog; captures `super-confirm-start`, `super-confirm-armed`. |
| Toy locks on every element | partial | Element menu lock and unlock for the session. Gap: locks do not persist; no unlock ladder or support tickets. |
| Unlock ladder, support tickets, authenticator, QR pairing | missing | |
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
| Minecraft world generator, Ollama suite manager, Drawesome drawing, forge publishing, external editor hand-off | missing | |

## Not applicable here

| Feature | Reason |
| --- | --- |
| Browser-extension download hand-off | The app is not a browser extension. |
| Roblox models and realism | The app is not a game. |
| Encrypted public builder | The repository is public. |
