# Support Tickets

A make-believe recovery desk, inside the Safety page, reachable from the unlock prompt's **Forgotten your password?** link.

**Nothing is sent anywhere. These tickets exist only on this computer, no network request is made, no data is collected, and nobody is reading them.** That line is plain, always shown, and never changed.

## How it works

- File a ticket: pick a category, a severity (nobody will honour it) and describe what happened. It gets a local number such as LOCAL-0001.
- Its status advances with time: New, Acknowledged, Under review, Resolution ready.
- After the first step, a canned reply appears. It is the app's own, not a person's.
- When the resolution is ready, **Open the app data folder** opens Windows Explorer on the exact folder shown beside the button, which you can also copy. The app deletes nothing. To get unstuck, delete `safety\locks.dat` (or the whole folder) yourself.
- The list is searchable, can be exported to Markdown, and can be cleared after a two-key confirmation.

## What is not done

- A Help-page link to Support Tickets is not added here. The Safety page and the unlock prompt reach it.
- Tickets are stored in `tickets.json`. Deleting the app data folder removes them, which is the same folder the resolution points at.
- No in-app deletion of the data folder is offered.
