# Install commands and editable text toasts

## Copied text, edited in place

Plain copied text opens a toast titled "Copied text" with the character and line counts. The text sits in a field that is
read-only and takes no focus until you click it. The first click makes the toast activatable, puts the caret there and holds
the countdown (the header shows "held"). While you type:

- the clipboard is written 400 ms after the last key, once, through the same path the app uses for every own write, so the
  clipboard watcher ignores it and no second toast opens;
- the history entry is updated in place and marked edited; it is never duplicated;
- a "Copied" pill dims while you type and returns when the write is done.

Keep (Ctrl+Enter) closes the toast with your edit on the clipboard. Revert (Esc in the text) puts the original text back on the
clipboard and in history, and the toast goes quiet again with its timer running. Esc anywhere else dismisses. Tab runs
text, Keep, Revert, Close. Long text scrolls inside a 180 px field.

## Install commands

A copied single-line `choco install ...`, `choco upgrade ...`, `winget install ...` or `winget upgrade ...` command gets an
Install command toast: package names, source, and whether Windows will ask for permission. Nothing runs until Install is pressed.

- Chocolatey always runs elevated behind the Windows consent prompt, which only you answer. The app starts `choco.exe` with the
  checked arguments (no shell string). A declined prompt is a plain outcome with exit code 1223.
- winget runs as you with the checked arguments and a closed input stream.
- The parser is an allow list. Anything not known to be safe is refused with a reason, never ignored: pipes, `&&`, `;`,
  redirects, variables, quotes, `--source` / `-s` to anything but the default source, `--ignore-checksums`,
  `--ignore-package-exit-codes`, `--force`, `--params`, `--install-arguments`, `--override`, `--pre`, `--allow-downgrade`,
  `--scripts`, `upgrade all`, file or URL names, and any unlisted flag.
- If the package manager is not installed the toast says so and offers only Copy command.
- Each run is logged in Activity with the exact command and has its own log file in the install-logs folder.

### Automatic mode

Settings, Clipboard, "Install copied commands automatically". Off by default. When on, a command that passes a stricter
subset (choco: `-y` only; winget: `--id` with optional `-e`; at most the chosen number of packages) starts after a delay of 3
to 60 seconds with a visible countdown and Cancel. Chocolatey is skipped when the app itself runs as administrator, because
Windows would not show a consent prompt. The same command is not started twice within ten minutes, and nothing starts
while toasts are held back (game mode, focus mode).

## Verification

`MonitoringTools.exe --selftest <folder>` (quick loop: `MT_ONLY=toasts`) covers the parser (accepted and refused commands),
the toast states with a test runner in place of a real install, the runner with a stand-in program, automatic mode, the
Settings card and Cantonese. No check installs software. The Windows consent prompt itself was not exercised.
