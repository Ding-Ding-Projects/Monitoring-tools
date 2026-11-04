# Element locks

Right-click (or press Shift+F10 or the Menu key on) a button, a setting row or a card and choose **Lock this element…**. A small card opens beside it.

## Creating a lock

1. Choose one of six ways to unlock: PIN, password, PIN plus password, password plus authenticator code, PIN plus authenticator code, or all three. Default: PIN.
2. Enter and repeat the PIN (4 to 8 digits) and/or password (at least 4 characters). Each lock has its own credentials. There is no master credential.
3. For an authenticator code, a secret is made on this computer for this lock only. It stays hidden until you press **Show secret**. Add it to an authenticator (or to this app's own, with the button provided) and type the current code. The lock only arms once a correct code is typed.
4. Choose how long it stays unlocked: until the app closes, 5 minutes (default), 15 minutes or 1 hour.

## What a locked element does

- It is dimmed and shows a lock chip. Its tooltip and accessible help text say it is locked.
- Clicking it, or pressing Enter or Space on it, opens the unlock prompt beside it instead of doing its action. A programmatic click is refused too.
- Locks are keyed by page and the element's visible text, so the same words on two pages are separate locks.
- Locks survive restarts. After a restart every lock is closed again.

## The unlock prompt

Ordered steps, one factor at a time. The PIN step has an on-screen keypad (Backspace, Clear, optional shuffled layout) and a typed field that feed the same checker and the same attempt budget. A wrong answer at any step starts the attempt over. Five wrong tries cause a wait of 30 seconds, doubling on each consecutive lockout up to 15 minutes. The prompt offers the [unlock ladder](unlock-ladder.md) during a wait, a **Forgotten your password?** link to [Support Tickets](support-tickets.md), the for-fun disclosure, and the exact recovery line naming `locks.dat` and the app data folder.

## Managing locks

The Safety page lists every lock, searchable, with its policy, state, and buttons to unlock, lock again or remove. Removing a lock needs it to be unlocked first.

## What is not done, and why

- Credentials are salted PBKDF2 hashes and a protected local file, not the Windows Credential Manager vault.
- Locking whole tabs, tab groups and individual appearance values is not done. This app has no user tabs, and the appearance editor is outside this feature. Cards, buttons and setting rows are covered.
- The command palette is not yet wired to refuse locked actions. `ElementMenu.KeyOfText` and `Locks.IsLocked(pageId, key)` are available for it.
- Bulk locking of many elements at once is not offered.
- The wizard is a separate small window beside the element, not an in-place popover.
- Locks are not yet shown as "locked" inside the page search results. A setting to hide locked items exists but only the lock list uses it today.
- Lock changes are recorded in the [change history](history.md), not in the settings version history.
