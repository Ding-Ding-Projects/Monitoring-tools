# App language

**What it does.** Chooses the language of every page, toast, dialog, tray menu and palette entry: English, Hong Kong Cantonese (traditional characters, colloquial wording) or Bilingual.

**Default.** English.

**Bilingual.** English comes first, then Cantonese. Short labels share one line ("Clipboard history · 剪貼簿紀錄"); sentences use two lines.

**Live.** The change applies at once to every open window and to windows opened later. The setting is saved and restored on the next start.

**Details.** Each piece of text keeps its English original, so choosing English again restores the exact original wording. Personal wording is applied after the language. Text in a monospace font (paths, commands, codes) is left alone. Strings without a Cantonese entry stay in English.

**Accessible names.** `AutomationProperties.Name` and `AutomationProperties.HelpText` are translated the same way as visible text, so a screen reader announces the same language as the screen. The English original is remembered per element, and choosing English again restores each name exactly. An element marked with `Language.SetLiteral` (or inside one) is left alone, and a name that is bound rather than set directly is not touched.

**Overrides.** The language in effect is the saved choice unless a schedule rule or the settings file sets one, and it is always English while School mode is on. Changing any of these re-applies the language to every open window at once.

**Failure modes.** A `TextBlock` that is already on screen and has its text replaced afterwards is not seen again by the layer until the next refresh; code that does this calls `Language.Refresh(element)`. The toast header, the School mode messages and the narrator note on the Language page already do. Text created after a window has loaded is translated when it loads.

**Privacy.** Nothing leaves the computer; the table is built into the program.
