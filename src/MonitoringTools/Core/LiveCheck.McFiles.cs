using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    // Live checks for the server file store over a real SSH host, and for Bedrock add-ons and the allow list on real servers.
    // MT_LIVE_ONLY=minecraft-ssh-files runs the SSH one (a throwaway OpenSSH container);
    // MT_LIVE_ONLY=minecraft-bedrock-files runs the Windows Bedrock one; MT_LIVE_ONLY=minecraft-bedrock-docker runs Bedrock in Docker.
    public static partial class LiveCheck
    {
        private static async Task McFilesLive(List<Result> results, string only)
        {
            if (only == null || only == "minecraft-ssh-files")
                await Check(results, "minecraft: files, players, plugins, backup, restore and worlds of a server on a real SSH host, and the Linux Bedrock install", SshFiles);
            if (only == null || only == "minecraft-bedrock-files")
                await Check(results, "minecraft: a real Bedrock server reads an installed add-on and an allow list entry written by the app", BedrockFiles);
            if (only == null || only == "minecraft-bedrock-docker")
                await Check(results, "minecraft: a real Bedrock server in Docker is created, started, takes a console command and stops", BedrockDocker);
        }

        private static async Task<string> SshFiles()
        {
            NeedDocker();
            var dir = Path.Combine(Settings.DataDir, "sshfiles-live-" + Stamp());
            Directory.CreateDirectory(dir);
            var (key, pub) = KeyPair(dir);
            int sshPort = 22420; while (!ServerManager.PortFree(sshPort)) sshPort++;
            var name = "mt-live-sshfiles-" + Stamp();
            var user = ThrowawayUser();
            ServerDef s = null;
            try
            {
                var sw = Stopwatch.StartNew();
                var run = Docker($"run -d --name {name} -e PUID=1000 -e PGID=1000 -e USER_NAME={user} -e \"PUBLIC_KEY={pub}\" -p 127.0.0.1:{sshPort}:2222 lscr.io/linuxserver/openssh-server:latest", 600000);
                if (run.Code != 0) return "docker run failed: " + ServerManager.ActionLine(run.Out);
                var apk = Docker($"exec {name} apk add --no-cache curl unzip", 600000);
                if (apk.Code != 0) return "installing curl and unzip failed: " + ServerManager.ActionLine(apk.Out);
                var host = SshHostSpec(user, sshPort);
                var approve = await ApproveHost(host);
                if (approve != null) return approve;
                Say($"SSH host ready in {sw.Elapsed.TotalSeconds:0} s");
                s = new ServerDef { Name = "Live SSH files", Version = Version, Flavour = Flavour.Paper, Target = Target.SshProcess, SshHost = host, SshKey = key, RemoteDir = "live files", BackupDir = Path.Combine(dir, "backups"), KeepBackups = 5 };
                var f = McFiles.For(s);
                if (f is not McSshFiles) return "the factory did not return the SSH file store";

                // ---- the file store itself, including a folder with a space and awkward characters
                var tricky = "it's $HOME `x` ünï.txt";
                f.WriteText("notes/" + tricky, "line1\nline2 with 'single' and \"double\" and $VAR and `tick` and ü\n");
                if (f.ReadText("notes/" + tricky) != "line1\nline2 with 'single' and \"double\" and $VAR and `tick` and ü\n") return "text did not round trip through the host";
                if (f.ReadText("nothing-here.txt") != null) return "a missing file read as something";
                if (!f.Exists("notes") || f.Exists("nope")) return "exists";
                var many = f.ReadMany(new[] { "notes/" + tricky, "missing.json" });
                if (many.Count != 1) return "ReadMany returned " + many.Count;
                var ls = f.List("notes");
                if (ls.Count != 1 || ls[0].Name != tricky || ls[0].Size <= 0) return "list: " + string.Join(",", ls.Select(e => e.Name));
                f.Copy("notes", "notes copy"); f.Move("notes copy", "moved/notes copy");
                if (!f.Exists("moved/notes copy/" + tricky)) return "copy or move";
                var local = Path.Combine(dir, "local.bin");
                File.WriteAllBytes(local, Enumerable.Range(0, 70000).Select(i => (byte)(i * 31)).ToArray());
                f.Upload(local, "bin/data file.bin");
                var back = Path.Combine(dir, "back", "data.bin");
                f.DownloadFile("bin/data file.bin", back);
                if (!File.ReadAllBytes(back).SequenceEqual(File.ReadAllBytes(local))) return "a binary file changed on the way";
                var tree = Path.Combine(dir, "tree"); Directory.CreateDirectory(Path.Combine(tree, "sub")); File.WriteAllText(Path.Combine(tree, "sub", "a.txt"), "A"); File.WriteAllText(Path.Combine(tree, "b.txt"), "B");
                f.UploadDir(tree, "up/tree");
                var got = Path.Combine(dir, "got"); f.DownloadDir("up/tree", got);
                if (File.ReadAllText(Path.Combine(got, "tree", "sub", "a.txt")) != "A") return "a folder did not round trip";
                try { f.UploadDir(tree, "up/tree"); return "an existing folder was overwritten"; } catch (IOException) { }
                if (f.Size("bin") < 69000) return "size " + f.Size("bin");
                var bk = f.BackUpFolder("up");
                if (bk == null || !f.Exists(bk + "/tree/b.txt")) return "folder backup";
                f.Delete("moved");
                if (f.Exists("moved")) return "delete";
                try { f.ReadText("../escape"); return "a path outside the folder was accepted"; } catch (ArgumentException) { }
                Say("file store: text with quotes and unicode, binary and folder transfers, copy, move, size, backup, delete");

                // ---- players while the server is stopped
                foreach (var (file, text) in new[] { ("usercache.json", "[ { \"name\": \"Ben\", \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000e002\", \"expiresOn\": \"2027-01-01 00:00:00 +0000\" } ]") }) f.WriteText(file, text);
                await McPlayers.ApplyAsync(s, false, McPlayers.Change.Op, "Ben");
                await McPlayers.ApplyAsync(s, false, McPlayers.Change.WhitelistAdd, "Ava");
                await McPlayers.ApplyAsync(s, false, McPlayers.Change.Ban, "Griefer_1", "griefing");
                var players = McPlayers.Read(f);
                if (!players.Any(p => p.Name == "Ben" && p.Operator && p.Uuid.EndsWith("e002")) || !players.Any(p => p.Name == "Ava" && p.Whitelisted) || !players.Any(p => p.Name == "Griefer_1" && p.Banned && p.BanReason == "griefing")) return "players on the host: " + string.Join(", ", players.Select(p => p.Name));
                var bulk = await McBulk.RunAsync(s, false, BulkVerb.AllowAdd, new[] { "Cleo", "Dan" }, new BulkArgs());
                if (bulk.Any(r => !r.Ok) || !McPlayers.Read(f).Any(p => p.Name == "Dan" && p.Whitelisted)) return "a bulk allow list change over SSH";
                Say("players: op, whitelist, ban and a two-player bulk change written to the host's files");

                // ---- plugins: list, switch, install, remove
                var jar = Path.Combine(dir, "Probe-1.jar");
                using (var z = ZipFile.Open(jar, ZipArchiveMode.Create)) { using var w = new StreamWriter(z.CreateEntry("plugin.yml").Open()); w.Write("name: Probe\nversion: 1"); }
                McContent.Install(f, "plugins", jar);
                var items = McContent.List(f, "plugins");
                if (items.Count != 1 || !items[0].Enabled || items[0].Name != "Probe-1") return "plugin list: " + items.Count;
                McContent.SetEnabled(items[0], false);
                if (!f.Exists("plugins/Probe-1.jar.disabled") || f.Exists("plugins/Probe-1.jar")) return "switch off on the host";
                McContent.SetEnabled(McContent.List(f, "plugins")[0], true);
                if (!f.Exists("plugins/Probe-1.jar")) return "switch on on the host";
                if (!f.List("plugins-backups").Any(e => e.IsDir)) return "no dated backup on the host";
                McContent.Remove(McContent.List(f, "plugins")[0]);
                if (f.Exists("plugins/Probe-1.jar")) return "remove on the host";
                Say("plugins: install by scp, switch off and on, remove, each after a dated backup on the host");

                // ---- world, backup and restore
                f.WriteText("server.properties", "level-name=live world\n");
                f.WriteText("live world/level.dat", "lvl");
                f.WriteText("live world/region/r.0.0.mca", "terrain A");
                f.WriteText("live world_nether/level.dat", "nether");
                var dims = McRemote.Dimensions(f, McRemote.LevelName(f, false));
                if (dims.Count != 2 || dims[0].Name != "live world" || dims[1].Kind != "Nether" || dims[0].Size <= 0) return "dimensions: " + string.Join(",", dims.Select(d => d.Name));
                var zip = Path.Combine(dir, "world.zip");
                McRemote.DownloadZip(f, "live world", zip);
                using (var z = ZipFile.OpenRead(zip)) if (z.GetEntry("live world/region/r.0.0.mca") == null) return "the world ZIP has no region file";
                var b = await McBackups.BackUpAsync(s, false, "By hand", "live ssh");
                if (!b.Contents.SequenceEqual(new[] { "live world", "live world_nether", "plugins" }) && !b.Contents.Contains("live world")) return "backup holds " + string.Join(",", b.Contents);
                Say($"world backed up to this computer ({Ui_Bytes(b.Size)}) holding {string.Join(", ", b.Contents)}");
                f.WriteText("live world/region/r.0.0.mca", "terrain B"); f.WriteText("live world/extra.txt", "x");
                var safety = await Task.Run(() => McRemote.Restore(s, f, "live world", b));
                if (f.ReadText("live world/region/r.0.0.mca") != "terrain A" || f.Exists("live world/extra.txt")) return "the restore did not bring back the old world";
                if (safety == null) return "no safety backup";
                if (f.List("").Any(e => e.Name.Contains(".restore-"))) return "a moved-aside folder was left on the host";
                Say("restore replaced the host's folders and kept the old world as a safety backup");

                // ---- import, replace and reset of a world over SSH. The worlds are built here (a gzip NBT level.dat holding a seed,
                // binary region files), not generated by a server; the point is that every byte survives scp, the swap and the dated copies.
                string BuildWorld(string root, long seed, int regionKb, string netherMarker)
                {
                    Directory.CreateDirectory(Path.Combine(root, "region"));
                    var nbt = new List<byte> { 10, 0, 0, 10, 0, 4, (byte)'D', (byte)'a', (byte)'t', (byte)'a', 4, 0, 4, (byte)'s', (byte)'e', (byte)'e', (byte)'d' };
                    for (int i = 7; i >= 0; i--) nbt.Add((byte)(seed >> (8 * i)));
                    nbt.Add(0); nbt.Add(0);
                    using (var fs = File.Create(Path.Combine(root, "level.dat"))) using (var gz = new GZipStream(fs, CompressionLevel.Optimal)) gz.Write(nbt.ToArray());
                    var rnd = new Random((int)(seed % 100000));
                    var region = new byte[regionKb * 1024]; rnd.NextBytes(region);
                    File.WriteAllBytes(Path.Combine(root, "region", "r.0.0.mca"), region);
                    Directory.CreateDirectory(Path.Combine(root, "DIM-1", "region"));
                    File.WriteAllText(Path.Combine(root, "DIM-1", "region", "r.-1.0.mca"), netherMarker);
                    return root;
                }
                var wsrc = Path.Combine(dir, "wsrc");
                var folderWorld = BuildWorld(Path.Combine(wsrc, "Imported Folder"), 123456789, 2048, "nether-a");
                var zipWorld = BuildWorld(Path.Combine(wsrc, "zip-src", "My Zip World"), 987654321, 1536, "nether-b");
                var worldZip = Path.Combine(wsrc, "world.zip"); ZipFile.CreateFromDirectory(Path.Combine(wsrc, "zip-src"), worldZip);
                bool SameBytes(string rel, string localFile) { var tmp = Path.Combine(dir, "readback", Guid.NewGuid().ToString("N")[..6]); f.DownloadFile(rel, tmp); return File.ReadAllBytes(tmp).SequenceEqual(File.ReadAllBytes(localFile)); }
                void NoLeftovers() { if (f.List("").Any(e => e.Name.Contains(".import-") || e.Name.Contains(".replaced-") || e.Name.Contains(".restore-"))) throw new InvalidOperationException("a staging or moved-aside folder was left on the host"); }
                var wsw = Stopwatch.StartNew();
                try { McRemoteWorlds.Import(f, folderWorld, "live world", false); return "an import over an existing world was not refused"; } catch (IOException) { }
                var imp = McRemoteWorlds.Import(f, folderWorld, "live world", true);
                if (imp.Backups.Count != 2 || !imp.Backups.All(x => f.Exists(x + "/level.dat"))) return "dated copies on the host: " + string.Join(",", imp.Backups);
                if (!SameBytes("live world/level.dat", Path.Combine(folderWorld, "level.dat")) || !SameBytes("live world/region/r.0.0.mca", Path.Combine(folderWorld, "region", "r.0.0.mca"))) return "the imported level.dat or region file differs on the host";
                if (f.ReadText("live world/DIM-1/region/r.-1.0.mca") != "nether-a" || f.Exists("live world_nether")) return "the imported world's own Nether did not replace the old one";
                if (McRemote.Seed(f, "live world") != 123456789) return "the seed read back from the host's level.dat is " + McRemote.Seed(f, "live world");
                NoLeftovers();
                Say($"world import: a folder replaced the world after dated copies of {imp.Backups.Count} folders; level.dat and a 2 MB region file are byte-identical on the host and the seed reads back");
                // a step that fails after the old folders are aside puts them back
                McRemoteWorlds.AfterAsideForTest = () => throw new IOException("injected");
                try { McRemoteWorlds.Import(f, worldZip, "live world", true); return "the injected failure was not reported"; }
                catch (IOException ex) when (ex.Message == "injected") { }
                finally { McRemoteWorlds.AfterAsideForTest = null; }
                if (!SameBytes("live world/region/r.0.0.mca", Path.Combine(folderWorld, "region", "r.0.0.mca"))) return "the old world was not put back after a failure";
                NoLeftovers();
                var rep = McRemoteWorlds.Import(f, worldZip, "live world", true);
                if (!SameBytes("live world/region/r.0.0.mca", Path.Combine(zipWorld, "region", "r.0.0.mca")) || McRemote.Seed(f, "live world") != 987654321) return "the ZIP did not replace the world";
                if (f.List("live world-backups").Count(e => e.IsDir) != 3) return "dated copies of the world: " + f.List("live world-backups").Count(e => e.IsDir);
                NoLeftovers();
                var reset = McRemoteWorlds.Reset(f, "live world");
                if (f.Exists("live world") || reset.Backups.Count != 1 || !f.Exists(reset.Backups[0] + "/level.dat")) return "reset left the world or took no copy";
                NoLeftovers();
                Say($"world replace by ZIP, a failure that restored the old world, and reset, each with a dated copy on the host ({wsw.Elapsed.TotalSeconds:0.0} s for the whole world group)");

                // ---- the Linux Bedrock server unpacks on the host
                var bds = new ServerDef { Name = "Live SSH Bedrock", Flavour = Flavour.Bedrock, Target = Target.SshProcess, SshHost = host, SshKey = key, Port = 19132, RemoteDir = "live bedrock", EulaAccepted = true };
                await ServerManager.CreateAsync(bds, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
                ServerManager.Servers.Remove(bds); ServerManager.Save();
                var bf = McFiles.For(bds);
                if (!bf.Exists("bedrock_server") || !bf.Exists("behavior_packs")) return "the Linux Bedrock server did not unpack on the host";
                var props = McProperties.Parse(bf.ReadText("server.properties") ?? "").ToDictionary();
                if (props.GetValueOrDefault("max-players") != "7" || props.GetValueOrDefault("server-port") != "19132") return "the Bedrock settings were not written";
                Say($"Linux Bedrock {bds.Version} downloaded and unpacked on the host, with its settings (it was not started: the host's Alpine has no glibc)");
                return null;
            }
            finally
            {
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                Docker($"rm -f {name}", 60000);
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static string Ui_Bytes(long n) => n < 1024 ? n + " B" : n < 1048576 ? (n / 1024.0).ToString("0.0") + " KB" : (n / 1048576.0).ToString("0.0") + " MB";

        private static async Task<string> BedrockFiles()
        {
            int port = 19132;
            while (!ServerManager.UdpPortFree(port) || !ServerManager.UdpPortFree(port + 1)) port += 2;
            var s = new ServerDef { Name = "Live Bedrock files", Flavour = Flavour.Bedrock, Target = Target.LocalProcess, Port = port, EulaAccepted = true };
            var sw = Stopwatch.StartNew();
            await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
            var live = McLive.For(s);
            var tmp = Path.Combine(Settings.DataDir, "bedrock-files-live-" + Stamp());
            try
            {
                var files = McFiles.For(s);
                Say($"installed Bedrock {s.Version} in {sw.Elapsed.TotalSeconds:0} s");
                // the first start makes the world (an add-on can only be switched on for a world that exists)
                async Task<bool> StartAndWait()
                {
                    int from = live.Lines().Count; live.MarkStarting();
                    await ServerManager.StartAsync(s, _ => { });
                    var until = DateTime.Now.AddMinutes(3);
                    while (DateTime.Now < until && !live.Lines().Skip(from).Any(l => l.Message.StartsWith("Server started.", StringComparison.Ordinal)))
                    {
                        if (ServerManager.ProcessOf(s) == null) return false;
                        await Task.Delay(500);
                    }
                    return live.Lines().Skip(from).Any(l => l.Message.StartsWith("Server started.", StringComparison.Ordinal));
                }
                if (!await StartAndWait()) return "the first start did not finish: " + Trim(string.Join(" | ", live.Lines().TakeLast(5).Select(l => l.Raw)));
                await ServerManager.StopAsync(s);
                var level = McRemote.LevelName(files, true);
                if (!files.Exists("worlds/" + level)) return "the first start made no world folder";
                Say($"the first start made the world \"{level}\"");

                // an add-on and an allow list entry, written by the app while the server is stopped
                var bpId = Guid.NewGuid().ToString(); var brokenId = Guid.NewGuid().ToString();
                var addon = Path.Combine(tmp, "Live.mcaddon");
                Directory.CreateDirectory(tmp);
                SelfTestZip(addon, ("Live BP/manifest.json", "{\"format_version\":2,\"header\":{\"name\":\"Live add-on\",\"description\":\"live check\",\"uuid\":\"" + bpId + "\",\"version\":[1,0,0],\"min_engine_version\":[1,20,0]},\"modules\":[{\"type\":\"data\",\"uuid\":\"" + Guid.NewGuid() + "\",\"version\":[1,0,0]}]}"),
                    ("Broken BP/manifest.json", "{\"format_version\":2,\"header\":{\"name\":\"Broken add-on\",\"description\":\"needs a pack that is not there\",\"uuid\":\"" + brokenId + "\",\"version\":[1,0,0],\"min_engine_version\":[1,20,0]},\"modules\":[{\"type\":\"data\",\"uuid\":\"" + Guid.NewGuid() + "\",\"version\":[1,0,0]}],\"dependencies\":[{\"uuid\":\"" + Guid.NewGuid() + "\",\"version\":[1,0,0]}]}"));
                var res = McBedrockAddons.Install(files, addon, level, true);
                if (res.Installed.Count != 2 || res.NeedsWorld) return "install: " + string.Join(",", res.Installed);
                McBedrockPlayers.ApplyToFiles(files, BedrockChange.AllowAdd, "LiveCheckPlayer", "2535400000000123");
                McBedrockPlayers.ApplyToFiles(files, BedrockChange.Operator, "LiveCheckPlayer");
                var props = McProperties.Load(Path.Combine(s.Folder, "server.properties")); props.Set("allow-list", "true"); props.Set("content-log-console-output-enabled", "true"); props.Save(Path.Combine(s.Folder, "server.properties"));
                Say("add-on installed and switched on, LiveCheckPlayer allowed and made operator, all through the files");

                var mark = live.Lines().Count;
                if (!await StartAndWait()) return "the second start did not finish: " + Trim(string.Join(" | ", live.Lines().TakeLast(5).Select(l => l.Raw)));
                var startup = live.Lines().Skip(mark).Select(l => l.Raw).ToList();
                var packLine = startup.LastOrDefault(l => l.Contains("Pack Stack") && l.Contains(bpId));
                if (packLine == null) return "the server log has no Pack Stack line for the add-on the app enabled. Pack lines: " + Trim(string.Join(" | ", startup.Where(l => l.ToLowerInvariant().Contains("pack")).Take(8)));
                Say("the server loaded the add-on from the world pack list the app wrote: " + Trim(packLine));
                await ServerManager.CommandAsync(s, "allowlist list");
                for (int i = 0; i < 20 && !live.Lines().Any(l => l.Raw.Contains("LiveCheckPlayer")); i++) await Task.Delay(500);
                var listed = live.Lines().LastOrDefault(l => l.Raw.Contains("LiveCheckPlayer"));
                if (listed == null) return "allowlist list never named the entry the app wrote";
                Say("allowlist list answered with the entry: " + Trim(listed.Raw));
                // the console path: the app's own command for a name with a space
                var add = McBedrockPlayers.Command(BedrockChange.AllowAdd, "Console Player");
                await ServerManager.CommandAsync(s, add);
                await Task.Delay(1500);
                if (!(files.ReadText("allowlist.json") ?? "").Contains("Console Player")) return "the console command " + add + " did not reach allowlist.json";
                Say("the console command `" + add + "` wrote allowlist.json");
                return null;
            }
            finally
            {
                try { await ServerManager.StopAsync(s); } catch { }
                ServerManager.Servers.Remove(s); ServerManager.Save();
                try { Directory.Delete(s.Folder, true); } catch { }
                try { Directory.Delete(tmp, true); } catch { }
            }
        }

        private static void SelfTestZip(string path, params (string Name, string Text)[] entries)
        {
            using var z = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var (n, t) in entries) { using var w = new StreamWriter(z.CreateEntry(n).Open()); w.Write(t); }
        }

        private static async Task<string> BedrockDocker()
        {
            NeedDocker();
            int port = 19232; while (!ServerManager.UdpPortFree(port)) port++;
            var s = new ServerDef { Name = "Live Bedrock Docker", Flavour = Flavour.Bedrock, Target = Target.LocalDocker, Port = port, EulaAccepted = true, Version = "latest" };
            var sw = Stopwatch.StartNew();
            try
            {
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
                Say($"container created in {sw.Elapsed.TotalSeconds:0} s");
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(8);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 300)).Contains("Server started.")) await Task.Delay(3000);
                if (!log.Contains("Server started.")) return "no \"Server started.\" within eight minutes: " + Trim(ServerManager.ActionLine(log));
                Say($"\"Server started.\" after {sw.Elapsed.TotalSeconds:0} s (the image downloads the server on its first start)");
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running && st.State != McRunState.Starting) return "state " + st.State;
                var files = McFiles.For(s);
                if (!files.Exists("worlds")) return "no worlds folder in the data folder";
                await ServerManager.CommandAsync(s, McBedrockPlayers.Command(BedrockChange.AllowAdd, "Docker Player"));
                await ServerManager.CommandAsync(s, "list");
                string answer = null;
                for (int i = 0; i < 20 && answer == null; i++) { await Task.Delay(1000); answer = (await ServerManager.LogTailAsync(s, 60)).Split('\n').LastOrDefault(l => l.Contains("players online") || l.Contains("There are ")); }
                if (answer == null) return "list gave no answer in the log";
                Say("list: " + Trim(answer));
                if (!(files.ReadText("allowlist.json") ?? "").Contains("Docker Player")) return "send-command allowlist add did not reach allowlist.json";
                Say("send-command wrote allowlist.json for a name with a space");
                await ServerManager.StopAsync(s);
                var after = await McState.ProbeAsync(s);
                if (after.State != McRunState.Stopped) return "after stopping the state is " + after.State;
                Say("stopped");
                return null;
            }
            finally
            {
                try { ServerManager.DeleteContainer(s); } catch { }
                try { Directory.Delete(s.Folder, true); } catch { }
            }
        }
    }
}
