using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonitoringTools.Clip;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    /// <summary>The two toast buttons that open the archive window and the virtual disk window for copied files and folders.</summary>
    internal static class ArchiveDiskActions
    {
        /// <summary>Adds "Archive…" and "Put in a virtual disk…" just before "File tools" (or at the end), so they sit among the file actions.</summary>
        public static void Add(ClipItem item, List<ToastAction> l)
        {
            if (item.Kind != ClipKind.Files || item.Files == null || item.Files.Length == 0) return;
            var files = item.Files.Where(p => File.Exists(p) || Directory.Exists(p.TrimEnd('\\'))).ToArray();
            if (files.Length == 0) return;
            var archive = new ToastAction
            {
                Label = "Archive…", Icon = Icons.Archive, HandsOff = true,
                Tip = "Make a zip, 7z or tar.gz of " + (files.Length == 1 ? "this" : "these") + ", with every option and a test step. The originals are kept unless you ask otherwise.",
                Run = () => ArchiveDialog.Open(files)
            };
            var disk = new ToastAction
            {
                Label = "Put in a virtual disk…", Icon = DialogParts.DiskIcon, HandsOff = true,
                Tip = "Create a VHD or VHDX, mount it, and copy or move " + (files.Length == 1 ? "this" : "these") + " onto it. Windows asks for permission once.",
                Run = () => DiskDialog.Open(files)
            };
            int at = l.FindIndex(a => a.Label == "File tools");
            if (at < 0) at = l.Count;
            l.Insert(at, archive); l.Insert(at + 1, disk);
        }
    }
}
