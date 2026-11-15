using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MonitoringTools.Core.Packing
{
    internal static class PackingFileIdentity
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

        public static void RequireLocalUnaliased(string path)
        {
            path = Path.GetFullPath(path);
            if (path.StartsWith(@"\\")) throw new IOException("Network paths cannot be used for transactional disk additions.");
            var drive = new DriveInfo(Path.GetPathRoot(path));
            if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                throw new IOException("Transactional disk additions require a local fixed or removable drive.");
            for (var part = path; !string.IsNullOrEmpty(part); part = Path.GetDirectoryName(part))
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Links and junctions in disk or source paths are not supported for transactional additions.");
        }
        public static string Read(string path)
        {
            RequireLocalUnaliased(path);
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error(), "The file identity could not be verified.");
            return info.Volume.ToString("X8") + ":" + info.IndexHigh.ToString("X8") + info.IndexLow.ToString("X8");
        }
    }
}
