using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    public enum WorldgenKind { NoiseSettings, DimensionType }

    /// <summary>One value the user changed: which file (kind and id), which value inside it, and the new value as text.</summary>
    public sealed class WorldgenEdit
    {
        public WorldgenKind Kind;
        public string Id = "minecraft:overworld";
        /// <summary>Dotted path inside the file's JSON, array items by number, for example noise.height or default_block.Name.</summary>
        public string Path = "";
        public string Value = "";
    }

    /// <summary>A single editable value found in a default file: its path, its JSON type and the default as text.</summary>
    public sealed class WorldgenLeaf
    {
        public string Path, Type, Default;
    }

    /// <summary>A finished data pack file: the jar's own default file with only the user's values replaced.</summary>
    public sealed class WorldgenPackFile
    {
        public WorldgenKind Kind;
        public string Id, Json;
    }

    /// <summary>The default worldgen files of one version, kept in memory: resource path to JSON text.</summary>
    public sealed class WorldgenSource
    {
        public string Origin = "";
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal);

        public static WorldgenSource FromFiles(IDictionary<string, string> files, string origin)
        {
            var s = new WorldgenSource { Origin = origin };
            foreach (var kv in files) s.Files[kv.Key] = kv.Value;
            return s;
        }

        /// <summary>Ids such as minecraft:overworld that this source has a default file for, sorted.</summary>
        public List<string> Ids(WorldgenKind kind)
        {
            var found = new List<string>();
            foreach (var path in Files.Keys)
            {
                var id = WorldgenEditor.IdFromResource(kind, path);
                if (id != null) found.Add(id);
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        public string Read(WorldgenKind kind, string id) =>
            Files.TryGetValue(WorldgenEditor.ResourcePath(kind, id), out var t) ? t : null;
    }

    /// <summary>
    /// Reads a version's default noise settings and dimension types, edits single values and writes a data pack that
    /// carries only the files that changed. A data pack file replaces a whole registry entry, so each written file is
    /// the jar's own default with just your values different. The jar publishes no schema, so validation checks the type
    /// of every value against the jar's own default plus a small set of documented world height limits.
    /// </summary>
    public static class WorldgenEditor
    {
        public const string PackFolder = "monitoring-tools-worldgen-edits";

        // -------------------------------------------------------------- paths and ids

        private static string Folder(WorldgenKind kind) => kind == WorldgenKind.NoiseSettings ? "worldgen/noise_settings/" : "dimension_type/";

        public static string KindLabel(WorldgenKind kind) => kind == WorldgenKind.NoiseSettings ? "noise settings" : "dimension type";

        /// <summary>The path of a default file inside a jar or data pack, for example data/minecraft/worldgen/noise_settings/overworld.json.</summary>
        public static string ResourcePath(WorldgenKind kind, string id)
        {
            var (ns, name) = SplitId(id);
            return "data/" + ns + "/" + Folder(kind) + name + ".json";
        }

        public static (string Ns, string Name) SplitId(string id)
        {
            var i = (id ?? "").IndexOf(':');
            return i < 0 ? ("minecraft", id) : (id[..i], id[(i + 1)..]);
        }

        public static readonly Regex IdPattern = new Regex(@"^[a-z0-9_.\-]+:[a-z0-9_./\-]+$", RegexOptions.Compiled);

        /// <summary>The id a resource path stands for, or null when the path is not a default file of that kind.</summary>
        public static string IdFromResource(WorldgenKind kind, string path)
        {
            if (!path.StartsWith("data/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal)) return null;
            var rest = path[5..];
            var slash = rest.IndexOf('/');
            if (slash <= 0) return null;
            var ns = rest[..slash];
            var tail = rest[(slash + 1)..^5];
            var f = Folder(kind);
            if (!tail.StartsWith(f, StringComparison.Ordinal)) return null;
            var name = tail[f.Length..];
            return name.Length == 0 || name.Contains("..") ? null : ns + ":" + name;
        }

        // -------------------------------------------------------------- reading the jar

        private static readonly string[] Wanted = { "worldgen/noise_settings/", "dimension_type/" };

        /// <summary>Reads the default noise settings and dimension type files out of a server jar (looking inside a bundler jar's nested jar).</summary>
        public static WorldgenSource ReadFromJar(string jar)
        {
            var src = new WorldgenSource { Origin = "the server jar" };
            using var z = WorldGenerator.OpenDataZip(jar);
            foreach (var e in z.Entries)
            {
                if (!e.FullName.StartsWith("data/", StringComparison.Ordinal) || !e.FullName.EndsWith(".json", StringComparison.Ordinal)) continue;
                if (!Wanted.Any(w => e.FullName.IndexOf("/" + w, StringComparison.Ordinal) > 0)) continue;
                if (IdFromResource(WorldgenKind.NoiseSettings, e.FullName) == null && IdFromResource(WorldgenKind.DimensionType, e.FullName) == null) continue;
                using var rd = new StreamReader(e.Open());
                src.Files[e.FullName] = rd.ReadToEnd();
            }
            return src;
        }

        /// <summary>Reads the same files from the output folder of the jar's data generator.</summary>
        public static WorldgenSource ReadFromDirectory(string generatedRoot)
        {
            var src = new WorldgenSource { Origin = "the data generator's output" };
            var data = Path.Combine(generatedRoot, "data");
            if (!Directory.Exists(data)) return src;
            foreach (var f in Directory.EnumerateFiles(data, "*.json", SearchOption.AllDirectories))
            {
                var rel = "data/" + Path.GetRelativePath(data, f).Replace('\\', '/');
                if (IdFromResource(WorldgenKind.NoiseSettings, rel) == null && IdFromResource(WorldgenKind.DimensionType, rel) == null) continue;
                src.Files[rel] = File.ReadAllText(f);
            }
            return src;
        }

        /// <summary>
        /// The default worldgen files for a version. The jar is read first; when it carries none (newer versions build them in code),
        /// the jar's own data generator is run once without starting a server and its output is cached. Throws with a plain reason when neither works.
        /// </summary>
        public static async Task<WorldgenSource> OpenAsync(string version, string jarOverride, Action<string> report)
        {
            var (cachedJar, javaMajor) = await WorldGenerator.EnsureJarAsync(version, report);
            var jar = jarOverride ?? cachedJar;
            var src = await Task.Run(() => ReadFromJar(jar));
            if (src.Ids(WorldgenKind.NoiseSettings).Count > 0 || src.Ids(WorldgenKind.DimensionType).Count > 0) return src;

            var gen = Path.Combine(WorldGenerator.CacheRoot, version, "gen-data");
            var fromGen = await Task.Run(() => ReadFromDirectory(gen));
            if (fromGen.Files.Count == 0)
            {
                var java = await ServerManager.JavaAsync(javaMajor, report);
                report?.Invoke("Asking the jar's data generator for its worldgen files…");
                var args = WorldGenerator.IsBundler(jar)
                    ? $"-DbundlerMainClass=net.minecraft.data.Main -jar \"{jar}\" --server --output \"{gen}\""
                    : $"-cp \"{jar}\" net.minecraft.data.Main --server --output \"{gen}\"";
                var r = await Task.Run(() => ServerManager.Run(java, args, 300000, workingDir: Path.Combine(WorldGenerator.CacheRoot, version, "run")));
                fromGen = await Task.Run(() => ReadFromDirectory(gen));
                if (fromGen.Files.Count == 0)
                    throw new InvalidOperationException("Neither this version's jar nor its data generator produced noise settings or dimension type files" + (r.Code != 0 ? " (the generator exited with code " + r.Code + ")" : "") + ", so they cannot be edited here.");
            }
            return fromGen;
        }

        // -------------------------------------------------------------- the editable values

        /// <summary>Subtrees too large or too intricate to edit as single values; they stay exactly as the jar defines them.</summary>
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.Ordinal) { "surface_rule", "noise_router", "spawn_target" };
        private const int MaxDepth = 6;

        public static List<WorldgenLeaf> Leaves(string json)
        {
            var list = new List<WorldgenLeaf>();
            var root = JsonNode.Parse(json);
            Walk(root, "", 0, list);
            return list;
        }

        private static void Walk(JsonNode node, string path, int depth, List<WorldgenLeaf> into)
        {
            if (node == null || depth > MaxDepth) return;
            if (node is JsonObject obj)
            {
                foreach (var kv in obj)
                {
                    if (kv.Key.Contains('.') || (depth == 0 && Excluded.Contains(kv.Key))) continue;
                    Walk(kv.Value, path.Length == 0 ? kv.Key : path + "." + kv.Key, depth + 1, into);
                }
            }
            else if (node is JsonArray arr)
            {
                if (arr.Count > 16) return;
                for (int i = 0; i < arr.Count; i++) Walk(arr[i], path + "." + i, depth + 1, into);
            }
            else if (node is JsonValue v)
            {
                var kind = v.GetValueKind();
                string type = kind switch
                {
                    JsonValueKind.True or JsonValueKind.False => "boolean",
                    JsonValueKind.String => "string",
                    JsonValueKind.Number => IsIntegerText(v.ToJsonString()) ? "integer" : "number",
                    _ => null
                };
                if (type == null || path.Length == 0) return;
                into.Add(new WorldgenLeaf { Path = path, Type = type, Default = kind == JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString() });
            }
        }

        private static bool IsIntegerText(string t) => t.IndexOfAny(new[] { '.', 'e', 'E' }) < 0;

        /// <summary>Turns the text a user typed into a JSON value of the leaf's type, or explains why not.</summary>
        public static (JsonNode Node, string Error) ParseValue(WorldgenLeaf leaf, string text)
        {
            text = (text ?? "").Trim();
            switch (leaf.Type)
            {
                case "integer":
                    return long.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var l)
                        ? (JsonValue.Create(l), null) : (null, $"{leaf.Path} must be a whole number.");
                case "number":
                    return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d) && !double.IsInfinity(d)
                        ? (JsonValue.Create(d), null) : (null, $"{leaf.Path} must be a number (use a dot for decimals).");
                case "boolean":
                    return text == "true" ? (JsonValue.Create(true), null) : text == "false" ? (JsonValue.Create(false), null) : (null, $"{leaf.Path} must be true or false.");
                default:
                    return text.Length > 0 ? (JsonValue.Create(text), null) : (null, $"{leaf.Path} cannot be empty.");
            }
        }

        // -------------------------------------------------------------- validation

        private static double? Num(JsonNode root, string path)
        {
            var n = Get(root, path);
            if (n is JsonValue v && v.GetValueKind() == JsonValueKind.Number && double.TryParse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return null;
        }

        private static JsonNode Get(JsonNode root, string path)
        {
            var cur = root;
            foreach (var seg in path.Split('.'))
            {
                if (cur is JsonObject o) cur = o.TryGetPropertyValue(seg, out var n) ? n : null;
                else if (cur is JsonArray a && int.TryParse(seg, out var i) && i >= 0 && i < a.Count) cur = a[i];
                else return null;
                if (cur == null) return null;
            }
            return cur;
        }

        private static bool Set(JsonNode root, string path, JsonNode value)
        {
            var segs = path.Split('.');
            var cur = root;
            for (int i = 0; i < segs.Length - 1; i++)
            {
                if (cur is JsonObject o) cur = o.TryGetPropertyValue(segs[i], out var n) ? n : null;
                else if (cur is JsonArray a && int.TryParse(segs[i], out var ix) && ix >= 0 && ix < a.Count) cur = a[ix];
                else return false;
                if (cur == null) return false;
            }
            var last = segs[^1];
            if (cur is JsonObject po && po.ContainsKey(last)) { po[last] = value; return true; }
            if (cur is JsonArray pa && int.TryParse(last, out var li) && li >= 0 && li < pa.Count) { pa[li] = value; return true; }
            return false;
        }

        private static void Range(List<string> bad, JsonNode root, string path, double min, double max, int multiple = 0)
        {
            var v = Num(root, path);
            if (v == null) return;
            if (v < min || v > max) bad.Add($"{path} must be between {min:0.#####} and {max:0.#####} (it is {v}).");
            else if (multiple > 0 && Math.Abs(v.Value % multiple) > 1e-9) bad.Add($"{path} must be a multiple of {multiple} (it is {v}).");
        }

        /// <summary>
        /// Checks changed values for one file. Each value must exist in the jar's own default and have its type; block ids are checked against the
        /// jar's block list when it is known; and the world height limits that the game documents for 1.18 and newer are applied to the result.
        /// </summary>
        public static List<string> Validate(WorldgenKind kind, string defaultJson, IReadOnlyDictionary<string, string> changes, ISet<string> knownBlocks)
        {
            var bad = new List<string>();
            JsonNode root;
            List<WorldgenLeaf> leaves;
            try { root = JsonNode.Parse(defaultJson); leaves = Leaves(defaultJson); }
            catch (Exception ex) { return new List<string> { "The default file could not be read as JSON: " + ex.Message }; }
            var byPath = leaves.ToDictionary(l => l.Path, StringComparer.Ordinal);
            foreach (var (path, text) in changes)
            {
                if (!byPath.TryGetValue(path, out var leaf)) { bad.Add($"{path} is not an editable value in this version's file."); continue; }
                var (node, err) = ParseValue(leaf, text);
                if (err != null) { bad.Add(err); continue; }
                if (knownBlocks != null && knownBlocks.Count > 0 && (path == "default_block.Name" || path == "default_fluid.Name"))
                {
                    var id = text.Trim();
                    if (!IdPattern.IsMatch(id)) bad.Add($"{path}: “{id}” is not a namespaced id such as minecraft:stone.");
                    else if (!knownBlocks.Contains(id)) bad.Add($"{path}: {id} is not a block in this version's jar.");
                }
                Set(root, path, node);
            }
            if (bad.Count > 0) return bad;
            if (kind == WorldgenKind.DimensionType)
            {
                Range(bad, root, "min_y", -2032, 2031, 16);
                Range(bad, root, "height", 16, 4064, 16);
                Range(bad, root, "logical_height", 0, 4064);
                Range(bad, root, "coordinate_scale", 1e-5, 3e7);
                var miny = Num(root, "min_y"); var h = Num(root, "height"); var lh = Num(root, "logical_height");
                if (miny != null && h != null && miny + h > 2032) bad.Add("min_y plus height must not go above 2032.");
                if (lh != null && h != null && lh > h) bad.Add("logical_height cannot be taller than height.");
            }
            else
            {
                Range(bad, root, "noise.min_y", -2032, 2031, 16);
                Range(bad, root, "noise.height", 0, 4064, 16);
                Range(bad, root, "noise.size_horizontal", 1, 4);
                Range(bad, root, "noise.size_vertical", 1, 4);
                var miny = Num(root, "noise.min_y"); var h = Num(root, "noise.height");
                if (miny != null && h != null && miny + h > 2032) bad.Add("noise.min_y plus noise.height must not go above 2032.");
            }
            return bad;
        }

        // -------------------------------------------------------------- building the pack

        private static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>The default file with the given values replaced, as indented JSON. Values are not validated here; call Validate first.</summary>
        public static string ApplyChanges(string defaultJson, IReadOnlyDictionary<string, string> changes)
        {
            var root = JsonNode.Parse(defaultJson);
            var byPath = Leaves(defaultJson).ToDictionary(l => l.Path, StringComparer.Ordinal);
            foreach (var (path, text) in changes)
            {
                if (!byPath.TryGetValue(path, out var leaf)) throw new InvalidOperationException(path + " is not an editable value.");
                var (node, err) = ParseValue(leaf, text);
                if (err != null) throw new InvalidOperationException(err);
                Set(root, path, node);
            }
            return root.ToJsonString(Pretty);
        }

        /// <summary>Groups edits by file, validates each group against its default and returns the files to write. Throws with every problem listed when anything is invalid.</summary>
        public static List<WorldgenPackFile> BuildFiles(WorldgenSource source, IEnumerable<WorldgenEdit> edits, ISet<string> knownBlocks)
        {
            var files = new List<WorldgenPackFile>();
            var problems = new List<string>();
            foreach (var g in edits.GroupBy(e => (e.Kind, e.Id)))
            {
                var def = source.Read(g.Key.Kind, g.Key.Id);
                if (def == null) { problems.Add($"{KindLabel(g.Key.Kind)} {g.Key.Id}: this version has no default file for it."); continue; }
                var changes = g.GroupBy(e => e.Path).ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.Ordinal);
                var bad = Validate(g.Key.Kind, def, changes, knownBlocks);
                if (bad.Count > 0) { problems.AddRange(bad.Select(b => $"{KindLabel(g.Key.Kind)} {g.Key.Id}: {b}")); continue; }
                files.Add(new WorldgenPackFile { Kind = g.Key.Kind, Id = g.Key.Id, Json = ApplyChanges(def, changes) });
            }
            if (problems.Count > 0) throw new InvalidOperationException("The worldgen changes are not valid: " + string.Join(" ", problems.Take(4)) + (problems.Count > 4 ? $" …and {problems.Count - 4} more." : ""));
            return files;
        }

        /// <summary>Writes pack.mcmeta and one file per changed registry entry into datapacksDir. Returns the files written.</summary>
        public static List<string> WritePack(string datapacksDir, int packFormat, IEnumerable<WorldgenPackFile> files)
        {
            var written = new List<string>();
            var pack = Path.Combine(datapacksDir, PackFolder);
            Directory.CreateDirectory(pack);
            File.WriteAllText(Path.Combine(pack, "pack.mcmeta"),
                new JsonObject { ["pack"] = new JsonObject { ["pack_format"] = packFormat, ["description"] = "Worldgen values changed in Monitoring Tools" } }.ToJsonString());
            foreach (var f in files)
            {
                var rel = ResourcePath(f.Kind, f.Id).Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(pack, rel));
                if (!full.StartsWith(Path.GetFullPath(pack) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to write outside the data pack: " + f.Id);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, f.Json, new UTF8Encoding(false));
                written.Add(full);
            }
            return written;
        }
    }
}
