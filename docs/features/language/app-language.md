# App language

**What it does.** Chooses the language of every page, toast, dialog, tray menu and palette entry: English, Hong Kong Cantonese (traditional characters, colloquial wording) or Bilingual.

**Default.** English.

**Bilingual.** English comes first, then Cantonese. Short labels share one line ("Clipboard history · 剪貼簿紀錄"); sentences use two lines.

**Live.** The change applies at once to every open window and to windows opened later. The setting is saved and restored on the next start.

**Details.** Each piece of text keeps its English original, so choosing English again restores the exact original wording. Personal wording is applied after the language. Text in a monospace font (paths, commands, codes) is left alone. Strings without a Cantonese entry stay in English.

**Failure modes.** Text that the app changes after a window has loaded is translated the next time the language is applied; code that sets such text calls `Language.Refresh(element)`.

**Privacy.** Nothing leaves the computer; the table is built into the program.
