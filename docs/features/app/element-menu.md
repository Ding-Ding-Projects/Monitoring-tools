# Element menu

Right-click an element, or focus it and press Shift+F10 or the Menu key, to get a menu for that exact element. It has its own filter field with the regex builder, shows each item's shortcut and is operated with the arrow keys, Enter and Escape.

## Items
Press (buttons), Copy text, anything the window adds for itself, Edit appearance…, and Lock this element… (or Unlock, Lock again, Remove this lock).

## Windows that have it
Control centre, tray menu, drawing board, toasts, offline dialog, setup, welcome, file tools, commit message, dim sum surprise.

| Window | What differs |
| --- | --- |
| Toast | Adds the toast's own actions, **Dismiss** and **Pause toasts for 15 minutes**. Toasts never take focus, so the menu lets the toast be activated while it is open so its filter field can receive keys, then restores that. The countdown is held while the menu is open. Opens by pointer only, because a toast cannot hold keyboard focus. |
| Offline dialog | Same activation handling as a toast. |
| Confirmation windows, the appearance editor, and the lock prompt, lock wizard, unlock ladder and QR cards | **Copy text only.** No Press, no appearance editing and no lock, so no menu entry can confirm for you, step around the two-key gate, or restyle or lock the window you are using to do it. |

## Pausing toasts
**Pause toasts for 15 minutes** stops new toasts from appearing. They are still written to the notification log, so nothing is lost. The pause lasts 15 minutes or until the app closes, and is not saved to settings.

## Not done
- The command palette overlay has no element menu.
- Window-specific extra items exist only for toasts; other windows offer the common items.
- Not yet captured on screen; compile-checked only.
