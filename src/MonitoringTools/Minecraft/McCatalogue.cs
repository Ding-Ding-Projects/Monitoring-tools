using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    public enum McCompat { Compatible, Update, MayNotWork, Unknown }

    /// <summary>What the catalogues say about one installed jar: whether it declares this game version, and a newer compatible file.</summary>
    public sealed class McCompatInfo
    {
        public McCompat Kind = McCompat.Unknown;
        public string Source = "", Current = "", Newest = "";
        /// <summary>Installs the newer file in place of the old one; null when there is nothing newer.</summary>
        internal Func<Action<string>, Task<string>> Apply;

        public string Label => Kind switch { McCompat.Compatible => "Compatible", McCompat.Update => "Update", McCompat.MayNotWork => "May not work", _ => "Not in a catalogue" };

        public string Explain(string version) => Kind switch
        {
            McCompat.Compatible => $"{Source} lists {Current} for {version}.",
            McCompat.Update => $"{Source} has {Newest} for {version}; this is {Current}.",
            McCompat.MayNotWork => $"{Source} does not list {version} for this file, and has no file for {version}.",
            _ => "This jar is not in Modrinth, and it was not installed from Hangar by this app.",
        };
    }

    /// <summary>
    /// Ties installed plugins and mods to the catalogue they came from. Modrinth recognises any of its files by its SHA-512,
    /// so even a jar copied in by hand is found; a Hangar install is recorded when this app makes it, because Hangar has no
    /// lookup by file. From that, each jar gets a compatibility badge and, when a newer compatible file exists, an update.
    /// </summary>
    public static class McCatalogue
    {
        public sealed class Entry
        {
            public string File { get; set; } = "";
            public string Source { get; set; } = "";
            public string ProjectId { get; set; } = "";
            public string Title { get; set; } = "";
            public string VersionId { get; set; } = "";
            public string VersionName { get; set; } = "";
            public List<string> GameVersions { get; set; } = new List<string>();
        }

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp() { var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0 (server manager)"); return h; }

        /// <summary>The last check for each server, by file name, so the Plugins section can show badges without asking again.</summary>
        internal static readonly Dictionary<string, Dictionary<string, McCompatInfo>> Last = new Dictionary<string, Dictionary<string, McCompatInfo>>();

        private static string RecordFile(ServerDef s) => Path.Combine(ServerManager.Root, "catalogue", s.Id + ".json");

        public static List<Entry> Records(ServerDef s)
        {
            try { var f = RecordFile(s); return File.Exists(f) ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(f)) ?? new List<Entry>() : new List<Entry>(); }
            catch { return new List<Entry>(); }
        }

        internal static void Record(ServerDef s, Entry e)
        {
            var list = Records(s).Where(x => !string.Equals(x.File, e.File, StringComparison.OrdinalIgnoreCase) && !(x.Source == e.Source && x.ProjectId == e.ProjectId)).ToList();
            list.Add(e);
            var f = RecordFile(s);
            Directory.CreateDirectory(Path.GetDirectoryName(f));
            File.WriteAllText(f, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>The badge for one jar from what the catalogue declares. Pure, so the self-test can check every case.</summary>
        public static McCompat Decide(string serverVersion, IEnumerable<string> declared, string currentId, string newestCompatibleId)
        {
            if (!string.IsNullOrEmpty(newestCompatibleId) && !string.Equals(newestCompatibleId, currentId, StringComparison.Ordinal)) return McCompat.Update;
            if (declared != null && declared.Contains(serverVersion)) return McCompat.Compatible;
            return McCompat.MayNotWork;
        }

        /// <summary>Copies a verified jar in (the folder is backed up first) and removes the file it replaces, keeping it off if it was off.</summary>
        internal static void Replace(string folder, string verifiedJar, string fileName, string replaces)
        {
            bool off = replaces != null && replaces.EndsWith(McContent.DisabledSuffix, StringComparison.OrdinalIgnoreCase);
            var to = McContent.Install(folder, verifiedJar, fileName);
            if (replaces != null && File.Exists(replaces) && !string.Equals(Path.GetFullPath(replaces), Path.GetFullPath(to), StringComparison.OrdinalIgnoreCase)) File.Delete(replaces);
            if (off && File.Exists(to)) File.Move(to, to + McContent.DisabledSuffix, true);
        }

        /// <summary>
        /// Like <see cref="Replace"/>, but for a server on an SSH host the verified jar is copied to the host's folder with scp, after
        /// the host's folder was copied to a dated backup beside it (which is what Undo puts back). The old file is removed there.
        /// </summary>
        internal static void ReplaceFor(ServerDef s, string folder, string verifiedJar, string fileName, string replaces)
        {
            if (!s.IsSsh) { Replace(folder, verifiedJar, fileName, replaces); return; }
            var files = McFiles.For(s);
            bool off = replaces != null && replaces.EndsWith(McContent.DisabledSuffix, StringComparison.OrdinalIgnoreCase);
            var to = McContent.Install(files, s.ContentFolder, verifiedJar, fileName);
            if (replaces != null && !string.Equals(replaces, to, StringComparison.Ordinal) && files.Exists(replaces)) files.Delete(replaces);
            if (off && files.Exists(to) && !files.Exists(to + McContent.DisabledSuffix)) files.Move(to, to + McContent.DisabledSuffix);
        }

        // ------------------------------------------------------------------ checking

        public static async Task<Dictionary<string, McCompatInfo>> CheckAsync(ServerDef s)
        {
            var folder = McContent.FolderOf(s);
            var remote = s.IsSsh ? McFiles.For(s) : null;
            var addons = remote != null ? await Task.Run(() => McContent.List(remote, s.ContentFolder)) : McContent.List(folder);
            var result = new Dictionary<string, McCompatInfo>(StringComparer.OrdinalIgnoreCase);
            if (addons.Count == 0) { Last[s.Id] = result; return result; }

            // ---- Modrinth: every jar by its SHA-512 (computed on the host for a server over SSH, so no jar is downloaded)
            var remoteHashes = remote != null ? await Task.Run(() => remote.Sha512(addons.Select(a => a.File))) : null;
            var hashes = addons.Where(a => remote == null || remoteHashes.ContainsKey(a.File)).ToDictionary(a => a.File, a => remote != null ? remoteHashes[a.File] : Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(a.File))).ToLowerInvariant());
            addons = addons.Where(a => hashes.ContainsKey(a.File)).ToList();
            var loaders = McModrinth.Loaders(s.Flavour);
            JsonElement current = default, newest = default;
            try
            {
                current = await Post($"{McModrinth.Api}/version_files", new { hashes = hashes.Values.ToArray(), algorithm = "sha512" });
                newest = await Post($"{McModrinth.Api}/version_files/update", new { hashes = hashes.Values.ToArray(), algorithm = "sha512", loaders, game_versions = new[] { s.Version } });
            }
            catch (Exception ex) { Core.Log.Error("Modrinth lookup", ex); }
            var records = Records(s);
            foreach (var a in addons)
            {
                var name = Path.GetFileName(a.File);
                var h = hashes[a.File];
                var info = new McCompatInfo();
                // A file this app installed from Hangar is checked against Hangar, even when Modrinth also carries it.
                if (records.FirstOrDefault(r => r.Source == "Hangar" && string.Equals(r.File, name, StringComparison.OrdinalIgnoreCase)) is Entry rec)
                {
                    info.Source = "Hangar";
                    info.Current = rec.VersionName;
                    McHangar.HangarVersion pick = null;
                    try { pick = McHangar.Newest(await McHangar.VersionsAsync(rec.ProjectId, s.Version)); } catch (Exception ex) { Core.Log.Error("Hangar lookup", ex); }
                    info.Newest = pick?.Name ?? "";
                    info.Kind = Decide(s.Version, rec.GameVersions, rec.VersionId, pick?.Name);
                    if (info.Kind == McCompat.Update)
                    {
                        var hit = new McCatalogHit { Source = "Hangar", ProjectId = rec.ProjectId, Title = rec.Title };
                        var file = a.File;
                        info.Apply = report => McHangar.InstallAsync(hit, s, report, pick, file);
                    }
                }
                else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(h, out var cur))
                {
                    info.Source = "Modrinth";
                    info.Current = cur.GetProperty("version_number").GetString();
                    var declared = cur.GetProperty("game_versions").EnumerateArray().Select(x => x.GetString()).ToList();
                    string newId = null;
                    JsonElement nv = default;
                    if (newest.ValueKind == JsonValueKind.Object && newest.TryGetProperty(h, out nv)) { newId = nv.GetProperty("id").GetString(); info.Newest = nv.GetProperty("version_number").GetString(); }
                    info.Kind = Decide(s.Version, declared, cur.GetProperty("id").GetString(), newId);
                    if (info.Kind == McCompat.Update)
                    {
                        var version = nv.Clone();
                        var file = a.File;
                        info.Apply = report => McModrinth.InstallVersionAsync(version, s, report, file);
                    }
                }
                result[name] = info;
            }
            Last[s.Id] = result;
            return result;
        }

        private static async Task<JsonElement> Post(string url, object body)
        {
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var r = await Http.PostAsync(url, content);
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        /// <summary>
        /// Installs every newer compatible file. The whole folder is backed up first; if any update fails, the folder is put
        /// back exactly as it was and the error says so.
        /// </summary>
        public static async Task<string> UpdateAllAsync(ServerDef s, Dictionary<string, McCompatInfo> checks, Action<string> report)
        {
            var due = checks.Where(c => c.Value.Kind == McCompat.Update && c.Value.Apply != null).ToList();
            if (due.Count == 0) return "Every plugin is already on its newest compatible file.";
            var folder = McContent.FolderOf(s);
            var remote = s.IsSsh ? McFiles.For(s) : null;
            string hostBackup = null;
            string backup = null;
            if (remote != null) { hostBackup = await Task.Run(() => remote.BackUpFolder(s.ContentFolder)); }
            else backup = McContent.BackUpFolder(folder);
            int done = 0;
            try
            {
                foreach (var (file, info) in due)
                {
                    report?.Invoke($"Updating {file} ({done + 1} of {due.Count})…");
                    await info.Apply(report);
                    done++;
                }
            }
            catch (Exception ex)
            {
                if (backup != null) McContent.RestoreFrom(folder, backup);
                if (hostBackup != null) await Task.Run(() => McContent.RestoreFrom(remote, s.ContentFolder, hostBackup));
                throw new InvalidOperationException($"An update failed, so the folder was put back as it was before: {ex.Message}");
            }
            return done == 1 ? "Updated 1 plugin. The folder was backed up first. Restart the server to load it." : $"Updated {done} plugins. The folder was backed up first. Restart the server to load them.";
        }
    }
}
