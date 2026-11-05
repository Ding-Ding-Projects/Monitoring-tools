# The project site

The project site explains Monitoring Tools and links the real download. It is not the app: nothing on it runs on
your computer, and every demonstration on it is labelled as one. It is built by `scripts/build-site.mjs` and
published to GitHub Pages on every push to `main`.

## What it carries

- **Home.** The installer button for the release the site documents, the counts of areas and articles, the line
  count from that release, every feature area, the tasks you can start from and a labelled quick-command
  demonstration.
- **Docs.** Every article in full, grouped by area, with suggested next reading at the end of each one.
- **How it works.** An interactive diagram of what happens when you copy a file path with `:mp4`: it plays by
  itself, pauses, replays, lets you pick any step and shows what changes when you hover the toast or ignore it.
- **Gallery.** Every capture of the real app made by the release build, with filters.
- **Status.** What is done, partial or missing.
- **Settings.** Language, humour, colour, text size, motion, the tab strip, presets, toy locks, a toy authenticator
  and the notification centre.

## Moving around

- **Search.** The search bar on every page searches every article, section, setting and capture. `.*` switches to
  regular expressions and **Builder** opens the regex builder anchored to it, with tokens, flags, a test sample and
  a piece-by-piece explanation. Patterns run in your browser's JavaScript engine with a time limit; the app's own
  builder uses the .NET engine.
- **Command palette.** `Ctrl+Shift+F` opens a palette of pages, sections, settings, actions and the elements on
  the current page. Choosing an element scrolls to it, opens anything folded around it and rings it.
- **Tabs.** Each page you open becomes a tab in the strip under the bar. Tabs group by area and groups fold.
  Drag a tab to reorder it, middle-click or press × to close it, and pin it from its menu. **Tabs** finds a tab
  four ways: open tabs, recently closed, groups and every page. The strip can sit on top or on the left.
- **Right-click menus.** Right-click anything for a menu with its own filter. Every item shows a shortcut that
  works on its own: `Ctrl+Shift+F` palette, `/` search, `Alt+L` copy a link to here, `Alt+E` edit appearance,
  `Alt+K` lock this element, `Alt+P` pin the tab, `Alt+W` close the tab, `Alt+T` light or dark, `Alt+M` reduce
  motion, `Alt+R` regex mode and `Alt+B` the builder. Shift and right-click gives the browser's own menu; the
  Menu key and `Shift+F10` open the site's menu at the focused element.
- **Panels.** On an article, drag the edge of the contents panel to resize it, or focus it and use the arrow keys.
  Double-click the edge to put it back. The width is kept.

## Make it yours

- **Language.** English, Cantonese or both. The site's own labels change; articles stay in English.
- **Humour.** English and Cantonese each have their own slider from 0 to 5, shown in a sample line.
- **Colour.** Dark, light or your system's choice, five seed colours or any colour, with a translator that shows its
  hex, RGB and HSL values and whether it can be read on the current background.
- **Edit one element.** Right-click it and choose **Edit appearance…** for its text colour, background, size and
  corner radius. Reset one element from the same editor, or all of them in Settings.
- **Presets.** Three built in, save your own, export them to a file and import them again.

## Locks, codes and the dim sum

- **Toy locks.** Right-click an element and choose **Lock this element…**. Locked elements are covered until the
  code is entered. Forgot it? The unlock ladder has the app's rungs: dim sum, ten easy sums, whack-a-mole, then the
  clock, which rests the ladder for 10 minutes.
- **Toy authenticator.** Add a Base32 secret and see six-digit codes that change every 30 seconds.
- **Dim sum surprise.** A dish from the release code-name list turns up on the home page now and then. Turn it off
  in Settings.

These are toys. The site has no account and no server, so a lock protects nothing and a secret is kept unencrypted.
Do not put a real account's secret in the authenticator.

## Where your settings live

Everything you change is kept in this browser's local storage under keys starting `mt-site-`. Nothing is sent
anywhere. Settings lists what is kept and its size. To start over press **Reset all** in Settings, or clear the
site's data in your browser. A private window starts empty and forgets when it closes.

## Motion

Pages rise into view, the hero's heartbeat line draws itself, toasts slide in, cards lift and buttons change shape
when pressed. **Reduce motion** in Settings, or your system's reduce-motion setting, stops every animation and shows
each element in its final state.

## What a static site cannot do

These shared app requirements have no site equivalent, and are left out on purpose:

- **Operating-system credential vault, Windows Hello and FIDO2 keys.** The site uses local storage and says so.
- **QR pairing.** Pairing needs a second device talking to the app; the site has no app to pair with.
- **Encrypted storage of codes.** There is no account or key to encrypt with, so the authenticator is a labelled toy.
- **Running anything on your computer.** Downloads link to the release; nothing is executed.

## How it is checked

The build fails when a font is missing or altered, when an article, category or anchor is missing, when a page
loads a remote asset or lacks its social tags, and when a shared feature from its hand-written list is missing
from the pages that must carry it.

## Related

- [Command palette](command-palette.md)
- [Element menu](element-menu.md)
- [Regex builder](../search/regex-builder.md)
