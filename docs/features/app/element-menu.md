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

## Clicks posted from outside
A click posted to a window by another program (an assistive or remote input route, or the self-test) does not move the cursor, and WPF finds a mouse event's target from the cursor. So each window with the menu also reads the position carried by the click message itself, hit-tests it, and runs the same lock veil (left click on a locked element opens its unlock prompt) and element menu (right click) from it. A click the hook does not act on reaches WPF as usual. Found while testing this: the veil's tooltip text was being read into a locked element's key, so a locked button lost its own key the moment it closed; the tooltip is now left out of the key.

`MT_ONLY=lane4clicks` holds the app on a hidden desktop and waits, for each step, for a click posted with the lowlevel `mouse_click` tool to a window handle (it writes `click-<step>.json` with the handle and client point); it then judges the outcome. Verified that way: a left click on the colour eyedropper overlay, a right click on the command palette, a left click on a locked button, and (step `locktypes`) left clicks on a locked radio button, WPF drop-down list, the app's own drop-down list, WPF tab, control centre tab strip tab and element menu item, each stopped with its unlock prompt opened and its value unchanged. The element menu is a window of its own, so it carries its own hook: a click posted to it on a locked item is stopped there.

`MT_ONLY=dialogs` does the same for the real native file dialogs and the date picker's calendar popup (steps `appearance`, `bedrock`, `bedrock-zip`, `bedrock-folder` and `date`, selected with `MT_DIALOGS_ONLY`): it writes `dialog-<step>.json` (the file to use, or the popup window and the client point of a day button), the lowlevel tools set the file name box of the dialog and press Enter (or cancel an Open dialog and set a folder dialog's Folder box and press Select Folder), or click the day, and the check judges the result. `MT_ONLY=lockwalk` runs the walk over every lockable element type on the real pages (see the Toy locks row of the required features inventory).

## Not done
- The command palette overlay now has the menu (Clear the search, Use the full window or card size, Close the palette). No real pointer click was made on it; the check opens the menu through the routine the right-click handler calls.
- Window-specific extra items exist only for toasts; other windows offer the common items.
- Not yet captured on screen; compile-checked only.
