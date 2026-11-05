using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    // The second half of the Minecraft lane live checks (MT_LIVE_ONLY=minecraft-lane, MT_LIVE_LANE=updates,fabric,sshdocker,gen):
    // update checks against the real software APIs, a Fabric server with a mod command read from help, the builder's replies over
    // Docker on an SSH host, and real world generation read back from the files it wrote.
    public static partial class LiveCheck
    {
        private static async Task McLane2(List<Result> results, string[] only)
        {
            if (only.Contains("updates")) await Check(results, "minecraft: update checks against the real Paper, Purpur, Fabric, Forge and NeoForge APIs", UpdateApis);
            if (only.Contains("fabric")) await Check(results, "minecraft: a real Fabric server in Docker with a mod, whose command is read from help and built into a form", FabricLane);
            if (only.Contains("sshdocker")) await Check(results, "minecraft: the command builder's lines run and answer on the SSH Docker target", SshDockerLane);
            if (only.Contains("gen")) await Check(results, "minecraft: world generation writes Nether and End regions, game rules, a custom superflat world in a ZIP, and a single biome, each read back from the files", GenLane);
        }

        // ------------------------------------------------------------------ update checks

        private static async Task<string> UpdateApis()
        {
            foreach (var f in new[] { Flavour.Paper, Flavour.Purpur, Flavour.Fabric, Flavour.Forge, Flavour.NeoForge })
            {
                var sw = Stopwatch.StartNew();
                var list = await McSoftware.BuildsAsync(f, Version);
                if (list.Unavailable != null) return $"{f}: the build list could not be read: {list.Unavailable}";
                if (list.Builds.Count == 0) return $"{f}: the API lists no build for {Version}";
                var latest = list.Latest; var oldest = list.Builds[^1];
                var s = new ServerDef { Name = "update " + f, Version = Version, Flavour = f, Build = latest.Id };
                var (none, why) = await McSoftware.UpdateAsync(s);
                if (why != null || none != null) return $"{f}: the newest build {latest.Id} was reported as {(none != null ? "having a newer one, " + none.Id : "unknown: " + why)}";
                if (list.Builds.Count < 2) { Say($"{f}: {latest.Id} is the only build listed ({sw.Elapsed.TotalSeconds:0.0} s)"); continue; }
                s.Build = oldest.Id;
                var (newer, why2) = await McSoftware.UpdateAsync(s);
                if (newer == null || newer.Id != latest.Id) return $"{f}: build {oldest.Id} was told {(newer == null ? "nothing newer (" + why2 + ")" : newer.Id)} instead of {latest.Id}";
                Say($"{f}: {list.Builds.Count} builds, newest {latest.Id} is current, {oldest.Id} is told {newer.Id} is newer ({sw.Elapsed.TotalSeconds:0.0} s)");
            }
            return null;
        }

        // ------------------------------------------------------------------ Fabric with a mod

        private static async Task<string> FabricLane()
        {
            NeedDocker();
            int port = 25565; while (!ServerManager.PortFree(port) || ServerManager.Servers.Any(x => x.Port == port)) port++;
            var s = new ServerDef { Name = "Live Fabric", Version = Version, Flavour = Flavour.Fabric, Target = Target.LocalDocker, Port = port, MemoryMb = 2048, EulaAccepted = true };
            var (tree, unavailable) = await McCommandTree.LoadAsync(Version, null, true);
            if (tree == null) return "the command list could not be read: " + unavailable;
            var sw = Stopwatch.StartNew();
            await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
            try
            {
                Say($"Fabric loader {s.Build} created as {s.ContainerName} in {sw.Elapsed.TotalSeconds:0} s");
                var mods = McContent.FolderOf(s);
                if (mods == null) return "the server has no mods folder recorded";
                Directory.CreateDirectory(mods);
                // spark needs Fabric API, as most Fabric mods do
                var fapi = (await McModrinth.SearchAsync("Fabric API", Flavour.Fabric, Version)).FirstOrDefault(h => h.Title.Equals("Fabric API", StringComparison.OrdinalIgnoreCase));
                if (fapi == null) throw new Skip("Modrinth lists no Fabric API for " + Version);
                await McModrinth.InstallAsync(fapi, s, null);
                var hits = await McModrinth.SearchAsync("spark", Flavour.Fabric, Version);
                var spark = hits.FirstOrDefault(h => h.Title.Equals("spark", StringComparison.OrdinalIgnoreCase)) ?? hits.FirstOrDefault();
                if (spark == null) throw new Skip("Modrinth lists no Fabric mod named spark for " + Version);
                await McModrinth.InstallAsync(spark, s, null);
                Say("installed the mod " + spark.Title + ": " + string.Join(", ", Directory.GetFiles(mods).Select(Path.GetFileName)));
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(10);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 600)).Contains("Done (") && !log.Contains("\tDone\n") && !log.TrimEnd().EndsWith("\tDone")) await Task.Delay(3000);
                if (!log.Contains("Done (")) return "the Fabric server did not become ready; its log ends: " + string.Join(" | ", log.Split('\n').Where(l => l.Trim().Length > 0).TakeLast(14).Select(l => l.Length > 220 ? l[..220] : l));
                Say($"Fabric ready after {sw.Elapsed.TotalSeconds:0} s");
                var read = await McLiveCommands.ReadAsync(s, tree);
                Say($"help printed {read.Raw.Count} lines, the first: {Trim(string.Join(" | ", read.Raw.Take(4)))}");
                Say($"help gave {read.Commands.Count} commands the jar does not list: {string.Join(", ", read.Commands.Take(8).Select(c => c.Name + (c.Source != "server" ? "@" + c.Source : "")))}");
                var sparkCmd = read.Commands.FirstOrDefault(c => c.Name.Equals("spark", StringComparison.OrdinalIgnoreCase));
                if (sparkCmd == null) return "the mod's /spark command is not among the commands read from help" + (read.Problem != null ? " (" + read.Problem + ")" : "");
                var task = McLiveCommands.ToTask(sparkCmd);
                var form = McCommandTasks.Build(task, task.Synthetic, new Dictionary<string, string>(), null);
                Say($"form for /spark from usage \"{Trim(sparkCmd.Usage)}\": template '{task.Template}', {form.Fields.Count} fields");
                var answer = await McCommandRun.SendAsync(s, "spark tps", TimeSpan.FromSeconds(8));
                Say("spark tps → " + Trim(answer));
                return null;
            }
            finally
            {
                try { await ServerManager.StopAsync(s); } catch { }
                try { ServerManager.DeleteContainer(s); } catch { }
                ServerManager.Servers.Remove(s); ServerManager.Save();
                try { Directory.Delete(s.Folder, true); } catch { }
            }
        }

        // ------------------------------------------------------------------ the builder over Docker on an SSH host

        private static async Task<string> SshDockerLane()
        {
            NeedDocker();
            var dir = Path.Combine(Settings.DataDir, "sshdind-lane-" + Stamp());
            Directory.CreateDirectory(dir);
            var (key, pub) = KeyPair(dir);
            int sshPort = 22520; while (!ServerManager.PortFree(sshPort)) sshPort++;
            var name = "mt-live-sshdindlane-" + Stamp();
            ServerDef s = null;
            try
            {
                var run = Docker($"run -d --privileged --name {name} -p 127.0.0.1:{sshPort}:2222 docker:dind", 600000);
                if (run.Code != 0) return "docker run failed: " + ServerManager.ActionLine(run.Out);
                var pw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                for (int i = 0; i < 60 && Docker($"exec {name} docker info --format \"{{{{.ServerVersion}}}}\"", 15000).Code != 0; i++) await Task.Delay(1000);
                var user = ThrowawayUser();
                var setup = Docker($"exec {name} sh -c \"apk add --no-cache openssh >/dev/null && ssh-keygen -A >/dev/null && adduser -D {user} && echo {user}:{pw} | chpasswd && addgroup {user} $(stat -c %G /var/run/docker.sock) && mkdir -p /home/{user}/.ssh && echo '{pub}' > /home/{user}/.ssh/authorized_keys && chown -R {user} /home/{user}/.ssh && chmod 700 /home/{user}/.ssh && chmod 600 /home/{user}/.ssh/authorized_keys && /usr/sbin/sshd -p 2222\"", 600000);
                if (setup.Code != 0) return "setting up SSH in the Docker host failed: " + ServerManager.ActionLine(setup.Out);
                var host = SshHostSpec(user, sshPort);
                var approve = await ApproveHost(host);
                if (approve != null) return approve;
                var (tree, unavailable) = await McCommandTree.LoadAsync(Version, null, true);
                if (tree == null) return "the command list could not be read: " + unavailable;
                var (reg, _) = await McRegistries.LoadAsync(Version);
                s = new ServerDef { Name = "Live lane SSH Docker", Version = Version, Flavour = Flavour.Vanilla, Target = Target.SshDocker, SshHost = host, SshKey = key, Port = 25565, MemoryMb = 1024, EulaAccepted = true, RemoteDir = "live-lane" };
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(10);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 400)).Contains("Done (")) await Task.Delay(3000);
                if (!log.Contains("Done (")) return "no Done line within ten minutes: " + Trim(ServerManager.ActionLine(log));
                await McState.ProbeAsync(s);
                var bad = await BuilderLines(s, tree, reg);
                if (bad != null) return bad;
                await ServerManager.StopAsync(s);
                return null;
            }
            finally
            {
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                Docker($"rm -f -v {name}", 120000);
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ------------------------------------------------------------------ world generation, read back from the files

        private static byte[] Gunzip(string file)
        {
            using var fs = File.OpenRead(file); using var gz = new GZipStream(fs, CompressionMode.Decompress); using var ms = new MemoryStream();
            gz.CopyTo(ms); return ms.ToArray();
        }

        private static bool Has(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }

        /// <summary>A game rule as level.dat stores it: a string tag named after the rule, holding the value as text.</summary>
        private static bool RuleStored(byte[] levelDat, string rule, string value)
        {
            var b = new List<byte> { 8, 0, (byte)rule.Length };
            b.AddRange(Encoding.ASCII.GetBytes(rule));
            b.Add(0); b.Add((byte)value.Length);
            b.AddRange(Encoding.ASCII.GetBytes(value));
            return Has(levelDat, b.ToArray());
        }

        /// <summary>Which of the needles appear in the decompressed chunk data of the region files in a folder (block and biome ids sit in the chunk palettes).</summary>
        private static HashSet<string> InChunks(string regionFolder, IEnumerable<string> needles, int maxChunks = 400, bool fullOnly = false)
        {
            var found = new HashSet<string>();
            var want = needles.ToDictionary(n => n, n => Encoding.ASCII.GetBytes(n));
            int seen = 0;
            foreach (var f in Directory.GetFiles(regionFolder, "r.*.mca"))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length < 8192) continue;
                for (int i = 0; i < 1024 && seen < maxChunks; i++)
                {
                    int sector = (bytes[4 * i] << 16) | (bytes[4 * i + 1] << 8) | bytes[4 * i + 2];
                    if (sector == 0) continue;
                    long pos = sector * 4096L;
                    if (pos + 5 > bytes.Length) continue;
                    int len = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                    int comp = bytes[pos + 4];
                    if (len < 2 || pos + 4 + len > bytes.Length) continue;
                    byte[] data;
                    try
                    {
                        using var src = new MemoryStream(bytes, (int)pos + 5, len - 1);
                        using Stream d = comp == 2 ? new ZLibStream(src, CompressionMode.Decompress) : comp == 1 ? new GZipStream(src, CompressionMode.Decompress) : src;
                        using var ms = new MemoryStream(); d.CopyTo(ms); data = ms.ToArray();
                    }
                    catch { continue; }
                    // a chunk saved part way through generation (a neighbour of the area) carries placeholder palettes; only finished chunks say what the world holds
                    if (fullOnly && !Has(data, Encoding.ASCII.GetBytes("minecraft:full"))) continue;
                    seen++;
                    foreach (var (n, nb) in want) if (!found.Contains(n) && Has(data, nb)) found.Add(n);
                }
            }
            return found;
        }

        /// <summary>The text around the first place a needle appears in any chunk, for explaining a surprise.</summary>
        private static string ChunkContext(string regionFolder, string needle)
        {
            var nb = Encoding.ASCII.GetBytes(needle);
            foreach (var f in Directory.GetFiles(regionFolder, "r.*.mca"))
            {
                var bytes = File.ReadAllBytes(f);
                for (int i = 0; i < 1024 && bytes.Length >= 8192; i++)
                {
                    int sector = (bytes[4 * i] << 16) | (bytes[4 * i + 1] << 8) | bytes[4 * i + 2];
                    if (sector == 0) continue;
                    long pos = sector * 4096L;
                    int len = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                    if (len < 2 || pos + 4 + len > bytes.Length) continue;
                    try
                    {
                        using var src = new MemoryStream(bytes, (int)pos + 5, len - 1);
                        using var d = new ZLibStream(src, CompressionMode.Decompress);
                        using var ms = new MemoryStream(); d.CopyTo(ms);
                        var data = ms.ToArray();
                        for (int k = 0; k + nb.Length <= data.Length; k++)
                        {
                            int j = 0; while (j < nb.Length && data[k + j] == nb[j]) j++;
                            if (j != nb.Length) continue;
                            var from = Math.Max(0, k - 70); var to = Math.Min(data.Length, k + nb.Length + 30);
                            return $"chunk {i % 32},{i / 32} of {Path.GetFileName(f)}: …" + new string(data.Skip(from).Take(to - from).Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray()) + "…";
                        }
                    }
                    catch { }
                }
            }
            return "not found";
        }

        private static List<string> RegionFiles(string folder) => Directory.Exists(folder) ? Directory.GetFiles(folder, "r.*.mca").Where(f => new FileInfo(f).Length > 8192).ToList() : new List<string>();

        private static async Task<(string Path, string Note, string Fail)> Generate(WorldGenOptions o)
        {
            var sw = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            try
            {
                var written = await WorldGenerator.GenerateAsync(o, (t, f) => { }, cts.Token);
                var parts = written.Split('\n');
                Say($"{o.Type} world generated in {sw.Elapsed.TotalSeconds:0} s");
                return (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : null, null);
            }
            catch (Exception ex) { return (null, null, ex.GetType().Name + ": " + ex.Message); }
        }

        private static async Task<string> GenLane()
        {
            var root = Path.Combine(Settings.DataDir, "gen-lane-" + Stamp());
            Directory.CreateDirectory(root);
            try
            {
                // ---- a normal world with the Nether and the End pre-generated, and two game rules
                var a = new WorldGenOptions { Version = Version, Seed = "lane", RadiusChunks = 2, PreGenNether = true, PreGenEnd = true, EulaAccepted = true, Destination = root, WorldName = "normal", BorderToSize = true };
                a.GameRules["keepInventory"] = "true"; a.GameRules["randomTickSpeed"] = "7";
                var (pa, na, fa) = await Generate(a);
                if (fa != null) return "the normal world failed: " + fa;
                if (na != null) return "the normal world said: " + na;
                var overworld = RegionFiles(Path.Combine(pa, "region")); var nether = RegionFiles(Path.Combine(pa, "DIM-1", "region")); var end = RegionFiles(Path.Combine(pa, "DIM1", "region"));
                if (overworld.Count == 0 || nether.Count == 0 || end.Count == 0) return $"region files: overworld {overworld.Count}, Nether {nether.Count}, End {end.Count}";
                Say($"regions: overworld {overworld.Count}, Nether {nether.Count}, End {end.Count} ({(overworld.Concat(nether).Concat(end).Sum(f => new FileInfo(f).Length)) / 1024} KB)");
                var level = Gunzip(Path.Combine(pa, "level.dat"));
                if (!RuleStored(level, "keepInventory", "true")) return "level.dat does not hold keepInventory=true";
                if (!RuleStored(level, "randomTickSpeed", "7")) return "level.dat does not hold randomTickSpeed=7";
                Say("level.dat holds keepInventory=true and randomTickSpeed=7 (the defaults are false and 3)");

                // ---- a normal world, Nether and End, written as a ZIP
                var z = new WorldGenOptions { Version = Version, Seed = "lane", RadiusChunks = 2, PreGenNether = true, PreGenEnd = true, EulaAccepted = true, Destination = root, WorldName = "normalzip", AsZip = true };
                var (pz, nz, fz) = await Generate(z);
                if (fz != null) return "the normal ZIP world failed: " + fz;
                if (nz != null) return "the normal ZIP world said: " + nz;
                if (!pz.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !File.Exists(pz)) return "the normal ZIP was not written: " + pz;
                var zdir = Path.Combine(root, "normalzip-unzipped"); ZipFile.ExtractToDirectory(pz, zdir);
                var zr = Directory.GetDirectories(zdir, "region", SearchOption.AllDirectories);
                int zo = zr.Where(d => !d.Contains("DIM")).SelectMany(RegionFiles).Count(), zn = zr.Where(d => d.Contains("DIM-1")).SelectMany(RegionFiles).Count(), ze = zr.Where(d => d.Contains("DIM1")).SelectMany(RegionFiles).Count();
                if (zo == 0 || zn == 0 || ze == 0 || Directory.GetFiles(zdir, "level.dat", SearchOption.AllDirectories).Length == 0) return $"the normal ZIP holds overworld {zo}, Nether {zn}, End {ze} region files (or no level.dat)";
                Say($"normal world ZIP {new FileInfo(pz).Length / 1024} KB: overworld {zo}, Nether {zn}, End {ze} region files and a level.dat");

                // ---- a custom superflat world as a ZIP
                var b = new WorldGenOptions { Version = Version, Seed = "", Type = WorldType.Flat, RadiusChunks = 2, PreGenNether = true, PreGenEnd = true, EulaAccepted = true, Destination = root, WorldName = "flat", AsZip = true, FlatBiome = "minecraft:plains" };
                b.FlatLayers.Add(new FlatLayer { Block = "minecraft:bedrock", Height = 1 });
                b.FlatLayers.Add(new FlatLayer { Block = "minecraft:emerald_block", Height = 2 });
                b.FlatLayers.Add(new FlatLayer { Block = "minecraft:gold_block", Height = 1 });
                var (pb, nb, fb) = await Generate(b);
                if (fb != null) return "the superflat world failed: " + fb;
                if (nb != null) return "the superflat world said: " + nb;
                if (!pb.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !File.Exists(pb)) return "the ZIP was not written: " + pb;
                var unzip = Path.Combine(root, "flat-unzipped");
                ZipFile.ExtractToDirectory(pb, unzip);
                var flatRegion = Directory.GetDirectories(unzip, "region", SearchOption.AllDirectories).FirstOrDefault(d => !d.Contains("DIM"));
                var flatLevel = Directory.GetFiles(unzip, "level.dat", SearchOption.AllDirectories).FirstOrDefault();
                if (flatRegion == null || flatLevel == null) return "the ZIP has no overworld region folder or no level.dat";
                var inFlat = InChunks(flatRegion, new[] { "minecraft:emerald_block", "minecraft:gold_block", "minecraft:bedrock", "minecraft:plains" });
                if (!inFlat.Contains("minecraft:emerald_block") || !inFlat.Contains("minecraft:gold_block")) return "the chunks do not hold the custom layers; found " + string.Join(",", inFlat);
                Say($"ZIP {new FileInfo(pb).Length / 1024} KB, {RegionFiles(flatRegion).Count} region files; the chunks hold {string.Join(", ", inFlat.OrderBy(x => x))}");
                var flatNether = Directory.GetDirectories(unzip, "region", SearchOption.AllDirectories).Where(d => d.Contains("DIM-1")).SelectMany(RegionFiles).Count();
                var flatEnd = Directory.GetDirectories(unzip, "region", SearchOption.AllDirectories).Where(d => d.Contains("DIM1")).SelectMany(RegionFiles).Count();
                if (flatNether == 0 || flatEnd == 0) return $"the superflat ZIP holds Nether {flatNether} and End {flatEnd} region files";
                Say($"the superflat ZIP also holds {flatNether} Nether and {flatEnd} End region files");

                // ---- a single biome world
                var c = new WorldGenOptions { Version = Version, Seed = "lane", Type = WorldType.SingleBiome, Biome = "minecraft:cherry_grove", RadiusChunks = 2, PreGenNether = true, PreGenEnd = true, EulaAccepted = true, Destination = root, WorldName = "biome" };
                var (pc, nc, fc) = await Generate(c);
                if (fc != null) return "the single biome world failed: " + fc;
                if (nc != null) return "the single biome world said: " + nc;
                var biomes = InChunks(Path.Combine(pc, "region"), new[] { "minecraft:cherry_grove", "minecraft:plains", "minecraft:forest", "minecraft:ocean" }, 2000, fullOnly: true);
                if (!biomes.Contains("minecraft:cherry_grove")) return "the chunks do not hold cherry_grove; found " + string.Join(",", biomes);
                if (biomes.Any(x => x != "minecraft:cherry_grove")) return "the chunks hold other biomes too: " + string.Join(",", biomes) + "; plains sits at " + ChunkContext(Path.Combine(pc, "region"), "minecraft:plains");
                var bNether = RegionFiles(Path.Combine(pc, "DIM-1", "region")).Count; var bEnd = RegionFiles(Path.Combine(pc, "DIM1", "region")).Count;
                if (bNether == 0 || bEnd == 0) return $"the single biome world holds Nether {bNether} and End {bEnd} region files";
                Say($"the single biome world holds {bNether} Nether and {bEnd} End region files");
                Say("the finished overworld chunks hold only minecraft:cherry_grove (chunks saved part way, as neighbours of the area, carry a placeholder plains palette and are not counted)");
                return null;
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }
    }
}
