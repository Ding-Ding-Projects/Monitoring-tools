using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MonitoringTools.Minecraft
{
    public sealed class McDimension
    {
        public string Folder, Name, Kind;   // Kind: Overworld, Nether, The End
        public long Size;
        public bool Exists;
    }

    /// <summary>
    /// A server's world folders. Vanilla keeps the Nether and the End inside the world folder (DIM-1, DIM1); Paper, Purpur
    /// and Spigot keep them beside it (world_nether, world_the_end). Imports are copied and checked, never moved, and a
    /// reset or replace backs the current world up first.
    /// </summary>
    public static class McWorlds
    {
        public static string LevelName(ServerDef s)
        {
            var props = ServerManager.ReadProperties(s);
            return props.TryGetValue("level-name", out var n) && n.Trim().Length > 0 ? n.Trim() : "world";
        }

        public static List<McDimension> Dimensions(string serverFolder, string level)
        {
            var list = new List<McDimension>();
            void Add(string rel, string kind)
            {
                var path = Path.Combine(serverFolder, rel);
                list.Add(new McDimension { Folder = path, Name = rel.Replace('\\', '/'), Kind = kind, Exists = Directory.Exists(path), Size = Directory.Exists(path) ? FolderSize(path) : 0 });
            }
            Add(level, "Overworld");
            if (Directory.Exists(Path.Combine(serverFolder, level + "_nether"))) Add(level + "_nether", "Nether");
            else if (Directory.Exists(Path.Combine(serverFolder, level, "DIM-1"))) Add(Path.Combine(level, "DIM-1"), "Nether");
            if (Directory.Exists(Path.Combine(serverFolder, level + "_the_end"))) Add(level + "_the_end", "The End");
            else if (Directory.Exists(Path.Combine(serverFolder, level, "DIM1"))) Add(Path.Combine(level, "DIM1"), "The End");
            return list;
        }

        /// <summary>
        /// The world's seed from its level.dat (gzip NBT): the long tag "seed" (1.16 and newer, under WorldGenSettings) or
        /// "RandomSeed" (older). Null when the file is missing or holds neither.
        /// </summary>
        public static long? Seed(string worldFolder)
        {
            var f = Path.Combine(worldFolder, "level.dat");
            if (!File.Exists(f)) return null;
            try
            {
                byte[] data;
                using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var ms = new MemoryStream()) { gz.CopyTo(ms); data = ms.ToArray(); }
                foreach (var name in new[] { "seed", "RandomSeed" })
                {
                    var tag = new byte[3 + name.Length];
                    tag[0] = 4; tag[1] = 0; tag[2] = (byte)name.Length;   // TAG_Long, then the name length as a big-endian short
                    for (int i = 0; i < name.Length; i++) tag[3 + i] = (byte)name[i];
                    int at = IndexOf(data, tag);
                    if (at >= 0 && at + tag.Length + 8 <= data.Length)
                    {
                        long v = 0;
                        for (int i = 0; i < 8; i++) v = (v << 8) | data[at + tag.Length + i];
                        return v;
                    }
                }
            }
            catch { }
            return null;
        }

        private static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        public static long FolderSize(string dir)
        {
            long n = 0;
            try { foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) { try { n += new FileInfo(f).Length; } catch { } } } catch { }
            return n;
        }

        /// <summary>
        /// Finds the world inside a folder or ZIP: the folder that holds level.dat. Returns its path relative to the source
        /// root ("" for the root itself), or null with a reason when there is none.
        /// </summary>
        public static (string Root, string Problem) Validate(string source)
        {
            if (Directory.Exists(source))
            {
                if (File.Exists(Path.Combine(source, "level.dat"))) return ("", null);
                var inner = Directory.EnumerateFiles(source, "level.dat", SearchOption.AllDirectories).OrderBy(f => f.Length).FirstOrDefault();
                return inner == null ? (null, "That folder has no level.dat, so it is not a Minecraft world.") : (Path.GetRelativePath(source, Path.GetDirectoryName(inner)), null);
            }
            if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var z = ZipFile.OpenRead(source);
                    var level = z.Entries.Where(e => e.Name == "level.dat").OrderBy(e => e.FullName.Length).FirstOrDefault();
                    if (level == null) return (null, "That ZIP has no level.dat, so it is not a Minecraft world.");
                    var root = level.FullName[..^"level.dat".Length].TrimEnd('/');
                    return (root, null);
                }
                catch (InvalidDataException) { return (null, "That file is not a readable ZIP."); }
            }
            return (null, "Choose a world folder or a .zip file.");
        }

        /// <summary>Copies a world folder or ZIP into the server as its world ("level-name"), replacing nothing that exists unless <paramref name="replace"/>.</summary>
        public static string ImportInto(ServerDef s, string source, Action<string> report = null, bool replace = false)
        {
            var (root, problem) = Validate(source);
            if (problem != null) throw new InvalidDataException(problem);
            Directory.CreateDirectory(s.Folder);
            var level = LevelName(s);
            var dest = Path.Combine(s.Folder, level);
            if (Directory.Exists(dest))
            {
                if (!replace) throw new IOException($"The server already has a world folder \"{level}\". Use Replace to swap it, which backs it up first.");
                Directory.Delete(dest, true);
            }
            report?.Invoke("Copying the world…");
            var staging = dest + ".import-" + Guid.NewGuid().ToString("N")[..6];
            if (Directory.Exists(source))
            {
                var from = Path.Combine(source, root);
                foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(f) == "session.lock") continue;
                    var to = Path.Combine(staging, Path.GetRelativePath(from, f));
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(f, to);
                }
            }
            else
            {
                using var z = ZipFile.OpenRead(source);
                var prefix = root.Length == 0 ? "" : root + "/";
                var full = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                foreach (var e in z.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.Name.Length > 0))
                {
                    var to = Path.GetFullPath(Path.Combine(staging, e.FullName[prefix.Length..]));
                    if (!to.StartsWith(full, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The ZIP has a path outside its own folder, so it was refused.");
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    e.ExtractToFile(to, true);
                }
            }
            if (!File.Exists(Path.Combine(staging, "level.dat"))) { Directory.Delete(staging, true); throw new InvalidDataException("The copied world has no level.dat."); }
            Directory.Move(staging, dest);
            return dest;
        }

        /// <summary>Writes one dimension folder (or the whole world when <paramref name="folder"/> is the world) to a ZIP.</summary>
        public static string Download(string folder, string zipPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            var tmp = zipPath + ".part";
            using (var fs = File.Create(tmp))
            using (var a = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var name = Path.GetFileName(folder.TrimEnd('\\', '/'));
                foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(f) == "session.lock") continue;
                    using var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var dst = a.CreateEntry(name + "/" + Path.GetRelativePath(folder, f).Replace('\\', '/'), CompressionLevel.Optimal).Open();
                    src.CopyTo(dst);
                }
            }
            File.Move(tmp, zipPath);
            return zipPath;
        }

        /// <summary>
        /// Removes the world so the server makes a new one on its next start (reset), or swaps in another world (replace).
        /// A backup is always taken first. The server must be stopped.
        /// </summary>
        public static McBackup ResetOrReplace(ServerDef s, string replacement)
        {
            var level = LevelName(s);
            McBackup b = null;
            if (McBackups.Parts(s.Folder, level).Any(p => p.StartsWith(level, StringComparison.Ordinal)))
                b = McBackups.Create(s.Folder, level, s.BackupFolder, replacement == null ? "Before a reset" : "Before a replace", "", s.KeepBackups + 1);
            if (replacement != null)
            {
                var (_, problem) = Validate(replacement);
                if (problem != null) throw new InvalidDataException(problem);
            }
            foreach (var d in new[] { level, level + "_nether", level + "_the_end" })
            {
                var p = Path.Combine(s.Folder, d);
                if (Directory.Exists(p)) Directory.Delete(p, true);
            }
            if (replacement != null) ImportInto(s, replacement);
            return b;
        }
    }
}
