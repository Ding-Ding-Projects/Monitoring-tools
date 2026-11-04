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

The Minecraft form keeps its draft and current step if the page redraws, shows creation progress under the last step, and Cancel discards the draft.

## Not done

Other editors (Appearance edits, Ollama chat profiles, the schedule rule form) are still single forms with inline validation.
