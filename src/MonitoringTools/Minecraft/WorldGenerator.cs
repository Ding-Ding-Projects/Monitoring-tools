using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core;

namespace MonitoringTools.Minecraft
{
    public enum WorldType { Normal, Flat, LargeBiomes, Amplified, SingleBiome }

    public sealed class WorldGenOptions
    {
        public string Version;
        public string Seed = "";
        public WorldType Type = WorldType.Normal;
        public string Biome = "minecraft:plains";
        public bool Structures = true;
        /// <summary>Half the side of the area to pre-generate, in chunks. 50 gives 100 x 100 chunks. 0 skips pre-generation.</summary>
        public int RadiusChunks = 50;
        public bool BorderToSize;
        public bool EulaAccepted;
        public bool AsZip;
        public string Destination = "";
        public string WorldName = "world";
    }

    /// <summary>
    /// Creates Java edition worlds with the real server jar. Nothing is simulated: the jar is run, and
    /// what it writes is what is delivered. Pre-generation uses the server's own forceload command and
    /// is only attempted when the jar itself reports a command that can tell when chunks are loaded.
    /// </summary>
    public static class WorldGenerator
    {
        public const int MaxRadiusChunks = 100;
        public const int ChunksPerBatchSide = 16;   // 16 x 16 = 256 chunks, the most forceload accepts in one command
        // Version boundaries, by release date of the version in Mojang's catalogue.
        private static readonly DateTime DataDrivenSince = new DateTime(2022, 6, 7, 0, 0, 0, DateTimeKind.Utc);   // 1.19: world presets are data
        private static readonly DateTime ForceloadSince = new DateTime(2018, 8, 22, 0, 0, 0, DateTimeKind.Utc);   // 1.13.1: forceload command

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp() { var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0"); return h; }

        private static string CacheRoot { get { var d = Path.Combine(ServerManager.Root, "worldgen-cache"); Directory.CreateDirectory(d); return d; } }

        // -------------------------------------------------------------- capabilities

        public sealed class Support
        {
            public bool DataDriven, Forceload;
            public string SingleBiomeReason, ForceloadReason;
        }

        public static async Task<Support> SupportAsync(string version)
        {
            var v = (await ServerManager.VersionsAsync()).FirstOrDefault(x => x.Id == version);
            var s = new Support();
            if (v == null) { s.SingleBiomeReason = s.ForceloadReason = "That version is not in the official catalogue."; return s; }
            s.DataDriven = v.Released >= DataDrivenSince;
            s.Forceload = v.Released >= ForceloadSince;
            if (!s.DataDriven) s.SingleBiomeReason = "A custom single biome needs data-driven world presets, which arrived with Java 1.19. This version predates them.";
            if (!s.Forceload) s.ForceloadReason = "The forceload command arrived with Java 1.13.1; this version predates it.";
            return s;
        }

        public static string LevelType(WorldType t, bool dataDriven) => t switch
        {
            WorldType.Flat => dataDriven ? "minecraft:flat" : "flat",
            WorldType.LargeBiomes => dataDriven ? "minecraft:large_biomes" : "largeBiomes",
            WorldType.Amplified => dataDriven ? "minecraft:amplified" : "amplified",
            WorldType.SingleBiome => "materialbrowser:generated",
            _ => dataDriven ? "minecraft:normal" : "default"
        };

        public static int BatchCount(int radius)
        {
            if (radius <= 0) return 0;
            int side = (int)Math.Ceiling(radius * 2 / (double)ChunksPerBatchSide);
            return side * side;
        }

        // -------------------------------------------------------------- the real jar

        /// <summary>A server jar for the version, downloaded once into a cache and checked against Mojang's SHA-1.</summary>
        public static async Task<(string Jar, int Java)> EnsureJarAsync(string version, Action<string> report)
        {
            var d = await ServerManager.ResolveAsync(version);
            if (d.Missing != null) throw new InvalidOperationException(d.Missing);
            var dir = Path.Combine(CacheRoot, version);
            Directory.CreateDirectory(dir);
            var jar = Path.Combine(dir, "server.jar");
            if (File.Exists(jar) && Sha1(jar).Equals(d.Sha1, StringComparison.OrdinalIgnoreCase)) return (jar, d.JavaMajor);
            report?.Invoke($"Downloading the {version} server ({d.Size / 1048576.0:0.0} MB)…");
            using (var r = await Http.GetAsync(d.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                r.EnsureSuccessStatusCode();
                await using var s = await r.Content.ReadAsStreamAsync();
                await using var f = File.Create(jar + ".part");
                await s.CopyToAsync(f);
            }
            if (!Sha1(jar + ".part").Equals(d.Sha1, StringComparison.OrdinalIgnoreCase))
            { File.Delete(jar + ".part"); throw new InvalidDataException("The server jar failed its SHA-1 check."); }
            File.Move(jar + ".part", jar, true);
            return (jar, d.JavaMajor);
        }

        private static string Sha1(string path) { using var f = File.OpenRead(path); return Convert.ToHexString(SHA1.HashData(f)); }

        /// <summary>The zip that holds the game data: the nested server jar inside a bundler jar (1.18+), or the jar itself.</summary>
        private static ZipArchive OpenDataZip(string jar)
        {
            var outer = new ZipArchive(File.OpenRead(jar), ZipArchiveMode.Read);
            var nested = outer.Entries.FirstOrDefault(e => e.FullName.StartsWith("META-INF/versions/", StringComparison.Ordinal) && e.FullName.EndsWith(".jar", StringComparison.Ordinal));
            if (nested == null) return outer;
            var ms = new MemoryStream();
            using (var es = nested.Open()) es.CopyTo(ms);
            ms.Position = 0;
            outer.Dispose();
            return new ZipArchive(ms, ZipArchiveMode.Read);
        }

        /// <summary>Every biome id the exact jar contains, read from its data folder. Empty when the jar predates data-driven biomes.</summary>
        public static List<string> Biomes(string jar)
        {
            using var z = OpenDataZip(jar);
            const string prefix = "data/minecraft/worldgen/biome/";
            return z.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.FullName.EndsWith(".json", StringComparison.Ordinal))
                .Select(e => "minecraft:" + e.FullName[prefix.Length..^5]).OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        private static string ReadEntry(string jar, string name)
        {
            using var z = OpenDataZip(jar);
            var e = z.GetEntry(name);
            if (e == null) return null;
            using var rd = new StreamReader(e.Open());
            return rd.ReadToEnd();
        }

        private static int DataPackFormat(string jar)
        {
            var json = ReadEntry(jar, "version.json");
            if (json == null) return -1;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("pack_version", out var pv))
            {
                if (pv.ValueKind == JsonValueKind.Number) return pv.GetInt32();
                if (pv.TryGetProperty("data", out var d)) return d.GetInt32();
                if (pv.TryGetProperty("data_major", out var dm)) return dm.GetInt32();
            }
            return -1;
        }

        /// <summary>Writes pack.mcmeta and a world preset with the chosen biome as the Overworld's only biome, derived from the jar's own single-biome preset.</summary>
        private static void WriteSingleBiomePack(string jar, string datapacksDir, string biome)
        {
            var fmt = DataPackFormat(jar);
            if (fmt < 0) throw new InvalidOperationException("Could not read the data pack format from the server jar.");
            var preset = ReadEntry(jar, "data/minecraft/worldgen/world_preset/single_biome_surface.json")
                         ?? throw new NotSupportedException("This server jar has no single biome world preset.");
            var node = JsonNode.Parse(preset);
            var gen = node?["dimensions"]?["minecraft:overworld"]?["generator"];
            if (gen == null) throw new InvalidDataException("The jar's single biome preset has an unexpected layout.");
            gen["biome_source"] = new JsonObject { ["type"] = "minecraft:fixed", ["biome"] = biome };
            var pack = Path.Combine(datapacksDir, "materialbrowser-worldgen");
            var presetDir = Path.Combine(pack, "data", "materialbrowser", "worldgen", "world_preset");
            Directory.CreateDirectory(presetDir);
            File.WriteAllText(Path.Combine(pack, "pack.mcmeta"), new JsonObject { ["pack"] = new JsonObject { ["pack_format"] = fmt, ["description"] = "World preset written by Monitoring Tools" } }.ToJsonString());
            File.WriteAllText(Path.Combine(presetDir, "generated.json"), node.ToJsonString());
        }

        // -------------------------------------------------------------- apply to an existing server

        /// <summary>
        /// Writes level-seed, level-type and generate-structures into a local server's properties, and the
        /// single biome data pack when needed. Only a world that has not been created yet picks them up.
        /// </summary>
        public static async Task<string> ApplyToServerAsync(ServerDef s, WorldGenOptions o)
        {
            if (s.Target != Target.LocalProcess && s.Target != Target.LocalDocker)
                throw new NotSupportedException("This tool edits local servers. A server on an SSH host keeps its files on that host.");
            var sup = await SupportAsync(s.Version);
            if (o.Type == WorldType.SingleBiome && (s.Target != Target.LocalProcess || !sup.DataDriven))
                throw new NotSupportedException(sup.SingleBiomeReason ?? "A single biome world needs a local process server, so the tool can read the real jar.");
            var props = ServerManager.ReadProperties(s);
            var level = props.TryGetValue("level-name", out var ln) && ln.Length > 0 ? ln : "world";
            var existing = File.Exists(Path.Combine(s.Folder, level, "level.dat"));
            if (o.Type == WorldType.SingleBiome)
            {
                var jar = Path.Combine(s.Folder, "server.jar");
                if (!File.Exists(jar)) throw new FileNotFoundException("The server jar is not in the server folder yet.");
                WriteSingleBiomePack(jar, Path.Combine(s.Folder, level, "datapacks"), o.Biome);
            }
            ServerManager.WriteProperties(s, new Dictionary<string, string>
            {
                ["level-seed"] = o.Seed ?? "",
                ["level-type"] = LevelType(o.Type, sup.DataDriven),
                ["generate-structures"] = o.Structures ? "true" : "false"
            });
            return existing
                ? $"Saved to server.properties. The world \"{level}\" already exists, so its terrain will not change; use a new world folder (change level-name or delete the old world) to apply these."
                : "Saved to server.properties. The world is created with these settings the first time the server starts.";
        }

        // -------------------------------------------------------------- generate a new world

        private sealed class Console2 : IDisposable
        {
            public readonly Process P;
            private readonly BlockingCollection<string> _lines = new BlockingCollection<string>();
            public readonly List<string> Tail = new List<string>();
            public Console2(Process p)
            {
                P = p;
                p.OutputDataReceived += (o, e) => Add(e.Data);
                p.ErrorDataReceived += (o, e) => Add(e.Data);
            }
            private void Add(string l) { if (l == null) return; _lines.Add(l); lock (Tail) { Tail.Add(l); if (Tail.Count > 40) Tail.RemoveAt(0); } }
            public void Send(string cmd) { P.StandardInput.WriteLine(cmd); P.StandardInput.Flush(); }
            public string TailText() { lock (Tail) return string.Join("\n", Tail.TakeLast(8)); }

            /// <summary>Reads lines until one satisfies the test. Returns that line, or null on timeout or when the server has exited.</summary>
            public string WaitFor(Func<string, bool> test, TimeSpan timeout, CancellationToken ct)
            {
                var end = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < end)
                {
                    ct.ThrowIfCancellationRequested();
                    if (_lines.TryTake(out var l, 300)) { if (test(l)) return l; }
                    else if (P.HasExited) return null;
                }
                return null;
            }
            public void Dispose() { try { if (!P.HasExited) P.Kill(true); } catch { } P.Dispose(); }
        }

