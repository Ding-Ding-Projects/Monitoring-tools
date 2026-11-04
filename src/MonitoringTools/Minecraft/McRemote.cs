using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// Worlds and backups of a server whose folder is on an SSH host. A backup is pulled to this computer with scp and written
    /// by the same code that backs up a local server, so it lands in the same list, with the same ZIP and description beside it.
    /// A restore puts the folders back the other way: the current ones are moved aside on the host, the backup's are copied up,
    /// and only when every one has arrived are the old ones removed; a failure moves them back. The server must be stopped.
    /// </summary>
    public static class McRemote
    {
        /// <summary>The world's folder name from the server's own settings. Java defaults to "world", Bedrock to "Bedrock level".</summary>
        public static string LevelName(IMcFiles files, bool bedrock)
        {
            var text = files.ReadText("server.properties");
            if (text != null && McProperties.Parse(text).ToDictionary().TryGetValue("level-name", out var n) && n.Trim().Length > 0) return n.Trim();
            return bedrock ? "Bedrock level" : "world";
        }

        /// <summary>The folders a backup holds that exist on the host: the world and its Nether and End folders, plugins, mods, config and a Bedrock worlds folder.</summary>
        public static List<string> Parts(IMcFiles files, string level)
        {
            var dirs = new HashSet<string>(files.List("").Where(e => e.IsDir).Select(e => e.Name), StringComparer.Ordinal);
            return new[] { level, level + "_nether", level + "_the_end", "plugins", "mods", "config", "worlds" }.Where(dirs.Contains).ToList();
        }

        /// <summary>Copies the host's world folders here and writes them as one backup ZIP in the server's backup folder.</summary>
        public static McBackup PullBackup(ServerDef s, IMcFiles files, string level, string reason, string note, int keep)
        {
            var parts = Parts(files, level);
            if (parts.Count == 0) throw new InvalidOperationException("There is no world on the host to back up yet. A world is made the first time the server starts.");
            var stage = Path.Combine(Path.GetTempPath(), "mt-pull-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Directory.CreateDirectory(stage);
                foreach (var p in parts) files.DownloadDir(p, stage);
                return McBackups.Create(stage, level, s.BackupFolder, reason, note, keep);
            }
            finally { try { Directory.Delete(stage, true); } catch { } }
        }

        /// <summary>Puts a backup's folders back on the host. The server must be stopped. Returns the safety backup of what was there, or null.</summary>
        public static McBackup Restore(ServerDef s, IMcFiles files, string level, McBackup backup)
        {
            if (!File.Exists(backup.Path)) throw new FileNotFoundException("That backup is not there any more.", backup.Path);
            McBackup safety = null;
            if (Parts(files, level).Count > 0)
                safety = PullBackup(s, files, level, "Before a restore", "Taken automatically before restoring " + Path.GetFileName(backup.Path), s.KeepBackups + 1);
            var stage = Path.Combine(Path.GetTempPath(), "mt-push-" + Guid.NewGuid().ToString("N")[..8]);
            var tag = ".restore-" + Guid.NewGuid().ToString("N")[..6];
            var moved = new List<string>(); var sent = new List<string>();
            try
            {
                ZipFile.ExtractToDirectory(backup.Path, stage);
                var tops = Directory.GetDirectories(stage).Select(Path.GetFileName).ToList();
                if (tops.Count == 0) throw new InvalidDataException("That backup holds no folders.");
                foreach (var t in tops)
                {
                    if (files.Exists(t)) { files.Move(t, t + tag); moved.Add(t); }
                    files.UploadDir(Path.Combine(stage, t), t);
                    sent.Add(t);
                }
                foreach (var t in moved) files.Delete(t + tag);
            }
            catch
            {
                // Put the old folders back exactly as they were.
                foreach (var t in sent) { try { files.Delete(t); } catch { } }
                foreach (var t in moved) { try { files.Move(t + tag, t); } catch { } }
                throw;
            }
            finally { try { Directory.Delete(stage, true); } catch { } }
            return safety;
        }

        // ---- backups kept on the host itself

        /// <summary>Where on-host backups live, inside the server folder; it is never part of a backup itself.</summary>
        public const string HostBackupDir = ".mt-backups";

        private static string Slug(string s) => new string((s ?? "backup").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

        /// <summary>Archives the world folders on the host into <c>.mt-backups/&lt;time&gt;-&lt;reason&gt;.tar.gz</c> (nothing is copied here) with a small description beside it.</summary>
        public static McBackup HostBackup(ServerDef s, IMcFiles files, string level, string reason, string note, int keep)
        {
            var parts = Parts(files, level);
            if (parts.Count == 0) throw new InvalidOperationException("There is no world on the host to back up yet. A world is made the first time the server starts.");
            var when = DateTime.Now;
            var name = when.ToString("yyyyMMdd-HHmmss") + "-" + Slug(reason);
            var rel = HostBackupDir + "/" + name + ".tar.gz";
            for (int n = 2; files.Exists(rel); n++) rel = HostBackupDir + "/" + name + "-" + n + ".tar.gz";
            files.Archive(parts, rel);
            files.WriteText(rel + ".backup.json", System.Text.Json.JsonSerializer.Serialize(new { reason, note = note ?? "", when, contents = parts }));
            HostPrune(files, keep);
            return HostList(files).First(b => b.Path == rel);
        }

        /// <summary>The backups on the host, newest first, read back from the host's own folder.</summary>
        public static List<McBackup> HostList(IMcFiles files)
        {
            var entries = files.List(HostBackupDir).Where(e => !e.IsDir && e.Name.EndsWith(".tar.gz", StringComparison.Ordinal)).ToList();
            var metas = files.ReadMany(entries.Select(e => HostBackupDir + "/" + e.Name + ".backup.json"));
            var list = new List<McBackup>();
            foreach (var e in entries)
            {
                var rel = HostBackupDir + "/" + e.Name;
                var b = new McBackup { Path = rel, Size = e.Size };
                if (metas.TryGetValue(rel + ".backup.json", out var m))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(m);
                        var r = doc.RootElement;
                        if (r.TryGetProperty("reason", out var rs)) b.Reason = rs.GetString();
                        if (r.TryGetProperty("note", out var nt)) b.Note = nt.GetString() ?? "";
                        if (r.TryGetProperty("when", out var w) && w.TryGetDateTime(out var dt)) b.When = dt;
                        if (r.TryGetProperty("contents", out var c)) b.Contents = c.EnumerateArray().Select(x => x.GetString()).ToList();
                    }
                    catch { }
                }
                list.Add(b);
            }
            return list.OrderByDescending(b => b.When).ToList();
        }

        public static int HostPrune(IMcFiles files, int keep)
        {
            if (keep < 1) keep = 1;
            int removed = 0;
            foreach (var b in HostList(files).Skip(keep)) { files.Delete(b.Path); if (files.Exists(b.Path + ".backup.json")) files.Delete(b.Path + ".backup.json"); removed++; }
            return removed;
        }

        /// <summary>Puts an on-host backup back: a safety backup first, the current folders moved aside, the archive unpacked, the old folders removed only when it worked. The server must be stopped.</summary>
        public static McBackup HostRestore(ServerDef s, IMcFiles files, string level, McBackup backup)
        {
            if (!files.Exists(backup.Path)) throw new FileNotFoundException("That backup is not on the host any more.", backup.Path);
            McBackup safety = null;
            if (Parts(files, level).Count > 0) safety = HostBackup(s, files, level, "Before a restore", "Taken automatically before restoring " + System.IO.Path.GetFileName(backup.Path), s.KeepBackups + 1);
            var tag = ".restore-" + Guid.NewGuid().ToString("N")[..6];
            var moved = new List<string>();
            try
            {
                foreach (var t in backup.Contents) if (files.Exists(t)) { files.Move(t, t + tag); moved.Add(t); }
                files.Extract(backup.Path);
                foreach (var t in backup.Contents) if (!files.Exists(t)) throw new InvalidDataException($"The backup did not unpack {t}.");
                foreach (var t in moved) files.Delete(t + tag);
            }
            catch
            {
                foreach (var t in backup.Contents) { try { if (files.Exists(t)) files.Delete(t); } catch { } }
                foreach (var t in moved) { try { files.Move(t + tag, t); } catch { } }
                throw;
            }
            return safety;
        }

        /// <summary>The Java world folders on the host with their sizes. Folder is the path inside the server folder.</summary>
        public static List<McDimension> Dimensions(IMcFiles files, string level)
        {
            var top = files.List("").Where(e => e.IsDir).Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
            var inside = top.Contains(level) ? files.List(level).Where(e => e.IsDir).Select(e => e.Name).ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
            var list = new List<McDimension>();
            void Add(string rel, string kind) => list.Add(new McDimension { Folder = rel, Name = rel, Kind = kind, Exists = true, Size = files.Size(rel) });
            if (top.Contains(level)) Add(level, "Overworld"); else list.Add(new McDimension { Folder = level, Name = level, Kind = "Overworld", Exists = false });
            if (top.Contains(level + "_nether")) Add(level + "_nether", "Nether"); else if (inside.Contains("DIM-1")) Add(level + "/DIM-1", "Nether");
            if (top.Contains(level + "_the_end")) Add(level + "_the_end", "The End"); else if (inside.Contains("DIM1")) Add(level + "/DIM1", "The End");
            return list;
        }

        /// <summary>The seed from the host's level.dat, read from a copy fetched to this computer. Null when it cannot be read.</summary>
        public static long? Seed(IMcFiles files, string worldRel)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "mt-seed-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Directory.CreateDirectory(tmp);
                files.DownloadFile(worldRel + "/level.dat", Path.Combine(tmp, "level.dat"));
                return McWorlds.Seed(tmp);
            }
            catch { return null; }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }

        /// <summary>Fetches a world folder from the host and writes it as a ZIP on this computer.</summary>
        public static string DownloadZip(IMcFiles files, string rel, string zipPath)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "mt-world-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Directory.CreateDirectory(tmp);
                files.DownloadDir(rel, tmp);
                return McWorlds.Download(Path.Combine(tmp, rel.Split('/').Last()), zipPath);
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }
    }
}
