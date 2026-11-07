# Base64 beside the file

A card on the Tools page writes a file's Base64 text next to it, and turns that text back into the file.

## Using it

- Drop any files on the card, or press **Choose files…** (or Enter on the card). A file becomes `name.ext.base64.txt` beside it. A `.base64.txt` file becomes the original again, beside it, with its own extension.
- Several files run one after another, with a progress bar, the file being worked on, and **Cancel**. Folders are left as they are.
- Nothing is overwritten: a name that is taken gets a number, as Windows does (`photo (1).jpg`).
- Each result is written to a temporary file first and only counts when a SHA-256 round trip matches: the decoded bytes must hash the same as the original file. A file that does not match is removed and the reason is shown.
- Each result has **Show** (selects it in File Explorer) and **Undo** (moves it to the Recycle Bin). Undo refuses when the file changed since it was written.

## What decoding accepts

The standard and the URL-safe alphabets, line breaks and other white space, missing padding, and a `data:` URL prefix. Anything else stops with the exact byte where the text stops being Base64.

## In a browser

The project site has a [Base64 page](https://ding-ding-projects.github.io/Monitoring-tools/base64/index.html) that does the same in a browser. A web page cannot write beside your file, so there each result is a download.

## Records

Every file written and every undo is added to the Records page under [Actions and discarded changes](../records/recorded-actions.md).

## Checks

`tests/SmartTests/Base64Checks.cs` runs on any machine: the text goes beside the file in the standard encoding with no temporary file left; a second encode is numbered and leaves the first untouched; decoding gives back the same bytes and the original extension without overwriting the original; URL-safe, wrapped, unpadded text with a `data:` prefix decodes; a stray character is refused and nothing is written; and a cancel leaves no new file and the source untouched.
