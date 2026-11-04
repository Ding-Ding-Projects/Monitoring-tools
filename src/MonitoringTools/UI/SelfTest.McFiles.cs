using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // Checks for the server file store, SSH players, plugins, backups and worlds, and for Bedrock players, add-ons and worlds.
    // An SSH server is stood in for by a folder on this computer through McFiles.Override, because the sample hosts do not
    // exist; the real ssh and scp routes are checked against a throwaway OpenSSH container by the live check
    // (MT_LIVE_ONLY=minecraft-ssh-files). Nothing here starts a server or touches the network.
    public static partial class SelfTest
    {
        internal static void Zip(string path, params (string Name, string Text)[] entries)
        {
            using var z = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var (n, t) in entries) { using var w = new StreamWriter(z.CreateEntry(n).Open()); w.Write(t); }
        }

        internal static string Manifest(string name, string uuid, string module, string depends = null) =>
            "{ // comments are allowed in a manifest\n  \"format_version\": 2,\n  \"header\": { \"name\": \"" + name + "\", \"description\": \"d\", \"uuid\": \"" + uuid + "\", \"version\": [1, 2, 3], \"min_engine_version\": [1, 20, 0] },\n" +
            "  \"modules\": [ { \"type\": \"" + module + "\", \"uuid\": \"" + Guid.NewGuid() + "\", \"version\": [1, 0, 0], }, ],\n" +
            (depends == null ? "" : "  \"dependencies\": [ { \"uuid\": \"" + depends + "\", \"version\": [1, 2, 3] } ]\n") + "}";

        private static async Task McFilesChecksAsync(ControlCentreWindow cc)
        {
            var mc = cc.MinecraftManager;

            Check("minecraft: the file store reads, writes, lists, moves, copies and removes, and refuses a path outside the server folder", () =>
            {
                var dir = McTemp("files");
                var f = new McLocalFiles(dir);
                f.WriteText("a/b/c.txt", "héllo");
                if (f.ReadText("a/b/c.txt") != "héllo" || f.ReadText("nope.txt") != null) return "write and read";
                f.WriteText("one.json", "1"); f.WriteText("two.json", "2");
                var many = f.ReadMany(new[] { "one.json", "two.json", "missing.json" });
                if (many.Count != 2 || many["two.json"] != "2") return "ReadMany returned " + many.Count;
                var l = f.List("a");
                if (l.Count != 1 || !l[0].IsDir || l[0].Name != "b") return "list";
                f.Move("one.json", "moved/one.json");
                if (f.Exists("one.json") || !f.Exists("moved/one.json")) return "move";
                f.Copy("a", "copy-of-a");
                if (f.ReadText("copy-of-a/b/c.txt") != "héllo") return "copy";
                var bk = f.BackUpFolder("a");
                if (bk == null || !bk.StartsWith("a-backups/") || f.ReadText(bk + "/b/c.txt") != "héllo") return "backup folder";
                if (f.BackUpFolder("empty") != null) return "backed up a folder that is not there";
                if (f.Size("a") != 6) return "size " + f.Size("a");
                f.Delete("a");
                if (f.Exists("a")) return "delete";
                foreach (var bad in new[] { "../x", "a/../../x", "/etc/passwd", "a//b", "C:/x", "a\\..\\b" })
                    try { f.ReadText(bad); return "accepted the path " + bad; } catch (ArgumentException) { }
                try { f.Delete(""); return "removed the server folder"; } catch (ArgumentException) { }
                return null;
            });

            await CheckAsync("minecraft: players of an SSH server are changed in its files while it is stopped, keeping every other entry", async () =>
            {
                var dir = McTemp("ssh-players");
                File.WriteAllText(Path.Combine(dir, "whitelist.json"), "[ { \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000c001\", \"name\": \"Ava\" } ]");
                File.WriteAllText(Path.Combine(dir, "usercache.json"), "[ { \"name\": \"Ben\", \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000c002\", \"expiresOn\": \"2027-01-01 00:00:00 +0000\" } ]");
                var s = new ServerDef { Id = "probe-ssh-players", Target = Target.SshProcess, SshHost = "mc@198.51.100.7" };
                McFiles.Override = x => x.Id == s.Id ? new McLocalFiles(dir) : null;
                try
                {
                    await McPlayers.ApplyAsync(s, false, McPlayers.Change.Op, "Ben");
                    await McPlayers.ApplyAsync(s, false, McPlayers.Change.WhitelistAdd, "Ben");
                    var r = await McPlayers.ApplyAsync(s, false, McPlayers.Change.Ban, "Griefer_1", "griefing");
                    if (!r.Contains("SSH host")) return "the answer does not say the files are on the host: " + r;
                    var list = McPlayers.Read(McFiles.For(s));
                    var ben = list.FirstOrDefault(p => p.Name == "Ben");
                    if (ben == null || !ben.Operator || !ben.Whitelisted || ben.Uuid != "6f1c2d3e-0000-4000-8000-00000000c002") return "Ben was not made operator and whitelisted with his known UUID";
                    if (!list.Any(p => p.Name == "Ava" && p.Whitelisted)) return "the existing whitelist entry was lost";
                    if (!list.Any(p => p.Name == "Griefer_1" && p.Banned && p.BanReason == "griefing")) return "the ban was not written";
                    await McPlayers.ApplyAsync(s, false, McPlayers.Change.Pardon, "Griefer_1");
                    await McPlayers.ApplyAsync(s, false, McPlayers.Change.Deop, "Ben");
                    if (McPlayers.Read(McFiles.For(s)).Any(p => p.Banned || p.Operator)) return "pardon or deop did not take";
                    try { await McPlayers.ApplyAsync(s, false, McPlayers.Change.Kick, "Ben"); return "a kick was accepted while stopped"; } catch (InvalidOperationException) { }
                    return null;
                }
                finally { McFiles.Override = null; }
            });

            await CheckAsync("minecraft: plugins on an SSH server are listed, switched, installed and removed in the host's folder with a dated backup first", async () =>
            {
                var dir = McTemp("ssh-plugins");
                Directory.CreateDirectory(Path.Combine(dir, "plugins"));
                File.WriteAllText(Path.Combine(dir, "plugins", "Essentials-2.jar"), "x");
                File.WriteAllText(Path.Combine(dir, "plugins", "Old-1.jar.disabled"), "y");
                File.WriteAllText(Path.Combine(dir, "plugins", "readme.txt"), "not a plugin");
                var s = new ServerDef { Id = "probe-ssh-plugins", Flavour = Flavour.Paper, Target = Target.SshDocker, SshHost = "mc@198.51.100.7" };
                var f = new McLocalFiles(dir);
                var items = McContent.List(f, "plugins");
                if (items.Count != 2 || items[0].Name != "Essentials-2" || !items[0].Enabled || items[1].Enabled) return "list: " + string.Join(", ", items.Select(i => i.Name + (i.Enabled ? "+" : "-")));
                McContent.SetEnabled(items[0], false);
                if (!f.Exists("plugins/Essentials-2.jar.disabled") || f.Exists("plugins/Essentials-2.jar")) return "switch off";
                McContent.SetEnabled(items[1], true);
                if (!f.Exists("plugins/Old-1.jar")) return "switch on";
                if (f.List("plugins-backups").Count(e => e.IsDir) < 2) return "no dated backup before the switches";
                var jar = Path.Combine(McTemp("ssh-plugin-src"), "New-3.jar");
                Zip(jar, ("plugin.yml", "name: New\nversion: 3"));
                McContent.Install(f, "plugins", jar);
                if (!f.Exists("plugins/New-3.jar")) return "install";
                try { McContent.Install(f, "plugins", Path.Combine(dir, "plugins", "readme.txt")); return "installed a file that is not a jar"; } catch (ArgumentException) { }
                var again = McContent.List(f, "plugins");
                McContent.Remove(again.First(a => a.Name == "New-3"));
                if (f.Exists("plugins/New-3.jar")) return "remove";
                // the section lists the same folder for a sample SSH server
                McFiles.Override = x => x.Id == "sample-modded" ? new McLocalFiles(dir) : null;
                var modded = ServerManager.Servers.First(x => x.Id == "sample-modded");
                var oldFlavour = modded.Flavour; modded.Flavour = Flavour.Paper;
                try
                {
                    mc.Select("sample-modded");
                    mc.ShowSection("content");
                    await Wave6Flows.Settle(cc, 1500);
                    var text = string.Join(" ", Descend<TextBlock>(mc).Select(t => t.Text));
                    return text.Contains("Essentials-2") && text.Contains("Old-1") && !text.Contains("readme") ? null : "the section shows: " + text[..Math.Min(300, text.Length)];
                }
                finally { McFiles.Override = null; modded.Flavour = oldFlavour; mc.Select("sample-friends"); }
            });

            await CheckAsync("minecraft: an SSH server's world is backed up to this computer and restored to the host, keeping the host's old folders as a safety backup", async () =>
            {
                var dir = McTemp("ssh-world");
                File.WriteAllText(Path.Combine(dir, "server.properties"), "level-name=survival\n");
                Directory.CreateDirectory(Path.Combine(dir, "survival", "region"));
                File.WriteAllText(Path.Combine(dir, "survival", "level.dat"), "lvl");
                File.WriteAllText(Path.Combine(dir, "survival", "region", "r.0.0.mca"), "terrain A");
                Directory.CreateDirectory(Path.Combine(dir, "survival_nether"));
                File.WriteAllText(Path.Combine(dir, "survival_nether", "level.dat"), "nether");
                var s = new ServerDef { Id = "probe-ssh-world", Target = Target.SshProcess, SshHost = "mc@198.51.100.7", BackupDir = McTemp("ssh-world-backups"), KeepBackups = 5 };
                McFiles.Override = x => x.Id == s.Id ? new McLocalFiles(dir) : null;
                try
                {
                    var b = await McBackups.BackUpAsync(s, false, "By hand", "ssh test");
                    if (!b.Contents.SequenceEqual(new[] { "survival", "survival_nether" })) return "the backup holds " + string.Join(", ", b.Contents);
                    if (!McBackups.List(s.BackupFolder).Any(x => x.Path == b.Path)) return "the backup is not in the list";
                    File.WriteAllText(Path.Combine(dir, "survival", "region", "r.0.0.mca"), "terrain B");
                    File.WriteAllText(Path.Combine(dir, "survival", "added-later.txt"), "z");
                    var files = McFiles.For(s);
                    var safety = await Task.Run(() => McRemote.Restore(s, files, "survival", b));
                    if (File.ReadAllText(Path.Combine(dir, "survival", "region", "r.0.0.mca")) != "terrain A") return "the restore did not bring back the old terrain";
                    if (File.Exists(Path.Combine(dir, "survival", "added-later.txt"))) return "the restore left a file that was not in the backup";
                    if (safety == null || safety.Reason != "Before a restore") return "no safety backup";
                    using (var z = ZipFile.OpenRead(safety.Path))
                    using (var r = new StreamReader(z.GetEntry("survival/region/r.0.0.mca").Open()))
                        if (r.ReadToEnd() != "terrain B") return "the safety backup does not hold the world as it was on the host";
                    if (Directory.EnumerateDirectories(dir).Any(d => d.Contains(".restore-"))) return "a moved-aside folder was left on the host";
                    // the Worlds section reads sizes and offers a download for each folder
                    var dims = McRemote.Dimensions(files, "survival");
                    if (dims.Count != 2 || dims[0].Kind != "Overworld" || dims[1].Kind != "Nether" || dims[0].Size <= 0) return "dimensions: " + string.Join(", ", dims.Select(d => d.Name));
                    var zip = Path.Combine(McTemp("ssh-world-zip"), "w.zip");
                    McRemote.DownloadZip(files, "survival", zip);
                    using (var z = ZipFile.OpenRead(zip)) if (z.GetEntry("survival/region/r.0.0.mca") == null) return "the world ZIP lacks the region file";
                    return null;
                }
                finally { McFiles.Override = null; }
            });

            await CheckAsync("minecraft: the Backups and Worlds sections work for a server on an SSH host", async () =>
            {
                var dir = McTemp("ssh-sections");
                File.WriteAllText(Path.Combine(dir, "server.properties"), "level-name=world\n");
                Directory.CreateDirectory(Path.Combine(dir, "world", "region"));
                File.WriteAllText(Path.Combine(dir, "world", "level.dat"), "lvl");
                File.WriteAllText(Path.Combine(dir, "world", "region", "r.0.0.mca"), "terrain");
                var modded = ServerManager.Servers.First(x => x.Id == "sample-modded");
                var oldBackups = modded.BackupDir; modded.BackupDir = McTemp("ssh-sections-backups");
                McFiles.Override = x => x.Id == "sample-modded" ? new McLocalFiles(dir) : null;
                McState.Set(modded, McRunState.Stopped, "sample server");
                try
                {
                    mc.Select("sample-modded");
                    mc.ShowSection("worlds");
                    await Wave6Flows.Settle(cc, 1500);
                    var text = string.Join(" ", Descend<TextBlock>(mc).Select(t => t.Text));
                    if (!text.Contains("on the host") || !text.Contains("Overworld")) return "Worlds shows: " + text[..Math.Min(300, text.Length)];
                    mc.ShowSection("backups");
                    await Wave6Flows.Settle(cc, 500);
                    if (!Press(mc, "Back up now")) return "no Back up now button";
                    for (int i = 0; i < 40 && McBackups.List(modded.BackupFolder).Count == 0; i++) await Task.Delay(250);
                    var made = McBackups.List(modded.BackupFolder);
                    return made.Count == 1 && made[0].Contents.Contains("world") ? null : "the section made " + made.Count + " backups";
                }
                finally { McFiles.Override = null; McState.Set(modded, McRunState.Running, "sample server"); modded.BackupDir = oldBackups; mc.Select("sample-friends"); }
            });

            Check("minecraft: Bedrock players are read from allowlist.json and permissions.json and edited while the server is stopped", () =>
            {
                var dir = McTemp("bedrock-players");
                var f = new McLocalFiles(dir);
                f.WriteText("allowlist.json", "[ { \"ignoresPlayerLimit\": false, \"name\": \"Steve\", \"xuid\": \"2535400000000001\" }, { \"ignoresPlayerLimit\": false, \"name\": \"Alex Smith\" } ]");
                f.WriteText("permissions.json", "[ { \"permission\": \"operator\", \"xuid\": \"2535400000000001\" }, { \"permission\": \"visitor\", \"xuid\": \"2535400000000099\" } ]");
                var list = McBedrockPlayers.Read(f);
                var steve = list.FirstOrDefault(p => p.Name == "Steve");
                if (steve == null || !steve.Operator || steve.Permission != "operator" || !steve.Whitelisted) return "Steve was read wrongly";
                if (!list.Any(p => p.Anonymous && p.Xuid == "2535400000000099" && p.Permission == "visitor")) return "an id in permissions.json with no name was hidden";
                McBedrockPlayers.ApplyToFiles(f, BedrockChange.AllowAdd, "Notch", "2535400000000002");
                McBedrockPlayers.ApplyToFiles(f, BedrockChange.Operator, "Notch");
                McBedrockPlayers.ApplyToFiles(f, BedrockChange.Visitor, "Steve");
                McBedrockPlayers.ApplyToFiles(f, BedrockChange.AllowRemove, "Alex Smith");
                list = McBedrockPlayers.Read(f);
                if (list.First(p => p.Name == "Notch").Permission != "operator") return "Notch was not made operator through his id";
                if (list.First(p => p.Name == "Steve").Permission != "visitor") return "Steve was not made a visitor";
                if (list.Any(p => p.Name == "Alex Smith")) return "Alex Smith stayed on the allow list";
                McBedrockPlayers.ApplyToFiles(f, BedrockChange.Member, "Steve");
                if (McBedrockPlayers.Read(f).First(p => p.Name == "Steve").Permission != "") return "back to the default permission did not clear the entry";
                McBedrockPlayers.ApplyToFiles(f, BedrockChange.AllowAdd, "Alex Smith");
                try { McBedrockPlayers.ApplyToFiles(f, BedrockChange.Operator, "Alex Smith"); return "an operator was made with no Xbox user id"; } catch (InvalidOperationException ex) when (ex.Message.Contains("Xbox user id")) { }
                McBedrockPlayers.RemovePermission(f, "2535400000000099");
                if (McBedrockPlayers.Read(f).Any(p => p.Anonymous)) return "the id-only entry was not removed";
                foreach (var bad in new[] { "", "a\"b", "x;y", "a  b", " lead", "trail ", new string('n', 40) }) if (McBedrockPlayers.ValidName(bad)) return "accepted the name \"" + bad + "\"";
                if (McBedrockPlayers.Command(BedrockChange.AllowAdd, "Alex Smith") != "allowlist add \"Alex Smith\"" || McBedrockPlayers.Command(BedrockChange.Member, "Steve") != "deop Steve" || McBedrockPlayers.Command(BedrockChange.Visitor, "x") != null) return "console lines";
                return null;
            });

            await CheckAsync("minecraft: Bedrock add-ons install from a .mcaddon, switch on and off with their dependencies, remove, and undo", async () =>
            {
                var dir = McTemp("bedrock-addons");
                var f = new McLocalFiles(dir);
                f.WriteText("server.properties", "level-name=Test world\n");
                foreach (var d in new[] { "behavior_packs/vanilla", "resource_packs/vanilla", "worlds/Test world/db" }) f.MakeDir(d);
                f.WriteText("worlds/Test world/level.dat", "x");
                var rpId = Guid.NewGuid().ToString(); var bpId = Guid.NewGuid().ToString();
                var addon = Path.Combine(McTemp("bedrock-addons-src"), "Cool.mcaddon");
                Zip(addon, ("Cool BP/manifest.json", Manifest("Cool behavior", bpId, "data", rpId)), ("Cool BP/scripts/a.js", "1"), ("Cool RP/manifest.json", Manifest("Cool resources", rpId, "resources")), ("Cool RP/textures/t.png", "png"));
                var packs = McBedrockAddons.List(f, "Test world");
                if (packs.Count != 2 || packs.Any(p => !p.BuiltIn)) return "the shipped packs were not marked built in";
                var res = McBedrockAddons.Install(f, addon, "Test world", enable: false);
                if (res.Installed.Count != 2 || !f.Exists("behavior_packs/Cool behavior/manifest.json") || !f.Exists("resource_packs/Cool resources/textures/t.png")) return "install: " + string.Join(", ", res.Installed);
                var mine = McBedrockAddons.List(f, "Test world").Where(p => !p.BuiltIn).ToList();
                var bp = mine.First(p => p.Behavior);
                if (bp.Uuid != bpId || bp.Version != "1.2.3" || bp.Enabled || !bp.DependsOn.Contains(rpId)) return "the behavior pack was read wrongly: " + bp.Uuid + " " + bp.Version;
                McBedrockAddons.Switch(f, "Test world", bp, true);
                mine = McBedrockAddons.List(f, "Test world").Where(p => !p.BuiltIn).ToList();
                if (!mine.All(p => p.Enabled)) return "switching the behavior pack on did not also switch on the resource pack it depends on";
                using (var doc = JsonDocument.Parse(f.ReadText("worlds/Test world/world_behavior_packs.json")))
                    if (doc.RootElement[0].GetProperty("pack_id").GetString() != bpId || doc.RootElement[0].GetProperty("version").GetArrayLength() != 3) return "world_behavior_packs.json is wrong";
                McBedrockAddons.Switch(f, "Test world", bp, false);
                if (McBedrockAddons.List(f, "Test world").First(p => p.Uuid == bpId).Enabled) return "switch off";
                McBedrockAddons.Remove(f, "Test world", mine.First(p => !p.Behavior));
                if (f.Exists("resource_packs/Cool resources")) return "remove left the folder";
                var undone = McBedrockAddons.Undo(f);
                if (!f.Exists("resource_packs/Cool resources/manifest.json")) return "undo did not put the removed pack back";
                // a pack whose id is already installed replaces it; a pack with an unknown module is skipped; an entry outside the folder is refused
                var again = McBedrockAddons.Install(f, addon, "Test world", enable: true);
                if (again.Installed.Count != 2 || McBedrockAddons.List(f, "Test world").Count(p => !p.BuiltIn) != 2) return "installing the same add-on twice duplicated it";
                var skin = Path.Combine(McTemp("bedrock-addons-skin"), "skin.mcpack");
                Zip(skin, ("manifest.json", Manifest("Skins", Guid.NewGuid().ToString(), "skin_pack")));
                var sk = McBedrockAddons.Install(f, skin, "Test world", true);
                if (sk.Installed.Count != 0 || sk.Skipped.Count != 1) return "a skin pack was installed";
                var evil = Path.Combine(McTemp("bedrock-addons-evil"), "evil.mcpack");
                Zip(evil, ("../escape.txt", "no"), ("manifest.json", Manifest("Evil", Guid.NewGuid().ToString(), "data")));
                try { McBedrockAddons.Install(f, evil, "Test world", true); return "a path outside the folder was accepted"; } catch (InvalidDataException) { }
                var nested = Path.Combine(McTemp("bedrock-addons-nested"), "pair.mcaddon");
                var inner = Path.Combine(McTemp("bedrock-addons-inner"), "inner.mcpack");
                Zip(inner, ("manifest.json", Manifest("Inner pack", Guid.NewGuid().ToString(), "resources")));
                using (var z = ZipFile.Open(nested, ZipArchiveMode.Create)) z.CreateEntryFromFile(inner, "inner.mcpack");
                if (McBedrockAddons.Install(f, nested, "Test world", false).Installed.Count != 1) return "a .mcpack inside a .mcaddon was not installed";
                var noWorld = new McLocalFiles(McTemp("bedrock-addons-noworld"));
                var early = McBedrockAddons.Install(noWorld, addon, "Not yet", true);
                if (!early.NeedsWorld || early.Installed.Count != 2) return "installing before the world exists did not say so";
                try { McBedrockAddons.SetEnabled(noWorld, "Not yet", McBedrockAddons.List(noWorld, "Not yet").Take(1), true); return "a pack was switched on for a world that does not exist"; } catch (InvalidOperationException) { }
                return null;
            });

            Check("minecraft: a Bedrock world is imported from a folder or a .mcworld, checked, and a Java world is refused", () =>
            {
                var dir = McTemp("bedrock-worlds");
                var f = new McLocalFiles(dir);
                var src = McTemp("bedrock-world-src");
                Directory.CreateDirectory(Path.Combine(src, "db"));
                File.WriteAllText(Path.Combine(src, "level.dat"), "L1"); File.WriteAllText(Path.Combine(src, "levelname.txt"), "My world"); File.WriteAllText(Path.Combine(src, "db", "000001.ldb"), "d");
                McBedrockWorlds.Import(f, src, "Bedrock level", false);
                if (f.ReadText("worlds/Bedrock level/level.dat") != "L1" || !f.Exists("worlds/Bedrock level/db/000001.ldb")) return "folder import";
                try { McBedrockWorlds.Import(f, src, "Bedrock level", false); return "an existing world was overwritten without replace"; } catch (IOException) { }
                var mcworld = Path.Combine(McTemp("bedrock-world-zip"), "w.mcworld");
                Zip(mcworld, ("level.dat", "L2"), ("levelname.txt", "Zip"), ("db/000001.ldb", "d"));
                McBedrockWorlds.Import(f, mcworld, "Bedrock level", true);
                if (f.ReadText("worlds/Bedrock level/level.dat") != "L2") return "replace from .mcworld";
                if (f.List("worlds").Any(e => e.Name.Contains("replaced"))) return "the moved-aside world was left";
                var listed = McBedrockWorlds.List(f, "Bedrock level");
                if (listed.Count != 1 || !listed[0].InUse || listed[0].Title != "Zip" || listed[0].Size <= 0) return "world list";
                var java = McTemp("java-world-src");
                Directory.CreateDirectory(Path.Combine(java, "region")); File.WriteAllText(Path.Combine(java, "level.dat"), "J");
                var (_, problem) = McBedrockWorlds.Validate(java);
                if (problem == null || !problem.Contains("Java")) return "a Java world was accepted: " + problem;
                var bad = Path.Combine(McTemp("bedrock-world-bad"), "bad.mcworld");
                Zip(bad, ("readme.txt", "no level"));
                if (McBedrockWorlds.Validate(bad).Problem == null) return "an archive with no level.dat was accepted";
                var evil = Path.Combine(McTemp("bedrock-world-evil"), "evil.mcworld");
                Zip(evil, ("level.dat", "x"), ("db/a", "x"), ("db/../../../escape.txt", "no"));
                try { McBedrockWorlds.Import(f, evil, "Other", false); if (File.Exists(Path.Combine(dir, "escape.txt")) || File.Exists(Path.Combine(Path.GetTempPath(), "escape.txt"))) return "an entry escaped the folder"; }
                catch (InvalidDataException) { }
                return null;
            });

            await CheckAsync("minecraft: the Players, Plugins and Worlds sections of a Bedrock server show allowlist, add-ons and worlds", async () =>
            {
                var dir = McTemp("bedrock-ui");
                var s = new ServerDef { Id = "probe-bedrock-ui", Name = "Bedrock probe", Version = "1.26.61.1", Flavour = Flavour.Bedrock, Target = Target.LocalProcess, Port = 19190, FolderPath = dir, BackupDir = McTemp("bedrock-ui-backups") };
                var f = new McLocalFiles(dir);
                f.WriteText("server.properties", "level-name=Bedrock level\nallow-list=true\n");
                f.WriteText("allowlist.json", "[ { \"ignoresPlayerLimit\": false, \"name\": \"Steve\", \"xuid\": \"2535400000000001\" }, { \"ignoresPlayerLimit\": false, \"name\": \"Alex\" } ]");
                f.WriteText("permissions.json", "[ { \"permission\": \"operator\", \"xuid\": \"2535400000000001\" } ]");
                f.MakeDir("worlds/Bedrock level/db"); f.WriteText("worlds/Bedrock level/level.dat", "x"); f.WriteText("worlds/Bedrock level/levelname.txt", "Probe");
                f.MakeDir("behavior_packs/vanilla");
                var bpId = Guid.NewGuid().ToString();
                f.WriteText("behavior_packs/My add-on/manifest.json", Manifest("My add-on", bpId, "data"));
                f.WriteText("worlds/Bedrock level/world_behavior_packs.json", "[ { \"pack_id\": \"" + bpId + "\", \"version\": [1,2,3] } ]");
                ServerManager.Servers.Add(s);
                McState.Set(s, McRunState.Stopped, "probe");
                try
                {
                    mc.Select(s.Id);
                    mc.ShowSection("players");
                    await Wave6Flows.Settle(cc, 900);
                    var text = string.Join(" ", Descend<TextBlock>(mc).Select(t => t.Text));
                    if (!text.Contains("Steve") || !text.Contains("Alex") || !text.Contains("allowlist.json")) return "Players shows: " + text[..Math.Min(300, text.Length)];
                    var ops = Descend<CheckBox>(mc).Where(c => AutomationProperties.GetName(c)?.StartsWith("Operator: ") == true).ToList();
                    var allow = Descend<CheckBox>(mc).Where(c => AutomationProperties.GetName(c)?.StartsWith("Allow list: ") == true).ToList();
                    if (ops.Count != 2 || allow.Count != 2) return $"{ops.Count} operator and {allow.Count} allow list switches";
                    if (ops.First(c => AutomationProperties.GetName(c) == "Operator: Steve").IsChecked != true) return "Steve is not shown as an operator";
                    mc.ShowSection("content");
                    await Wave6Flows.Settle(cc, 900);
                    text = string.Join(" ", Descend<TextBlock>(mc).Select(t => t.Text));
                    if (!text.Contains("My add-on") || !text.Contains("Add-ons") || text.Contains("vanilla")) return "Add-ons shows: " + text[..Math.Min(300, text.Length)];
                    if (FindButton(mc, "Install an add-on…") == null || FindButton(mc, "Undo the last change") == null) return "the install and undo buttons are missing";
                    mc.ShowSection("worlds");
                    await Wave6Flows.Settle(cc, 900);
                    text = string.Join(" ", Descend<TextBlock>(mc).Select(t => t.Text));
                    if (!text.Contains("Bedrock level") || !text.Contains("the world the server uses") || FindButton(mc, "Import a world…") == null || Named(mc, "Download Bedrock level as a ZIP") == null) return "Worlds shows: " + text[..Math.Min(300, text.Length)];
                    return null;
                }
                finally { ServerManager.Servers.Remove(s); mc.Select("sample-friends"); }
            });

            await CheckAsync("minecraft: the player list is a multi-select list with search and a regex builder, and bulk actions send one exact command per selected player", async () =>
            {
                var sent = new List<string>();
                McBulk.Sender = (sv, line) => { sent.Add(line); return Task.FromResult(line.StartsWith("tp ") && line.Contains("Nobody") ? "No player was found" : "ok"); };
                var s = ServerManager.Servers.First(x => x.Id == "sample-friends");
                try
                {
                    mc.Select("sample-friends");
                    mc.ShowSection("players");
                    await Wave6Flows.Settle(cc, 700);
                    var list = Descend<ListBox>(mc).FirstOrDefault(l => AutomationProperties.GetName(l) == "Players");
                    if (list == null || list.SelectionMode != SelectionMode.Extended) return "no multi-select Players list";
                    if (list.Items.Count != 5) return list.Items.Count + " players in the list";
                    var field = Descend<MonitoringTools.UI.Search.SearchField>(mc).FirstOrDefault(f => f.Label == "Search players");
                    if (field == null) return "no search field above the players";
                    field.State.SetRegex(true);
                    field.State.SetQuery("^(Ava|Ben|Dev)$");
                    await Task.Delay(300);
                    if (list.Items.Count != 3) return "the regex shows " + list.Items.Count + " players";
                    field.State.SetRegex(false);
                    field.State.SetQuery("e");   // Ben, Chloe, Dev
                    await Task.Delay(300);
                    if (list.Items.Count != 3) return "the plain search shows " + list.Items.Count;
                    field.State.SetQuery("");
                    field.State.SetRegex(true);
                    field.State.SetQuery("^(Ava|Ben|Dev)$");
                    await Task.Delay(300);
                    list.SelectAll();
                    await Task.Delay(200);
                    var text = string.Join(" ", Descend<TextBlock>(mc).Select(t => t.Text));
                    if (!text.Contains("3 selected: Ava, Ben, Dev")) return "the selection line says: " + text[Math.Max(0, text.IndexOf("selected") - 40)..Math.Min(text.Length, text.IndexOf("selected") + 60)];
                    var reason = Descend<TextBox>(mc).First(t => AutomationProperties.GetName(t) == "Reason for kick or ban");
                    reason.Text = "see rules";
                    var tp = Descend<TextBox>(mc).First(t => AutomationProperties.GetName(t) == "Teleport to");
                    var item = Descend<TextBox>(mc).First(t => AutomationProperties.GetName(t) == "Item to give");
                    var count = Descend<TextBox>(mc).First(t => AutomationProperties.GetName(t) == "Count");
                    // the two-key actions ask first, naming the count and the players, and send nothing until confirmed
                    foreach (var (label, word) in new[] { ("Kick…", "Kick 3 players?"), ("Ban…", "Ban 3 players?"), ("Make operators…", "Make operators of 3 players?") })
                    {
                        if (!Press(mc, label)) return "no " + label + " button";
                        await Task.Delay(300);
                        var dlg = System.Windows.Application.Current.Windows.OfType<ConfirmWindow>().FirstOrDefault();
                        if (dlg == null) return label + " did not ask for confirmation";
                        var dt = string.Join(" ", Descend<TextBlock>(dlg).Select(t => t.Text));
                        dlg.Close();
                        if (!dt.Contains(word) || !dt.Contains("Ava, Ben, Dev")) return label + " asked: " + dt;
                        if (sent.Count > 0) return label + " sent a command before it was confirmed";
                    }
                    Press(mc, "Add to whitelist"); await Task.Delay(500);
                    Press(mc, "Remove operators"); await Task.Delay(500);
                    Press(mc, "Pardon"); await Task.Delay(500);
                    tp.Text = "Chloe"; Press(mc, "Teleport"); await Task.Delay(500);
                    tp.Text = "10 64 -20"; Press(mc, "Teleport"); await Task.Delay(500);
                    item.Text = "minecraft:diamond"; count.Text = "2"; Press(mc, "Give"); await Task.Delay(500);
                    var want = new[]
                    {
                        "whitelist add Ava", "whitelist add Ben", "whitelist add Dev", "deop Ava", "deop Ben", "deop Dev", "pardon Ava", "pardon Ben", "pardon Dev",
                        "tp Ava Chloe", "tp Ben Chloe", "tp Dev Chloe", "tp Ava 10 64 -20", "tp Ben 10 64 -20", "tp Dev 10 64 -20",
                        "give Ava minecraft:diamond 2", "give Ben minecraft:diamond 2", "give Dev minecraft:diamond 2",
                    };
                    if (!sent.SequenceEqual(want)) return "sent: " + string.Join(" | ", sent);
                    tp.Text = "bad target!"; sent.Clear(); Press(mc, "Teleport"); await Task.Delay(200);
                    if (sent.Count != 0) return "an invalid teleport target was sent";
                    // exact lines for the actions that ask first, for game mode, and for Bedrock names with spaces
                    var a = new BulkArgs { Reason = "see rules", Mode = "creative" };
                    string Join(ServerDef sv, BulkVerb v, params string[] n) => string.Join("|", McBulk.Lines(sv, v, n, a).Select(x => x.Line));
                    if (Join(s, BulkVerb.Kick, "Ava", "Ben") != "kick Ava see rules|kick Ben see rules" || Join(s, BulkVerb.Ban, "Ava") != "ban Ava see rules" || Join(s, BulkVerb.Op, "Ava") != "op Ava" || Join(s, BulkVerb.GameMode, "Ava", "Ben") != "gamemode creative Ava|gamemode creative Ben") return "Java lines are wrong";
                    var b = new ServerDef { Flavour = Flavour.Bedrock };
                    if (Join(b, BulkVerb.AllowAdd, "Alex Smith") != "allowlist add \"Alex Smith\"" || Join(b, BulkVerb.Kick, "Alex Smith") != "kick \"Alex Smith\" see rules" || McBulk.Supported(b, BulkVerb.Ban)) return "Bedrock lines are wrong";
                    // every action of the selection reports per player, including the one that failed
                    sent.Clear();
                    var res = await McBulk.RunAsync(s, true, BulkVerb.Teleport, new[] { "Ava", "Ben" }, new BulkArgs { Target = "Nobody" });
                    var sum = McBulk.Summary(BulkVerb.Teleport, res);
                    if (sent.Count != 2 || res.Any(r => r.Ok) || !sum.Contains("0 of 2 done") || !sum.Contains("No player was found")) return "the failures were not reported per player: " + sum;
                    list.UnselectAll(); await Task.Delay(200);
                    return Descend<TextBlock>(mc).Any(t => t.Text.StartsWith("Nothing selected")) ? null : "clearing the selection did not say so";
                }
                finally
                {
                    McBulk.Sender = (sv, line) => McDemo.IsSample(sv) ? Task.FromResult("Sample server: not sent.") : ServerManager.CommandAsync(sv, line);
                    foreach (var w in System.Windows.Application.Current.Windows.OfType<ConfirmWindow>().ToList()) w.Close();
                    var f = Descend<MonitoringTools.UI.Search.SearchField>(mc).FirstOrDefault(x => x.Label == "Search players");
                    f?.State.SetQuery(""); f?.State.SetRegex(false);
                }
            });

            await CheckAsync("minecraft: Give in the command builder opens the give form with the item, the count and the first selected player filled in", async () =>
            {
                mc.Select("sample-friends");
                mc.ShowSection("players");
                await Wave6Flows.Settle(cc, 700);
                var s = ServerManager.Servers.First(x => x.Id == "sample-friends");
                var vals = McManagerView.ValuesFor(s, "give");
                var before = new Dictionary<string, string>(vals);
                try
                {
                    var list = Descend<ListBox>(mc).First(l => AutomationProperties.GetName(l) == "Players");
                    if (FindButton(mc, "Give in the command builder…") == null) return "no Give in the command builder button";
                    list.UnselectAll(); await Task.Delay(150);
                    if (FindButton(mc, "Give in the command builder…")?.IsEnabled == true) return "the hand-off is enabled with nothing selected";
                    list.SelectAll(); await Task.Delay(200);
                    Descend<TextBox>(mc).First(t => AutomationProperties.GetName(t) == "Item to give").Text = "minecraft:emerald";
                    Descend<TextBox>(mc).First(t => AutomationProperties.GetName(t) == "Count").Text = "5";
                    if (!Press(mc, "Give in the command builder…")) return "the button did not press";
                    await Wave6Flows.Settle(cc, 700);
                    if (McManagerView.Section != "commands" || McManagerView.CommandTaskId != "give") return $"the builder opened on {McManagerView.Section}/{McManagerView.CommandTaskId}";
                    if (vals.GetValueOrDefault("targets") != "Ava" || vals.GetValueOrDefault("item") != "minecraft:emerald" || vals.GetValueOrDefault("count") != "5") return "values: " + string.Join(", ", vals.Select(kv => kv.Key + "=" + kv.Value));
                    var line = mc.PreviewLine ?? "";
                    return line.Contains("give Ava minecraft:emerald 5") ? null : "the preview says: " + line;
                }
                finally
                {
                    vals.Clear(); foreach (var kv in before) vals[kv.Key] = kv.Value;
                    mc.ShowSection("players");
                }
            });

            await CheckAsync("minecraft: an SSH server's plugins are replaced and rolled back on the host, jars are hashed there, and backups can live on the host and be read back", async () =>
            {
                var dir = McTemp("ssh-catalogue");
                var f = new McLocalFiles(dir);
                var s = new ServerDef { Id = "probe-ssh-catalogue", Flavour = Flavour.Paper, Target = Target.SshProcess, SshHost = "mc@198.51.100.7", BackupDir = McTemp("ssh-catalogue-backups"), KeepBackups = 3 };
                McFiles.Override = x => x.Id == s.Id ? f : null;
                try
                {
                    var oldJar = Path.Combine(McTemp("ssh-catalogue-src"), "Old-1.jar"); Zip(oldJar, ("plugin.yml", "name: Old\nversion: 1"));
                    var newJar = Path.Combine(Path.GetDirectoryName(oldJar), "New-2.jar"); Zip(newJar, ("plugin.yml", "name: Old\nversion: 2"));
                    McContent.Install(f, "plugins", oldJar);
                    var hashes = f.Sha512(new[] { "plugins/Old-1.jar", "plugins/missing.jar" });
                    var want = Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(File.ReadAllBytes(oldJar))).ToLowerInvariant();
                    if (hashes.Count != 1 || hashes["plugins/Old-1.jar"] != want) return "hashes on the host";
                    // a catalogue update: the verified download replaces the old jar on the host after a dated copy
                    McCatalogue.ReplaceFor(s, null, newJar, "New-2.jar", "plugins/Old-1.jar");
                    if (!f.Exists("plugins/New-2.jar") || f.Exists("plugins/Old-1.jar")) return "the update did not replace the jar on the host";
                    var last = McContent.LatestBackup(f, "plugins");
                    if (last == null || !f.Exists(last + "/Old-1.jar")) return "no dated copy holding the old jar";
                    McContent.RestoreFrom(f, "plugins", last, keepCurrent: true);
                    if (!f.Exists("plugins/Old-1.jar") || f.Exists("plugins/New-2.jar")) return "Undo did not put the old jar back and remove the new one";
                    // a disabled jar stays disabled when it is updated
                    McContent.SetEnabled(McContent.List(f, "plugins")[0], false);
                    McCatalogue.ReplaceFor(s, null, newJar, "New-2.jar", "plugins/Old-1.jar.disabled");
                    if (!f.Exists("plugins/New-2.jar.disabled") || f.Exists("plugins/New-2.jar")) return "a disabled jar became enabled by its update";
                    // on-host backups
                    f.WriteText("server.properties", "level-name=w\n");
                    f.WriteText("w/level.dat", "lvl"); f.WriteText("w/region/r.mca", "A"); f.WriteText("w_nether/level.dat", "n");
                    s.BackupOnHost = true;
                    var b = await McBackups.BackUpAsync(s, false, "By hand", "on host");
                    if (!b.Path.StartsWith(".mt-backups/") || !b.Contents.SequenceEqual(new[] { "w", "w_nether", "plugins" }) || b.Note != "on host" || b.Size <= 0) return "on-host backup: " + b.Path + " " + string.Join(",", b.Contents);
                    if (Directory.EnumerateFiles(s.BackupFolder).Any()) return "a backup was also copied to this computer";
                    var listed = McRemote.HostList(f);
                    if (listed.Count != 1 || listed[0].Reason != "By hand" || !listed[0].Contents.Contains("w")) return "the list read back from the host";
                    f.WriteText("w/region/r.mca", "B"); f.WriteText("w/extra.txt", "x");
                    var safety = McRemote.HostRestore(s, f, "w", listed[0]);
                    if (f.ReadText("w/region/r.mca") != "A" || f.Exists("w/extra.txt")) return "the on-host restore did not bring back the old world";
                    if (safety == null || McRemote.HostList(f).Count != 2) return "no safety backup on the host";
                    for (int i = 0; i < 4; i++) await McBackups.BackUpAsync(s, false, "Scheduled", "");
                    return McRemote.HostList(f).Count == 3 ? null : "keep 3 left " + McRemote.HostList(f).Count;
                }
                finally { McFiles.Override = null; }
            });

            await CheckAsync("minecraft: bulk actions on a stopped server edit the files, on this computer, over SSH and for Bedrock, and say what needs a running server", async () =>
            {
                var dir = McTemp("bulk-files");
                var j = new ServerDef { Id = "probe-bulk-java", Target = Target.SshProcess, SshHost = "mc@198.51.100.7" };
                var bd = McTemp("bulk-bedrock");
                var br = new ServerDef { Id = "probe-bulk-bedrock", Flavour = Flavour.Bedrock, Target = Target.LocalProcess, FolderPath = bd };
                McFiles.Override = x => x.Id == j.Id ? new McLocalFiles(dir) : null;
                try
                {
                    var names = new[] { "Ava", "Ben" };
                    var a = new BulkArgs { Reason = "spam" };
                    foreach (var v in new[] { BulkVerb.Ban, BulkVerb.Op, BulkVerb.AllowAdd })
                        if ((await McBulk.RunAsync(j, false, v, names, a)).Any(r => !r.Ok)) return v + " failed over SSH";
                    var list = McPlayers.Read(new McLocalFiles(dir));
                    if (list.Count != 2 || list.Any(p => !p.Banned || p.BanReason != "spam" || !p.Operator || !p.Whitelisted)) return "the files hold: " + string.Join(", ", list.Select(p => $"{p.Name} ban={p.Banned} op={p.Operator} wl={p.Whitelisted}"));
                    foreach (var v in new[] { BulkVerb.Pardon, BulkVerb.Deop, BulkVerb.AllowRemove }) await McBulk.RunAsync(j, false, v, names, a);
                    if (McPlayers.Read(new McLocalFiles(dir)).Any(p => p.Banned || p.Operator || p.Whitelisted)) return "the reverse actions did not take";
                    var kick = await McBulk.RunAsync(j, false, BulkVerb.Kick, names, a);
                    if (kick.Any(r => r.Ok) || !kick[0].Message.Contains("running")) return "a kick was accepted while stopped";
                    var give = await McBulk.RunAsync(j, false, BulkVerb.GameMode, names, new BulkArgs { Mode = "creative" });
                    if (give.Any(r => r.Ok)) return "game mode was accepted while stopped";
                    // Bedrock: allow list and operator through its two files; ban is not a Bedrock command
                    var seeded = new McLocalFiles(bd);
                    seeded.WriteText("allowlist.json", "[ { \"ignoresPlayerLimit\": false, \"name\": \"Steve\", \"xuid\": \"2535400000000001\" } ]");
                    var r1 = await McBulk.RunAsync(br, false, BulkVerb.Op, new[] { "Steve" }, a);
                    var r2 = await McBulk.RunAsync(br, false, BulkVerb.Op, new[] { "Stranger" }, a);
                    var r3 = await McBulk.RunAsync(br, false, BulkVerb.Ban, new[] { "Steve" }, a);
                    if (!r1[0].Ok || McBedrockPlayers.Read(seeded).First(p => p.Name == "Steve").Permission != "operator") return "Bedrock op through the files";
                    if (r2[0].Ok || !r2[0].Message.Contains("Xbox user id")) return "an operator with no id: " + r2[0].Message;
                    if (r3[0].Ok) return "Bedrock ban was accepted";
                    return McBulk.Problem(j, BulkVerb.Give, new BulkArgs { Item = "Diamond!", Count = 1 }) != null && McBulk.Problem(j, BulkVerb.Give, new BulkArgs { Item = "diamond", Count = 0 }) != null && McBulk.Problem(j, BulkVerb.Give, new BulkArgs { Item = "minecraft:diamond", Count = 64 }) == null ? null : "give arguments are not checked";
                }
                finally { McFiles.Override = null; }
            });

            Check("minecraft: Bedrock runs on Docker and over SSH: the container is made for the image, the Linux install script is safe, and console commands use send-command", () =>
            {
                var s = new ServerDef { Id = "abc123", Name = "Family", Flavour = Flavour.Bedrock, Target = Target.LocalDocker, Port = 19140 };
                var args = McBedrock.DockerCreateArgs(s, "\"C:\\data\"");
                if (!args.Contains("-p 19140:19132/udp") || !args.Contains("itzg/minecraft-bedrock-server") || !args.Contains("-e EULA=TRUE") || !args.Contains("-i -t") || !args.Contains("monitoring-tools.server=abc123")) return "container arguments: " + args;
                var script = McBedrock.LinuxInstallScript("mc/it's", "https://www.minecraft.net/bedrockdedicatedserver/bin-linux/bedrock-server-1.26.61.1.zip");
                if (!script.Contains("mkdir -p 'mc/it'\\''s'") || !script.Contains("unzip -q -n") || script.Contains("unzip -q -o")) return "install script: " + script;
                if (McBedrock.CheckUrl("http://www.minecraft.net/a.zip") == null || McBedrock.CheckUrl("https://evil.example/a.zip") == null || McBedrock.CheckUrl("https://www.minecraft.net/a.zip") != null) return "the download link check";
                return null;
            });
        }

    }
}
