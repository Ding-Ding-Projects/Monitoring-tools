using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MonitoringTools.Core.Disks
{
    /// <summary>What the Windows virtual disk service reports about one VHD, VHDX, VHD Set or ISO file.</summary>
    public sealed class VDiskInfo
    {
        public string Path { get; set; } = "";
        /// <summary>ISO, VHD, VHDX or VHD Set.</summary>
        public string Format { get; set; } = "";
        /// <summary>fixed, dynamic or differencing; empty for an ISO.</summary>
        public string Type { get; set; } = "";
        public ulong VirtualSize { get; set; }
        public ulong PhysicalSize { get; set; }
        public uint BlockSize { get; set; }
        public uint LogicalSectorSize { get; set; }
        public uint? PhysicalSectorSize { get; set; }
        public ulong? SmallestSafeSize { get; set; }
        public uint? FragmentationPercent { get; set; }
        public bool? Is4KAligned { get; set; }
        public bool? IsLoaded { get; set; }
        public Guid? Identifier { get; set; }
        public Guid? VirtualDiskId { get; set; }
        public bool? ChangeTracking { get; set; }
        public string ChangeTrackingId { get; set; }
        /// <summary>The parent locations a differencing disk records, best first.</summary>
        public List<string> ParentPaths { get; set; } = new List<string>();
        public bool? ParentResolved { get; set; }
        public Guid? ParentIdentifier { get; set; }
        public uint? ParentTimestamp { get; set; }
        /// <summary>\\.\PhysicalDriveN while attached, otherwise null.</summary>
        public string PhysicalPath { get; set; }
        /// <summary>Why the disk could not be read, or null when it was.</summary>
        public string Error { get; set; }

        public bool Attached => PhysicalPath != null;
        public int? DiskNumber => PhysicalPath != null && PhysicalPath.StartsWith(@"\\.\PhysicalDrive", StringComparison.OrdinalIgnoreCase) && int.TryParse(PhysicalPath.Substring(17), out var n) ? n : null;
    }

    /// <summary>
    /// The Windows virtual disk service (VirtDisk.dll), which every edition of Windows 8 and later has, Home included, so
    /// nothing here depends on Hyper-V. Each parameter block is written at its documented byte offsets into zeroed memory
    /// rather than marshalled through a C# structure, so a layout cannot drift. Reading information needs no rights;
    /// attaching, detaching and changing a disk need administrator rights and run in the elevated helper.
    /// </summary>
    public static class VirtDisk
    {
        public static readonly Guid VendorMicrosoft = new Guid("EC984AEC-A0F9-47E9-901F-71415A66345B");
        public const int DeviceUnknown = 0, DeviceIso = 1, DeviceVhd = 2, DeviceVhdx = 3, DeviceVhdSet = 4;

        private const int AccessNone = 0, AccessGetInfo = 0x80000, AccessAttachRo = 0x10000, AccessDetach = 0x40000, AccessMetaOps = 0x200000;
        private const int OpenFlagNone = 0, OpenFlagNoParents = 0x1;
        public const int AttachReadOnly = 0x1, AttachNoDriveLetter = 0x2, AttachPermanent = 0x4;
        private const int ResizeToSmallestSafe = 0x2;
        private const int CreateFullPhysicalAllocation = 0x1;
        private const int ErrorInsufficientBuffer = 122, ErrorMoreData = 234;

        [StructLayout(LayoutKind.Sequential)]
        private struct StorageType { public int DeviceId; public Guid VendorId; }

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern int OpenVirtualDisk(ref StorageType type, string path, int access, int flags, IntPtr parameters, out SafeFileHandle handle);
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern int CreateVirtualDisk(ref StorageType type, string path, int access, IntPtr securityDescriptor, int flags, int providerFlags, IntPtr parameters, IntPtr overlapped, out SafeFileHandle handle);
        [DllImport("virtdisk.dll")]
        private static extern int GetVirtualDiskInformation(SafeFileHandle handle, ref int size, IntPtr info, out int used);
        [DllImport("virtdisk.dll")]
        private static extern int SetVirtualDiskInformation(SafeFileHandle handle, IntPtr info);
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern int GetVirtualDiskPhysicalPath(SafeFileHandle handle, ref int sizeInBytes, IntPtr path);
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern int GetAllAttachedVirtualDiskPhysicalPaths(ref int sizeInBytes, IntPtr paths);
        [DllImport("virtdisk.dll")]
        private static extern int AttachVirtualDisk(SafeFileHandle handle, IntPtr securityDescriptor, int flags, int providerFlags, IntPtr parameters, IntPtr overlapped);
        [DllImport("virtdisk.dll")]
        private static extern int DetachVirtualDisk(SafeFileHandle handle, int flags, int providerFlags);
        [DllImport("virtdisk.dll")]
        private static extern int ResizeVirtualDisk(SafeFileHandle handle, int flags, IntPtr parameters, IntPtr overlapped);
        [DllImport("virtdisk.dll")]
        private static extern int ExpandVirtualDisk(SafeFileHandle handle, int flags, IntPtr parameters, IntPtr overlapped);
        [DllImport("virtdisk.dll")]
        private static extern int CompactVirtualDisk(SafeFileHandle handle, int flags, IntPtr parameters, IntPtr overlapped);
        [DllImport("virtdisk.dll")]
        private static extern int MergeVirtualDisk(SafeFileHandle handle, int flags, IntPtr parameters, IntPtr overlapped);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string name);

        private static bool? _available;
        /// <summary>True when this Windows has the virtual disk service.</summary>
        public static bool Available
        {
            get
            {
                if (_available == null)
                {
                    try { _available = OperatingSystem.IsWindows() && LoadLibraryW("virtdisk.dll") != IntPtr.Zero; }
                    catch { _available = false; }
                }
                return _available.Value;
            }
        }

        /// <summary>The Windows message for a failure code, with the code for searching.</summary>
        public static string Message(int code) => code == 0 ? null : new Win32Exception(code).Message.TrimEnd('.') + $" (code {code}).";

        private static void Check(int code, string what)
        {
            if (code != 0) throw new VDiskException(what + ": " + Message(code), code);
        }

        public static int DeviceOf(string path)
        {
            var ext = System.IO.Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext switch { ".iso" => DeviceIso, ".vhd" or ".avhd" => DeviceVhd, ".vhdx" or ".avhdx" => DeviceVhdx, ".vhds" => DeviceVhdSet, _ => DeviceUnknown };
        }

        private static StorageType TypeFor(string path)
        {
            int d = DeviceOf(path);
            return new StorageType { DeviceId = d, VendorId = d == DeviceUnknown ? Guid.Empty : VendorMicrosoft };
        }

        /// <summary>Zeroed native memory, freed with the scope.</summary>
        private sealed class Block : IDisposable
        {
            public IntPtr P { get; }
            public int Size { get; }
            public Block(int size) { Size = size; P = Marshal.AllocHGlobal(size); for (int i = 0; i < size; i++) Marshal.WriteByte(P, i, 0); }
            public void I32(int off, int v) => Marshal.WriteInt32(P, off, v);
            public void I64(int off, long v) => Marshal.WriteInt64(P, off, v);
            public void Ptr(int off, IntPtr v) => Marshal.WriteIntPtr(P, off, v);
            public void G(int off, Guid g) { var b = g.ToByteArray(); Marshal.Copy(b, 0, P + off, 16); }
            public int RI32(int off) => Marshal.ReadInt32(P, off);
            public long RI64(int off) => Marshal.ReadInt64(P, off);
            public Guid RG(int off) { var b = new byte[16]; Marshal.Copy(P + off, b, 0, 16); return new Guid(b); }
            public void Dispose() => Marshal.FreeHGlobal(P);
        }

        /// <summary>
        /// Opens a disk. Version 2 parameters (GetInfoOnly, ReadOnly) for VHDX and VHD Set; version 1 (read-write depth) for a
        /// VHD that is changed with the older calls, and for an ISO.
        /// </summary>
        private static SafeFileHandle Open(string path, bool infoOnly, bool readOnly = false, bool noParents = false, int v1Access = 0, int rwDepth = 1)
        {
            var type = TypeFor(path);
            bool v1 = v1Access != 0 || type.DeviceId == DeviceIso;
            using var p = new Block(64);
            int access;
            if (v1)
            {
                p.I32(0, 1);                                    // OPEN_VIRTUAL_DISK_VERSION_1
                p.I32(4, type.DeviceId == DeviceIso ? 0 : rwDepth);   // RWDepth
                access = v1Access != 0 ? v1Access : AccessAttachRo | AccessGetInfo | AccessDetach;
            }
            else
            {
                p.I32(0, 2);                                    // OPEN_VIRTUAL_DISK_VERSION_2
                p.I32(4, infoOnly ? 1 : 0);                     // GetInfoOnly
                p.I32(8, readOnly ? 1 : 0);                     // ReadOnly
                access = AccessNone;
            }
            int rc = OpenVirtualDisk(ref type, path, access, noParents ? OpenFlagNoParents : OpenFlagNone, p.P, out var h);
            if (rc != 0) { h?.Dispose(); throw new VDiskException("Could not open " + System.IO.Path.GetFileName(path) + ": " + Message(rc), rc); }
            return h;
        }

        /// <summary>Everything the service reports. Never throws: a disk that cannot be read comes back with Error set.</summary>
        public static VDiskInfo Read(string path)
        {
            var info = new VDiskInfo { Path = path };
            if (!Available) { info.Error = "The Windows virtual disk service is not on this computer."; return info; }
            SafeFileHandle h = null;
            try
            {
                try { h = Open(path, infoOnly: true); }
                catch (VDiskException ex) when (DeviceOf(path) != DeviceIso)
                {
                    // a differencing disk whose parent moved still opens on its own, so it can be shown and reconnected
                    try { h = Open(path, infoOnly: true, noParents: true); info.ParentResolved = false; }
                    catch { throw ex; }
                }
                using (h)
                {
                    using var b = Query(h, 6);                  // VIRTUAL_STORAGE_TYPE
                    if (b != null) info.Format = b.RI32(8) switch { DeviceIso => "ISO", DeviceVhd => "VHD", DeviceVhdx => "VHDX", DeviceVhdSet => "VHD Set", _ => "unknown" };
                    using (var s = Query(h, 1))                 // SIZE
                        if (s != null) { info.VirtualSize = (ulong)s.RI64(8); info.PhysicalSize = (ulong)s.RI64(16); info.BlockSize = (uint)s.RI32(24); info.LogicalSectorSize = (uint)s.RI32(28); }
                    using (var t = Query(h, 7))                 // PROVIDER_SUBTYPE
                        if (t != null) info.Type = t.RI32(8) switch { 2 => "fixed", 3 => "dynamic", 4 => "differencing", _ => "" };
                    using (var g = Query(h, 2)) if (g != null) info.Identifier = g.RG(8);
                    using (var g = Query(h, 14)) if (g != null) info.VirtualDiskId = g.RG(8);
                    using (var a = Query(h, 8)) if (a != null) info.Is4KAligned = a.RI32(8) != 0;
                    using (var ps = Query(h, 10)) if (ps != null) info.PhysicalSectorSize = (uint)ps.RI32(8);
                    using (var ss = Query(h, 11)) if (ss != null) info.SmallestSafeSize = (ulong)ss.RI64(8);
                    using (var f = Query(h, 12)) if (f != null) info.FragmentationPercent = (uint)f.RI32(8);
                    using (var l = Query(h, 13)) if (l != null) info.IsLoaded = l.RI32(8) != 0;
                    using (var ct = Query(h, 15))
                        if (ct != null) { info.ChangeTracking = ct.RI32(8) != 0; var id = Marshal.PtrToStringUni(ct.P + 16); info.ChangeTrackingId = string.IsNullOrEmpty(id) ? null : id; }
                    if (info.Type == "differencing")
                    {
                        using (var pl = Query(h, 3))
                            if (pl != null)
                            {
                                info.ParentResolved ??= pl.RI32(8) != 0;
                                // a list of strings ends with an empty one
                                int off = 12;
                                while (off < pl.Size - 2)
                                {
                                    var sPath = Marshal.PtrToStringUni(pl.P + off);
                                    if (string.IsNullOrEmpty(sPath)) break;
                                    info.ParentPaths.Add(sPath);
                                    off += (sPath.Length + 1) * 2;
                                }
                            }
                        using (var pi = Query(h, 4)) if (pi != null) info.ParentIdentifier = pi.RG(8);
                        using (var pt = Query(h, 5)) if (pt != null) info.ParentTimestamp = (uint)pt.RI32(8);
                    }
                    info.PhysicalPath = PhysicalPathOf(h);
                }
            }
            catch (VDiskException ex) { info.Error = ex.Message; }
            catch (Exception ex) { info.Error = ex.Message; }
            if (string.IsNullOrEmpty(info.Format)) info.Format = DeviceOf(path) switch { DeviceIso => "ISO", DeviceVhd => "VHD", DeviceVhdx => "VHDX", DeviceVhdSet => "VHD Set", _ => "unknown" };
            return info;
        }

        /// <summary>One information version, or null when this disk or this Windows does not have it.</summary>
        private static Block Query(SafeFileHandle h, int version)
        {
            int size = 1024;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var b = new Block(size);
                b.I32(0, version);
                int need = size;
                int rc = GetVirtualDiskInformation(h, ref need, b.P, out _);
                if (rc == 0) return b;
                b.Dispose();
                if ((rc == ErrorInsufficientBuffer || rc == ErrorMoreData) && need > size) { size = need; continue; }
                return null;
            }
            return null;
        }

        private static string PhysicalPathOf(SafeFileHandle h)
        {
            int size = 1024;
            using var b = new Block(size);
            int rc = GetVirtualDiskPhysicalPath(h, ref size, b.P);
            if (rc != 0) return null;
            var s = Marshal.PtrToStringUni(b.P);
            return string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>The Windows error the last <see cref="Attached"/> call ended with, 0 when it succeeded. For checks.</summary>
        internal static int LastAttachedError { get; private set; }

        /// <summary>
        /// The backing files of every virtual disk attached on this computer right now. The call is made with a buffer from the
        /// start, as the single-disk path call is, because the service refuses a size query without one; a buffer that is too
        /// small is grown to the size the service asks for. Paths come back without the \\?\ prefix, so they compare equal
        /// to the paths in the library.
        /// </summary>
        public static List<string> Attached()
        {
            var list = new List<string>();
            LastAttachedError = 0;
            if (!Available) return list;
            try
            {
                int size = 8192;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    using var b = new Block(size + 4);
                    int used = size;
                    int rc = GetAllAttachedVirtualDiskPhysicalPaths(ref used, b.P);
                    if (rc == ErrorInsufficientBuffer || rc == ErrorMoreData) { size = Math.Max(used + 2, size * 2); continue; }
                    LastAttachedError = rc;
                    if (rc != 0) return list;
                    int end = Math.Min(Math.Max(used, 0), size);
                    int off = 0;
                    while (off < end - 1)
                    {
                        var one = Marshal.PtrToStringUni(b.P + off);
                        if (string.IsNullOrEmpty(one)) break;
                        list.Add(WithoutDevicePrefix(one));
                        off += (one.Length + 1) * 2;
                    }
                    return list;
                }
                LastAttachedError = ErrorInsufficientBuffer;
            }
            catch (Exception ex) { LastAttachedError = ex.HResult; }
            return list;
        }

        /// <summary>A path as Windows usually writes it: \\?\C:\x becomes C:\x and \\?\UNC\server\share becomes \\server\share.</summary>
        internal static string WithoutDevicePrefix(string path)
        {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + path.Substring(8);
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal)) return path.Substring(4);
            return path;
        }

        // ------------------------------------------------------------------ changes (administrator rights)

        /// <summary>
        /// Runs one change with the disk opened the way Windows 8 and later prefer (version 2, no access mask), and, when that
        /// open or call is refused as a bad parameter or access, once more with the older version 1 open and the given access.
        /// </summary>
        private static void Run(string path, string what, Func<SafeFileHandle, int> op, int v1Access, int rwDepth = 1, bool readOnly = false, bool noParents = false)
        {
            int rc;
            try
            {
                using var h = Open(path, infoOnly: false, readOnly: readOnly, noParents: noParents);
                rc = op(h);
                if (rc == 0) return;
                if (rc != 87 && rc != 5 && rc != 50) Check(rc, what);
            }
            catch (VDiskException ex) when (ex.Code == 87 || ex.Code == 5 || ex.Code == 50) { }
            using (var h1 = Open(path, infoOnly: false, noParents: noParents, v1Access: v1Access, rwDepth: rwDepth))
                Check(op(h1), what);
        }

        public static void Attach(string path, bool readOnly, bool noDriveLetters)
        {
            bool iso = DeviceOf(path) == DeviceIso;
            int flags = AttachPermanent | (readOnly || iso ? AttachReadOnly : 0) | (noDriveLetters ? AttachNoDriveLetter : 0);
            using var p = new Block(24);
            p.I32(0, 1);                                        // ATTACH_VIRTUAL_DISK_VERSION_1
            Run(path, "Attach failed", h => AttachVirtualDisk(h, IntPtr.Zero, flags, 0, p.P, IntPtr.Zero), AccessAttachRo | AccessGetInfo | AccessDetach | (readOnly || iso ? 0 : 0x20000), readOnly: readOnly || iso);
        }

        public static void Detach(string path) =>
            Run(path, "Detach failed", h => DetachVirtualDisk(h, 0, 0), AccessDetach);

        /// <summary>Grows a disk to the new size in bytes. A VHDX uses resize; a VHD uses the older expand call.</summary>
        public static void Grow(string path, ulong newSize)
        {
            if (DeviceOf(path) == DeviceVhd)
            {
                using var p = new Block(16);
                p.I32(0, 1); p.I64(8, (long)newSize);           // EXPAND_VIRTUAL_DISK_VERSION_1, NewSize
                Run(path, "Grow failed", h => ExpandVirtualDisk(h, 0, p.P, IntPtr.Zero), AccessMetaOps);
                return;
            }
            Resize(path, newSize, toSmallestSafe: false);
        }

        /// <summary>Resizes a VHDX. To the smallest safe size never cuts into a partition.</summary>
        public static void Resize(string path, ulong newSize, bool toSmallestSafe)
        {
            using var p = new Block(16);
            p.I32(0, 1); p.I64(8, toSmallestSafe ? 0 : (long)newSize);   // RESIZE_VIRTUAL_DISK_VERSION_1, NewSize
            Run(path, "Resize failed", h => ResizeVirtualDisk(h, toSmallestSafe ? ResizeToSmallestSafe : 0, p.P, IntPtr.Zero), AccessMetaOps);
        }

        /// <summary>Returns unused space to the host drive. Quick skips the scan for zeroed blocks.</summary>
        public static void Compact(string path, bool quick)
        {
            using var p = new Block(8);
            p.I32(0, 1);                                        // COMPACT_VIRTUAL_DISK_VERSION_1
            Run(path, "Compact failed", h => CompactVirtualDisk(h, quick ? 1 : 0, p.P, IntPtr.Zero), AccessMetaOps);
        }

        /// <summary>Merges a differencing disk into its parent. The parent changes, so its other children stop working.</summary>
        public static void MergeIntoParent(string child)
        {
            using var v2 = new Block(12);
            v2.I32(0, 2); v2.I32(4, 1); v2.I32(8, 2);           // MERGE_VIRTUAL_DISK_VERSION_2: source depth 1 into target depth 2
            using var v1 = new Block(12);
            v1.I32(0, 1); v1.I32(4, 1);                         // MERGE_VIRTUAL_DISK_VERSION_1: depth 1, for the older open
            bool first = true;
            Run(child, "Merge failed", h => { var r = MergeVirtualDisk(h, 0, first ? v2.P : v1.P, IntPtr.Zero); first = false; return r; }, AccessMetaOps, rwDepth: 2);
        }

        /// <summary>Gives a disk a new identifier, so two copies of one disk can be attached together.</summary>
        public static Guid ResetIdentifier(string path)
        {
            var g = Guid.NewGuid();
            using var p = new Block(32);
            p.I32(0, 2); p.G(8, g);                             // SET_VIRTUAL_DISK_INFO_IDENTIFIER
            Run(path, "The new identifier could not be set", h => SetVirtualDiskInformation(h, p.P), AccessMetaOps);
            return g;
        }

        /// <summary>Points a differencing disk at a moved parent.</summary>
        public static void SetParent(string child, string parent)
        {
            using var p = new Block(32);
            var s = Marshal.StringToHGlobalUni(parent);
            try
            {
                p.I32(0, 1); p.Ptr(8, s);                       // SET_VIRTUAL_DISK_INFO_PARENT_PATH
                Run(child, "The parent could not be set", h => SetVirtualDiskInformation(h, p.P), AccessMetaOps, noParents: true);
            }
            finally { Marshal.FreeHGlobal(s); }
        }

        /// <summary>What to create.</summary>
        public sealed class CreateSpec
        {
            public string Path;
            public ulong SizeBytes;
            public bool Fixed;
            public string ParentPath;           // a differencing child
            public string SourcePath;           // an independent copy of a virtual disk
            public uint BlockSizeBytes;         // 0 for the default
            public uint LogicalSectorBytes;     // 0 for the default
            public uint PhysicalSectorBytes;    // 0 for the default
        }

        /// <summary>Creates the file. Version 2 parameters, so VHD, VHDX and VHD Set all work and sizes are in bytes.</summary>
        public static void Create(CreateSpec c)
        {
            var type = TypeFor(c.Path);
            if (type.DeviceId == DeviceIso || type.DeviceId == DeviceUnknown) throw new VDiskException("Only .vhd, .vhdx and .vhds files can be created.", 0);
            using var p = new Block(256);
            IntPtr parent = IntPtr.Zero, source = IntPtr.Zero;
            try
            {
                p.I32(0, 2);                                    // CREATE_VIRTUAL_DISK_VERSION_2
                // UniqueId at 8 stays zero, so Windows makes one
                p.I64(24, (long)(c.ParentPath != null || c.SourcePath != null ? 0 : c.SizeBytes));   // MaximumSize
                p.I32(32, (int)c.BlockSizeBytes);
                p.I32(36, (int)c.LogicalSectorBytes);
                p.I32(40, (int)c.PhysicalSectorBytes);
                if (c.ParentPath != null) { parent = Marshal.StringToHGlobalUni(c.ParentPath); p.Ptr(48, parent); }
                if (c.SourcePath != null) { source = Marshal.StringToHGlobalUni(c.SourcePath); p.Ptr(56, source); }
                // OpenFlags at 64, ParentVirtualStorageType at 68, SourceVirtualStorageType at 88, ResiliencyGuid at 108: all zero
                int flags = c.Fixed && c.ParentPath == null ? CreateFullPhysicalAllocation : 0;
                int rc = CreateVirtualDisk(ref type, c.Path, AccessNone, IntPtr.Zero, flags, 0, p.P, IntPtr.Zero, out var h);
                h?.Dispose();
                Check(rc, "Create failed");
            }
            finally
            {
                if (parent != IntPtr.Zero) Marshal.FreeHGlobal(parent);
                if (source != IntPtr.Zero) Marshal.FreeHGlobal(source);
            }
        }
    }

    public sealed class VDiskException : Exception
    {
        public int Code { get; }
        public VDiskException(string message, int code) : base(message) { Code = code; }
    }
}
