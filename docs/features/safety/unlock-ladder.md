# Unlock ladder

When a lock makes you wait, **Play your way out** replaces staring at a countdown with something to do. The countdown stays on screen the whole time, so the honest route, waiting, is never hidden.

## The rungs

1. **Dim sum.** One dish described in words, four choices. A right pick wins. After five wrong picks you go to the sums.
2. **Ten easy sums.** Every answer must be right. One wrong sum drops you to whack-a-mole.
3. **Whack-a-mole.** A 15 second round on a 3 by 3 grid. A square says MOLE while a mole is up. Click it or press its number key. Hit enough to win. A lost round drops you to the clock.
4. **The clock.** The ladder is not offered again for that wait. You are exactly where you started.

If School mode removes dim sum, the ladder starts at the sums, with no mention of dim sum.

## What winning does, and does not

- It ends the **wait** only. You still have to enter your credential. Nothing is signed in.
- It returns the same number of tries the clock would, never more.
- The next wait still gets longer. The escalation is not touched.
- At most 3 waits can be skipped per rolling hour. After that the clock is the only way through. This cap is stored in a protected file, so a restart does not refill it.
- Each challenge has a single-use nonce that is consumed before grading, and expires. A whack-a-mole answer sent before the round could have finished is refused, each mole counts once, and only on the right square while it was up.

## Honest limits

This is a local toy. The games are generated and graded inside the app, so someone with a debugger could get around them, just as they could around the lock itself. That is why the lock says it is just for fun.

Dim sum questions use a short built-in list of dish descriptions. The pictures from the dim sum surprise are not used here.
