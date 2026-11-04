# Change history

A local, append-only list in `safety-history.json` of what changed and when:

- locks created, opened, closed again, removed, and waits cleared by the ladder
- authenticator accounts added, removed, relabelled or reordered
- support tickets filed and the recovery folder opened
- the display name changed (only that it changed, not its value)

It stores labels and actions. No PIN, password, secret or code is ever written to it. The Safety page lists the newest 100, searchable. The file keeps the latest 500.

## What is not done

The full requirement asks for a local version-control repository per app, encrypted snapshots, restore, diff, retention policy, and a history manager behind its own password. This build keeps a plain list instead. There is no restore, no password-protected manager, and the display-name watcher starts when the first window with element menus opens.

Records survive until the app data folder is deleted.
