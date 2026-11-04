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
