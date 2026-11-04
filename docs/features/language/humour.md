# English humour and Cantonese humour

**What they do.** Two independent sliders, 1 to 5, choose the tone of headline messages: the offline dialog title, back online, public IP changed, conversion done and failed, push done and failed, game mode, snooze, empty history, no search results, update available and welcome. Level 1 is plain and precise; level 5 is playful.

**Default.** 3 for both.

**Rules.** Only the tone changes. Names, versions, times and results are passed in as arguments and are identical at every level. In Bilingual mode each language line uses its own slider. Each slider shows a live preview of four headlines at the chosen level.

**Levels.** 1 Plain, 2 Light, 3 Friendly, 4 Cheeky, 5 Playful.

**Keyboard.** Focus the slider and use the arrow keys; the value snaps to whole levels.

**Where it is used.** Every toast and dialog headline written in `ToastManager`, `App` and the offline dialog goes through `Language.Headline`: the offline dialog and its causes (`offline.*`), back online, public IP changed, conversion started, saved, failed, cancelled and FFmpeg missing, push and shortcut results (`push.*`, `shortcut.*`), the generic result of any background task (`task.done`, `task.failed`), and the notices the app shows by title (`notice.*`, `mode.on`). The facts (file format, address, repository, exit code, seconds) are arguments and appear in every level; when a result has more facts than the headline, they stay in the toast's second line or body. Level 1 of each key is the original plain wording.

**School mode.** While School mode is on, every headline is the plain level 1 wording, the humour sliders are not shown, and language is English.

**Not routed yet.** Headlines built outside these files are unchanged: the welcome, game mode, snooze, empty history, no search results and update available keys exist but their call sites in other windows do not use them yet. Notice titles that carry their own facts and have no key (for example the sentence built for a file-tools result) also stay as written.

**For developers.** `Language.Headline(key, args)` returns the text for the current mode; `Language.Variant(key, language, level, args)` returns one specific variant.
