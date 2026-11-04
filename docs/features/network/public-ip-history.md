# Public IP history

When your public IP address changes, a dialog shows the old and new address and the change is committed to a small local git repository.

## What is recorded

The repository is at `%LOCALAPPDATA%\MonitoringTools\ip-history`. It holds `ip-history.md` and `history.json`, with the first seen and last seen time of every address.

## Check now

On the Network page, **Check now** looks up the address immediately. Lookups use ipinfo.io, with api.ipify.org as a fallback.

## Push it somewhere

Set a remote in the control centre and the history is pushed automatically after each change.

## Privacy

The lookup services see your address, as any website does. Nothing else is sent. The history stays on this computer unless you set a remote yourself.

## Related

- [Internet watch](internet-watch.md)
- [Push a repo from the clipboard](../commands/push-a-repo.md)
