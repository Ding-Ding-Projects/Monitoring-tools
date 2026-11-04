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

    /// <summary>
    /// Pause and resume for pre-generation. Setting Paused holds the run at the next batch boundary (the current batch of at most
    /// 256 chunks finishes first), where the server is idle; clearing it carries on from the next batch. Cancelling still works while paused.
    /// </summary>
    public sealed class GenPause
    {
        private volatile bool _paused;
        public bool Paused { get => _paused; set => _paused = value; }
        /// <summary>How many times a run has actually waited because of a pause.</summary>
        public int Waits { get; private set; }

        public async Task WaitAsync(CancellationToken ct, Action announce)
        {
            if (!_paused) return;
            Waits++;
            announce?.Invoke();
            while (_paused) await Task.Delay(150, ct);
        }
    }

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
        /// <summary>Also pre-generate the same number of chunks in the Nether and the End (runs the same bounded forceload procedure through execute in).</summary>
        public bool PreGenNether, PreGenEnd;
        /// <summary>Game rules to set after the server starts: only the rules the user changed, name to value text. Everything else keeps the server default.</summary>
        public Dictionary<string, string> GameRules = new Dictionary<string, string>();
        /// <summary>Custom superflat layers, bottom to top. Empty means the version's own default layers. Used only for the Flat type on Java 1.19 and newer.</summary>
        public List<FlatLayer> FlatLayers = new List<FlatLayer>();
        public string FlatBiome = "minecraft:plains";
        /// <summary>Centre of the pre-generated square, in chunk coordinates. Default 0, 0. The square covers chunks centre - radius up to centre + radius - 1.</summary>
        public int AreaCenterX, AreaCenterZ;
        /// <summary>Changed noise settings and dimension type values, written as a data pack of only the changed files. Empty means the jar's defaults.</summary>
        public List<WorldgenEdit> WorldgenEdits = new List<WorldgenEdit>();
    }

    public sealed class FlatLayer
    {
        public string Block = "minecraft:stone";
        public int Height = 1;
    }

    public enum RuleKind { Bool, Integer, Other }

    public sealed class GameRuleDef
    {
        public string Name, ParserType;
        public RuleKind Kind;
    }

    /// <summary>What a specific server jar says about itself, read from its own data generator reports. Cached per version.</summary>
    public sealed class JarFacts
    {
        public bool Available;
        public string Reason;
        public bool HasLoadedTest, HasExecuteIn, HasGamerule;
        public List<GameRuleDef> GameRules = new List<GameRuleDef>();
        public HashSet<string> Blocks = new HashSet<string>(StringComparer.Ordinal);
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

        internal static string CacheRoot { get { var d = Path.Combine(ServerManager.Root, "worldgen-cache"); Directory.CreateDirectory(d); return d; } }

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

        /// <summary>True when the options ask for custom superflat layers that need the data pack route.</summary>
        public static bool UsesFlatPack(WorldGenOptions o, bool dataDriven) => o.Type == WorldType.Flat && o.FlatLayers != null && o.FlatLayers.Count > 0 && dataDriven;

        public static string LevelType(WorldGenOptions o, bool dataDriven) => UsesFlatPack(o, dataDriven) ? "materialbrowser:generated" : LevelType(o.Type, dataDriven);

        /// <summary>
        /// Splits a span of chunks into as few batches as the 16 x 16 limit allows, all of nearly the same width, so no batch is a thin strip
        /// (17 chunks became a batch of 16 and a batch of 1, and thin batches were the ones seen to stall). Returns inclusive first and last chunk.
        /// </summary>
        internal static List<(int First, int Last)> Tiles(int start, int span)
        {
            var list = new List<(int, int)>();
            if (span <= 0) return list;
            int count = (int)Math.Ceiling(span / (double)ChunksPerBatchSide), at = start;
            for (int i = 0; i < count; i++)
            {
                int width = span / count + (i < span % count ? 1 : 0);
                list.Add((at, at + width - 1));
                at += width;
            }
            return list;
        }

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
        internal static ZipArchive OpenDataZip(string jar)
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

        internal static int DataPackFormat(string jar)
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

        // -------------------------------------------------------------- what the jar says about itself

        internal static bool IsBundler(string jar)
        {
            using var z = new ZipArchive(File.OpenRead(jar), ZipArchiveMode.Read);
            return z.Entries.Any(e => e.FullName.StartsWith("META-INF/versions/", StringComparison.Ordinal));
        }

        /// <summary>
        /// Runs the jar's own data generator (no server start, no EULA needed) once per version and reads the command list and block registry.
        /// Results are cached next to the jar. An unreadable jar returns facts with Available false and the reason instead of throwing.
        /// </summary>
        public static async Task<JarFacts> JarFactsAsync(string version, Action<string> report)
        {
            var (jar, javaMajor) = await EnsureJarAsync(version, report);
            var gen = Path.Combine(CacheRoot, version, "gen");
            var commands = Path.Combine(gen, "reports", "commands.json");
            var registries = Path.Combine(gen, "reports", "registries.json");
            if (!File.Exists(commands) || !File.Exists(registries))
            {
                var java = await ServerManager.JavaAsync(javaMajor, report);
                report?.Invoke("Asking the jar for its command and block lists…");
                var args = IsBundler(jar)
                    ? $"-DbundlerMainClass=net.minecraft.data.Main -jar \"{jar}\" --reports --output \"{gen}\""
                    : $"-cp \"{jar}\" net.minecraft.data.Main --reports --output \"{gen}\"";
                var r = await Task.Run(() => ServerManager.Run(java, args, 240000, workingDir: Path.Combine(CacheRoot, version, "run")));
                if (r.Code != 0 || !File.Exists(commands))
                    return new JarFacts { Reason = "This jar's data generator could not list its commands, so jar-driven options are unavailable for this version." };
            }
            return await Task.Run(() => ParseFacts(commands, registries));
        }

        private static JarFacts ParseFacts(string commandsFile, string registriesFile)
        {
            var f = new JarFacts();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(commandsFile));
                var kids = doc.RootElement.GetProperty("children");
                if (kids.TryGetProperty("execute", out var ex) && ex.TryGetProperty("children", out var ek))
                {
                    f.HasExecuteIn = ek.TryGetProperty("in", out _);
                    f.HasLoadedTest = ek.TryGetProperty("if", out var iff) && iff.TryGetProperty("children", out var ik) && ik.TryGetProperty("loaded", out _);
                }
                if (kids.TryGetProperty("gamerule", out var gr) && gr.TryGetProperty("children", out var gk))
                {
                    foreach (var rule in gk.EnumerateObject())
                    {
                        string parser = null;
                        if (rule.Value.TryGetProperty("children", out var rc) && rc.TryGetProperty("value", out var val) && val.TryGetProperty("parser", out var pa)) parser = pa.GetString();
                        f.GameRules.Add(new GameRuleDef { Name = rule.Name, ParserType = parser ?? "unknown", Kind = parser == "brigadier:bool" ? RuleKind.Bool : parser == "brigadier:integer" ? RuleKind.Integer : RuleKind.Other });
                    }
                    f.HasGamerule = f.GameRules.Count > 0;
                }
                f.GameRules = f.GameRules.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
                if (File.Exists(registriesFile))
                {
                    using var reg = JsonDocument.Parse(File.ReadAllText(registriesFile));
                    if (reg.RootElement.TryGetProperty("minecraft:block", out var blk) && blk.TryGetProperty("entries", out var entries))
                        foreach (var b in entries.EnumerateObject()) f.Blocks.Add(b.Name);
                }
                f.Available = true;
            }
            catch (Exception ex) { f.Reason = "The jar's command list had an unexpected shape (" + ex.Message + "), so jar-driven options are unavailable for this version."; }
            return f;
        }

        // -------------------------------------------------------------- superflat layers

        private static readonly System.Text.RegularExpressions.Regex IdPattern = new System.Text.RegularExpressions.Regex(@"^[a-z0-9_.\-]+:[a-z0-9_./\-]+$");
        public const int OverworldHeight = 384;   // total height of the vanilla overworld; layers above it are cut off

        /// <summary>Checks superflat layers and returns a plain-language list of problems. Block names are checked against the jar only when its block list is known.</summary>
        public static List<string> ValidateFlat(IReadOnlyList<FlatLayer> layers, string biome, HashSet<string> knownBlocks)
        {
            var problems = new List<string>();
            if (layers == null || layers.Count == 0) return problems;
            int total = 0, i = 0;
            foreach (var l in layers)
            {
                i++;
                var id = (l.Block ?? "").Trim();
                if (!IdPattern.IsMatch(id)) problems.Add($"Layer {i}: \"{id}\" is not a block id such as minecraft:stone.");
                else if (knownBlocks != null && knownBlocks.Count > 0 && !knownBlocks.Contains(id)) problems.Add($"Layer {i}: {id} is not a block in this version's jar.");
                if (l.Height < 1 || l.Height > OverworldHeight) problems.Add($"Layer {i}: height must be 1 to {OverworldHeight}.");
                total += Math.Max(0, l.Height);
            }
            if (total > OverworldHeight) problems.Add($"The layers add up to {total} blocks, more than the overworld's height of {OverworldHeight}.");
            if (!IdPattern.IsMatch((biome ?? "").Trim())) problems.Add($"The biome \"{biome}\" is not a biome id such as minecraft:plains.");
            return problems;
        }

        /// <summary>Reads a superflat preset string such as "minecraft:bedrock,2*minecraft:dirt,minecraft:grass_block;minecraft:plains". Structure and feature options in the string are not applied.</summary>
        public static (List<FlatLayer> Layers, string Biome, string Note) ParseFlatPreset(string text)
        {
            var parts = (text ?? "").Trim().Split(';');
            int at = 0;
            if (parts.Length > 1 && parts[0].Trim().Length > 0 && parts[0].Trim().All(char.IsDigit)) at = 1;
            var layerText = parts[at].Trim();
            if (layerText.Length == 0) throw new FormatException("The preset has no layers.");
            var layers = new List<FlatLayer>();
            foreach (var raw in layerText.Split(','))
            {
                var item = raw.Trim();
                int height = 1;
                var star = item.IndexOf('*');
                if (star > 0)
                {
                    if (!int.TryParse(item[..star], out height) || height < 1) throw new FormatException($"\"{item}\" does not start with a valid height.");
                    item = item[(star + 1)..];
                }
                if (item.Count(c => c == ':') > 1 || item.Contains('[')) throw new FormatException($"\"{item}\" uses block data values or states, which this editor does not support.");
                if (!item.Contains(':')) item = "minecraft:" + item;
                layers.Add(new FlatLayer { Block = item, Height = height });
            }
            string biome = "minecraft:plains", note = null;
            if (parts.Length > at + 1 && parts[at + 1].Trim().Length > 0)
            {
                var b = parts[at + 1].Trim();
                if (b.All(char.IsDigit)) note = "The preset names its biome by an old numeric id, which cannot be mapped reliably; minecraft:plains was kept.";
                else biome = b.Contains(':') ? b : "minecraft:" + b;
            }
            if (parts.Length > at + 2 && parts[at + 2].Trim().Length > 0)
                note = (note == null ? "" : note + " ") + "Structure and feature options in the preset were ignored.";
            return (layers, biome, note);
        }

        public static string ExportFlatPreset(IEnumerable<FlatLayer> layers, string biome) =>
            string.Join(",", layers.Select(l => (l.Height > 1 ? l.Height + "*" : "") + l.Block)) + ";" + biome;

        /// <summary>Writes pack.mcmeta and a world preset whose overworld uses the jar's own flat preset with the chosen layers and biome swapped in.</summary>
        private static void WriteFlatPack(string jar, string datapacksDir, IEnumerable<FlatLayer> layers, string biome)
        {
            var fmt = DataPackFormat(jar);
            if (fmt < 0) throw new InvalidOperationException("Could not read the data pack format from the server jar.");
            if (!Biomes(jar).Contains(biome.Trim())) throw new InvalidOperationException($"{biome} is not a biome in this version's jar.");
            var preset = ReadEntry(jar, "data/minecraft/worldgen/world_preset/flat.json") ?? throw new NotSupportedException("This server jar has no flat world preset to start from.");
            var node = JsonNode.Parse(preset);
            var settings = node?["dimensions"]?["minecraft:overworld"]?["generator"]?["settings"];
            if (settings == null) throw new InvalidDataException("The jar's flat preset has an unexpected layout.");
            var arr = new JsonArray();
            foreach (var l in layers) arr.Add(new JsonObject { ["block"] = l.Block.Trim(), ["height"] = l.Height });
            settings["layers"] = arr;
            settings["biome"] = biome.Trim();
            var pack = Path.Combine(datapacksDir, "materialbrowser-worldgen");
            var presetDir = Path.Combine(pack, "data", "materialbrowser", "worldgen", "world_preset");
            Directory.CreateDirectory(presetDir);
            File.WriteAllText(Path.Combine(pack, "pack.mcmeta"), new JsonObject { ["pack"] = new JsonObject { ["pack_format"] = fmt, ["description"] = "World preset written by Monitoring Tools" } }.ToJsonString());
            File.WriteAllText(Path.Combine(presetDir, "generated.json"), node.ToJsonString());
        }

        /// <summary>Validates the edits against the version's own default files and writes the data pack with only the changed files.</summary>
        internal static async Task<List<string>> WriteWorldgenEditsAsync(string version, string jar, string datapacksDir, IEnumerable<WorldgenEdit> edits, ISet<string> knownBlocks)
        {
            var fmt = DataPackFormat(jar);
            if (fmt < 0) throw new InvalidOperationException("Could not read the data pack format from the server jar.");
            var source = await WorldgenEditor.OpenAsync(version, jar, null);
            var files = WorldgenEditor.BuildFiles(source, edits, knownBlocks);
            return WorldgenEditor.WritePack(datapacksDir, fmt, files);
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
            bool flatPack = UsesFlatPack(o, sup.DataDriven);
            if (o.Type == WorldType.Flat && o.FlatLayers.Count > 0 && !sup.DataDriven)
                throw new NotSupportedException("Custom superflat layers need data-driven world presets, which arrived with Java 1.19. This version predates them.");
            if (flatPack && s.Target != Target.LocalProcess)
                throw new NotSupportedException("Custom superflat layers need a local process server, so the tool can read the real jar.");
            var props = ServerManager.ReadProperties(s);
            var level = props.TryGetValue("level-name", out var ln) && ln.Length > 0 ? ln : "world";
            var existing = File.Exists(Path.Combine(s.Folder, level, "level.dat"));
            if (o.Type == WorldType.SingleBiome)
            {
                var jar = Path.Combine(s.Folder, "server.jar");
                if (!File.Exists(jar)) throw new FileNotFoundException("The server jar is not in the server folder yet.");
                WriteSingleBiomePack(jar, Path.Combine(s.Folder, level, "datapacks"), o.Biome);
            }
            if (flatPack)
            {
                var jar = Path.Combine(s.Folder, "server.jar");
                if (!File.Exists(jar)) throw new FileNotFoundException("The server jar is not in the server folder yet.");
                var bad = ValidateFlat(o.FlatLayers, o.FlatBiome, null);
                if (bad.Count > 0) throw new InvalidOperationException(bad[0]);
                WriteFlatPack(jar, Path.Combine(s.Folder, level, "datapacks"), o.FlatLayers, o.FlatBiome);
            }
            if (o.WorldgenEdits.Count > 0)
            {
                if (s.Target != Target.LocalProcess || !sup.DataDriven)
                    throw new NotSupportedException("Noise settings and dimension type edits need a local process server on Java 1.19 or newer, so the tool can read the real jar.");
                var jar = Path.Combine(s.Folder, "server.jar");
                if (!File.Exists(jar)) throw new FileNotFoundException("The server jar is not in the server folder yet.");
                await WriteWorldgenEditsAsync(s.Version, jar, Path.Combine(s.Folder, level, "datapacks"), o.WorldgenEdits, null);
            }
            ServerManager.WriteProperties(s, new Dictionary<string, string>
            {
                ["level-seed"] = o.Seed ?? "",
                ["level-type"] = LevelType(o, sup.DataDriven),
                ["generate-structures"] = o.Structures ? "true" : "false"
            });
            return existing
                ? $"Saved to server.properties. The world \"{level}\" already exists, so its terrain will not change; use a new world folder (change level-name or delete the old world) to apply these."
                : "Saved to server.properties. The world is created with these settings the first time the server starts.";
        }

        // -------------------------------------------------------------- generate a new world

        private sealed class Console2 : IDisposable
        {
            public Process P { get; private set; }
            private readonly BlockingCollection<string> _lines = new BlockingCollection<string>();
            public readonly List<string> Tail = new List<string>();
            public Console2(Process p) { Attach(p); }
            private void Attach(Process p)
            {
                P = p;
                p.OutputDataReceived += (o, e) => Add(e.Data);
                p.ErrorDataReceived += (o, e) => Add(e.Data);
            }

            /// <summary>
            /// Stops the staging server (saving first) and starts it again on the same folder, waiting for Done. A server was seen to go on running normally
            /// while every chunk it had been asked to force-load stayed unloaded, with nothing in its log; a fresh process loads them. The world is saved
            /// after every batch, so a restart loses nothing that was finished.
            /// </summary>
            public void Restart(CancellationToken ct)
            {
                var psi = P.StartInfo;
                try { Send("save-all flush"); WaitFor(l => l.Contains("Saved the game") || l.Contains("Saved the world"), TimeSpan.FromSeconds(60), ct); Send("stop"); } catch (OperationCanceledException) { throw; } catch { }
                if (!P.WaitForExit(60000)) { try { P.Kill(true); P.WaitForExit(10000); } catch { } }
                P.Dispose();
                while (_lines.TryTake(out _)) { }
                var fresh = new Process { StartInfo = psi };
                Attach(fresh);
                fresh.Start(); fresh.BeginOutputReadLine(); fresh.BeginErrorReadLine();
                if (WaitFor(l => l.Contains("Done ("), TimeSpan.FromMinutes(6), ct) == null)
                    throw new InvalidOperationException("The staging server did not start again. Last output:\n" + TailText());
            }
            private void Add(string l) { if (l == null) return; _lines.Add(l); lock (Tail) { Tail.Add(l); if (Tail.Count > 400) Tail.RemoveAt(0); } }
            public void Send(string cmd) { P.StandardInput.WriteLine(cmd); P.StandardInput.Flush(); }
            public string TailText() { lock (Tail) return string.Join("\n", Tail.Where(l => !l.Contains("Test failed") && !l.Contains("Test passed")).TakeLast(8)); }

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
            /// <summary>What the server says about itself when a batch will not load: its forced chunks, its tick state and, when the runtime has jcmd, what its busiest threads are doing.</summary>
            public string Diagnose(string run)
            {
                var sb = new System.Text.StringBuilder();
                try
                {
                    Send($"{run}forceload query");
                    var q = WaitFor(l => l.Contains("force loaded") || l.Contains("force-loaded") || l.Contains("No chunks"), TimeSpan.FromSeconds(10), CancellationToken.None);
                    sb.Append("forceload query: ").Append(q?.Trim() ?? "no answer").Append('\n');
                    Send("tick query");
                    var t = WaitFor(l => l.Contains("tick rate") || l.Contains("Target tick") || l.Contains("The game is"), TimeSpan.FromSeconds(10), CancellationToken.None);
                    sb.Append("tick query: ").Append(t?.Trim() ?? "no answer").Append('\n');
                    var jcmd = Path.Combine(Path.GetDirectoryName(P.MainModule.FileName) ?? "", "jcmd.exe");
                    if (File.Exists(jcmd))
                    {
                        var psi = new ProcessStartInfo(jcmd, $"{P.Id} Thread.print") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                        using var j = Process.Start(psi);
                        var dump = j.StandardOutput.ReadToEnd(); j.WaitForExit(15000);
                        var keep = dump.Split('\n').Where(x => x.StartsWith("\"Server thread") || x.StartsWith("\"Worker-Main") || x.Contains("State: BLOCKED") || x.Contains("deadlock")).Take(12);
                        sb.Append("threads: ").Append(string.Join(" | ", keep.Select(x => x.Trim()))).Append('\n');
                        File.WriteAllText(Path.Combine(Path.GetTempPath(), "pregen-threads-" + DateTime.Now.ToString("HHmmss") + ".txt"), dump);
                    }
                    else sb.Append("no jcmd beside this runtime\n");
                }
                catch (Exception ex) { sb.Append("diagnosis failed: " + ex.Message); }
                return sb.ToString();
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
        public static async Task<string> GenerateAsync(WorldGenOptions o, Action<string, double> progress, CancellationToken ct, GenPause pause = null)
        {
            if (!o.EulaAccepted) throw new InvalidOperationException("Accept the Minecraft EULA first; the server will not run without it.");
            if (string.IsNullOrWhiteSpace(o.Destination)) throw new InvalidOperationException("Choose where to put the world first.");
            if (string.IsNullOrWhiteSpace(o.Version)) throw new InvalidOperationException("Choose a version first.");
            o.RadiusChunks = Math.Clamp(o.RadiusChunks, 0, MaxRadiusChunks);
            var sup = await SupportAsync(o.Version);
            if (o.Type == WorldType.SingleBiome && !sup.DataDriven) throw new NotSupportedException(sup.SingleBiomeReason);
            if (o.Type == WorldType.Flat && o.FlatLayers.Count > 0 && !sup.DataDriven)
                throw new NotSupportedException("Custom superflat layers need data-driven world presets, which arrived with Java 1.19. This version predates them.");
            bool flatPack = UsesFlatPack(o, sup.DataDriven);

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
                JarFacts facts = null;
                if (preGen || o.GameRules.Count > 0 || flatPack || o.WorldgenEdits.Count > 0)
                {
                    progress("Asking the jar which commands and blocks it has…", 0.09);
                    facts = await JarFactsAsync(o.Version, t => progress(t, 0.09));
                }
                if (flatPack)
                {
                    var bad = ValidateFlat(o.FlatLayers, o.FlatBiome, facts?.Available == true ? facts.Blocks : null);
                    if (bad.Count > 0) throw new InvalidOperationException("The superflat layers are not valid: " + string.Join(" ", bad));
                }
                if (preGen && !(facts.Available && facts.HasLoadedTest))
                {
                    preGen = false;
                    preGenNote = facts.Available ? "This version has no command that reports whether a chunk is loaded (execute if loaded), so pre-generation was skipped to avoid guessing. The world still generates as it is explored." : facts.Reason;
                }
                var dims = new List<(string Id, string Label)> { (null, "Overworld") };
                var skipped = new List<string>();
                if (preGen && o.PreGenNether) { if (facts.HasExecuteIn) dims.Add(("minecraft:the_nether", "Nether")); else skipped.Add("the Nether"); }
                if (preGen && o.PreGenEnd) { if (facts.HasExecuteIn) dims.Add(("minecraft:the_end", "End")); else skipped.Add("the End"); }
                if (!preGen && (o.PreGenNether || o.PreGenEnd) && o.RadiusChunks > 0) skipped.Add("the Nether and the End");
                if (skipped.Count > 0) preGenNote = (preGenNote == null ? "" : preGenNote + " ") + "Not pre-generated: " + string.Join(" and ", skipped) + (preGen ? " (this jar has no execute in command)." : " (pre-generation itself was skipped).");

                var port = FreeLoopbackPort();
                var props = new Dictionary<string, string>
                {
                    ["server-ip"] = "127.0.0.1", ["server-port"] = port.ToString(), ["online-mode"] = "false",
                    ["level-name"] = "world", ["level-seed"] = o.Seed ?? "", ["level-type"] = LevelType(o, sup.DataDriven),
                    ["generate-structures"] = o.Structures ? "true" : "false", ["max-tick-time"] = "-1",
                    ["view-distance"] = "3", ["simulation-distance"] = "3", ["enable-rcon"] = "false", ["spawn-protection"] = "0"
                };
                File.WriteAllLines(Path.Combine(stage, "server.properties"), props.Select(kv => kv.Key + "=" + kv.Value));
                if (o.Type == WorldType.SingleBiome)
                    WriteSingleBiomePack(jar, Path.Combine(stage, "world", "datapacks"), o.Biome);
                if (flatPack)
                    WriteFlatPack(jar, Path.Combine(stage, "world", "datapacks"), o.FlatLayers, o.FlatBiome);
                if (o.WorldgenEdits.Count > 0)
                {
                    if (!sup.DataDriven) throw new NotSupportedException("Noise settings and dimension type edits need Java 1.19 or newer. This version keeps them inside the game.");
                    progress("Writing the worldgen data pack…", 0.1);
                    await WriteWorldgenEditsAsync(o.Version, jar, Path.Combine(stage, "world", "datapacks"), o.WorldgenEdits, facts?.Available == true ? facts.Blocks : null);
                }

                progress("Starting a private server to build the world…", 0.12);
                var psi = new ProcessStartInfo(java, "-Xmx" + (Environment.GetEnvironmentVariable("MT_STAGING_XMX") ?? "2G") + " -jar server.jar nogui")
                { WorkingDirectory = stage, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var con = new Console2(new Process { StartInfo = psi });
                con.P.Start(); con.P.BeginOutputReadLine(); con.P.BeginErrorReadLine();

                if (con.WaitFor(l => l.Contains("Done ("), TimeSpan.FromMinutes(6), ct) == null)
                    throw new InvalidOperationException("The server did not finish starting. Last output:\n" + con.TailText());

                string note = preGenNote;
                var ruleNote = ApplyGameRules(con, o.GameRules, facts, ct);
                if (ruleNote != null) note = (note == null ? "" : note + " ") + ruleNote;
                if (preGen) await PreGenerateAsync(con, o.RadiusChunks, o.AreaCenterX, o.AreaCenterZ, dims, progress, ct, pause);
                if (o.BorderToSize)
                {
                    con.Send($"worldborder center {o.AreaCenterX * 16} {o.AreaCenterZ * 16}");
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

        /// <summary>Sets only the rules the user changed, after checking each against the jar's own rule list and type. Returns a note about anything skipped, or null.</summary>
        private static string ApplyGameRules(Console2 con, Dictionary<string, string> rules, JarFacts facts, CancellationToken ct)
        {
            if (rules == null || rules.Count == 0) return null;
            if (facts == null || !facts.Available || !facts.HasGamerule)
                return "Game rules were not set: " + (facts?.Reason ?? "this jar's gamerule command could not be read.");
            var skipped = new List<string>();
            foreach (var kv in rules.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var def = facts.GameRules.FirstOrDefault(r => r.Name == kv.Key);
                var v = (kv.Value ?? "").Trim();
                bool ok = def != null && ((def.Kind == RuleKind.Bool && (v == "true" || v == "false")) || (def.Kind == RuleKind.Integer && int.TryParse(v, out _)));
                if (!ok) { skipped.Add(kv.Key + " (not a valid rule or value for this jar)"); continue; }
                con.Send($"gamerule {kv.Key} {v}");
                var reply = con.WaitFor(l => l.Contains("is now set to") || l.Contains("Incorrect argument") || l.Contains("Unknown or incomplete"), TimeSpan.FromSeconds(15), ct);
                if (reply == null || !reply.Contains("is now set to")) skipped.Add(kv.Key + " (the server did not confirm it)");
            }
            return skipped.Count == 0 ? null : "Some game rules were not set: " + string.Join(", ", skipped) + ".";
        }

        /// <summary>Force-loads rectangles of at most 256 chunks in each requested dimension, confirming every chunk loaded before moving on. A null dimension id means the overworld.</summary>
        private static async Task PreGenerateAsync(Console2 con, int radius, int centerX, int centerZ, List<(string Id, string Label)> dims, Action<string, double> progress, CancellationToken ct, GenPause pause = null)
        {
            int perDim = BatchCount(radius), total = perDim * dims.Count, n = 0;
            for (int d = 0; d < dims.Count; d++)
            {
                var dim = dims[d];
                string run = dim.Id == null ? "" : $"execute in {dim.Id} run ";
                string inDim = dim.Id == null ? "execute" : $"execute in {dim.Id}";
                int inThis = 0;
                foreach (var (cx, cxEnd) in Tiles(centerX - radius, radius * 2))
                    foreach (var (cz, czEnd) in Tiles(centerZ - radius, radius * 2))
                    {
                        ct.ThrowIfCancellationRequested();
                        // a pause takes effect between batches, when no chunk is being loaded and nothing is force-loaded
                        if (pause != null) await pause.WaitAsync(ct, () => progress($"Paused after batch {inThis} of {perDim}. Resume to carry on.", 0.15 + 0.7 * n / total));
                        int cx2 = cxEnd, cz2 = czEnd;
                        con.Send($"{run}forceload add {cx * 16} {cz * 16} {cx2 * 16 + 15} {cz2 * 16 + 15}");
                        // the server answers the forceload command; an error here is reported with its own words instead of a five minute wait
                        var added = con.WaitFor(l => l.Contains("force loaded") || l.Contains("force-loaded") || l.Contains("Unable") || l.Contains("Incorrect") || l.Contains("Too many") || l.Contains("Unknown or incomplete") || l.Contains("out of the world") || l.Contains("Cannot"), TimeSpan.FromSeconds(30), ct);
                        if (added != null && !(added.Contains("force loaded") || added.Contains("force-loaded")))
                            throw new InvalidOperationException($"The server refused to force-load {dim.Label} batch {inThis + 1} of {perDim}: {added.Trim()}");
                        var pending = new List<(int X, int Z)>();
                        for (int x = cx; x <= cx2; x++) for (int z = cz; z <= cz2; z++) pending.Add((x, z));
                        var deadline = DateTime.UtcNow.AddMinutes(5);
                        // A batch whose chunks stop loading is asked for again: the server's force-load tickets can stall when a batch is
                        // requested right after the last one was removed, and removing them all and adding the area once more starts them over.
                        var lastGain = DateTime.UtcNow; int lastCount = pending.Count, restarts = 0;
                        while (pending.Count > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (pending.Count < lastCount) { lastCount = pending.Count; lastGain = DateTime.UtcNow; }
                            else if ((DateTime.UtcNow - lastGain).TotalSeconds > 45 && restarts < 3)
                            {
                                restarts++; lastGain = DateTime.UtcNow; deadline = DateTime.UtcNow.AddMinutes(5);
                                progress($"Pre-generating the {dim.Label}: batch {inThis + 1} of {perDim} stopped loading, so the staging server was restarted ({restarts} of 3)", 0.15 + 0.7 * n / total);
                                var diagnosis = con.Diagnose(run);
                                Core.Log.Info("Pre-generation stalled and the staging server was restarted. " + diagnosis.Replace("\n", " | "));
                                con.Restart(ct);
                                con.Send($"{run}forceload remove all");
                                con.WaitFor(l => l.Contains("no longer") || l.Contains("No chunks"), TimeSpan.FromSeconds(15), ct);
                                con.Send($"{run}forceload add {cx * 16} {cz * 16} {cx2 * 16 + 15} {cz2 * 16 + 15}");
                                con.WaitFor(l => l.Contains("force loaded") || l.Contains("force-loaded"), TimeSpan.FromSeconds(30), ct);
                                lastCount = pending.Count;
                            }
                            if (DateTime.UtcNow > deadline) throw new TimeoutException($"{dim.Label} batch {inThis + 1} of {perDim} did not finish loading in 5 minutes (asked again {restarts} times); {pending.Count} chunks were still not loaded, the first at chunk {pending[0].X}, {pending[0].Z}. Last output:\n" + con.TailText() + "\n" + con.Diagnose(run));
                            var still = new List<(int X, int Z)>();
                            foreach (var c in pending)
                            {
                                con.Send($"{inDim} if loaded {c.X * 16 + 8} 0 {c.Z * 16 + 8}");
                                var reply = con.WaitFor(l => l.Contains("Test passed") || l.Contains("Test failed"), TimeSpan.FromSeconds(20), ct);
                                if (reply == null) throw new InvalidOperationException("The server stopped answering during pre-generation. Last output:\n" + con.TailText());
                                if (reply.Contains("Test failed")) still.Add(c);
                            }
                            pending = still;
                            if (pending.Count > 0) await Task.Delay(400, ct);
                        }
                        con.Send($"{run}forceload remove all");
                        con.WaitFor(l => l.Contains("no longer") || l.Contains("No chunks"), TimeSpan.FromSeconds(15), ct);
                        // Let the chunks just released be written out before the next batch asks for chunks beside them: a batch requested while its neighbours
                        // were still being unloaded was seen never to load.
                        con.Send("save-all flush");
                        con.WaitFor(l => l.Contains("Saved the game") || l.Contains("Saved the world"), TimeSpan.FromSeconds(60), ct);
                        n++; inThis++;
                        progress($"Pre-generating the {dim.Label}: batch {inThis} of {perDim}" + (dims.Count > 1 ? $" (area {d + 1} of {dims.Count})" : ""), 0.15 + 0.7 * n / total);
                    }
            }
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
