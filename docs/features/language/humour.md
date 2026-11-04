# English humour and Cantonese humour

**What they do.** Two independent sliders, 1 to 5, choose the tone of headline messages: the offline dialog title, back online, public IP changed, conversion done and failed, push done and failed, game mode, snooze, empty history, no search results, update available and welcome. Level 1 is plain and precise; level 5 is playful.

**Default.** 3 for both.

**Rules.** Only the tone changes. Names, versions, times and results are passed in as arguments and are identical at every level. In Bilingual mode each language line uses its own slider. Each slider shows a live preview of four headlines at the chosen level.

**Levels.** 1 Plain, 2 Light, 3 Friendly, 4 Cheeky, 5 Playful.

**Keyboard.** Focus the slider and use the arrow keys; the value snaps to whole levels.

**For developers.** `Language.Headline(key, args)` returns the text for the current mode; `Language.Variant(key, language, level, args)` returns one specific variant.