        private static int FreeLoopbackPort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start(); int p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p;
        }

        /// <summary>
        /// Runs a private staging server with the chosen seed and type, optionally pre-generates the square area with
        /// forceload, saves, stops, and copies the world to a new folder or a ZIP. Returns the path written.
        /// </summary>
        public static async Task<string> GenerateAsync(WorldGenOptions o, Action<string, double> progress, CancellationToken ct)
        {
            if (!o.EulaAccepted) throw new InvalidOperationException("Accept the Minecraft EULA first; the server will not run without it.");
            if (string.IsNullOrWhiteSpace(o.Destination)) throw new InvalidOperationException("Choose where to put the world first.");
            if (string.IsNullOrWhiteSpace(o.Version)) throw new InvalidOperationException("Choose a version first.");
            o.RadiusChunks = Math.Clamp(o.RadiusChunks, 0, MaxRadiusChunks);
            var sup = await SupportAsync(o.Version);
            if (o.Type == WorldType.SingleBiome && !sup.DataDriven) throw new NotSupportedException(sup.SingleBiomeReason);

            progress("Preparing the server jar…", 0.02);
            var (jar, javaMajor) = await EnsureJarAsync(o.Version, t => progress(t, 0.05));
            var java = await ServerManager.JavaAsync(javaMajor, t => progress(t, 0.08));
            ct.ThrowIfCancellationRequested();

            var stage = Path.Combine(ServerManager.Root, "worldgen-stage", Guid.NewGuid().ToString("N")[..10]);
            Directory.CreateDirectory(stage);
            try
            {
                File.Copy(jar, Path.Combine(stage, "server.jar"));
                File.WriteAllText(Path.Combine(stage, "eula.txt"), $"# Accepted by the user in Monitoring Tools on {DateTime.Now:O}\neula=true\n");

                bool preGen = o.RadiusChunks > 0;
                string preGenNote = null;
                if (preGen && !sup.Forceload) { preGen = false; preGenNote = sup.ForceloadReason; }
                if (preGen)
                {
                    progress("Asking the jar which commands it has…", 0.09);
                    var (ok, why) = await Task.Run(() => HasLoadedTest(java, Path.Combine(stage, "server.jar"), Path.Combine(stage, "reports")));
                    if (!ok) { preGen = false; preGenNote = why; }
                }

                var port = FreeLoopbackPort();
                var props = new Dictionary<string, string>
                {
                    ["server-ip"] = "127.0.0.1", ["server-port"] = port.ToString(), ["online-mode"] = "false",
                    ["level-name"] = "world", ["level-seed"] = o.Seed ?? "", ["level-type"] = LevelType(o.Type, sup.DataDriven),
                    ["generate-structures"] = o.Structures ? "true" : "false", ["max-tick-time"] = "-1",
                    ["view-distance"] = "3", ["simulation-distance"] = "3", ["enable-rcon"] = "false", ["spawn-protection"] = "0"
                };
                File.WriteAllLines(Path.Combine(stage, "server.properties"), props.Select(kv => kv.Key + "=" + kv.Value));
                if (o.Type == WorldType.SingleBiome)
                    WriteSingleBiomePack(jar, Path.Combine(stage, "world", "datapacks"), o.Biome);

                progress("Starting a private server to build the world…", 0.12);
                var psi = new ProcessStartInfo(java, "-Xmx2G -jar server.jar nogui")
                { WorkingDirectory = stage, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var con = new Console2(new Process { StartInfo = psi });
                con.P.Start(); con.P.BeginOutputReadLine(); con.P.BeginErrorReadLine();

                if (con.WaitFor(l => l.Contains("Done ("), TimeSpan.FromMinutes(6), ct) == null)
                    throw new InvalidOperationException("The server did not finish starting. Last output:\n" + con.TailText());

                string note = preGenNote;
                if (preGen) note = await PreGenerateAsync(con, o.RadiusChunks, progress, ct);
                if (o.BorderToSize)
                {
                    con.Send("worldborder center 0 0");
                    con.Send($"worldborder set {o.RadiusChunks * 32}");
                }

                progress("Saving the world…", 0.88);
                con.Send("save-all flush");
                if (con.WaitFor(l => l.Contains("Saved the game") || l.Contains("Saved the world"), TimeSpan.FromMinutes(5), ct) == null)
                    throw new InvalidOperationException("The server did not confirm the save. Last output:\n" + con.TailText());
                con.Send("stop");
                if (!con.P.WaitForExit(120000)) throw new InvalidOperationException("The staging server did not stop in time.");

                var source = Path.Combine(stage, "world");
                if (!File.Exists(Path.Combine(source, "level.dat"))) throw new InvalidDataException("The server did not write a world (no level.dat).");
                progress(o.AsZip ? "Writing the ZIP…" : "Copying the world…", 0.92);
                var written = await Task.Run(() => o.AsZip ? WriteZip(source, o.Destination, o.WorldName) : CopyFolder(source, o.Destination, o.WorldName), ct);
                progress("Done." + (note != null ? " " + note : ""), 1.0);
                return written + (note != null ? "\n" + note : "");
            }
            finally
            {
                try { Directory.Delete(stage, true); } catch { }
            }
        }

        /// <summary>The data generator lists the jar's commands; pre-generation only proceeds when `execute if loaded` exists, because that is the only honest way to know a chunk is done.</summary>
        private static (bool Ok, string Reason) HasLoadedTest(string java, string jar, string outDir)
        {
            var r = ServerManager.Run(java, $"-DbundlerMainClass=net.minecraft.data.Main -jar \"{jar}\" --reports --output \"{outDir}\"", 240000);
            var f = Path.Combine(outDir, "reports", "commands.json");
            if (r.Code != 0 || !File.Exists(f))
                return (false, "This jar's data generator could not list its commands, so pre-generation was skipped. The world still generates as it is explored.");
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            try
            {
                var ok = doc.RootElement.GetProperty("children").GetProperty("execute").GetProperty("children").GetProperty("if").GetProperty("children").TryGetProperty("loaded", out _);
                return ok ? (true, null) : (false, "This version has no command that reports whether a chunk is loaded (execute if loaded), so pre-generation was skipped to avoid guessing. The world still generates as it is explored.");
            }
            catch (KeyNotFoundException) { return (false, "The command list had an unexpected shape, so pre-generation was skipped."); }
        }

        private static async Task<string> PreGenerateAsync(Console2 con, int radius, Action<string, double> progress, CancellationToken ct)
        {
            int total = BatchCount(radius), n = 0;
            for (int cx = -radius; cx < radius; cx += ChunksPerBatchSide)
                for (int cz = -radius; cz < radius; cz += ChunksPerBatchSide)
                {
                    ct.ThrowIfCancellationRequested();
                    int cx2 = Math.Min(cx + ChunksPerBatchSide - 1, radius - 1), cz2 = Math.Min(cz + ChunksPerBatchSide - 1, radius - 1);
                    con.Send($"forceload add {cx * 16} {cz * 16} {cx2 * 16 + 15} {cz2 * 16 + 15}");
                    var pending = new List<(int X, int Z)>();
                    for (int x = cx; x <= cx2; x++) for (int z = cz; z <= cz2; z++) pending.Add((x, z));
                    var deadline = DateTime.UtcNow.AddMinutes(5);
                    while (pending.Count > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (DateTime.UtcNow > deadline) throw new TimeoutException($"Batch {n + 1} of {total} did not finish loading in 5 minutes. Last output:\n" + con.TailText());
                        var still = new List<(int X, int Z)>();
                        foreach (var c in pending)
                        {
                            con.Send($"execute if loaded {c.X * 16 + 8} 0 {c.Z * 16 + 8}");
                            var reply = con.WaitFor(l => l.Contains("Test passed") || l.Contains("Test failed"), TimeSpan.FromSeconds(20), ct);
                            if (reply == null) throw new InvalidOperationException("The server stopped answering during pre-generation. Last output:\n" + con.TailText());
                            if (reply.Contains("Test failed")) still.Add(c);
                        }
                        pending = still;
                        if (pending.Count > 0) await Task.Delay(400, ct);
                    }
                    con.Send("forceload remove all");
                    n++;
                    progress($"Pre-generating: batch {n} of {total}", 0.15 + 0.7 * n / total);
                }
            return null;
        }

        // -------------------------------------------------------------- output

        private static string UniquePath(string parent, string name, string ext)
        {
            var p = Path.Combine(parent, name + ext);
            for (int i = 2; File.Exists(p) || Directory.Exists(p); i++) p = Path.Combine(parent, $"{name} ({i}){ext}");
            return p;
        }

        private static IEnumerable<string> WorldFiles(string root) =>
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(f => !Path.GetFileName(f).Equals("session.lock", StringComparison.OrdinalIgnoreCase));

        /// <summary>Copies into a new folder (never an existing one) and verifies every file by length and SHA-256.</summary>
        public static string CopyFolder(string source, string parent, string name)
        {
            Directory.CreateDirectory(parent);
            var target = UniquePath(parent, name, "");
            foreach (var f in WorldFiles(source))
            {
                var rel = Path.GetRelativePath(source, f);
                var dest = Path.Combine(target, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest);
            }
            foreach (var f in WorldFiles(source))
            {
                var dest = Path.Combine(target, Path.GetRelativePath(source, f));
                if (new FileInfo(f).Length != new FileInfo(dest).Length || Hash(f) != Hash(dest))
                    throw new InvalidDataException("A copied file did not match its source: " + Path.GetRelativePath(source, f));
            }
            return target;
        }

        /// <summary>Writes to a temporary file, checks every entry against the source, then renames. Never replaces an existing ZIP.</summary>
        public static string WriteZip(string source, string parent, string name)
        {
            Directory.CreateDirectory(parent);
            var target = UniquePath(parent, name, ".zip");
            var part = target + ".part";
            try
            {
                using (var z = new ZipArchive(File.Create(part), ZipArchiveMode.Create))
                    foreach (var f in WorldFiles(source))
                        z.CreateEntryFromFile(f, name + "/" + Path.GetRelativePath(source, f).Replace('\\', '/'), CompressionLevel.Optimal);
                using (var z = ZipFile.OpenRead(part))
                {
                    var files = WorldFiles(source).ToList();
                    if (z.Entries.Count != files.Count) throw new InvalidDataException("The ZIP has a different number of files than the world.");
                    foreach (var f in files)
                    {
                        var e = z.GetEntry(name + "/" + Path.GetRelativePath(source, f).Replace('\\', '/'));
                        if (e == null || e.Length != new FileInfo(f).Length) throw new InvalidDataException("A ZIP entry did not match its source: " + Path.GetRelativePath(source, f));
                    }
                }
                File.Move(part, target);
                return target;
            }
            catch { try { File.Delete(part); } catch { } throw; }
        }

        private static string Hash(string path) { using var f = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)); }

        public static string RandomSeed()
        {
            var b = new byte[8]; RandomNumberGenerator.Fill(b);
            return BitConverter.ToInt64(b, 0).ToString();
        }
    }
}
