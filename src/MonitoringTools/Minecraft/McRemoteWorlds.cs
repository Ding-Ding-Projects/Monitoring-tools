using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// Import, replace and reset of a Java world for a server whose folder is on an SSH host, over the file store. The new
    /// world is staged on this computer (a ZIP is unpacked with every path checked), checked for a level.dat, and copied up
    /// beside the live world under a temporary name, so a copy that fails touches nothing. Then every live world folder gets
    /// a dated copy on the host (<c>&lt;folder&gt;-backups/&lt;stamp&gt;</c>), is moved aside, and the new world is moved
    /// in. A failure at any step moves the old folders back and removes what was added. The server must be stopped.
    /// </summary>
    public static class McRemoteWorlds
    {
        /// <summary>Set by the self-test to fail a swap after the old folders are aside, to prove they are put back.</summary>
        internal static Action AfterAsideForTest;

        public sealed class Result
        {
            /// <summary>Paths on the host (inside the server folder) of the dated copies taken before the change.</summary>
            public List<string> Backups = new List<string>();
        }

        /// <summary>The world folders that exist on the host: the world and its Nether and End folders.</summary>
        public static List<string> WorldFolders(IMcFiles files, string level)
        {
            var dirs = new HashSet<string>(files.List("").Where(e => e.IsDir).Select(e => e.Name), StringComparer.Ordinal);
            return new[] { level, level + "_nether", level + "_the_end" }.Where(dirs.Contains).ToList();
        }

        /// <summary>
        /// Puts the world in <paramref name="source"/> (a folder or ZIP) on the host as <paramref name="level"/>. An existing
        /// world is refused unless <paramref name="replace"/>, in which case it is backed up and swapped.
        /// </summary>
        public static Result Import(IMcFiles files, string source, string level, bool replace, Action<string> report = null)
        {
            var (root, problem) = McWorlds.Validate(source);
            if (problem != null) throw new InvalidDataException(problem);
            level = McFiles.Clean(level);
            if (level.Length == 0 || level.Contains('/')) throw new ArgumentException("The level name must be one folder name.");
            if (WorldFolders(files, level).Contains(level, StringComparer.Ordinal) && !replace)
                throw new IOException($"The server already has a world folder \"{level}\". Use Replace to swap it, which backs it up first.");
            return Swap(files, source, root, level, report);
        }

        /// <summary>Removes the world folders (reset) after a dated copy of each, so the server makes a new world on its next start.</summary>
        public static Result Reset(IMcFiles files, string level, Action<string> report = null) => Swap(files, null, null, McFiles.Clean(level), report);

        private static Result Swap(IMcFiles files, string source, string root, string level, Action<string> report)
        {
            var result = new Result();
            var tag = McFiles.Stamp();
            var staging = level + ".import-" + tag;
            var local = Path.Combine(Path.GetTempPath(), "mt-rw-" + Guid.NewGuid().ToString("N")[..8]);
            var aside = new List<(string Name, string Moved)>();
            var placed = false;
            try
            {
                if (source != null)
                {
                    report?.Invoke("Preparing the world…");
                    var staged = Path.Combine(local, staging);
                    Stage(source, root, staged);
                    if (!File.Exists(Path.Combine(staged, "level.dat"))) throw new InvalidDataException("The copied world has no level.dat.");
                    report?.Invoke("Copying the world to the host…");
                    files.UploadDir(staged, staging);
                    if (!files.Exists(staging + "/level.dat")) throw new InvalidDataException("The copy on the host has no level.dat.");
                }
                var live = WorldFolders(files, level);
                report?.Invoke(live.Count > 0 ? "Backing up the current world on the host…" : "Swapping the world…");
                foreach (var d in live) { var b = files.BackUpFolder(d); if (b != null) result.Backups.Add(b); }
                foreach (var d in live) { var moved = d + ".replaced-" + tag; files.Move(d, moved); aside.Add((d, moved)); }
                AfterAsideForTest?.Invoke();
                if (source != null)
                {
                    files.Move(staging, level);
                    placed = true;
                    if (!files.Exists(level + "/level.dat")) throw new InvalidDataException("The world on the host has no level.dat after the swap.");
                }
                foreach (var (_, moved) in aside) files.Delete(moved);
                aside.Clear();
                return result;
            }
            catch
            {
                // Put the old folders back exactly as they were and remove what was added.
                if (placed) { try { files.Delete(level); } catch { } }
                try { if (files.Exists(staging)) files.Delete(staging); } catch { }
                foreach (var (name, moved) in aside) { try { files.Move(moved, name); } catch { } }
                throw;
            }
            finally { try { Directory.Delete(local, true); } catch { } }
        }

        private static void Stage(string source, string root, string staged)
        {
            if (Directory.Exists(source)) McLocalFiles.CopyDir(Path.Combine(source, root), staged);
            else
            {
                using var z = ZipFile.OpenRead(source);
                var prefix = root.Length == 0 ? "" : root + "/";
                var full = Path.GetFullPath(staged) + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(staged);
                foreach (var e in z.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.Name.Length > 0))
                {
                    var to = Path.GetFullPath(Path.Combine(staged, e.FullName[prefix.Length..]));
                    if (!to.StartsWith(full, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The ZIP has a path outside its own folder, so it was refused.");
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    e.ExtractToFile(to, true);
                }
            }
            foreach (var l in Directory.EnumerateFiles(staged, "session.lock", SearchOption.AllDirectories).ToList()) File.Delete(l);
        }
    }
}
