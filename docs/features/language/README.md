# Language and voice

The Language and voice page (Control centre, Settings) controls the language of the whole app, the tone of headline messages and the optional narrator.

| Control | What it does | Default | Article |
| --- | --- | --- | --- |
| App language | English, Hong Kong Cantonese or Bilingual, applied live to every window | English | [app-language.md](app-language.md) |
| English humour | Tone of English headline messages, level 1 to 5 | 3 | [humour.md](humour.md) |
| Cantonese humour | Tone of Cantonese headline messages, level 1 to 5 | 3 | [humour.md](humour.md) |
| Emoji in dialogs | Keep or strip emoji at the start and end of titles | On | [emoji-in-dialogs.md](emoji-in-dialogs.md) |
| Read toasts aloud | Speak toast titles with a Windows voice | Off | [narrator.md](narrator.md) |
| English voice, Cantonese voice | Pick the installed Windows voice for each language | Automatic | [voices.md](voices.md) |
| Speaking rate | Speed of the narrator, 0.5x to 2.0x | 1.0x | [speaking-rate.md](speaking-rate.md) |
| Test voice, Stop speaking | Hear the current voice, or silence it | n/a | [test-voice.md](test-voice.md) |

## How it works

- `Core/Language.cs` is the language layer. It walks every window, remembers the English original of each piece of text and shows the text for the current mode. Switching back to English restores the exact original strings. It runs before the personal wording layer, so personal wording is applied on top of the chosen language and clearing it restores the chosen language.
- `Core/Strings.yue.cs` holds the Cantonese table plus pattern rules for built strings such as counts and times. Paths, commands, codes, format names, game titles and product names are deliberately left in English. Text in a monospace font is never translated, exactly like the wording layer.
- `Core/Narrator.cs` wraps `Windows.Media.SpeechSynthesis` and plays through `MediaPlayer` with a single queue.
- `Core/Settings.Language.cs` holds the persisted settings.

## Developer hooks

```csharp
Core.Language.Initialize();                          // once at start-up, after WordingLayer.Install()
Core.Language.T("Clipboard history");                // text for the current mode
Core.Language.Headline("convert.done", "MP4");       // humour headline; facts go in as arguments
Core.Language.Title(title);                          // strips emoji when the switch is off
Narrator.Speak(title, Core.Language.Yue(title));     // queues speech for a toast title
```

Inside a class derived from `FrameworkElement` (any window), write `Core.Language` or alias it, because `FrameworkElement.Language` hides the type name.

Headline keys: `offline.title`, `online.title`, `ip.changed`, `convert.done`, `convert.failed`, `push.done`, `push.failed`, `game.mode`, `snooze`, `history.empty`, `search.none`, `update.available`, `welcome`, `narrator.test`, plus `convert.start`, `convert.cancelled`, `ffmpeg.missing`, `task.done`, `task.failed`, `shortcut.done`, `shortcut.failed`, `offline.noadapter`, `offline.noaddress`, `offline.noroute`, `offline.signin`, `offline.router`, `notice.*` and `mode.on`. `Language.NoticeTitle(title)` maps a plain notice title to its key, and `Language.NetHeadline(diagnosis)` maps a network diagnosis the same way. Each key has five English and five Cantonese variants, and the self-test checks that the levels differ while keeping the same facts.

## Failure modes

- A string with no Cantonese entry stays in English in every mode; it is never replaced by a guess.
- Without an installed Cantonese voice, Cantonese is not spoken and the page says so, with a link to the Windows speech settings (`ms-settings:speech`).
- Speech failures are logged and shown on the page; they never block a toast.

## Verification

Compile-checked, with self-test checks written for headline levels and facts, accessible-name round trips and the School mode language page (not yet run by the author on a hidden desktop). Runtime checks still to do: switching the mode with several windows open, restoring English exactly, bilingual line breaks in narrow chips, and speaking with real installed voices.
