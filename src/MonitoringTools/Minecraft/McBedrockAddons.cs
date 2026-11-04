using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MonitoringTools.Minecraft
{
    public sealed class McPack
    {
        /// <summary>The pack's folder inside the server folder, such as "behavior_packs/My pack".</summary>
        public string Folder = "";
        public bool Behavior;
        public string Name = "", Uuid = "", Version = "", Description = "";
        public int[] VersionParts = { 1, 0, 0 };
        public List<string> DependsOn = new List<string>();
        public bool Enabled, BuiltIn;
        public long Size;
        public string Problem = "";
        public string Kind => Behavior ? "Behavior pack" : "Resource pack";
    }

    /// <summary>
    /// Add-ons of a Bedrock Dedicated Server. A pack is a folder with a manifest.json inside behavior_packs or resource_packs;
    /// it is used by a world only when that world's world_behavior_packs.json or world_resource_packs.json names its id and
    /// version. A .mcpack is one pack as a ZIP, a .mcaddon is several packs in one ZIP. Everything works through
    /// <see cref="IMcFiles"/>, so the same code serves a folder on this computer, a Docker bind mount and an SSH host.
    /// Every change first copies what it touches into a dated folder under addons-backups, so the last change can be undone.
    /// </summary>
    public static class McBedrockAddons
    {
        public const string Behavior = "behavior_packs", Resource = "resource_packs", Backups = "addons-backups";
        private static readonly Regex BuiltInName = new Regex(@"^(vanilla|chemistry|editor|experimental|server_)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly JsonDocumentOptions Lenient = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        public static string WorldFile(string level, bool behavior) => $"worlds/{level}/{(behavior ? "world_behavior_packs.json" : "world_resource_packs.json")}";

        /// <summary>The packs in both pack folders, with whether the world uses each. Packs the server ships are marked built in.</summary>
        public static List<McPack> List(IMcFiles files, string level)
        {
            var list = new List<McPack>();
            var wanted = new List<string> { WorldFile(level, true), WorldFile(level, false) };
            var folders = new List<(string Rel, bool Behavior, McFileEntry E)>();
            foreach (var (root, beh) in new[] { (Behavior, true), (Resource, false) })
                foreach (var e in files.List(root).Where(e => e.IsDir)) { folders.Add((root + "/" + e.Name, beh, e)); wanted.Add(root + "/" + e.Name + "/manifest.json"); }
            var t = files.ReadMany(wanted);
            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var beh in new[] { true, false })
                if (t.TryGetValue(WorldFile(level, beh), out var wj))
                    foreach (var o in McPlayers.Entries(wj)) { var id = McPlayers.Str(o, "pack_id"); if (id != null) enabled.Add((beh ? "b:" : "r:") + id); }
            foreach (var (rel, beh, e) in folders)
            {
                var p = new McPack { Folder = rel, Behavior = beh, Name = e.Name, BuiltIn = BuiltInName.IsMatch(e.Name) };
                if (t.TryGetValue(rel + "/manifest.json", out var mf)) Fill(p, mf); else p.Problem = "No manifest.json in this folder.";
                p.Enabled = p.Uuid.Length > 0 && enabled.Contains((beh ? "b:" : "r:") + p.Uuid);
                list.Add(p);
            }
            return list.OrderBy(p => p.BuiltIn).ThenBy(p => !p.Behavior).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Reads a manifest.json (comments and trailing commas allowed, as the game allows them) into the pack.</summary>
        internal static void Fill(McPack p, string manifest)
        {
            try
            {
                using var doc = JsonDocument.Parse(manifest.TrimStart('﻿'), Lenient);
                var r = doc.RootElement;
                var h = r.GetProperty("header");
                var name = h.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (name.Length > 0 && !name.StartsWith("pack.", StringComparison.Ordinal)) p.Name = name;   // "pack.name" is a language key, not a name
                p.Uuid = h.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";
                p.Description = h.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String && !(d.GetString() ?? "").StartsWith("pack.", StringComparison.Ordinal) ? d.GetString() : "";
                if (h.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Array) { p.VersionParts = v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToArray(); p.Version = string.Join(".", p.VersionParts); }
                if (r.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
                    foreach (var dep in deps.EnumerateArray()) if (dep.TryGetProperty("uuid", out var du) && du.GetString() is string ds) p.DependsOn.Add(ds);
                if (p.Uuid.Length == 0) p.Problem = "The manifest has no header uuid.";
            }
            catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is FormatException)
            { p.Problem = "manifest.json could not be read: " + ex.Message; }
        }

        // ---- install

        /// <summary>Unpacks a .mcpack or .mcaddon (or a ZIP of either) into a folder, refusing any path outside it. Nested .mcpack files are unpacked once more.</summary>
        internal static void Unpack(string archive, string dest, int depth = 0)
        {
            Directory.CreateDirectory(dest);
            var full = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
            using var z = ZipFile.OpenRead(archive);
            if (z.Entries.Count > 20000) throw new InvalidDataException("That archive holds too many files to be an add-on.");
            long total = 0;
            foreach (var e in z.Entries)
            {
                if (e.Name.Length == 0) continue;
                var to = Path.GetFullPath(Path.Combine(dest, e.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!to.StartsWith(full, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The archive has a path outside its own folder, so it was refused.");
                total += e.Length;
                if (total > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("That archive unpacks to more than 2 GB, so it was refused.");
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                e.ExtractToFile(to, true);
            }
            if (depth == 0)
                foreach (var inner in Directory.EnumerateFiles(dest, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    Unpack(inner, Path.Combine(Path.GetDirectoryName(inner), Path.GetFileNameWithoutExtension(inner)), 1);
                    File.Delete(inner);
                }
        }

        /// <summary>The folders inside an unpacked add-on that hold a manifest.json, shallowest first, never nesting one pack inside another.</summary>
        internal static List<string> FindPacks(string root)
        {
            var found = new List<string>();
            foreach (var m in Directory.EnumerateFiles(root, "manifest.json", SearchOption.AllDirectories).OrderBy(f => f.Length))
            {
                var dir = Path.GetDirectoryName(m);
                if (found.Any(f => dir.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
                found.Add(dir);
            }
            return found;
        }

        /// <summary>Behavior for modules that run in the game's logic, resource for modules that only change what players see; null for skin packs, world templates and anything else.</summary>
        internal static bool? KindOf(string manifest)
        {
            try
            {
                using var doc = JsonDocument.Parse(manifest.TrimStart('﻿'), Lenient);
                if (!doc.RootElement.TryGetProperty("modules", out var mods) || mods.ValueKind != JsonValueKind.Array) return null;
                var types = mods.EnumerateArray().Select(m => m.TryGetProperty("type", out var t) ? t.GetString() : null).ToList();
                if (types.Any(t => t is "data" or "script" or "javascript" or "gametest" or "client_data" or "interface")) return true;
                if (types.Any(t => t == "resources")) return false;
            }
            catch (JsonException) { }
            return null;
        }

        private static string FolderName(string name, string uuid)
        {
            var s = Regex.Replace(name ?? "", @"[^A-Za-z0-9 _.-]", "").Trim(' ', '.');
            return s.Length == 0 ? (uuid.Length >= 8 ? uuid[..8] : "pack") : (s.Length > 48 ? s[..48].TrimEnd() : s);
        }

        public sealed class InstallResult { public List<string> Installed = new List<string>(), Skipped = new List<string>(); public bool NeedsWorld; }

        /// <summary>
        /// Installs the packs in a .mcpack or .mcaddon. When the world already exists and <paramref name="enable"/> is set, each
        /// pack is also switched on for it (with the packs it depends on). A pack with the same id as an installed one replaces it.
        /// </summary>
        public static InstallResult Install(IMcFiles files, string archive, string level, bool enable)
        {
            var stage = Path.Combine(Path.GetTempPath(), "mt-addon-" + Guid.NewGuid().ToString("N")[..8]);
            var res = new InstallResult();
            try
            {
                Unpack(archive, stage);
                var packs = FindPacks(stage);
                if (packs.Count == 0) throw new InvalidDataException("That file holds no pack: there is no manifest.json in it.");
                var existing = List(files, level);
                var undo = new Backup(files);
                var toEnable = new List<McPack>();
                foreach (var dir in packs)
                {
                    var manifest = File.ReadAllText(Path.Combine(dir, "manifest.json"));
                    var p = new McPack(); Fill(p, manifest);
                    if (p.Problem.Length > 0) { res.Skipped.Add(Path.GetFileName(dir) + ": " + p.Problem); continue; }
                    var beh = KindOf(manifest);
                    if (beh == null) { res.Skipped.Add(p.Name + ": skin packs and world templates are not server add-ons"); continue; }
                    p.Behavior = beh.Value;
                    var root = p.Behavior ? Behavior : Resource;
                    var same = existing.FirstOrDefault(x => x.Behavior == p.Behavior && x.Uuid.Equals(p.Uuid, StringComparison.OrdinalIgnoreCase));
                    string rel;
                    if (same != null) { rel = same.Folder; undo.Keep(rel); files.Delete(rel); }
                    else
                    {
                        var name = FolderName(p.Name, p.Uuid);
                        rel = root + "/" + name;
                        if (files.Exists(rel)) rel = root + "/" + name + "-" + p.Uuid[..Math.Min(8, p.Uuid.Length)];
                        undo.Added(rel);
                    }
                    files.UploadDir(dir, rel);
                    p.Folder = rel;
                    res.Installed.Add(p.Name + (p.Version.Length > 0 ? " " + p.Version : ""));
                    toEnable.Add(p);
                }
                if (enable && toEnable.Count > 0)
                {
                    if (!files.Exists("worlds/" + level)) res.NeedsWorld = true;
                    else
                    {
                        undo.Keep(WorldFile(level, true)); undo.Keep(WorldFile(level, false));
                        SetEnabled(files, level, toEnable, true, List(files, level));
                    }
                }
                undo.Finish();
                return res;
            }
            finally { try { Directory.Delete(stage, true); } catch { } }
        }

        // ---- switch, remove, undo

        /// <summary>Names a pack (and, when switching on, the installed packs it depends on) in the world's pack files, or takes them out.</summary>
        public static void SetEnabled(IMcFiles files, string level, IEnumerable<McPack> packs, bool on, List<McPack> all = null)
        {
            if (!files.Exists("worlds/" + level)) throw new InvalidOperationException("The world does not exist yet. Start the server once so it makes its world, then switch the add-on on.");
            all ??= List(files, level);
            var chosen = new List<McPack>(packs);
            if (on)
                for (int i = 0; i < chosen.Count; i++)
                    foreach (var dep in chosen[i].DependsOn)
                    {
                        var d = all.FirstOrDefault(x => x.Uuid.Equals(dep, StringComparison.OrdinalIgnoreCase));
                        if (d != null && !chosen.Any(c => c.Uuid.Equals(d.Uuid, StringComparison.OrdinalIgnoreCase))) chosen.Add(d);
                    }
            foreach (var beh in new[] { true, false })
            {
                var mine = chosen.Where(c => c.Behavior == beh).ToList();
                if (mine.Count == 0) continue;
                var file = WorldFile(level, beh);
                var a = McPlayers.Load(files.ReadText(file));
                foreach (var p in mine)
                {
                    for (int k = a.Count - 1; k >= 0; k--) if (McPlayers.Str((JsonObject)a[k], "pack_id")?.Equals(p.Uuid, StringComparison.OrdinalIgnoreCase) == true) a.RemoveAt(k);
                    if (on) a.Add(new JsonObject { ["pack_id"] = p.Uuid, ["version"] = new JsonArray(p.VersionParts.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) });
                }
                files.WriteText(file, McPlayers.Store(a));
            }
        }

        /// <summary>Switches one pack on or off after saving a copy of the world's two pack files.</summary>
        public static void Switch(IMcFiles files, string level, McPack p, bool on)
        {
            var undo = new Backup(files);
            undo.Keep(WorldFile(level, true)); undo.Keep(WorldFile(level, false));
            SetEnabled(files, level, new[] { p }, on);
            undo.Finish();
        }

        /// <summary>Removes a pack's folder and its entry in the world's pack files, after saving copies of both.</summary>
        public static void Remove(IMcFiles files, string level, McPack p)
        {
            if (p.BuiltIn) throw new InvalidOperationException("This pack ships with the server and is not removed from here.");
            var undo = new Backup(files);
            undo.Keep(p.Folder); undo.Keep(WorldFile(level, true)); undo.Keep(WorldFile(level, false));
            if (files.Exists("worlds/" + level)) SetEnabled(files, level, new[] { p }, false);
            files.Delete(p.Folder);
            undo.Finish();
        }

        public static string LatestBackup(IMcFiles files) =>
            files.List(Backups).Where(e => e.IsDir).Select(e => e.Name).OrderByDescending(n => n, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>Puts back what the newest add-on backup saved and removes what that change added. The state it replaces is saved first, so undo can be undone.</summary>
        public static string Undo(IMcFiles files)
        {
            var name = LatestBackup(files) ?? throw new InvalidOperationException("There is no add-on change to undo yet.");
            var dir = Backups + "/" + name;
            var plan = files.ReadText(dir + "/changes.txt") ?? "";
            var redo = new Backup(files);
            var lines = plan.Replace("\r", "").Split('\n').Where(l => l.Length > 0).ToList();
            foreach (var l in lines)
            {
                var rel = l[(l.IndexOf(' ') + 1)..];
                if (l.StartsWith("restore ", StringComparison.Ordinal) || files.Exists(rel)) redo.Keep(rel);   // what is there now can be put back by undoing the undo
            }
            foreach (var l in lines)
            {
                var rel = l[(l.IndexOf(' ') + 1)..];
                if (l.StartsWith("remove ", StringComparison.Ordinal)) { if (files.Exists(rel)) files.Delete(rel); }
                else
                {
                    if (files.Exists(rel)) files.Delete(rel);
                    if (files.Exists(dir + "/data/" + rel)) files.Copy(dir + "/data/" + rel, rel);
                }
            }
            redo.Finish();
            files.Delete(dir);
            return name;
        }

        /// <summary>A dated copy of what one add-on change touches, with a plan telling Undo what to put back and what to remove.</summary>
        internal sealed class Backup
        {
            private readonly IMcFiles _f;
            private readonly string _dir = Backups + "/" + McFiles.Stamp();
            private readonly List<string> _plan = new List<string>();
            public Backup(IMcFiles f) { _f = f; }
            /// <summary>Saves what is at <paramref name="rel"/> now so it can be put back; a path that is not there yet is recorded as one to remove.</summary>
            public void Keep(string rel)
            {
                if (_plan.Contains("restore " + rel) || _plan.Contains("remove " + rel)) return;
                if (_f.Exists(rel)) { _f.Copy(rel, _dir + "/data/" + rel); _plan.Add("restore " + rel); }
                else _plan.Add("remove " + rel);
            }
            public void Added(string rel) { if (!_plan.Contains("remove " + rel)) _plan.Add("remove " + rel); }
            public void Finish()
            {
                if (_plan.Count == 0) return;
                _f.WriteText(_dir + "/changes.txt", string.Join("\n", _plan) + "\n");
            }
        }
    }
}
