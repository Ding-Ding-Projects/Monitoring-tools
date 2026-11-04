# Service and tray

Monitoring Tools installs per user, starts at sign-in and runs under a supervisor that restarts it after a crash.

![The Service page](../../screenshots/control-service.png)

## The tray icon

The dot shows the state: mint online, amber offline, violet game mode, grey paused.

![The tray menu](../../screenshots/tray-menu.png)

Right-click for the control centre, history, shortcuts, connection test, toggles, snooze, DNS, administrator rights, restart, logs, uninstall and quit.

## Administrator rights

Off by default. Turning them on asks once through UAC, then the service starts elevated at every sign-in until you turn it off. Things that need it, such as changing DNS, then run without a prompt.

## Where things live

The program is in `%LOCALAPPDATA%\Programs\MonitoringTools`. Settings, logs and scripts are in `%LOCALAPPDATA%\MonitoringTools`.

## Personal wording

Optional. A local JSON file can swap ordinary phrases in on-screen text for your own words. It never leaves this computer, and paths, commands and code are never changed.

## Uninstall

Right-click the tray icon and choose **Uninstall service**. It stops the service, removes start-up and deletes the program.

## Related

- [Command palette](command-palette.md)
- [DNS profiles](../network/dns-profiles.md)
