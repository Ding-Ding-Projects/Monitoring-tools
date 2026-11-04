# Plugins and mods

![Plugins with their badges](../../screenshots/mc-plugins-badges.png)

Paper and Purpur servers take plugins (the `plugins` folder); Fabric, Forge and NeoForge take mods (`mods`); Vanilla
takes neither, and the section says so. A Bedrock server takes add-ons (behavior and resource packs) instead, which this
app does not manage yet; the section says that too. The section is named **Plugins** or **Mods** to match.

## Installed

Every jar in the folder, with the name and version it declares for itself (`plugin.yml`, `paper-plugin.yml`,
`fabric.mod.json` or `mods.toml`), its file and size, an on switch, a remove button and a compatibility badge.

- **Off** renames the jar to `.jar.disabled`, which the server skips; **on** renames it back.
- **Remove** deletes the jar after the two-key confirmation.
- **Install from a file** copies a jar in (anything that is not a readable jar is refused). For SSH servers it is copied to
  the host with `scp`.
- Before any install, update, removal or switch, the whole folder is copied to a dated backup in `plugins-backups` or
  `mods-backups` beside it.
- **Undo the last change** puts the folder back as the newest backup holds it: jars added since are removed and every
  backed-up file is copied back. The folder as it is now is backed up first, so an undo can itself be undone.

Changes take effect at the next start.

## Compatibility badges

When the section opens (and on **Check for updates**) every jar is looked up:

- **Modrinth** recognises any of its own files by SHA-512, so a jar copied in by hand is found too. Its declared game
  versions and the newest file for this version and loader come from Modrinth's `version_files` and
  `version_files/update` endpoints.
- **Hangar** has no lookup by file, so a jar installed from Hangar by this app is recorded (in `minecraft/catalogue/` in
  the app's data folder) and checked against Hangar's version list for this game version. A recorded Hangar install is
  checked against Hangar even when Modrinth also carries the file.

| Badge | Meaning |
| --- | --- |
| Compatible | The catalogue lists this file for the server's game version, and there is nothing newer for it. |
| Update | The catalogue has a newer file for this version; the row gets an **Update** button. |
| May not work | The catalogue does not list this version for the file and has no file for it. |
| Not in a catalogue | The jar is not in Modrinth and was not installed from Hangar by this app. |

**Update all** installs every newer compatible file, each checked against its catalogue's hash. The whole folder is
backed up first, and if any one update fails the folder is put back exactly as it was and the message says so.

## The catalogues

![Hangar results](../../screenshots/mc-plugins-hangar.png)

**Modrinth** and, for Paper and Purpur, **Hangar** are chips above the search.

- **Modrinth** asks the public Modrinth API, filtered to the server's game version and to the loaders its software accepts
  (Paper and Purpur also accept Spigot and Bukkit plugins), for server-side projects. **Install** takes the newest file for
  that version and loader and copies it in only after its bytes match the SHA-512 the catalogue lists.
- **Hangar** asks `hangar.papermc.io/api/v1/projects` with the Paper platform and the game version. **Install** takes the
  newest Release-channel version for that game version (or the newest one when there is no Release) and copies it in only
  after its bytes match the SHA-256 Hangar lists. A version Hangar offers only as a link to another site has no checksum,
  so it is refused.

## Failure modes

- A catalogue that cannot be reached leaves the badges unchecked and says so; nothing is changed.
- A download whose hash does not match is deleted and nothing is installed.
- An update that fails during **Update all** rolls the whole folder back.

## Verification

Self-test checks turn a sample jar off and on (rename, folder backup, the name from `plugin.yml`, a non-jar refused);
check every badge rule; run **Update all** with one update that fails (the folder comes back unchanged) and one that
succeeds, then **Undo**; and open the section on the sample Paper server to find the three badges, **Update all**, **Undo
the last change** and the Hangar chip (absent on a Vanilla server). The [live check](../tools/live-check.md) searches
Modrinth and Hangar for real, installs a Hangar plugin and an older Modrinth file into a throwaway server folder with
their hashes checked, finds the older file badged **Update** and the Hangar one **Compatible**, and runs **Update all**.
Captures `mc-plugins-badges` and `mc-plugins-hangar` (sample data and sample results).
