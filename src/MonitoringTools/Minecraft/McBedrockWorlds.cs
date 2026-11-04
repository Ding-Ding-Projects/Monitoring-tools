using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MonitoringTools.Minecraft
{
    public sealed class McWorldFolder
    {
        public string Name = "", Rel = "";
        public long Size;
        /// <summary>The text of levelname.txt, which is the name players see.</summary>
        public string Title = "";
        public bool InUse;
    }

    /// <summary>
    /// The worlds of a Bedrock Dedicated Server: folders inside worlds/, each with a level.dat and a db folder. The server uses
    /// the one named by level-name in server.properties. A .mcworld is a ZIP of such a folder's contents. An import is checked,
    /// copied (never moved) and refused when the destination already holds a world unless the caller replaces it, which the
    /// caller does only after a backup. The server must be stopped.
    /// </summary>
    public static class McBedrockWorlds
    {
        public static List<McWorldFolder> List(IMcFiles files, string level)
        {
            var list = new List<McWorldFolder>();
            var dirs = files.List("worlds").Where(e => e.IsDir).ToList();
            var titles = files.ReadMany(dirs.Select(d => "worlds/" + d.Name + "/levelname.txt"));
            foreach (var d in dirs)
            {
                var rel = "worlds/" + d.Name;
                titles.TryGetValue(rel + "/levelname.txt", out var title);
                list.Add(new McWorldFolder { Name = d.Name, Rel = rel, Size = files.Size(rel), Title = (title ?? "").Trim(), InUse = string.Equals(d.Name, level, StringComparison.Ordinal) });
            }
            return list;
        }

        /// <summary>The folder inside a world folder or .mcworld/.zip that holds level.dat ("" for its root), or why the source is not a Bedrock world.</summary>
        public static (string Root, string Problem) Validate(string source)
        {
            if (Directory.Exists(source))
            {
                var level = Directory.EnumerateFiles(source, "level.dat", SearchOption.AllDirectories).OrderBy(f => f.Length).FirstOrDefault();
                if (level == null) return (null, "That folder has no level.dat, so it is not a Minecraft world.");
                var dir = Path.GetDirectoryName(level);
                return Directory.Exists(Path.Combine(dir, "db")) ? (Path.GetRelativePath(source, dir) is var r && r == "." ? "" : r, null)
                    : (null, Directory.Exists(Path.Combine(dir, "region")) ? "That is a Java edition world (it has region files and no db folder). A Bedrock server cannot open it." : "That folder has a level.dat but no db folder, so it is not a Bedrock world.");
            }
            if (File.Exists(source) && (source.EndsWith(".mcworld", StringComparison.OrdinalIgnoreCase) || source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using var z = ZipFile.OpenRead(source);
                    var level = z.Entries.Where(e => e.Name == "level.dat").OrderBy(e => e.FullName.Length).FirstOrDefault();
                    if (level == null) return (null, "That file has no level.dat, so it is not a Minecraft world.");
                    var root = level.FullName[..^"level.dat".Length].TrimEnd('/');
                    var prefix = root.Length == 0 ? "" : root + "/";
                    if (!z.Entries.Any(e => e.FullName.StartsWith(prefix + "db/", StringComparison.Ordinal)))
                        return (null, z.Entries.Any(e => e.FullName.StartsWith(prefix + "region/", StringComparison.Ordinal)) ? "That is a Java edition world (it has region files and no db folder). A Bedrock server cannot open it." : "That file has a level.dat but no db folder, so it is not a Bedrock world.");
                    return (root, null);
                }
                catch (InvalidDataException) { return (null, "That file is not a readable ZIP."); }
            }
            return (null, "Choose a world folder or a .mcworld file.");
        }

        /// <summary>
        /// Copies a world into worlds/&lt;level&gt;. Refuses when that folder already exists unless <paramref name="replace"/> is set,
        /// in which case the old folder is moved aside and removed only after the new one has arrived; on a failure it is moved back.
        /// </summary>
        public static void Import(IMcFiles files, string source, string level, bool replace)
        {
            var (root, problem) = Validate(source);
            if (problem != null) throw new InvalidDataException(problem);
            var dest = "worlds/" + level;
            if (files.Exists(dest) && !replace) throw new IOException($"The server already has a world folder \"{level}\". Replace it to swap it, which backs it up first.");
            var stage = Path.Combine(Path.GetTempPath(), "mt-bw-" + Guid.NewGuid().ToString("N")[..8]);
            var staged = Path.Combine(stage, level);
            string aside = null;
            try
            {
                if (Directory.Exists(source)) McLocalFiles.CopyDir(Path.Combine(source, root), staged);
                else
                {
                    using var z = ZipFile.OpenRead(source);
                    var prefix = root.Length == 0 ? "" : root + "/";
                    var full = Path.GetFullPath(staged) + Path.DirectorySeparatorChar;
                    foreach (var e in z.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.Name.Length > 0))
                    {
                        var to = Path.GetFullPath(Path.Combine(staged, e.FullName[prefix.Length..]));
                        if (!to.StartsWith(full, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The archive has a path outside its own folder, so it was refused.");
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        e.ExtractToFile(to, true);
                    }
                }
                if (!File.Exists(Path.Combine(staged, "level.dat")) || !Directory.Exists(Path.Combine(staged, "db"))) throw new InvalidDataException("The copied world has no level.dat and db folder.");
                files.MakeDir("worlds");
                if (files.Exists(dest)) { aside = dest + ".replaced-" + McFiles.Stamp(); files.Move(dest, aside); }
                try { files.UploadDir(staged, dest); }
                catch { if (files.Exists(dest)) files.Delete(dest); if (aside != null) files.Move(aside, dest); aside = null; throw; }
                if (aside != null) files.Delete(aside);
            }
            finally { try { Directory.Delete(stage, true); } catch { } }
        }
    }
}
