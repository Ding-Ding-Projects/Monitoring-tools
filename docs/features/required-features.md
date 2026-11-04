# Required features: delivery inventory

The maintainer's shared app requirements, checked against this app on 2026-11-04. Status words:
**done** (built and rendered from the real app by `--screenshots`), **partial** (works, with the gaps listed),
**missing**, **n/a** (does not apply, with the reason). Nothing is marked done from reading source alone. `MonitoringTools.exe --selftest <folder>` drives 71 checks inside the built app in a throwaway data folder and writes a pass or fail report.

## Language, voice and personalization

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Language modes (English, Hong Kong Cantonese, bilingual) | partial | Language and voice page; about 580 table entries and 60 patterns; captures `lang-cantonese-*`, `lang-bilingual-*`. Accessible names and help text are translated and restored like visible text. Gaps: wording not reviewed by a native speaker; a text block changed after it has loaded needs `Language.Refresh`. |
| Independent English and Cantonese humour levels (1 to 5) | partial | Sliders on the Language page; about 45 headline keys with five variants each; every headline in the toast manager, the offline dialog and the app start-up path uses them, and the notice titles follow. Gap: the welcome, game mode, snooze, empty history, no results and update available keys still have no call site outside these files. |
| Emoji in dialogs switch | done | Language page; toast titles go through `Language.Title`. |
| Personal wording file (upload, drop and copy) | done | Service page, drop on any window, or copy the file or its path; unrelated JSON files are ignored. Self-test applies a copied file, ignores a `package.json` and respects the switch. |
| School mode | partial | Modes page: renamable, PIN with PBKDF2-SHA256, back-off, shared record. PIN or password (separate PBKDF2 hashes, 8 or more characters), the Language page hides the language choices, humour sliders and personal wording while it is on, and English is forced. Driven in the built app by the self-test check "school: a PIN set through the Modes page..." (PIN boxes and Turn on, English forced, Language page choices hidden, a wrong PIN refused, the right PIN turns it off) and captured as `school-on-language`. A throwaway-data run now keeps the shared record inside its own folder. Gap: no passkey. |
| Narrator | partial | Windows voices, separate English and Cantonese pickers, queue, test button. Gap: not exercised with real voices on this machine. |
| Action sound effects | done | Existing; now governed by Modes. |
| Scheduled and external settings | partial | Weekday and time rules with optional start and end dates, in-place rule editing, external JSON file with validation and watching. The Modes page form is driven by a self-test check (rule created with a date range, a reversed range refused, Edit loads the rule, Save changes updates it in place, stored fields asserted) and captured as `modes-schedule-edit`. Date and time are validated text boxes, not native pickers. Gap: following the external settings file is not driven through the page here. |
| App rename | done | Appearance → display name drives the drawer title. |
| ADHD modes | partial | Five switches and reminders, a Focus spotlight (Modes page and Ctrl+Shift+Space) and palette entries for every switch. The spotlight is driven through its Modes page switch by a self-test check (the dim layer is added to the open window and removed again, the setting follows) and captured as `adhd-spotlight`. Gap: the effects of the other five modes are not driven in a running window by this check. |
| Dim sum surprise | partial | Modes page; reads the public photo catalogue, with an honest unavailable state. Gap: not exercised against the live catalogue. |

