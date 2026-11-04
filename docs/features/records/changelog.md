# Changelog

Release notes are fetched from the project's GitHub releases (10 second timeout) and rendered with the same Markdown renderer as the clipboard toasts. The release that matches the running version is highlighted.

- The last successful answer is cached on disk. When GitHub cannot be reached, the page shows the cache and says how old it is.
- With no cache and no connection the page says the changelog is unavailable and why, rather than showing an empty list.
- **Refresh** fetches again. The search bar filters by name, tag and notes.

Only public release data is requested; nothing about you is sent beyond a user-agent string with the app version.
