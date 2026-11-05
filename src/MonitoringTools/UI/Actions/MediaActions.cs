using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonitoringTools.Clip;

namespace MonitoringTools.UI
{
    /// <summary>The clipboard notice buttons that open the media file tool dialogs: trim and GIF for a copied video, audio editing for
    /// copied audio, and resize, privacy clean-up and icon making for copied pictures. The buttons sit right after Convert.</summary>
    internal static class MediaActions
    {
        /// <summary>Which media buttons a set of copied files earns, as labels (for the toast and for checks).</summary>
        internal static List<string> LabelsFor(string[] files)
        {
            var l = new List<string>();
            files = (files ?? Array.Empty<string>()).Where(f => File.Exists(f)).ToArray();
            if (files.Length == 0) return l;
            if (files.Length == 1)
            {
                var f = files[0];
                var kind = ClipItem.Classify(f);
                if (kind == FileKind.Video) { if (VideoTrimDialog.Handles(f)) { l.Add("Trim…"); l.Add("To GIF…"); } }
                else if (kind == FileKind.Audio) { if (AudioEditDialog.Handles(f)) l.Add("Edit audio…"); }
                else if (kind == FileKind.Image)
                {
                    if (ImagesResizeDialog.Handles(f)) l.Add("Resize…");
                    if (ImagePrivacyDialog.Handles(f)) l.Add("Privacy…");
                    if (MakeIconDialog.Handles(f)) l.Add("Make icon…");
                }
                return l;
            }
            if (files.Any(ImagesResizeDialog.Handles)) l.Add("Resize…");
            if (files.Any(ImagePrivacyDialog.Handles)) l.Add("Privacy…");
            return l;
        }

        internal static void Add(ClipItem item, List<ToastAction> l)
        {
            var files = item.Files ?? Array.Empty<string>();
            var labels = LabelsFor(files);
            if (labels.Count == 0) return;
            var add = new List<ToastAction>();
            foreach (var label in labels)
            {
                switch (label)
                {
                    case "Trim…": add.Add(New(label, MediaIcons.Scissors, "Keep or cut out a part of this video. A cut on key frames copies the video with no quality loss.", () => VideoTrimDialog.OpenFor(files[0]))); break;
                    case "To GIF…": add.Add(New(label, Icons.Image, "Make an animated GIF, animated WebP or short MP4 from part of this video, optionally under a size limit.", () => VideoGifDialog.OpenFor(files[0]))); break;
                    case "Edit audio…": add.Add(New(label, MediaIcons.Wave, "Trim, fade in and out, and set the loudness of this audio.", () => AudioEditDialog.OpenFor(files[0]))); break;
                    case "Resize…": add.Add(New(label, MediaIcons.Crop, "Resize, crop and compress these pictures. The originals are kept.", () => ImagesResizeDialog.OpenFor(files))); break;
                    case "Privacy…": add.Add(New(label, MediaIcons.Eye, "See and remove location, camera, date and author data hidden in these pictures.", () => ImagePrivacyDialog.OpenFor(files))); break;
                    case "Make icon…": add.Add(New(label, MediaIcons.Stamp, "Turn this picture into a multi-size .ico file.", () => MakeIconDialog.OpenFor(files[0]))); break;
                }
            }
            int at = l.FindIndex(x => x.Label == "Convert…");
            if (at >= 0) l.InsertRange(at + 1, add); else l.AddRange(add);
        }

        private static ToastAction New(string label, string icon, string tip, Action run) =>
            new ToastAction { Label = label, Icon = icon, Tip = tip, HandsOff = true, Run = run };
    }
}
