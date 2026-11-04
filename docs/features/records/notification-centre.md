# Notification centre

Every toast, notice, offline alert and public IP change is logged with its time, kind, title and detail. The log lives in memory only, holds at most 500 entries (the oldest drop off) and is forgotten when the app quits.

- **Search** filters by kind, title and detail. It supports plain text and regular expressions through the builder next to it.
- **Kind chips** (All, Toast, Notice, Offline, IP changed) narrow the list further.
- **Clear all** empties the log after a confirmation. This cannot be undone.
- The page shows the newest 200 matching entries; exports include all of them.

Kinds are inferred from the toast header, so an unusual title is logged as a plain toast.