## Material 3, appearance and layout

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Material 3 conformance | partial | Colour roles, Roboto (pinned and embedded), pill buttons with state layers, 24 to 28 corner containers, Material 3 switch, 56 high search bar, tab strip; toasts, the offline dialog, setup wizard, welcome tour, file tools, commit dialog, tray menu and confirmation all read theme roles live.  |
| Motion with a full reduced-motion path | done | Spring and decelerate curves; Reduce motion and low-stimulation mode skip entrance motion, toast slides, the builder container transform and menus. |
| Appearance editor (theme, seed, text size, density, contrast) | partial | Appearance page; per-element editor from any element menu; per-tab and per-group looks (Shift+right-click); density applied to rows, cards, tabs and buttons. The editor is driven by a self-test check: every tab opens, a Hover and a Pressed text colour are typed through the picker and stored per state (the normal look is untouched), Undo and Redo step through them, a preset is saved and applied to a second element, and an export then import keeps the per-state colours. Captured as `appearance-editor-font`, `-colour`, `-shape`, `-library`, `-states` and `-presets`. Gap: the Export and Import buttons open native file dialogs, which are not driven; the same export and import calls are asserted directly. |
| Colour picker | partial | HSV square, hue and alpha, hex, RGB, HSL, HSV, HWB, CMYK, OKLCH, CIELAB and LCH fields with gamut clipping notes, theme swatches, recent colours, eyedropper and a contrast readout. A self-test check types the value each of the nine fields shows into that field (after changing the colour) and asserts the colour and the HEX field come back, for an exact colour and a rounded one; an out-of-range OKLCH value reports clipping, a short hex expands and an unreadable value changes nothing. Captured as `colour-picker`. Gap: the screen eyedropper is not driven. |
| Logo customization | partial | Logo colour from the seed or a custom hex. |
| Accessibility and sizing | partial | Text size 90 to 150 %, keyboard paths for search, menus and the confirmation gate, live regions. Captures `scale200-*` render overview, history, appearance, safety and Ollama at 960 by 540 (1920 by 1080 at 200 %) in English and bilingual with no clipped content. Gap: bilingual text is incomplete on the Ollama and Safety pages. |

## Navigation, search and commands

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Onboarding wizard | done | Existing setup wizard. |
| Regex builder on every search | partial | Every page, palette, tray, version picker, lists and menus; Build, Test (with expected outcomes), Debug (traced and checked against .NET), Compare (.NET engine modes), Benchmark (growth), Replace, Files (search and replace with backups), Code and Library (opt-in history) views; `--regex` command line. See the parity ledger for what remains. |
| Command palette | done | Existing; now on the shared search field. |
| Context menu on every element | partial | Right-click, Shift+F10 or Menu key in the control centre and tray: press, copy, search the page, edit appearance, lock; each menu has its own filter. "Edit appearance…" opens the per-element appearance editor. Windows now carrying the menu: control centre, tray, drawing board, toasts, offline dialog, setup, welcome, file tools, commit message, dim sum and the appearance editor, confirmation and lock cards (see [element menu](app/element-menu.md)). A self-test check opens the menu on a toast, the offline dialog, the setup wizard, the confirmation window (asserted to offer only Copy text, on its button and on a key) and file tools; captured as `menu-toast`, `menu-offline`, `menu-setup`, `menu-confirm` and `menu-filetools`. The check opens the menu through the same routine the right-click handler calls, because the synthetic mouse event is not delivered to the handler on a hidden desktop. Gaps: the command palette overlay has no menu; no real pointer click was made. |
| Menus show working shortcuts | done | Shortcut hints in element menus; Ctrl+C and Ctrl+F work. |
| Browser-style tabs | done | Tab strip docked left by default and movable to any edge, New tab catalog, pinning, groups with a picker, overflow, four tab searches, bulk close by text, per-tab and per-group appearance, shortcuts, Cantonese, persistence. |
| Settings explain themselves | done | Every switch and choice on every control-centre page and every setup step shows a visible sentence saying what it does, usually with its default, beside the information button. A self-test check walks all pages and the seven setup steps and fails for any switch or choice without one (a caption directly under the control counts). It found and the same task fixed the missing ones on the Minecraft world generator, Tools, Ollama and Modes pages, the schedule form, the EULA switches and four setup switches. Not covered by the walk: windows other than the control centre and the setup wizard. |
| Guided forms, info icons, blank-editor presets | partial | Every switch and choice has an info button; New shortcut, Add a DNS profile and New Minecraft server are guided forms; a blank script offers presets. A self-test check drives the DNS profile form (all three steps, empty and invalid input refused, Back keeps answers, saves a profile) and the new-server form (all four steps, version, port and EULA validation, Cancel drops the draft without creating a server); each step is captured as `guided-dns-1` to `-3` and `guided-minecraft-1` to `-4`. Gap: the schedule rule form, Appearance edits and Ollama profiles are single forms. See [guided forms](app/guided-forms.md). |

