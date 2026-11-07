using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonitoringTools.Core.Disks
{
    /// <summary>One change to a virtual disk, as the page asks for it and as the elevated helper checks it again.</summary>
    public sealed class DiskJob
    {
        public int Version { get; set; } = 1;
        /// <summary>attach, detach, grow, shrink, compact, merge, reset-id, set-parent, convert, create</summary>
        public string Op { get; set; } = "";
        public string File { get; set; } = "";
        public bool ReadOnly { get; set; }
        public bool NoLetters { get; set; }
        /// <summary>grow: the new size; create: the size.</summary>
        public long SizeBytes { get; set; }
        /// <summary>grow: extend the last volume into the new space; shrink: shrink the volume first.</summary>
        public bool Volume { get; set; } = true;
        /// <summary>compact: skip the scan for zeroed blocks.</summary>
        public bool Quick { get; set; } = true;
        /// <summary>set-parent and create (differencing): the parent; convert and create (copy): the source.</summary>
        public string Other { get; set; }
        /// <summary>create and convert: fixed or dynamic; create also differencing or copy.</summary>
        public string Type { get; set; } = "dynamic";
        public int BlockSizeMb { get; set; }
        public int LogicalSector { get; set; }
        public int PhysicalSector { get; set; }
        /// <summary>create: GPT, MBR or none (left empty).</summary>
        public string Init { get; set; } = "GPT";
        public string FileSystem { get; set; } = "NTFS";
        public string Label { get; set; } = "";
        public bool DevDrive { get; set; }
        /// <summary>create: leave it attached afterwards.</summary>
        public bool KeepAttached { get; set; } = true;
        public string StatusFile { get; set; }
    }

    public sealed class DiskJobStatus
    {
        public string Phase { get; set; } = "";
        public string Message { get; set; } = "";
        public string Error { get; set; }
        public string Route { get; set; }
        public string Log { get; set; }
    }

    /// <summary>
    /// Checks, scripts and runs one virtual disk change. The page checks a job, shows the same steps as PowerShell and
    /// DiskPart scripts, and runs it through the elevated helper behind one consent prompt; the helper checks the job again
    /// before it touches anything. Every change goes through the Windows virtual disk service, which Windows 8 and later
    /// have on every edition, with the Storage commands for volumes, so nothing depends on Hyper-V.
    /// </summary>
    public static partial class DiskJobs
    {
        public const string Arg = "--vdisk-job";
        public const long Mb = 1024L * 1024, Gb = 1024L * Mb;
        public const long MinBytes = 3 * Mb, MaxVhdBytes = 2040 * Gb, MaxVhdxBytes = 64L * 1024 * Gb;
        public static readonly string[] Ops = { "attach", "detach", "grow", "shrink", "compact", "merge", "reset-id", "set-parent", "convert", "create" };
        public static readonly string[] DiskExtensions = { ".vhd", ".vhdx", ".vhds", ".avhd", ".avhdx", ".iso" };

        public static bool IsDiskFile(string path) => path != null && DiskExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        public static long MaxFor(string path) => Path.GetExtension(path ?? "").ToLowerInvariant() is ".vhd" or ".avhd" ? MaxVhdBytes : MaxVhdxBytes;

        /// <summary>Null when the job can run, or the sentence that says why not. Checks the paths, the format limits and the options.</summary>
        public static string Validate(DiskJob j, Func<string, bool> exists = null)
        {
            exists ??= System.IO.File.Exists;
            if (j == null || j.Version != 1 || !Ops.Contains(j.Op)) return "The request is not one this version understands.";
            string PathProblem(string p, string what)
            {
                if (string.IsNullOrWhiteSpace(p) || !Path.IsPathFullyQualified(p)) return what + " needs a full path.";
                if (p.StartsWith(@"\\")) return what + " must be on a local drive, not on a network share.";
                if (p.IndexOfAny(new[] { '"', '<', '>', '|', '*', '?', '\r', '\n', '\0' }) >= 0) return what + " has characters that cannot be used.";
                return null;
            }
            var bad = PathProblem(j.File, "The disk file");
            if (bad != null) return bad;
            var ext = Path.GetExtension(j.File).ToLowerInvariant();
            bool iso = ext == ".iso", vhd = ext is ".vhd" or ".avhd", vhdx = ext is ".vhdx" or ".avhdx", set = ext == ".vhds";
            if (!DiskExtensions.Contains(ext)) return "The disk file must end in .vhd, .vhdx, .vhds, .avhd, .avhdx or .iso.";
            if (j.Op == "create")
            {
                if (iso) return "An ISO image is made by other tools; this creates VHD, VHDX and VHD Set files.";
                if (exists(j.File)) return Path.GetFileName(j.File) + " already exists. Choose another name; nothing is ever overwritten.";
                if (j.Type == "differencing" || j.Type == "copy")
                {
                    var p = PathProblem(j.Other, j.Type == "differencing" ? "The parent" : "The source");
                    if (p != null) return p;
                    if (!exists(j.Other)) return (j.Type == "differencing" ? "The parent " : "The source ") + Path.GetFileName(j.Other) + " is not there.";
                }
                else
                {
                    if (j.Type != "fixed" && j.Type != "dynamic") return "Choose fixed size or dynamically expanding.";
                    if (j.SizeBytes < MinBytes) return "3 MB is the smallest disk any format allows.";
                    if (vhd && j.SizeBytes > MaxVhdBytes) return "A VHD holds at most 2,040 GB. Choose VHDX for a bigger disk, or a smaller size.";
                    if (j.SizeBytes > MaxVhdxBytes) return "A VHDX holds at most 64 TB.";
                }
                if (j.BlockSizeMb != 0)
                {
                    if (!vhdx && !set) return "Only a VHDX or VHD Set takes a block size.";
                    if (j.BlockSizeMb < 1 || j.BlockSizeMb > 256 || (j.BlockSizeMb & (j.BlockSizeMb - 1)) != 0) return "The block size is a power of two from 1 MB to 256 MB.";
                }
                if (j.LogicalSector != 0 && j.LogicalSector != 512 && j.LogicalSector != 4096) return "The logical sector size is 512 or 4096 bytes.";
                if (j.PhysicalSector != 0 && j.PhysicalSector != 512 && j.PhysicalSector != 4096) return "The physical sector size is 512 or 4096 bytes.";
                if (j.Init != "GPT" && j.Init != "MBR" && j.Init != "none") return "Choose GPT, MBR or leave it empty.";
                if (j.Init != "none" && j.Type != "differencing" && j.Type != "copy")
                {
                    if (!new[] { "NTFS", "ReFS", "exFAT", "FAT32", "FAT" }.Contains(j.FileSystem)) return "The file system is NTFS, ReFS, exFAT, FAT32 or FAT.";
                    int max = j.FileSystem is "NTFS" or "ReFS" ? 32 : 11;
                    if ((j.Label ?? "").Length > max) return $"The label can have {max} characters for {j.FileSystem}.";
                    if ((j.Label ?? "").Any(c => !(char.IsLetterOrDigit(c) || " _.()-".IndexOf(c) >= 0))) return "The label can use letters, numbers, spaces and - _ . ( ) only.";
                    if (j.DevDrive && j.FileSystem != "ReFS") return "A Dev Drive is ReFS.";
                    if (j.DevDrive && j.SizeBytes < 50 * Gb) return "A Dev Drive needs 50 GB at least.";
                    if (j.FileSystem == "FAT32" && j.SizeBytes > 32 * Gb) return "Windows formats FAT32 only up to 32 GB. Choose NTFS or exFAT.";
                }
                return null;
            }
            if (!exists(j.File)) return Path.GetFileName(j.File) + " is not there.";
            switch (j.Op)
            {
                case "grow":
                    if (iso) return "An ISO image cannot change size.";
                    if (set) return "Grow a VHD Set from Hyper-V, which manages its checkpoints.";
                    if (j.SizeBytes > MaxFor(j.File)) return vhd ? "A VHD holds at most 2,040 GB." : "A VHDX holds at most 64 TB.";
                    if (j.SizeBytes < MinBytes) return "Choose the new size.";
                    break;
                case "shrink":
                    if (!vhdx) return "Only a VHDX can shrink.";
                    break;
                case "compact":
                case "merge":
                case "reset-id":
                    if (iso) return "An ISO image cannot be changed.";
                    break;
                case "set-parent":
                    if (iso) return "An ISO image has no parent.";
                    var pp = PathProblem(j.Other, "The parent");
                    if (pp != null) return pp;
                    if (!exists(j.Other)) return "The parent " + Path.GetFileName(j.Other) + " is not there.";
                    break;
                case "convert":
                    if (iso) return "An ISO image cannot be converted here.";
                    var tp = PathProblem(j.Other, "The new file");
                    if (tp != null) return tp;
                    var text = Path.GetExtension(j.Other).ToLowerInvariant();
                    if (text != ".vhd" && text != ".vhdx" && text != ".vhds") return "The new file must end in .vhd, .vhdx or .vhds.";
                    if (exists(j.Other)) return Path.GetFileName(j.Other) + " already exists. Choose another name; nothing is ever overwritten.";
                    if (j.Type != "fixed" && j.Type != "dynamic") return "Choose fixed size or dynamically expanding for the new file.";
                    break;
            }
            return null;
        }

        // ------------------------------------------------------------------ the same steps as scripts

        private static string Q(string s) => "'" + (s ?? "").Replace("'", "''") + "'";
        private static string Dq(string s) => "\"" + s + "\"";

        /// <summary>
        /// The same steps as a PowerShell script and a DiskPart script, for the person to read or copy. A step a route
        /// cannot take says so in a comment instead of pretending.
        /// </summary>
        public static (string PowerShell, string DiskPart) Scripts(DiskJob j, bool hyperV)
        {
            var ps = new List<string>(); var dp = new List<string>();
            string f = j.File;
            void NoHyperV(string cmdlet) => ps.Add($"# {cmdlet} needs the Hyper-V commands, which this PC does not have; the DiskPart steps do it here.");
            switch (j.Op)
            {
                case "attach":
                    ps.Add($"Mount-DiskImage -ImagePath {Q(f)}" + (j.ReadOnly ? " -Access ReadOnly" : "") + (j.NoLetters ? " -NoDriveLetter" : ""));
                    dp.Add($"select vdisk file={Dq(f)}"); dp.Add("attach vdisk" + (j.ReadOnly ? " readonly" : ""));
                    break;
                case "detach":
                    ps.Add($"Dismount-DiskImage -ImagePath {Q(f)}");
                    dp.Add($"select vdisk file={Dq(f)}"); dp.Add("detach vdisk");
                    break;
                case "grow":
                    if (hyperV) ps.Add($"Resize-VHD -Path {Q(f)} -SizeBytes {j.SizeBytes}"); else NoHyperV("Resize-VHD");
                    if (j.Volume)
                    {
                        ps.Add($"$d = Get-DiskImage -ImagePath {Q(f)} | Get-Disk");
                        ps.Add("$p = Get-Partition -DiskNumber $d.Number | Where-Object { $_.Type -ne 'Reserved' } | Sort-Object Offset | Select-Object -Last 1");
                        ps.Add("Resize-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -Size (Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber).SizeMax");
                    }
                    dp.Add($"select vdisk file={Dq(f)}"); dp.Add($"expand vdisk maximum={j.SizeBytes / Mb}");
                    if (j.Volume) { dp.Add("attach vdisk"); dp.Add("rem select the last partition with: list partition, select partition <n>"); dp.Add("extend"); }
                    break;
                case "shrink":
                    if (j.Volume)
                    {
                        ps.Add($"$d = Get-DiskImage -ImagePath {Q(f)} | Get-Disk");
                        ps.Add("$p = Get-Partition -DiskNumber $d.Number | Where-Object { $_.Type -ne 'Reserved' } | Sort-Object Offset | Select-Object -Last 1");
                        ps.Add("Resize-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -Size (Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber).SizeMin");
                        ps.Add($"Dismount-DiskImage -ImagePath {Q(f)}");
                    }
                    if (hyperV) ps.Add($"Resize-VHD -Path {Q(f)} -ToMinimumSize");
                    else ps.Add("# Resize-VHD needs the Hyper-V commands; Monitoring Tools shrinks it through the Windows virtual disk service (ResizeVirtualDisk, to the smallest safe size).");
                    dp.Add("rem DiskPart cannot shrink a virtual disk. Shrink the volume with: select volume <n>, shrink");
                    break;
                case "compact":
                    if (hyperV) ps.Add($"Optimize-VHD -Path {Q(f)} -Mode {(j.Quick ? "Quick" : "Full")}");
                    else ps.Add("# Optimize-VHD needs the Hyper-V commands; the DiskPart steps do it here.");
                    dp.Add($"select vdisk file={Dq(f)}"); dp.Add("attach vdisk readonly"); dp.Add("compact vdisk"); dp.Add("detach vdisk");
                    break;
                case "merge":
                    if (hyperV) ps.Add($"Merge-VHD -Path {Q(f)}" + (j.Other != null ? $" -DestinationPath {Q(j.Other)}" : "")); else NoHyperV("Merge-VHD");
                    dp.Add($"select vdisk file={Dq(f)}"); dp.Add("merge vdisk depth=1");
                    break;
                case "reset-id":
                    if (hyperV) ps.Add($"Set-VHD -Path {Q(f)} -ResetDiskIdentifier");
                    else ps.Add("# Set-VHD needs the Hyper-V commands; Monitoring Tools sets the new identifier through the Windows virtual disk service.");
                    dp.Add("rem DiskPart cannot give a virtual disk a new identifier.");
                    break;
                case "set-parent":
                    if (hyperV) ps.Add($"Set-VHD -Path {Q(f)} -ParentPath {Q(j.Other)}");
                    else ps.Add("# Set-VHD needs the Hyper-V commands; Monitoring Tools sets the parent through the Windows virtual disk service.");
                    dp.Add("rem DiskPart cannot reconnect a moved parent.");
                    break;
                case "convert":
                    if (hyperV) ps.Add($"Convert-VHD -Path {Q(f)} -DestinationPath {Q(j.Other)} -VHDType {(j.Type == "fixed" ? "Fixed" : "Dynamic")}"); else NoHyperV("Convert-VHD");
                    dp.Add($"create vdisk file={Dq(j.Other)} source={Dq(f)}" + (j.Type == "fixed" ? " type=fixed" : " type=expandable"));
                    break;
                case "create":
                    long mb = j.SizeBytes / Mb;
                    if (hyperV)
                        ps.Add(j.Type == "differencing" ? $"New-VHD -Path {Q(f)} -Differencing -ParentPath {Q(j.Other)}"
                            : j.Type == "copy" ? $"New-VHD -Path {Q(f)} -Dynamic -SourceDisk <number>   # for a virtual disk source, Convert-VHD -Path {Q(j.Other)} -DestinationPath {Q(f)}"
                            : $"New-VHD -Path {Q(f)} -SizeBytes {j.SizeBytes} -{(j.Type == "fixed" ? "Fixed" : "Dynamic")}" + (j.BlockSizeMb > 0 ? $" -BlockSizeBytes {j.BlockSizeMb}MB" : "") + (j.LogicalSector > 0 ? $" -LogicalSectorSizeBytes {j.LogicalSector}" : "") + (j.PhysicalSector > 0 ? $" -PhysicalSectorSizeBytes {j.PhysicalSector}" : ""));
                    else NoHyperV("New-VHD");
                    dp.Add(j.Type == "differencing" ? $"create vdisk file={Dq(f)} parent={Dq(j.Other)}"
                        : j.Type == "copy" ? $"create vdisk file={Dq(f)} source={Dq(j.Other)}"
                        : $"create vdisk file={Dq(f)} maximum={mb} type={(j.Type == "fixed" ? "fixed" : "expandable")}");
                    if (j.BlockSizeMb > 0 || j.LogicalSector > 0 || j.PhysicalSector > 0) dp.Add("rem DiskPart cannot set the block or sector sizes; the Windows virtual disk service does.");
                    if (j.Init != "none" && j.Type != "differencing" && j.Type != "copy")
                    {
                        ps.Add($"Mount-DiskImage -ImagePath {Q(f)} -NoDriveLetter");
                        ps.Add($"$d = Get-DiskImage -ImagePath {Q(f)} | Get-Disk");
                        ps.Add($"Initialize-Disk -Number $d.Number -PartitionStyle {j.Init}");
                        ps.Add("New-Partition -DiskNumber $d.Number -UseMaximumSize" + (j.NoLetters ? "" : " -AssignDriveLetter") + " |");
                        ps.Add("  Format-Volume " + (j.DevDrive ? "-DevDrive" : "-FileSystem " + j.FileSystem) + (string.IsNullOrEmpty(j.Label) ? "" : " -NewFileSystemLabel " + Q(j.Label)) + " -Confirm:$false");
                        if (!j.KeepAttached) ps.Add($"Dismount-DiskImage -ImagePath {Q(f)}");
                        dp.Add($"select vdisk file={Dq(f)}"); dp.Add("attach vdisk"); dp.Add("convert " + j.Init.ToLowerInvariant()); dp.Add("create partition primary");
                        dp.Add($"format fs={j.FileSystem.ToLowerInvariant()} quick" + (string.IsNullOrEmpty(j.Label) ? "" : " label=" + Dq(j.Label)));
                        if (j.DevDrive) dp.Add("rem A Dev Drive needs Format-Volume -DevDrive; see the PowerShell steps.");
                        if (!j.NoLetters) dp.Add("assign");
                        if (!j.KeepAttached) dp.Add("detach vdisk");
                    }
                    break;
            }
            return (string.Join("\r\n", ps), string.Join("\r\n", dp));
        }

    }
}
