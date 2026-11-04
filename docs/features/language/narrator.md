# Read toasts aloud

**What it does.** Speaks toast titles with the Windows voice chosen on the page. English mode speaks English, Cantonese mode speaks Cantonese, Bilingual speaks English then Cantonese.

**Default.** Off.

**Behaviour.** One queue, so speech never overlaps. A burst of more than eight waiting items is trimmed rather than read out for a minute. It stops the moment game mode starts and stays quiet while game mode is on or notifications are snoozed. Turning the switch off stops speech immediately.

**Failure modes.** No voice for a language means that language is skipped and the page says so. Synthesis or playback errors are logged and shown on the page.

**Privacy.** Speech is synthesised on this computer by Windows; nothing is sent anywhere.
