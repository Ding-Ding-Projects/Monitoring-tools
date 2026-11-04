using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    public sealed class McBackup
    {
        public string Path, Reason = "By hand", Note = "";
        public DateTime When;
        public long Size;
        public List<string> Contents = new List<string>();
    }

    /// <summary>
    /// World backups as ZIP files with a small description beside each. A backup of a running server turns saving off,
    /// flushes the world to disk, copies it and turns saving back on, so the copy is consistent while players stay online.
    /// A restore always keeps the current world as one more backup before it swaps anything.
    /// </summary>
    public static class McBackups
    {
        private const string Suffix = ".zip";
        private const string Meta = ".backup.json";

        /// <summary>The folders a backup holds: the world and its Nether and End folders, the plugins or mods folder, and a Bedrock server's worlds folder.</summary>
        public static List<string> Parts(string serverFolder, string levelName)
        {
            levelName = string.IsNullOrWhiteSpace(levelName) ? "world" : levelName;
            var parts = new List<string>();
            foreach (var d in new[] { levelName, levelName + "_nether", levelName + "_the_end", "plugins", "mods", "config", "worlds" })
                if (Directory.Exists(System.IO.Path.Combine(serverFolder, d))) parts.Add(d);
            return parts;
        }

        public static List<McBackup> List(string backupFolder)
        {
            if (!Directory.Exists(backupFolder)) return new List<McBackup>();
            var list = new List<McBackup>();
            foreach (var zip in Directory.EnumerateFiles(backupFolder, "*" + Suffix))
            {
                var b = new McBackup { Path = zip, Size = new FileInfo(zip).Length, When = File.GetLastWriteTime(zip) };
                var meta = zip + Meta;
                if (File.Exists(meta))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(meta));
                        var r = doc.RootElement;
                        if (r.TryGetProperty("reason", out var rs)) b.Reason = rs.GetString();
                        if (r.TryGetProperty("note", out var n)) b.Note = n.GetString() ?? "";
                        if (r.TryGetProperty("when", out var w) && w.TryGetDateTime(out var dt)) b.When = dt;
                        if (r.TryGetProperty("contents", out var c)) b.Contents = c.EnumerateArray().Select(x => x.GetString()).ToList();
                    }
                    catch { }
                }
                list.Add(b);
            }
            return list.OrderByDescending(b => b.When).ToList();
        }

        /// <summary>Copies the world folders of <paramref name="serverFolder"/> into a new ZIP and prunes to the newest <paramref name="keep"/>.</summary>
        public static McBackup Create(string serverFolder, string levelName, string backupFolder, string reason, string note, int keep)
        {
            Directory.CreateDirectory(backupFolder);
            var parts = Parts(serverFolder, levelName);
            if (parts.Count == 0) throw new InvalidOperationException("There is no world to back up yet. A world is made the first time the server starts.");
            var when = DateTime.Now;
            var name = when.ToString("yyyyMMdd-HHmmss") + "-" + Slug(reason);
            var zip = System.IO.Path.Combine(backupFolder, name + Suffix);
            for (int n = 2; File.Exists(zip); n++) zip = System.IO.Path.Combine(backupFolder, name + "-" + n + Suffix);
            var tmp = zip + ".part";
            using (var fs = File.Create(tmp))
            using (var a = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var part in parts)
                {
                    var root = System.IO.Path.Combine(serverFolder, part);
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        if (System.IO.Path.GetFileName(file) == "session.lock") continue;   // held open by a running server, and recreated on start
                        var rel = part + "/" + System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
                        using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var dst = a.CreateEntry(rel, CompressionLevel.Fastest).Open();
                        src.CopyTo(dst);
                    }
                }
            }
            File.Move(tmp, zip);
            File.WriteAllText(zip + Meta, JsonSerializer.Serialize(new { reason, note = note ?? "", when, contents = parts }));
            Prune(backupFolder, keep);
            return List(backupFolder).First(b => b.Path == zip);
        }

        /// <summary>Removes the oldest backups beyond the newest <paramref name="keep"/>.</summary>
        public static int Prune(string backupFolder, int keep)
        {
            if (keep < 1) keep = 1;
            int removed = 0;
            foreach (var b in List(backupFolder).Skip(keep))
            {
                File.Delete(b.Path);
                if (File.Exists(b.Path + Meta)) File.Delete(b.Path + Meta);
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// Replaces the world folders with a backup's. The current folders are first saved as a backup named "Before a restore",
        /// then moved aside, and only removed once the backup's files are all in place, so a failure leaves the old world.
        /// The server must be stopped.
        /// </summary>
        public static McBackup Restore(string serverFolder, string levelName, string backupFolder, McBackup backup, int keep)
        {
            if (!File.Exists(backup.Path)) throw new FileNotFoundException("That backup is not there any more.", backup.Path);
            McBackup safety = null;
            if (Parts(serverFolder, levelName).Count > 0) safety = Create(serverFolder, levelName, backupFolder, "Before a restore", "Taken automatically before restoring " + System.IO.Path.GetFileName(backup.Path), keep + 1);
            var aside = System.IO.Path.Combine(serverFolder, ".restore-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(aside);
            List<string> tops;
            using (var a = ZipFile.OpenRead(backup.Path)) tops = a.Entries.Select(e => e.FullName.Split('/')[0]).Distinct().Where(t => t.Length > 0).ToList();
            try
            {
                foreach (var t in tops)
                {
                    var cur = System.IO.Path.Combine(serverFolder, t);
                    if (Directory.Exists(cur)) Directory.Move(cur, System.IO.Path.Combine(aside, t));
                }
                ZipFile.ExtractToDirectory(backup.Path, serverFolder, true);
                Directory.Delete(aside, true);
            }
            catch
            {
                // Put the old folders back exactly as they were.
                foreach (var t in tops)
                {
                    var moved = System.IO.Path.Combine(aside, t);
                    if (!Directory.Exists(moved)) continue;
                    var cur = System.IO.Path.Combine(serverFolder, t);
                    if (Directory.Exists(cur)) Directory.Delete(cur, true);
                    Directory.Move(moved, cur);
                }
                try { Directory.Delete(aside, true); } catch { }
                throw;
            }
            return safety;
        }

        /// <summary>
        /// Backs up a server, pausing its saving while it runs. A server on an SSH host is copied here with scp first (see
        /// <see cref="McRemote"/>), so its backup lands in the same list and restores the same way.
        /// </summary>
        public static async Task<McBackup> BackUpAsync(ServerDef s, bool running, string reason, string note, Action<string> report = null)
        {
            if (s.IsSsh) return await BackUpSshAsync(s, running, reason, note, report);
            var level = ServerManager.ReadProperties(s).TryGetValue("level-name", out var ln) && ln.Length > 0 ? ln : "world";
            if (running)
            {
                report?.Invoke("Pausing saves…");
                if (s.IsBedrock) await ServerManager.CommandAsync(s, "save hold");   // Bedrock: hold saving so the files stay consistent while copied
                else
                {
                    await ServerManager.CommandAsync(s, "save-off");
                    await ServerManager.CommandAsync(s, "save-all flush");
                }
                await Task.Delay(3000);
            }
            try
            {
                report?.Invoke("Copying the world…");
                return await Task.Run(() => Create(s.Folder, level, s.BackupFolder, reason, note, s.KeepBackups));
            }
            finally
            {
                if (running) await ServerManager.CommandAsync(s, s.IsBedrock ? "save resume" : "save-on");
            }
        }

        private static async Task<McBackup> BackUpSshAsync(ServerDef s, bool running, string reason, string note, Action<string> report)
        {
            var files = McFiles.For(s);
            var level = await Task.Run(() => McRemote.LevelName(files, s.IsBedrock));
            if (running)
            {
                report?.Invoke("Pausing saves…");
                if (s.IsBedrock) await ServerManager.CommandAsync(s, "save hold");
                else { await ServerManager.CommandAsync(s, "save-off"); await ServerManager.CommandAsync(s, "save-all flush"); }
                await Task.Delay(3000);
            }
            try
            {
                report?.Invoke(s.BackupOnHost ? "Archiving the world on the SSH host…" : "Copying the world from the SSH host…");
                return await Task.Run(() => s.BackupOnHost ? McRemote.HostBackup(s, files, level, reason, note, s.KeepBackups) : McRemote.PullBackup(s, files, level, reason, note, s.KeepBackups));
            }
            finally
            {
                if (running) await ServerManager.CommandAsync(s, s.IsBedrock ? "save resume" : "save-on");
            }
        }

        private static string Slug(string s) => new string((s ?? "backup").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
    }
}
