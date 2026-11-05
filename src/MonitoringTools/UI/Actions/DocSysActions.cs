using System.Collections.Generic;
using System.IO;
using MonitoringTools.Clip;
using MonitoringTools.Core.DocTools;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The clipboard toast buttons for archives, subtitles, fonts and disc images. A single copied file of one of those types
    /// gets a button that opens the matching dialog; several files or other types get nothing from here.
    /// </summary>
    public static class DocSysActions
    {
        /// <summary>The kind of dialog a file opens, or null when none applies.</summary>
        public static string KindFor(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path).ToLowerInvariant();
            if (ArchiveTools.IsArchive(name)) return "archive";
            if (ext == ".srt" || ext == ".vtt") return "subtitle";
            if (FontTools.IsFont(path)) return "font";
            if (DiscImageTools.IsDiscImage(path)) return "disc";
            return null;
        }

        public static void Add(ClipItem item, List<ToastAction> l)
        {
            if (item.Files.Length != 1) return;
            var f = item.FirstFile;
            if (f == null || Directory.Exists(f)) return;
            switch (KindFor(f))
            {
                case "archive": l.Add(new ToastAction { Label = "Extract…", Icon = Icons.Archive, HandsOff = true, Tip = "Look inside the archive, pick files and extract them", Run = () => ExtractArchiveDialog.Open(f) }); break;
                case "subtitle": l.Add(new ToastAction { Label = "Subtitle timing…", Icon = DocIcons.Subtitle, HandsOff = true, Tip = "Shift, stretch or convert the subtitle timing", Run = () => SubtitleTimingDialog.Open(f) }); break;
                case "font": l.Add(new ToastAction { Label = "Preview font…", Icon = DocIcons.Font, HandsOff = true, Tip = "See the font at several sizes and install it for you only", Run = () => FontInstallDialog.Open(f) }); break;
                case "disc": l.Add(new ToastAction { Label = "Disc image…", Icon = DocIcons.Disc, HandsOff = true, Tip = "Browse, mount or extract from the disc image", Run = () => DiscImageDialog.Open(f) }); break;
            }
        }
    }
}
