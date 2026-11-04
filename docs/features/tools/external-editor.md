# External editor

Detects Visual Studio Code (per-user, machine, Insiders, and `code.cmd` on PATH), Notepad++, the system default and one custom program. The preferred editor is a setting (default: automatic, which picks VS Code, then Notepad++, then the system default).

- Folders open in VS Code as a workspace root in a new window.
- If VS Code is missing, the page says so and offers the download. A portable copy is not detected automatically; choose its executable as the custom program.
- Failures (missing path, editor will not start) are reported with a plain message, never silently.
- Helpers for other pages: `ExternalEditor.Open(path, out message)`, `ExternalEditor.OpenAndTell(path)` (shows a notice on failure) and `ExternalEditor.OpenInVsCode(path, ...)`.

Status: compile-checked only.
