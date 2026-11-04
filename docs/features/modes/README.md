# Modes

The Modes page (Settings, then Modes) holds five features. Each control on the page states what it does and its default.

| Feature | Article | Default |
| --- | --- | --- |
| School mode | [school-mode.md](school-mode.md) | Off |
| ADHD modes and Focus spotlight | [adhd-modes.md](adhd-modes.md) | All off |
| Dim sum surprise | [dim-sum-surprise.md](dim-sum-surprise.md) | 10% chance per launch, no switch |
| Scheduled settings | [scheduled-settings.md](scheduled-settings.md) | No rules |
| Settings file | [settings-file.md](settings-file.md) | Not following a file |

## For developers: the hooks the rest of the app must consult

`MonitoringTools.Core.Modes` (see `Core/Modes*.cs`) exposes the effective values. Other code should call these instead of reading the saved settings directly, so that School mode, the schedule and the settings file take effect.

| Hook | Replaces |
| --- | --- |
| `Modes.Initialize()` | Call once at startup after `Settings.Load()` and after the toast manager exists. |
| `Modes.SchoolActive`, `Modes.SchoolName` | Use the chosen name for every label; never print the default name once it was renamed. |
| `Modes.Blocks(ModeFeature.X)` | Omit the feature entirely (not disable it) when true. |
| `Modes.GameModeOn` | `Settings.Current.GameModeEnabled` in `App.ShouldSuppress`. |
| `Modes.SoundsOn`, `Modes.SoundAllowed(essential)` | `Settings.Current.SoundsEnabled` in `Sounds.Play`. |
| `Modes.ClipboardToastsOn` | `Settings.Current.ClipboardEnabled` in the clipboard handler. |
| `Modes.ScheduledLanguage`, `Modes.ScheduledTheme` | Null means use the saved choice; otherwise apply the returned string. |
| `Modes.Focus`, `Modes.ShouldHoldToast(urgent)`, `Modes.OneThing`, `Modes.LowStimulation`, `Modes.ReduceMotion`, `Modes.TimeAwareness`, `Modes.Momentum` | ADHD behaviour; see [adhd-modes.md](adhd-modes.md). |
| `Modes.PasswordIsSet`, `Modes.CredentialIsSet`, `Modes.CredentialWord` | Whether a PIN, a password or both turn School mode off. |
| `FocusSpotlight.Set(bool)` | Switch the Focus spotlight and keep the choice. |
| `Modes.Changed` event | Raised on the UI thread when any effective value changes. |

Verification status: the page and core compile; the behaviour has not been exercised in a running build.
