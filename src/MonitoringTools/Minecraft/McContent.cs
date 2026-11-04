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
            BackUpFolder(Path.GetDirectoryName(a.File));
            var to = on ? a.File[..^DisabledSuffix.Length] : a.File + DisabledSuffix;
            if (File.Exists(to)) throw new IOException($"{Path.GetFileName(to)} already exists in the folder.");
            File.Move(a.File, to);
            a.File = to; a.Enabled = on;
            return to;
        }

        public static void Remove(McAddon a)
        {
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
