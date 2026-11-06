using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core.Disks
{
    /// <summary>What the normal app asks the elevated helper to build. Everything in it is validated again by the helper.</summary>
    public sealed class VhdRequest
    {
        public int Version { get; set; } = 1;
        public string File { get; set; }              // absolute path ending in .vhd or .vhdx
        public long SizeMb { get; set; }
        public bool Fixed { get; set; }               // false: dynamically expanding
        public string FileSystem { get; set; } = "NTFS";   // NTFS, exFAT or FAT32
        public string Label { get; set; } = "";
        public string MountMode { get; set; } = "letter";  // letter or folder
        public string Letter { get; set; }            // one letter, for letter mode
        public string Folder { get; set; }            // an empty NTFS folder, for folder mode
        public string UserSid { get; set; }           // the person who gets write access to a new NTFS volume
        public int ParentPid { get; set; }            // the helper leaves (keeping the mount) when this process is gone
        public string StatusFile { get; set; }
        public string CommandFile { get; set; }

        public bool Vhdx => File != null && File.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class VhdStatus
    {
        public string Phase { get; set; } = "";       // started, created, attached, formatted, ready, detached, deleted, error
        public string Message { get; set; } = "";
        public string Error { get; set; }
        public int ExitCode { get; set; }
        public string Mount { get; set; }             // the root path of the new volume, once it exists
        public string Log { get; set; }
        public long Stamp { get; set; }
    }

    public static class VhdEngine
    {
        public const long MaxVhdMb = 2040L * 1024;
        public const long MaxVhdxMb = 64L * 1024 * 1024;
        public const long MaxFat32Mb = 32L * 1024;
        public const long MinMb = 32;
        public const long Fat32FileLimit = 4294967295L;

        public static readonly string[] FileSystems = { "NTFS", "exFAT", "FAT32" };

        public static long ToMb(double value, string unit) => unit switch { "TB" => (long)(value * 1024 * 1024), "GB" => (long)(value * 1024), _ => (long)value };

        public static int MaxLabel(string fs) => fs == "NTFS" ? 32 : 11;

        private static readonly Regex LabelChars = new Regex(@"^[\p{L}\p{N} _.()-]*$", RegexOptions.Compiled);
        private static readonly Regex SidChars = new Regex(@"^S-1-[0-9]+(-[0-9]+)*$", RegexOptions.Compiled);

        /// <summary>The disk file name with the right extension, never colliding with an existing file.</summary>
        public static string UniqueFile(string folder, string name, bool vhdx)
        {
            var ext = vhdx ? ".vhdx" : ".vhd";
            for (int n = 0; n < 1000; n++)
            {
                var c = Path.Combine(folder, n == 0 ? name + ext : $"{name} ({n}){ext}");
                if (!System.IO.File.Exists(c)) return c;
            }
            return Path.Combine(folder, name + "-" + Guid.NewGuid().ToString("N")[..6] + ext);
        }

        /// <summary>Drive letters nobody uses, lowest first, from D to Z.</summary>
        public static List<string> FreeLetters()
        {
            var used = new HashSet<char>(DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])));
            // A mapped network drive or a card reader with no card still holds its letter, so ask the logical drive mask too.
            uint mask = GetLogicalDrives();
            var list = new List<string>();
            for (char c = 'D'; c <= 'Z'; c++) if (!used.Contains(c) && (mask & (1u << (c - 'A'))) == 0) list.Add(c.ToString());
            return list;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern uint GetLogicalDrives();

        /// <summary>
        /// Every check that can be made before the consent prompt. Null means the request can run. Strict on purpose: the
        /// elevated helper runs the same function on the bytes it reads, so a changed request file cannot widen what it does.
        /// </summary>
        /// <summary>The code page diskpart reads its scripts in: the console (OEM) code page of this computer, not Unicode.</summary>
        public static Encoding ScriptEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            int cp; try { cp = GetOEMCP(); } catch { cp = 437; }
            return Encoding.GetEncoding(cp, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern int GetOEMCP();

        /// <summary>The first character in the text that the script code page cannot carry, or null when all of it can.</summary>
        public static string ScriptUnreadable(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var enc = ScriptEncoding();
            foreach (var ch in text.EnumerateRunes())
            {
                try { enc.GetBytes(ch.ToString()); } catch (EncoderFallbackException) { return ch.ToString(); }
            }
            return null;
        }

        public static string Validate(VhdRequest r, long? freeOnTargetBytes = null, long contentBytes = 0, long largestFile = 0, bool checkFolderState = true)
        {
            if (r == null || r.Version != 1) return "The request is not one this version understands.";
            if (string.IsNullOrWhiteSpace(r.File) || !Path.IsPathFullyQualified(r.File)) return "The disk file needs a full path.";
            if (r.File.StartsWith(@"\\")) return "A virtual disk file must be on a local drive, not on a network share.";
            if (r.File.IndexOfAny(new[] { '"', '<', '>', '|', '*', '?', '\r', '\n', '\0' }) >= 0) return "The disk file path contains characters that cannot be used.";
            var fileName = Path.GetFileNameWithoutExtension(r.File);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "Give the disk a name without \\ / : * ? \" < > or |.";
            var ext = Path.GetExtension(r.File);
            if (!ext.Equals(".vhd", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".vhdx", StringComparison.OrdinalIgnoreCase)) return "The disk file must end in .vhd or .vhdx.";
            if (!FileSystems.Contains(r.FileSystem)) return "The file system must be NTFS, exFAT or FAT32.";
            var unreadable = ScriptUnreadable(r.File) ?? ScriptUnreadable(r.Label) ?? (r.MountMode == "folder" ? ScriptUnreadable(r.Folder) : null);
            if (unreadable != null) return $"Windows disk tools cannot read the character {unreadable} on this computer. Use a name, folder and label without it.";
            if (r.SizeMb < MinMb) return $"The disk must be at least {MinMb} MB.";
            if (r.Vhdx && r.SizeMb > MaxVhdxMb) return "VHDX goes up to 64 TB.";
            if (!r.Vhdx && r.SizeMb > MaxVhdMb) return "VHD goes up to 2,040 GB. Use VHDX for a bigger disk.";
            if (r.FileSystem == "FAT32" && r.SizeMb > MaxFat32Mb) return "Windows formats FAT32 only up to 32 GB here. Choose NTFS or exFAT, or a smaller disk.";
            if (r.FileSystem == "FAT32" && r.SizeMb < 64) return "FAT32 needs a disk of at least 64 MB.";
            if (r.FileSystem == "FAT32" && largestFile > Fat32FileLimit) return "A file in your selection is larger than 4 GB, which FAT32 cannot hold. Choose NTFS or exFAT.";
            var label = r.Label ?? "";
            if (label.Length > MaxLabel(r.FileSystem)) return $"The volume label can have {MaxLabel(r.FileSystem)} characters for {r.FileSystem}.";
            if (!LabelChars.IsMatch(label)) return "The volume label can use letters, numbers, spaces and - _ . ( ) only.";
            if (r.MountMode == "letter")
            {
                if (string.IsNullOrEmpty(r.Letter) || r.Letter.Length != 1 || !char.IsAsciiLetter(r.Letter[0])) return "Choose a drive letter.";
                if (r.Letter.ToUpperInvariant()[0] < 'D') return "Drive letters before D are kept for the system.";
                if (checkFolderState && !FreeLetters().Contains(r.Letter.ToUpperInvariant())) return $"{r.Letter.ToUpperInvariant()}: is already in use.";
            }
            else if (r.MountMode == "folder")
            {
                if (string.IsNullOrWhiteSpace(r.Folder) || !Path.IsPathFullyQualified(r.Folder)) return "Choose an empty folder on an NTFS drive to mount into.";
                if (r.Folder.IndexOfAny(new[] { '"', '<', '>', '|', '*', '?', '\r', '\n', '\0' }) >= 0 || r.Folder.StartsWith(@"\\")) return "The mount folder path cannot be used.";
                if (Path.GetPathRoot(r.Folder).TrimEnd('\\').Equals(r.Folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return "Mount into a folder, not the top of a drive.";
                if (checkFolderState)
                {
                    if (!Directory.Exists(r.Folder)) return "The mount folder does not exist.";
                    if (Directory.EnumerateFileSystemEntries(r.Folder).Any()) return "The mount folder is not empty.";
                    try { if (!new DriveInfo(Path.GetPathRoot(r.Folder)).DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase)) return "A volume can be mounted only into a folder on an NTFS drive."; } catch { return "The mount folder's drive cannot be read."; }
                }
            }
            else return "Choose how to mount: a drive letter or a folder.";
            if (r.UserSid != null && !SidChars.IsMatch(r.UserSid)) return "The account identifier is not valid.";
            if (freeOnTargetBytes.HasValue)
            {
                long sizeBytes = r.SizeMb * 1048576L;
                if (r.Fixed && sizeBytes > freeOnTargetBytes.Value - 64L * 1048576) return $"A fixed disk takes the full size at once, and the drive has {Bytes(freeOnTargetBytes.Value)} free.";
                if (!r.Fixed && freeOnTargetBytes.Value < 128L * 1048576) return "The drive has less than 128 MB free, which a new disk needs to start.";
            }
            if (contentBytes > 0)
            {
                long usable = (long)(r.SizeMb * 1048576L * (r.FileSystem == "NTFS" ? 0.94 : 0.97)) - 8L * 1048576;
                if (contentBytes > usable) return $"The selected items ({Bytes(contentBytes)}) do not fit in a {Bytes(r.SizeMb * 1048576L)} disk once the file system takes its share. Make the disk bigger or leave it empty.";
            }
            return null;
        }

        public static string Bytes(long b)
        {
            if (b < 1024L * 1024) return (b / 1024.0).ToString("0.#") + " KB";
            if (b < 1024L * 1024 * 1024) return (b / 1048576.0).ToString("0.#") + " MB";
            if (b < 1024L * 1024 * 1024 * 1024) return (b / 1073741824.0).ToString("0.##") + " GB";
            return (b / 1099511627776.0).ToString("0.##") + " TB";
        }

        // ------------------------------------------------------------ the diskpart scripts, written from validated values only

        private static string Q(string s) => "\"" + s + "\"";

        /// <summary>Phase one: create the file and attach it.</summary>
        public static string CreateScript(VhdRequest r) => string.Join("\r\n", new[]
        {
            $"create vdisk file={Q(r.File)} maximum={r.SizeMb} type={(r.Fixed ? "fixed" : "expandable")}",
            $"select vdisk file={Q(r.File)}",
            "attach vdisk",
            "exit"
        }) + "\r\n";

        /// <summary>Phase two: a GPT partition, a quick format and the mount point.</summary>
        public static string FormatScript(VhdRequest r)
        {
            var fs = r.FileSystem.ToLowerInvariant();
            var label = string.IsNullOrEmpty(r.Label) ? "" : " label=" + Q(r.Label);
            var lines = new List<string>
            {
                $"select vdisk file={Q(r.File)}",
                "convert gpt",
                "create partition primary",
                $"format fs={fs} quick{label}",
                r.MountMode == "folder" ? $"assign mount={Q(r.Folder)}" : $"assign letter={r.Letter.ToUpperInvariant()}",
                "exit"
            };
            return string.Join("\r\n", lines) + "\r\n";
        }

        public static string DetachScript(VhdRequest r) => $"select vdisk file={Q(r.File)}\r\ndetach vdisk\r\nexit\r\n";

        /// <summary>The root path the new volume will have.</summary>
        public static string MountRoot(VhdRequest r) => r.MountMode == "folder" ? r.Folder.TrimEnd('\\') + "\\" : r.Letter.ToUpperInvariant() + @":\";
    }
}
