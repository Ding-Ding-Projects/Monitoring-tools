using System;
using System.Collections.Generic;
using System.Linq;
using MonitoringTools.Clip;

namespace MonitoringTools.Media
{
    public enum TargetKind { Video, Audio, Image }

    public sealed class FormatSpec
    {
        public string Id;
        public TargetKind Kind;
        public string Extension;
        public string Label;
        public Func<FileKind, string> Args;
    }

    public static class Formats
    {
        private const string H264 = "-c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p";

        public static readonly List<FormatSpec> All = new List<FormatSpec>
        {
            new FormatSpec { Id = "mp4",  Kind = TargetKind.Video, Extension = ".mp4",  Label = "MP4 (H.264)",
                Args = src => src == FileKind.Audio
                    ? "-f lavfi -i color=c=black:s=1280x720:r=30 -shortest -c:v libx264 -tune stillimage -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart"
                    : $"{H264} -c:a aac -b:a 192k -movflags +faststart" },
            new FormatSpec { Id = "mov",  Kind = TargetKind.Video, Extension = ".mov",  Label = "QuickTime", Args = _ => $"{H264} -c:a aac -b:a 192k" },
            new FormatSpec { Id = "mkv",  Kind = TargetKind.Video, Extension = ".mkv",  Label = "Matroska", Args = _ => "-map 0 -c copy" },
            new FormatSpec { Id = "webm", Kind = TargetKind.Video, Extension = ".webm", Label = "WebM (VP9)", Args = _ => "-c:v libvpx-vp9 -crf 32 -b:v 0 -row-mt 1 -c:a libopus -b:a 128k" },
            new FormatSpec { Id = "avi",  Kind = TargetKind.Video, Extension = ".avi",  Label = "AVI", Args = _ => "-c:v mpeg4 -q:v 3 -c:a mp3 -b:a 192k" },
            new FormatSpec { Id = "gif",  Kind = TargetKind.Image, Extension = ".gif",  Label = "Animated GIF",
                Args = src => src == FileKind.Video
                    ? "-vf \"fps=15,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse\" -loop 0"
                    : "" },
            new FormatSpec { Id = "mp3",  Kind = TargetKind.Audio, Extension = ".mp3",  Label = "MP3", Args = _ => "-vn -c:a libmp3lame -q:a 2" },
            new FormatSpec { Id = "wav",  Kind = TargetKind.Audio, Extension = ".wav",  Label = "WAV", Args = _ => "-vn -c:a pcm_s16le" },
            new FormatSpec { Id = "flac", Kind = TargetKind.Audio, Extension = ".flac", Label = "FLAC", Args = _ => "-vn -c:a flac" },
            new FormatSpec { Id = "m4a",  Kind = TargetKind.Audio, Extension = ".m4a",  Label = "AAC (M4A)", Args = _ => "-vn -c:a aac -b:a 192k" },
            new FormatSpec { Id = "ogg",  Kind = TargetKind.Audio, Extension = ".ogg",  Label = "Ogg Vorbis", Args = _ => "-vn -c:a libvorbis -q:a 6" },
            new FormatSpec { Id = "opus", Kind = TargetKind.Audio, Extension = ".opus", Label = "Opus", Args = _ => "-vn -c:a libopus -b:a 160k" },
            new FormatSpec { Id = "png",  Kind = TargetKind.Image, Extension = ".png",  Label = "PNG", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "jpg",  Kind = TargetKind.Image, Extension = ".jpg",  Label = "JPEG", Args = _ => "-frames:v 1 -q:v 2" },
            new FormatSpec { Id = "jpeg", Kind = TargetKind.Image, Extension = ".jpg",  Label = "JPEG", Args = _ => "-frames:v 1 -q:v 2" },
            new FormatSpec { Id = "webp", Kind = TargetKind.Image, Extension = ".webp", Label = "WebP", Args = _ => "-frames:v 1 -quality 90" },
            new FormatSpec { Id = "bmp",  Kind = TargetKind.Image, Extension = ".bmp",  Label = "Bitmap", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "tiff", Kind = TargetKind.Image, Extension = ".tiff", Label = "TIFF", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "avif", Kind = TargetKind.Image, Extension = ".avif", Label = "AVIF", Args = _ => "-frames:v 1 -c:v libaom-av1 -still-picture 1 -crf 30" },
            new FormatSpec { Id = "ico",  Kind = TargetKind.Image, Extension = ".ico",  Label = "Icon",
                Args = _ => "-frames:v 1 -vf \"scale=256:256:force_original_aspect_ratio=decrease,pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0x00000000,format=rgba\"" },
        };

        public static bool IsKnown(string id) => Find(id) != null;
        public static FormatSpec Find(string id) => All.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>The quick commands offered on a toast for a given kind of source.</summary>
        public static string[] SuggestedFor(FileKind kind, string currentExt)
        {
            string[] list;
            switch (kind)
            {
                case FileKind.Video: list = new[] { "mp4", "webm", "gif", "mp3", "wav", "mkv", "png" }; break;
                case FileKind.Audio: list = new[] { "mp3", "wav", "flac", "m4a", "ogg", "opus", "mp4" }; break;
                case FileKind.Image: list = new[] { "png", "jpg", "webp", "avif", "ico", "bmp" }; break;
                default: return Array.Empty<string>();
            }
            var cur = (currentExt ?? "").TrimStart('.').ToLowerInvariant();
            if (cur == "jpeg") cur = "jpg";
            return list.Where(f => f != cur).ToArray();
        }
    }
}