## Safety and records

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| Two-key super confirmation | done | Every destructive dialog; captures `super-confirm-start`, `super-confirm-armed`. |
| Toy locks on every element | partial | Persistent locks with a wizard and veil, respected by the command palette; self-test locks, refuses removal while locked, unlocks with the PIN and removes. Appearance values can be locked from the editor (Library tab). |
| Unlock ladder, support tickets, authenticator | done | Four-rung ladder, Support Tickets desk, TOTP authenticator (RFC 6238 vectors pass in the self-test) with Windows-protected secrets, and Windows Hello as an extra unlock route where available. |
| QR pairing | done | A built-in QR decoder and encoder (versions 1 to 10): read a code from the clipboard or a file, show an account as a code behind the two-key confirmation. Round-trip in the self-test; 320 of 320 rotated and noisy test images decoded. Gaps: versions above 10, mirrored codes, phone photos untested. |
| Local history | partial | Settings history with restore and diff (Records page). Shortcut scripts and the commit message template also keep up to 50 versions each, with a version list, line comparison and a two-key Restore (see [local history](tools/local-history.md)). A self-test check saves three versions of a script through the editor, opens the History panel, picks the oldest and asserts the comparison, and that Restore asks for the two-key confirmation; captured as `history-panel`. Gaps: no encrypted snapshots, no password-protected manager, no retention setting. |
| Export everything | partial | History, activity, settings, notifications and settings history as JSON, CSV (formula-safe), Markdown, HTML and text. |
| Bulk actions | partial | One shared select mode ([bulk actions](app/bulk-actions.md)) on clipboard history, activity, notifications, shortcuts, watched games, the regex Library and installed models. A self-test check drives it on activity, notifications, shortcuts and the regex Library: the count reads 0, 2 and all, Select none works, actions stay disabled until a row is chosen, and the destructive action (Remove, Delete…, Delete selected…) is shown to need both keys and a full slide (sliding with no key or one key does nothing; Notifications completes and removes exactly the chosen rows; the others are cancelled with Emergency exit). Captured as `bulk-activity`, `bulk-notifications`, `bulk-shortcuts` and `bulk-library`. Gap: watched games and installed models are not driven; lists without per-item actions (settings history, changelog, outages) have no select mode; the regex Library has no search of its own. |
| Changelog viewer | done | Records page, cached releases rendered as Markdown. |
| Offline documentation browser | done | Help and docs page: task-led landing, article reader, search with the regex builder. |
| Notification centre | done | Records page; toasts and offline alerts are logged. |

## Built-in tools

| Feature | Status | Evidence and gaps |
| --- | --- | --- |
| File converter, Base64 toolkit, Minecraft server manager | done | Existing pages and file tools. |
| Ollama suite manager | partial | Detect, install with consent, start and stop, a dated model catalogue with a hardware-fit verdict from real RAM, VRAM and disk, a pull cart, model details, loaded models, a chat test and a troubleshooter. Chat takes up to six image or text attachments. Gap: not exercised against a live server here. |
| Minecraft world generator | partial | Seed, level type, structures, single biome, game-rule editor and superflat layers from the real jar, bounded pre-generation in the overworld, Nether and End, folder or ZIP output. Gaps: noise and dimension-type editors, not exercised against a real jar here. |
| Drawing and annotation | partial | Native Windows ink board (the named JavaScript library needs a web view this app does not have); pens, shapes, text, undo, PNG and SVG export. |
| Publishing to a forge | partial | Publish a folder to GitHub with bundled Git and an optional GitHub CLI, explicit consent, no stored tokens, recovery steps. Also GitLab (gitlab.com or self-hosted) and Gitea/Forgejo, with `glab` and `tea` preferred when signed in and a one-time pasted token otherwise (see [publishing](tools/publishing.md)). Gap: the GitLab and Gitea routes have not been run against real servers. |
| External editor hand-off | done | VS Code, Notepad++, the default app or a custom program, for scripts and copied file paths. |

## Not applicable here

| Feature | Reason |
| --- | --- |
| Browser-extension download hand-off | The app is not a browser extension. |
| Roblox models and realism | The app is not a game. |
| Encrypted public builder | The repository is public. |
