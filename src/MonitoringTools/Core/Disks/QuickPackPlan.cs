using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonitoringTools.Core.Disks
{
    /// <summary>
    /// Where a Quick pack puts its VHDX, what it is called and how big it starts. Kept free of any window or Windows-only
    /// code so the console checks can run it anywhere.
    /// </summary>
    public sealed class QuickPackPlan
    {
        public string Folder = "";
        public string Name = "";
        public string File = "";
        /// <summary>True when the dropped items came from more than one folder, so the result can say which folder was used.</summary>
        public bool MixedFolders;
        /// <summary>True when the plain name was taken and a number was added.</summary>
        public bool Numbered;
        public long ContentBytes;
        public int Files;
        public int Folders;
        public long RoundingBytes;
        public long RecordBytes;
        public long TableBytes;
        public long HeadroomBytes;
        public long StartBytes;

        public const long Mb = 1024L * 1024;
        public const long MaxVhdxBytes = 64L * 1024 * 1024 * Mb;
        /// <summary>The NTFS cluster size Windows picks for volumes of this size range.</summary>
        public const long Cluster = 4096;
        /// <summary>A file record is 1 KB on NTFS; folders also get index blocks, so they are counted at 4 KB.</summary>
        public const long FileRecord = 1024, FolderRecord = 4096;
        /// <summary>GPT tables and the reserved partition at the start, the backup table at the end, and the 1 MB alignment.</summary>
        public const long PartitionTables = 16 * Mb + 2 * Mb;

        /// <summary>
        /// The plan for these items. The name comes from the single dropped item, or the folder the items came from; items from
        /// several folders use the first item's folder. A taken name gets a number; nothing is ever overwritten.
        /// </summary>
        public static QuickPackPlan For(IReadOnlyList<string> items, Func<string, bool> exists = null, IEnumerable<(long Size, bool IsFolder)> entries = null)
        {
            exists ??= p => System.IO.File.Exists(p) || Directory.Exists(p);
            if (items == null || items.Count == 0) throw new ArgumentException("Nothing was dropped.");
            var clean = items.Select(i => i.TrimEnd('\\', '/')).ToList();
            var parents = clean.Select(i => Path.GetDirectoryName(i) ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var p = new QuickPackPlan { Folder = Path.GetDirectoryName(clean[0]) ?? "", MixedFolders = parents.Count > 1 };
            string baseName = clean.Count == 1
                ? (Directory.Exists(clean[0]) || Path.GetExtension(clean[0]).Length == 0 ? Path.GetFileName(clean[0]) : Path.GetFileNameWithoutExtension(clean[0]))
                : Path.GetFileName(p.Folder);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "Quick pack";   // items at the top of a drive
            foreach (var c in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');
            p.Name = baseName;
            var file = Path.Combine(p.Folder, baseName + ".vhdx");
            for (int n = 1; exists(file); n++) { file = Path.Combine(p.Folder, $"{baseName} ({n}).vhdx"); p.Numbered = true; }
            p.File = file;
            if (entries != null) p.Size(entries);
            return p;
        }

        /// <summary>
        /// Works out the starting size: the bytes, every file rounded up to whole clusters, a file system record for every file
        /// and folder, the partition tables and reserved space, and headroom of a tenth (at least 64 MB). If the copy still runs
        /// short, the pack grows the disk; this is only where it starts.
        /// </summary>
        public void Size(IEnumerable<(long Size, bool IsFolder)> entries)
        {
            ContentBytes = 0; RoundingBytes = 0; RecordBytes = 0; Files = 0; Folders = 0;
            foreach (var (size, folder) in entries)
            {
                if (folder) { Folders++; RecordBytes += FolderRecord; continue; }
                Files++;
                ContentBytes += size;
                long rounded = (size + Cluster - 1) / Cluster * Cluster;
                RoundingBytes += rounded - size;
                RecordBytes += FileRecord;
            }
            TableBytes = PartitionTables;
            long body = ContentBytes + RoundingBytes + RecordBytes + TableBytes;
            HeadroomBytes = Math.Max(64 * Mb, body / 10);
            StartBytes = RoundUp(body + HeadroomBytes, Mb);
            StartBytes = Math.Max(StartBytes, 64 * Mb);
        }

        /// <summary>The size to grow to when the copy runs short: enough for what is left plus a tenth, at least 1 GB more.</summary>
        public static long GrowTo(long currentBytes, long bytesLeft) =>
            Math.Min(MaxVhdxBytes, RoundUp(currentBytes + Math.Max(1024 * Mb, bytesLeft + bytesLeft / 10 + 64 * Mb), Mb));

        public static long RoundUp(long v, long unit) => (v + unit - 1) / unit * unit;

        /// <summary>The volume label from the name: NTFS takes 32 characters of letters, digits, spaces and - _ . ( ).</summary>
        public static string LabelFor(string name)
        {
            var chars = (name ?? "").Where(c => char.IsLetterOrDigit(c) || " _.()-".IndexOf(c) >= 0).ToArray();
            var label = new string(chars).Trim();
            if (label.Length > 32) label = label.Substring(0, 32).Trim();
            return label.Length == 0 ? "Quick pack" : label;
        }
    }
}
