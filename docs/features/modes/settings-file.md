# Settings file

Follow a local JSON file you choose. It is read again when it changes and applied over your saved settings, below any scheduled rule. It is off by default.

## Controls

- Follow this file: stores the path, starts watching it, and applies it.
- Apply now: re-reads the file immediately.
- Stop following: stops watching and drops the file's values. Saved settings are untouched.

## Schema

```json
{ "version": 1, "settings": { "language": "en", "theme": "dark", "sounds": true, "toasts": true, "gameMode": false } }
```

- `version` must be 1. `settings` must be an object.
- Allowed keys: `language` (`en`, `yue`, `bilingual`), `theme` (`system`, `light`, `dark`), `sounds`, `toasts`, `gameMode` (true or false). Keys are case-insensitive. Keys you leave out change nothing.
- Unknown keys are ignored and counted in the status line. A wrong type or unsupported value rejects the whole file.
- Maximum size is 64 KB, maximum depth 4.

## Failure behaviour

A missing file, oversized file, malformed JSON or schema error leaves the last valid values in effect, and the status line says so. Nothing from the file is ever written into the saved settings. Changes are debounced by half a second after the file stops changing.

## Limits

Only a local file path is supported. There is no network source. The path must end in `.json`, and the file is read as plain text with no other access to the file system.
