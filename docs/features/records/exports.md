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

## Export everything

One **Export everything** action on the Records page writes a zip with every dataset in all five formats, in `json/`, `csv/`, `markdown/`, `html/` and `text/` folders plus a `README.txt` with row counts. Datasets: settings, activity, notifications, settings history, shortcuts (with their scripts), the regex library, appearance and element looks, schedule rules, lock records and DNS profiles. Lock records list each lock's label, page, rule, open time and which factors it uses, never a PIN or password hash, salt or authenticator secret. Clipboard history is left out unless the switch beside the button is turned on. Code: `Core/ExportAll.cs`.
