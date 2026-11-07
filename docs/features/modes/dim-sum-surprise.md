# Dim sum surprise

At startup there is a 10% chance, drawn fresh on each launch and at most once per launch, that the app shows one randomly chosen dim sum dish: its English and Cantonese names and a picture, in a small card at the bottom left for about 12 seconds.

## Behaviour

- Never takes focus, never delays startup, and is dismissible. The card is announced politely to screen readers with the dish name as its accessible name, and the picture has alt text.
- Waits about 10 seconds after startup and is skipped (with the reason shown on the Modes page) during first-run setup, while game mode or snooze is holding popups, and while School mode is on.
- Motion follows `Modes.ReduceMotion`: no fade when reduced.
- Names follow the scheduled language when one is set (English, Cantonese or both); the default is both.
- There is deliberately no way to turn it off. The Modes page offers a clearly labelled preview button that does not count as the launch draw.

## The switch that stays on

The Modes page shows a switch labelled **Dim sum surprise**. It looks like it turns the surprise off; it does not. Pressing it (or Space while it has focus) slides it off, and it springs back on with a different line each time: "Nice try.", "Still no.", "It really does stay on." and then "Have a har gow instead.", each followed by "The dim sum surprise cannot be turned off." The line is announced politely to screen readers and becomes the switch's help text, and the switch stays on. With Reduce motion or Low stimulation it springs back at once instead of after a short pause. Nothing is saved, because there is nothing to turn off.

School mode is still the one thing that holds the surprise back, as before.

## Where pictures and names come from

Only the public catalogue `Ding-Ding-Projects/dim-sum-photos`:

- Names and picture file names: `catalog/index.json` on its main branch, fetched over HTTPS on demand. A small cache of names and file names is kept in the app data folder, recording the source URL and the revision (ETag), and refreshed after 7 days.
- Pictures: the published `catalog-v1` and `catalog-v1-part-NNN` release assets, downloaded one at a time on demand over HTTPS (limit 12 MB) and cached in the app data folder.
- Attribution ("Image from Ding-Ding-Projects/dim-sum-photos") is shown on the card.
- No picture or catalogue copy is stored in this repository. A dish with no published picture is skipped; if none is found the surprise does not show and the Modes page reports the reason. Nothing is generated locally.

The page has a button to clear cached pictures.

## Verification

The catalogue URL and release asset layout were checked against the public repository. The draw, download and card have not been run in a built app.
