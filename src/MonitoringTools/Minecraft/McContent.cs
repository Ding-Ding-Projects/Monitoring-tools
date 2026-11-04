using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MonitoringTools.Minecraft
{
    public sealed class McAddon
    {
        public string File, Name, Version = "";
        public bool Enabled;
        public long Size;
        /// <summary>Set for a jar on an SSH host: <see cref="File"/> is then its path inside the server folder and every change goes through this.</summary>
        public IMcFiles Files;
    }

    /// <summary>
    /// The plugins (Paper, Purpur) or mods (Fabric, Forge, NeoForge) in a server's folder. Disabling renames a jar to
    /// ".jar.disabled", which the server skips; enabling renames it back. Every change first copies the whole folder to a
    /// dated backup beside it, so any install, removal or switch can be undone.
    /// </summary>
    public static class McContent
    {
        public const string DisabledSuffix = ".disabled";

        public static string FolderOf(ServerDef s) => s.ContentFolder == null ? null : Path.Combine(s.Folder, s.ContentFolder);

        public static List<McAddon> List(string folder)
        {
            if (folder == null || !Directory.Exists(folder)) return new List<McAddon>();
            var list = new List<McAddon>();
            foreach (var f in Directory.EnumerateFiles(folder))
            {
                bool enabled = f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
                bool disabled = f.EndsWith(".jar" + DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                if (!enabled && !disabled) continue;
                var a = new McAddon { File = f, Enabled = enabled, Size = new FileInfo(f).Length };
                (a.Name, a.Version) = Describe(f);
                list.Add(a);
            }
            return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// The jars in a folder of an SSH host (or any <see cref="IMcFiles"/>). A host's jars are not downloaded to read their
        /// metadata, so the name is the file name without its extension and the version is left empty.
        /// </summary>
        public static List<McAddon> List(IMcFiles files, string folder)
        {
            var list = new List<McAddon>();
            if (folder == null) return list;
            foreach (var e in files.List(folder))
            {
                if (e.IsDir) continue;
                bool enabled = e.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
                bool disabled = e.Name.EndsWith(".jar" + DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                if (!enabled && !disabled) continue;
                var baseName = disabled ? e.Name[..^DisabledSuffix.Length] : e.Name;
                list.Add(new McAddon { File = folder + "/" + e.Name, Name = Path.GetFileNameWithoutExtension(baseName), Enabled = enabled, Size = e.Size, Files = files });
            }
            return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Copies a jar from this computer into a folder of an SSH host, after the folder has been copied to a dated backup beside it on the host.</summary>
        public static string Install(IMcFiles files, string folder, string jar, string fileName = null)
        {
            if (!jar.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && fileName?.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) != true) throw new ArgumentException("Choose a .jar plugin or mod.");
            using (var z = ZipFile.OpenRead(jar)) { if (z.Entries.Count == 0) throw new InvalidDataException("That jar is empty."); }
            files.MakeDir(folder);
            files.BackUpFolder(folder);
            var to = folder + "/" + Path.GetFileName(fileName ?? jar);
            files.Upload(jar, to);
            return to;
        }

        /// <summary>The newest dated backup of a folder on an SSH host (or any file store), as a path inside the server folder, or null.</summary>
        public static string LatestBackup(IMcFiles files, string folder) =>
            files.List(folder + "-backups").Where(e => e.IsDir).Select(e => e.Name).OrderByDescending(n => n, StringComparer.Ordinal).Select(n => folder + "-backups/" + n).FirstOrDefault();

        /// <summary>
        /// Puts a folder on an SSH host back as a backup holds it: jars added since are removed and every backed-up entry is copied
        /// back over what is there. The folder as it is now is backed up first when <paramref name="keepCurrent"/> is set, so a rollback can be undone.
        /// </summary>
        public static void RestoreFrom(IMcFiles files, string folder, string backup, bool keepCurrent = false)
        {
            if (!files.Exists(backup)) throw new DirectoryNotFoundException("That backup is not there any more.");
            if (keepCurrent) files.BackUpFolder(folder);
            var inBackup = files.List(backup);
            foreach (var e in files.List(folder))
            {
                bool jar = !e.IsDir && (e.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".jar" + DisabledSuffix, StringComparison.OrdinalIgnoreCase));
                if (jar && !inBackup.Any(b => b.Name == e.Name)) files.Delete(folder + "/" + e.Name);
            }
            foreach (var e in inBackup)
            {
                var to = folder + "/" + e.Name;
                if (files.Exists(to)) files.Delete(to);
                files.Copy(backup + "/" + e.Name, to);
            }
        }

        /// <summary>Reads the name and version a jar declares for itself (plugin.yml, paper-plugin.yml, fabric.mod.json, mods.toml), falling back to the file name.</summary>
        internal static (string Name, string Version) Describe(string jar)
        {
            var baseName = Path.GetFileName(jar);
            if (baseName.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)) baseName = baseName[..^DisabledSuffix.Length];
            baseName = Path.GetFileNameWithoutExtension(baseName);
            try
            {
                using var z = ZipFile.OpenRead(jar);
                string Read(string name) { var e = z.GetEntry(name); if (e == null) return null; using var r = new StreamReader(e.Open()); return r.ReadToEnd(); }
                var yml = Read("plugin.yml") ?? Read("paper-plugin.yml");
                if (yml != null)
                {
                    string Field(string k) { var m = Regex.Match(yml, "^" + k + @":\s*['""]?([^'""\r\n]+)", RegexOptions.Multiline); return m.Success ? m.Groups[1].Value.Trim() : null; }
                    return (Field("name") ?? baseName, Field("version") ?? "");
                }
                var fabric = Read("fabric.mod.json");
                if (fabric != null)
                {
                    using var doc = JsonDocument.Parse(fabric);
                    var r = doc.RootElement;
                    return (r.TryGetProperty("name", out var n) ? n.GetString() : baseName, r.TryGetProperty("version", out var v) ? v.GetString() : "");
                }
                var toml = Read("META-INF/neoforge.mods.toml") ?? Read("META-INF/mods.toml");
                if (toml != null)
                {
                    var name = Regex.Match(toml, @"^\s*displayName\s*=\s*""([^""]+)""", RegexOptions.Multiline);
                    var ver = Regex.Match(toml, @"^\s*version\s*=\s*""([^""]+)""", RegexOptions.Multiline);
                    return (name.Success ? name.Groups[1].Value : baseName, ver.Success && !ver.Groups[1].Value.Contains("${") ? ver.Groups[1].Value : "");
                }
            }
            catch { }
            return (baseName, "");
        }

        /// <summary>Copies the folder to "&lt;folder&gt;-backups/yyyyMMdd-HHmmss" and returns that path, or null when there is nothing to copy.</summary>
        public static string BackUpFolder(string folder)
        {
            if (folder == null || !Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any()) return null;
            var dest = Path.Combine(folder + "-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var to = Path.Combine(dest, Path.GetRelativePath(folder, f));
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(f, to);
            }
            return dest;
        }

        /// <summary>The newest dated backup of the folder, or null.</summary>
        public static string LatestBackup(string folder)
        {
            var root = folder + "-backups";
            return Directory.Exists(root) ? Directory.EnumerateDirectories(root).OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal).FirstOrDefault() : null;
        }

        /// <summary>
        /// Puts the folder back as the backup holds it: jars that were added since are removed and every backed-up file is
        /// copied back. The folder as it is now is backed up first, so even a rollback can be undone.
        /// </summary>
        public static void RestoreFrom(string folder, string backup, bool keepCurrent = false)
        {
            if (backup == null || !Directory.Exists(backup)) throw new DirectoryNotFoundException("That backup is not there any more.");
            if (keepCurrent) BackUpFolder(folder);
            Directory.CreateDirectory(folder);
            var inBackup = new HashSet<string>(Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(backup, f)), StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.EnumerateFiles(folder))
            {
                bool jar = f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jar" + DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                if (jar && !inBackup.Contains(Path.GetFileName(f))) File.Delete(f);
            }
            foreach (var rel in inBackup)
            {
                var to = Path.Combine(folder, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(Path.Combine(backup, rel), to, true);
            }
        }

        /// <summary>Turns a jar on or off by renaming it. Returns the new path.</summary>
        public static string SetEnabled(McAddon a, bool on)
        {
            if (a.Enabled == on) return a.File;
            var to0 = on ? a.File[..^DisabledSuffix.Length] : a.File + DisabledSuffix;
            if (a.Files != null)
            {
                var dir = a.File[..a.File.LastIndexOf('/')];
                if (a.Files.Exists(to0)) throw new IOException($"{to0[(to0.LastIndexOf('/') + 1)..]} already exists in the folder.");
                a.Files.BackUpFolder(dir);
                a.Files.Move(a.File, to0);
                a.File = to0; a.Enabled = on;
                return to0;
            }
            BackUpFolder(Path.GetDirectoryName(a.File));
            var to = to0;
            if (File.Exists(to)) throw new IOException($"{Path.GetFileName(to)} already exists in the folder.");
            File.Move(a.File, to);
            a.File = to; a.Enabled = on;
            return to;
        }

        public static void Remove(McAddon a)
        {
            if (a.Files != null)
            {
                a.Files.BackUpFolder(a.File[..a.File.LastIndexOf('/')]);
                a.Files.Delete(a.File);
                return;
            }
            BackUpFolder(Path.GetDirectoryName(a.File));
            File.Delete(a.File);
        }

        /// <summary>Copies a jar into the folder, after backing the folder up. A file with the same name is replaced.</summary>
        public static string Install(string folder, string jar, string fileName = null)
        {
            if (!jar.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && fileName?.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) != true) throw new ArgumentException("Choose a .jar plugin or mod.");
            using (var z = ZipFile.OpenRead(jar)) { if (z.Entries.Count == 0) throw new InvalidDataException("That jar is empty."); }   // a jar is a ZIP; anything else is refused
            Directory.CreateDirectory(folder);
            BackUpFolder(folder);
            var to = Path.Combine(folder, Path.GetFileName(fileName ?? jar));
            File.Copy(jar, to, true);
            return to;
        }
    }
}
