using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>One built-in extension as shipped inside the app: its id and its files by relative path.</summary>
    public sealed class BundledExtension
    {
        public string Id;
        public SortedDictionary<string, byte[]> Files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        public string Hash => ExtensionDefaults.HashOf(Files);
    }

    /// <summary>
    /// Puts the app's built-in extensions into the extensions folder and keeps them current. A built-in extension is
    /// installed once and turned on; it is updated only while its files are exactly as the app installed them, so your
    /// edits are never overwritten; and once you delete its folder it is not put back. defaults.json records what was
    /// installed.
    /// </summary>
    public static class ExtensionDefaults
    {
        public const string RecordFile = "defaults.json";

        public sealed class Record { public string Hash { get; set; } public bool Removed { get; set; } }

        public static string HashOf(IDictionary<string, byte[]> files)
        {
            using var sha = SHA256.Create();
            foreach (var kv in files.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var name = Encoding.UTF8.GetBytes(kv.Key.Replace('\\', '/') + "\n");
                sha.TransformBlock(name, 0, name.Length, null, 0);
                var body = Normalise(kv.Value);
                sha.TransformBlock(body, 0, body.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash);
        }

        // Line endings do not count as an edit, so a checkout that turned LF into CRLF still updates.
        private static byte[] Normalise(byte[] b) => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\r\n", "\n"));

        public static SortedDictionary<string, byte[]> ReadFolder(string folder)
        {
            var d = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                d[Path.GetRelativePath(folder, f).Replace('\\', '/')] = File.ReadAllBytes(f);
            return d;
        }

        public static Dictionary<string, Record> ReadRecords(string root)
        {
            try
            {
                var p = Path.Combine(root, RecordFile);
                if (File.Exists(p)) return JsonSerializer.Deserialize<Dictionary<string, Record>>(File.ReadAllText(p)) ?? new Dictionary<string, Record>();
            }
            catch { }
            return new Dictionary<string, Record>();
        }

        /// <summary>Installs or updates the built-in extensions. Returns the ids it installed or updated, which the
        /// caller turns on (they ship with the app, so they start on).</summary>
        public static List<string> Sync(string root, IEnumerable<BundledExtension> bundled)
        {
            Directory.CreateDirectory(root);
            var records = ReadRecords(root);
            var changed = new List<string>();
            foreach (var b in bundled)
            {
                var folder = Path.Combine(root, b.Id);
                records.TryGetValue(b.Id, out var rec);
                if (rec != null && rec.Removed) continue;
                if (!Directory.Exists(folder))
                {
                    if (rec != null) { rec.Removed = true; continue; }   // installed before, deleted since: respect that
                    Write(folder, b.Files);
                    records[b.Id] = new Record { Hash = b.Hash };
                    changed.Add(b.Id);
                    continue;
                }
                if (rec == null) continue;                                 // a folder of yours that happens to share the id
                var now = HashOf(ReadFolder(folder));
                if (now != rec.Hash || rec.Hash == b.Hash) continue;       // edited by you, or already current
                var old = folder + ".old";
                if (Directory.Exists(old)) Directory.Delete(old, true);
                Directory.Move(folder, old);
                try { Write(folder, b.Files); Directory.Delete(old, true); }
                catch { if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.Move(old, folder); throw; }
                rec.Hash = b.Hash;
                changed.Add(b.Id);
            }
            var tmp = Path.Combine(root, RecordFile + ".tmp");
            File.WriteAllText(tmp, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, Path.Combine(root, RecordFile), true);
            return changed;
        }

        /// <summary>True when the extension in this folder is a built-in one exactly as the app installed it.</summary>
        public static bool IsBuiltIn(string root, string id) => ReadRecords(root).TryGetValue(id, out var r) && !r.Removed;

        private static void Write(string folder, IDictionary<string, byte[]> files)
        {
            foreach (var kv in files)
            {
                var p = Path.GetFullPath(Path.Combine(folder, kv.Key));
                if (!p.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllBytes(p, kv.Value);
            }
        }
    }
}
