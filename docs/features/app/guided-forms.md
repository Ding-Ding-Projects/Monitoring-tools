# Guided forms and setting subtitles

## Settings explain themselves

Every switch and choice on the Clipboard, Network and DNS, Game mode, Service and Shortcuts pages carries a visible subtitle that says what it does and its default, in plain words, for example "How long a toast stays on screen before it fades. Default: 3 s." The information button beside it opens a longer explanation with a sample that changes nothing. Pages added later (Modes, Appearance, Language, Records, Safety, Tools) already follow the same rule.

## Guided forms

A guided form shows one step at a time with a progress indicator, Back, Next and Cancel. A step cannot be left until its input is valid, and the message says what to fix. The last step carries the real action and reports its own result.

| Form | Where | Steps |
| --- | --- | --- |
| New shortcut | Shortcuts and scripts | Name, shell, template (existing) |
| Add a DNS profile | Network and DNS | Name, servers (IPv4 format checked as you type), review. After saving it offers **Use now** or **Not now** |
| New Minecraft server | Minecraft servers | Name and version, where it runs, memory and port, review with the EULA |
| Add a schedule rule | Modes, Scheduled settings | What to change, days and times, optional dates, review. Uses the same check as the single form |
| Make a look | Appearance | Theme, seed colour, contrast, density and text size, then a review of what changes |
| New chat profile | Local AI models, Chat | Name and instructions, optional parameters, review. Uses the same checks as Save as profile |

The Minecraft form keeps its draft and current step if the page redraws, shows creation progress under the last step, and Cancel discards the draft.

## Not done

The single forms for schedule rules and chat profiles remain beside the guided ones. Per-element appearance edits (the element editor window) are still one form.
