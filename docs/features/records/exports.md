# Exports

Choose a dataset and a format, then save to a file or copy to the clipboard.

| Dataset | Contents |
| --- | --- |
| Clipboard history | Text, Markdown, URL, email, colour and JSON entries. Images, files and audio are never exported. |
| Activity | The activity feed (up to 300 entries) |
| Settings | Every setting and its value |
| Notifications | The notification centre log |
| Settings history | Time and summary of each snapshot |

Formats: JSON (array of objects), CSV (RFC 4180 quoting; saved with a byte-order mark so spreadsheets read it as UTF-8), Markdown table, HTML (one self-contained page, escaped, no remote assets) and plain text.

Formatting and writing run off the UI thread, with a progress bar next to the buttons. Clipboard history is plain text and may contain sensitive copies, so treat exported files accordingly.
