# Element locks

Right-click (or press Shift+F10 or the Menu key on) a button, a setting row or a card and choose **Lock this element…**. A small card opens beside it.

## Creating a lock

1. Choose one of six ways to unlock: PIN, password, PIN plus password, password plus authenticator code, PIN plus authenticator code, or all three. Default: PIN.
2. Enter and repeat the PIN (4 to 8 digits) and/or password (at least 4 characters). Each lock has its own credentials. There is no master credential.
3. For an authenticator code, a secret is made on this computer for this lock only. It stays hidden until you press **Show secret**. Add it to an authenticator (or to this app's own, with the button provided) and type the current code. The lock only arms once a correct code is typed.
4. Choose how long it stays unlocked: until the app closes, 5 minutes (default), 15 minutes or 1 hour.

## What a locked element does

- It is dimmed and shows a lock chip. Its tooltip and accessible help text say it is locked.
- Clicking it, or pressing Enter or Space on it, opens the unlock prompt beside it instead of doing its action. A programmatic click is refused too. The release of a stopped click is stopped as well, a middle click (which closes a tab) is stopped, and so are the keys that change a value: the arrows, Home, End, Page Up and Down, F4 and Alt+Down (which open a drop-down list) and Delete (which closes a tab).
- Locks are keyed by page and the element's words. When two lockable elements in one window say exactly the same words, the second is keyed as `<words> #2`, the third `<words> #3`, and so on, in the order the window lays them out, so each is locked on its own. The first keeps the plain words. Locks saved before positions were added carry the plain words, so they still hold, on the first element that says them (before, such a lock held on every element with those words).
- A drop-down list is keyed by its name, not by the value it shows, so choosing another value does not lose its lock. A tab of the control centre's tab strip shows on every page, so its lock is kept under the tab strip and holds on every page (a tab lock made before this is still found on the page it was made on).
- Locks survive restarts. After a restart every lock is closed again.

## What can be locked

Buttons of every kind (buttons, toggle buttons, switches, radio buttons), drop-down lists (the app's own and WPF's), tabs (the control centre's tab strip tabs and WPF tabs), setting rows, list rows, search results and cards. Keyboard focus does not move into a locked tab, because a tab is chosen the moment it takes focus.

**Menu items.** An element's right-click menu is a separate window, so the lock handlers of the window it belongs to never see it. Its own items are locked from the menu itself: right-click an item, or press the Menu key or Shift+F10 on it, and choose **Lock this menu item…** (or Unlock this menu item…, Lock again, Remove this lock). A menu item's lock is keyed by the element the menu is for and the item's label, so "Press" in one button's menu is locked apart from "Press" in another's. A locked item is dimmed with the lock chip; clicking it, pressing Enter or Space on it, or choosing it any other way opens its unlock prompt, and a click posted to the menu's window from another program is stopped by the menu window's own hook.

## The unlock prompt

Ordered steps, one factor at a time. The PIN step has an on-screen keypad (Backspace, Clear, optional shuffled layout) and a typed field that feed the same checker and the same attempt budget. A wrong answer at any step starts the attempt over. Five wrong tries cause a wait of 30 seconds, doubling on each consecutive lockout up to 15 minutes. The prompt offers the [unlock ladder](unlock-ladder.md) during a wait, a **Forgotten your password?** link to [Support Tickets](support-tickets.md), the for-fun disclosure, and the exact recovery line naming `locks.dat` and the app data folder.

## Unlock with Windows Hello

When Windows Hello (face, fingerprint or Windows PIN) is set up for your Windows account, the unlock prompt and the [unlock ladder](unlock-ladder.md) show **Unlock with Windows Hello**. It asks Windows to confirm it is you and, if Windows says yes, opens the lock for the usual unlock time. It also clears the failed-try count and any wait, as a correct PIN would.

- It is an extra route. The lock's PIN or password stays stored and still works, and Hello can never be the only credential.
- The app only receives yes or no. It never sees or stores a face, fingerprint or Windows PIN.
- If Windows Hello cannot be used, the prompt shows the exact reason instead of a button (no Hello-capable device, not set up for this account, turned off by policy, busy, or an unexpected error).
- A **Offer Windows Hello** switch on the Safety page turns the route off. Default: on. The route is recorded in the change history as "Lock opened (Windows Hello)".
- It is still a toy lock: someone already signed in to your Windows account can pass Windows Hello as easily as you can, and the Hello dialog is Windows' own.
- The Hello dialog is shown by Windows without a window handle for this app, so on some systems it may open behind other windows; the prompt drops its always-on-top flag while the dialog is open to reduce this.

## Managing locks

The Safety page lists every lock, searchable, with its policy, state, and buttons to unlock, lock again or remove. Removing a lock needs it to be unlocked first.

## What is not done, and why

- Credentials are salted PBKDF2 hashes and a protected local file, not the Windows Credential Manager vault.
- Locking a whole tab group at once is not done; each tab is locked on its own.
- A position key moves when an earlier element with the same words appears or disappears (for example a row added above it), so its lock then holds on whichever element is now in that position.
- A locked radio button or switch can still be changed through UI Automation's own select or toggle pattern (an assistive technology calling it directly): the self-test shows a locked radio button is still selected that way. Pointer, keyboard and programmatic clicks are stopped.
- The windows that hold radio buttons and drop-down lists today (the file tool dialogs) offer Copy text only from their element menu, by design, so locks are not offered there; the lock types are proven in windows that carry locks.
- The command palette is not yet wired to refuse locked actions. `ElementMenu.KeyOfText` and `Locks.IsLocked(pageId, key)` are available for it.
- Bulk locking of many elements at once is not offered.
- The wizard is a separate small window beside the element, not an in-place popover.
- Locks are not yet shown as "locked" inside the page search results. A setting to hide locked items exists but only the lock list uses it today.
- Lock changes are recorded in the [change history](history.md), not in the settings version history.
