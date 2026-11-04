using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// The id lists of one game version: items, blocks, effects, entities and the rest, read from the jar's own
    /// reports/registries.json. Registries the game keeps as data rather than code (enchantments, dimensions and others
    /// since 1.21) are not in that report, so they are read from the data files inside the server jar itself. Nothing is
    /// curated: what the jar lists is what the builder offers.
    /// </summary>
    public sealed class McRegistries
    {
        private readonly Dictionary<string, List<string>> _lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public string Version { get; private set; }

        /// <summary>The ids of a registry, sorted, or null when this version's files do not have it.</summary>
        public IReadOnlyList<string> Get(string registry)
        {
            if (registry == null) return null;
            if (!registry.Contains(':')) registry = "minecraft:" + registry;
            return _lists.TryGetValue(registry, out var l) ? l : null;
        }

        public IEnumerable<string> Names => _lists.Keys;

        public void Set(string registry, IEnumerable<string> ids) => _lists[registry] = ids.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        public static string FileFor(string version) => Path.Combine(WorldGenerator.CacheRoot, version ?? "", "gen", "reports", "registries.json");
        private static string DataFileFor(string version) => Path.Combine(WorldGenerator.CacheRoot, version ?? "", "gen", "reports", "data-registries.json");

        /// <summary>Data-driven registries read from the jar's data folder: the registry name and its folder under data/minecraft.</summary>
        public static readonly (string Registry, string Folder)[] DataDriven =
        {
            ("minecraft:enchantment", "enchantment"), ("minecraft:damage_type", "damage_type"),
            ("minecraft:worldgen/biome", "worldgen/biome"), ("minecraft:trim_material", "trim_material"), ("minecraft:trim_pattern", "trim_pattern"),
            ("minecraft:jukebox_song", "jukebox_song"), ("minecraft:painting_variant", "painting_variant"), ("minecraft:instrument", "instrument"),
            ("minecraft:banner_pattern", "banner_pattern"), ("minecraft:wolf_variant", "wolf_variant"),
        };

        /// <summary>Parses a registries.json report: {"minecraft:item": {"entries": {"minecraft:air": {...}, ...}}, ...}.</summary>
        public static McRegistries Parse(string json, string version = null)
        {
            var r = new McRegistries { Version = version };
            using var doc = JsonDocument.Parse(json);
            foreach (var reg in doc.RootElement.EnumerateObject())
                if (reg.Value.ValueKind == JsonValueKind.Object && reg.Value.TryGetProperty("entries", out var e) && e.ValueKind == JsonValueKind.Object)
                    r.Set(reg.Name, e.EnumerateObject().Select(x => x.Name));
            return r;
        }

        /// <summary>Adds data-driven registries from a JSON object of registry name to id list.</summary>
        public void AddData(string json)
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var reg in doc.RootElement.EnumerateObject())
                if (reg.Value.ValueKind == JsonValueKind.Array) Set(reg.Name, reg.Value.EnumerateArray().Select(x => x.GetString()));
        }

        private static readonly Dictionary<string, McRegistries> Cache = new Dictionary<string, McRegistries>(StringComparer.Ordinal);

        /// <summary>Drops the cached lists of a version (used by the self-test when it writes its own sample files).</summary>
        public static void Forget(string version) { lock (Cache) Cache.Remove(version ?? ""); }

        /// <summary>The lists for a version from the cache the world generator fills, or null with the reason when they have not been read.</summary>
        public static async Task<(McRegistries Lists, string Unavailable)> LoadAsync(string version)
        {
            lock (Cache) if (Cache.TryGetValue(version ?? "", out var hit)) return (hit, null);
            var f = FileFor(version);
            if (!File.Exists(f)) return (null, "This version's item, block and effect lists have not been read yet.");
            try
            {
                var r = Parse(await File.ReadAllTextAsync(f), version);
                var data = DataFileFor(version);
                if (!File.Exists(data))
                {
                    var jar = Path.Combine(WorldGenerator.CacheRoot, version ?? "", "server.jar");
                    if (File.Exists(jar)) await Task.Run(() => WriteDataFromJar(jar, data));
                }
                if (File.Exists(data)) r.AddData(await File.ReadAllTextAsync(data));
                // Every world has these three; dimensions a data pack adds are typed by hand.
                if (r.Get("minecraft:dimension") == null) r.Set("minecraft:dimension", new[] { "minecraft:overworld", "minecraft:the_nether", "minecraft:the_end" });
                lock (Cache) Cache[version ?? ""] = r;
                return (r, null);
            }
            catch (Exception ex) { return (null, "The item and block lists had an unexpected shape: " + ex.Message); }
        }

        /// <summary>
        /// Reads the data-driven registries from a server jar: the ids are the file names under data/minecraft/&lt;folder&gt;/.
        /// A bundler jar (1.18 and later) keeps the real server jar inside META-INF/versions, which is opened in memory.
        /// </summary>
        public static void WriteDataFromJar(string jar, string output)
        {
            var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            void Read(ZipArchive z)
            {
                foreach (var e in z.Entries)
                {
                    if (!e.FullName.StartsWith("data/minecraft/", StringComparison.Ordinal) || !e.FullName.EndsWith(".json", StringComparison.Ordinal)) continue;
                    var rel = e.FullName.Substring("data/minecraft/".Length);
                    foreach (var (reg, folder) in DataDriven)
                    {
                        if (!rel.StartsWith(folder + "/", StringComparison.Ordinal)) continue;
                        var name = rel.Substring(folder.Length + 1);
                        if (name.Contains('/')) continue;
                        if (!found.TryGetValue(reg, out var l)) found[reg] = l = new List<string>();
                        l.Add("minecraft:" + name[..^5]);
                    }
                }
            }
            using (var outer = ZipFile.OpenRead(jar))
            {
                Read(outer);
                foreach (var inner in outer.Entries.Where(e => e.FullName.StartsWith("META-INF/versions/", StringComparison.Ordinal) && e.FullName.EndsWith(".jar", StringComparison.Ordinal)))
                {
                    using var ms = new MemoryStream();
                    using (var s = inner.Open()) s.CopyTo(ms);
                    ms.Position = 0;
                    using var z = new ZipArchive(ms, ZipArchiveMode.Read);
                    Read(z);
                }
            }
            File.WriteAllText(output, JsonSerializer.Serialize(found.ToDictionary(k => k.Key, k => k.Value.OrderBy(x => x, StringComparer.Ordinal).ToList())));
        }

        /// <summary>A readable name from an id: minecraft:diamond_sword becomes "Diamond sword", minecraft:mob_effect "Mob effect".</summary>
        public static string Readable(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var s = id.TrimStart('#');
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s.Substring(colon + 1);
            int slash = s.LastIndexOf('/');
            if (slash >= 0) s = s.Substring(slash + 1);
            s = s.Replace('_', ' ').Trim();
            return s.Length == 0 ? id : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s.Substring(1);
        }

        /// <summary>The registry an argument's values come from: its own registry property, or the one implied by its parser.</summary>
        public static string RegistryOf(McCommandNode n) => n?.Parser switch
        {
            "minecraft:item_stack" or "minecraft:item_predicate" => "minecraft:item",
            "minecraft:block_state" or "minecraft:block_predicate" => "minecraft:block",
            "minecraft:dimension" => "minecraft:dimension",
            _ => n?.Registry,
        };
    }
}
