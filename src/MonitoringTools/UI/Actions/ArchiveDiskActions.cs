using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonitoringTools.Clip;
using MonitoringTools.Core.Packing;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The toast buttons that open the archive window and the virtual disk window for copied files and folders, Quick pack,
    /// and, for copied virtual disk files, the Virtual disks page.
    /// </summary>
    internal static class ArchiveDiskActions
    {
        // A toast remains hit-testable while closing. Each direct operation belongs to this one action instance.
        private static Action Once(Action run)
        {
            int invoked = 0;
            return () => { if (Interlocked.Exchange(ref invoked, 1) == 0) run(); };
        }

        /// <summary>Adds "Archive…" and "Put in a virtual disk…" just before "File tools" (or at the end), so they sit among the file actions.</summary>
        public static void Add(ClipItem item, List<ToastAction> l)
        {
            if (item.Kind != ClipKind.Files || item.Files == null || item.Files.Length == 0) return;
            var files = item.Files;
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
            var pack = new ToastAction
            {
                Label = "Pack to VHDX", Icon = DialogParts.DiskIcon, HandsOff = true,
                Tip = "Pack " + (files.Length == 1 ? "this" : "these") + " into one new VHDX beside " + (files.Length == 1 ? "it" : "them") + ", which grows by itself and checks every file by SHA-256. Windows asks for permission once.",
                Run = Once(() => PackingProgressDialog.Start(files, PackingFormat.Vhdx))
            };
            int at = l.FindIndex(a => a.Label == "File tools");
            if (at < 0) at = l.Count;
            l.Insert(at, new ToastAction { Label = "Pack to ZIP", Icon = Icons.Archive, HandsOff = true,
                Tip = "Create and verify a ZIP beside the original files. Starts immediately; originals are kept.",
                Run = Once(() => PackingProgressDialog.Start(files, PackingFormat.Zip)) });
            l.Insert(at + 1, pack);
            l.Insert(at + 2, new ToastAction { Label = "Choose VHDX", Icon = DialogParts.DiskIcon, HandsOff = true,
                Tip = "Add these files to a detached dynamic VHDX, keeping a rollback backup.", Run = Once(() => PackingProgressDialog.ChooseTarget(files)) });
            l.Insert(at + 3, archive); l.Insert(at + 4, disk);
            // copied virtual disk files open on the Virtual disks page, first among the file actions
            if (files.All(Core.Disks.DiskJobs.IsDiskFile))
                l.Insert(at, new ToastAction
                {
                    Label = "Open in Virtual disks", Icon = DialogParts.DiskIcon, HandsOff = true,
                    Tip = "Adds " + (files.Length == 1 ? "this disk" : "these disks") + " to the disk library and shows everything about " + (files.Length == 1 ? "it" : "them") + ": attach, grow, shrink, compact, convert and more.",
                    Run = () => App.Current.OpenVirtualDisks(disks: files)
                });
        }
    }
}
