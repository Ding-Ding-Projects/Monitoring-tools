using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MonitoringTools.Core.Packing
{
    /// <summary>A bounded, immutable source inventory shared by both packing formats.</summary>
    public sealed class PackingSources
    {
        public string[] Items;
        public string Folder, Name;
        public bool MixedFolders;
        public readonly List<(string Source, string Relative, bool Folder, long Size)> Entries = new();
        public readonly List<string> Warnings = new();
        public const int Limit = 250000;
        private int _visited, _entryLimit;
        private sealed class LimitException : Exception { public LimitException(string message = "The selection exceeds 250,000 entries. Pack a smaller selection.") : base(message) { } }

        public static PackingSources Scan(IEnumerable<string> paths, CancellationToken ct, int entryLimit = Limit)
        {
            if (entryLimit < 1 || entryLimit > Limit) throw new ArgumentOutOfRangeException(nameof(entryLimit));
            var result = new PackingSources { _entryLimit = entryLimit };
            var raw = paths.Take(entryLimit + 1).ToArray();
            if (raw.Length > entryLimit) throw new LimitException();
            var items = raw.Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p.Trim().Trim('"'))))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (items.Length == 0) throw new IOException("Nothing to pack.");
            // Keep input order for destination selection, even when a later parent subsumes the first item.
            result.Folder = Path.GetDirectoryName(items[0]);
            if (string.IsNullOrEmpty(result.Folder)) throw new IOException("Choose files or folders below a drive root.");
            result.MixedFolders = items.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
            result.Name = items.Length == 1 ? (Directory.Exists(items[0]) ? Path.GetFileName(items[0]) : Path.GetFileNameWithoutExtension(items[0])) : Path.GetFileName(result.Folder);
            if (string.IsNullOrWhiteSpace(result.Name)) result.Name = "Quick pack";
            var selectedFolders = new HashSet<string>(items.Where(Directory.Exists), StringComparer.OrdinalIgnoreCase);
            bool HasSelectedParent(string path)
            {
                for (var parent = Path.GetDirectoryName(path); parent != null; parent = Path.GetDirectoryName(parent))
                    if (selectedFolders.Contains(Path.TrimEndingDirectorySeparator(parent))) return true;
                return false;
            }
            result.Items = items.Where(p => !HasSelectedParent(p)).ToArray();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in result.Items)
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(path);
                var unique = name;
                for (int n = 2; !names.Add(unique); n++) unique = Path.GetFileNameWithoutExtension(name) + " (" + n + ")" + Path.GetExtension(name);
                result.Visit(path, unique, ct);
            }
            if (result.Entries.Count == 0) throw new IOException("No readable files or folders were found. " + string.Join("\n", result.Warnings));
            return result;
        }

        private void Visit(string path, string relative, CancellationToken ct, int depth = 0)
        {
            ct.ThrowIfCancellationRequested();
            if (depth > 128) throw new LimitException("The selection exceeds 128 folder levels. Pack a shallower selection.");
            if (++_visited > _entryLimit) throw new LimitException();
            try
            {
                var attr = File.GetAttributes(path);
                if ((attr & FileAttributes.ReparsePoint) != 0) { Warnings.Add(path + ": symbolic links and junctions are not followed."); return; }
                if ((attr & FileAttributes.Directory) == 0) { Entries.Add((path, relative, false, new FileInfo(path).Length)); return; }
                Entries.Add((path, relative, true, 0));
                // Enumeration remains bounded; the surrounding catch records lazy read failures.
                foreach (var child in Directory.EnumerateFileSystemEntries(path)) Visit(child, Path.Combine(relative, Path.GetFileName(child)), ct, depth + 1);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { Warnings.Add(path + ": " + ex.Message); }
        }

        /// <summary>Rename on the same volume, atomically refusing every occupied destination.</summary>
        public static string Publish(string staged, string folder, string name, string extension)
        {
            for (int n = 0; n < 10000; n++)
            {
                var path = Path.Combine(folder, name + (n == 0 ? "" : " (" + n + ")") + extension);
                try { File.Move(staged, path, overwrite: false); return path; }
                catch (IOException) when (File.Exists(path) || Directory.Exists(path)) { }
            }
            throw new IOException("No available output name was found.");
        }
    }
}
