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
        /// <summary>One plain line on when to pick this format, shown in the format list.</summary>
        public string Note = "";
        /// <summary>Kinds of source file this format can be made from, in plain words ("video, audio").</summary>
        public string From => Kind switch
        {
            TargetKind.Video => Id == "mp4" ? "video, audio" : "video",
            TargetKind.Audio => "audio, video",
            _ => Id == "gif" || Id == "apng" ? "video, image" : Id == "ico" ? "image" : "image, video",
        };
    }

    public static class Formats
    {
        private const string H264 = "-c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p";
        private const string Aac = "-c:a aac -b:a 192k";
        private const string Loop = "fps=15,scale='min(720,iw)':-2:flags=lanczos";

        public static readonly List<FormatSpec> All = new List<FormatSpec>
        {
            // ---- video
            new FormatSpec { Id = "mp4",  Kind = TargetKind.Video, Extension = ".mp4",  Label = "MP4 (H.264)", Note = "Plays everywhere; the safe default",
                Args = src => src == FileKind.Audio
                    ? "-f lavfi -i color=c=black:s=1280x720:r=30 -shortest -c:v libx264 -tune stillimage -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart"
                    : $"{H264} {Aac} -movflags +faststart" },
            new FormatSpec { Id = "hevc", Kind = TargetKind.Video, Extension = ".mp4",  Label = "MP4 (H.265 / HEVC)", Note = "About half the size of H.264 at the same quality",
                Args = _ => $"-c:v libx265 -preset medium -crf 24 -pix_fmt yuv420p -tag:v hvc1 {Aac} -movflags +faststart" },
            new FormatSpec { Id = "av1",  Kind = TargetKind.Video, Extension = ".mp4",  Label = "MP4 (AV1)", Note = "Smallest files; newer players only",
                Args = _ => "-c:v libsvtav1 -preset 8 -crf 32 -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart" },
            new FormatSpec { Id = "webm", Kind = TargetKind.Video, Extension = ".webm", Label = "WebM (VP9)", Note = "For the web and chat apps",
                Args = _ => "-c:v libvpx-vp9 -crf 32 -b:v 0 -row-mt 1 -c:a libopus -b:a 128k" },
            new FormatSpec { Id = "mkv",  Kind = TargetKind.Video, Extension = ".mkv",  Label = "Matroska", Note = "Copies every stream without re-encoding", Args = _ => "-map 0 -c copy" },
            new FormatSpec { Id = "mov",  Kind = TargetKind.Video, Extension = ".mov",  Label = "QuickTime", Note = "For Apple apps and video editors", Args = _ => $"{H264} {Aac}" },
            new FormatSpec { Id = "m4v",  Kind = TargetKind.Video, Extension = ".m4v",  Label = "M4V (H.264)", Note = "Apple style MP4 for TVs and players", Args = _ => $"{H264} {Aac} -movflags +faststart" },
            new FormatSpec { Id = "avi",  Kind = TargetKind.Video, Extension = ".avi",  Label = "AVI", Note = "Old players and devices", Args = _ => "-c:v mpeg4 -q:v 3 -c:a mp3 -b:a 192k" },
            new FormatSpec { Id = "wmv",  Kind = TargetKind.Video, Extension = ".wmv",  Label = "Windows Media Video", Note = "Old Windows players and slide shows", Args = _ => "-c:v wmv2 -b:v 4M -c:a wmav2 -b:a 192k" },
            new FormatSpec { Id = "flv",  Kind = TargetKind.Video, Extension = ".flv",  Label = "Flash Video (H.264)", Note = "Old streaming tools", Args = _ => "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p -c:a aac -b:a 160k -ar 44100" },
            new FormatSpec { Id = "mpg",  Kind = TargetKind.Video, Extension = ".mpg",  Label = "MPEG-2", Note = "DVD tools and old hardware players", Args = _ => "-c:v mpeg2video -q:v 3 -c:a mp2 -b:a 224k" },
            new FormatSpec { Id = "ts",   Kind = TargetKind.Video, Extension = ".ts",   Label = "MPEG transport stream", Note = "Broadcast and recording tools", Args = _ => $"{H264} {Aac} -f mpegts" },
            new FormatSpec { Id = "3gp",  Kind = TargetKind.Video, Extension = ".3gp",  Label = "3GP", Note = "Old phones; small and low resolution",
                Args = _ => "-c:v libx264 -profile:v baseline -level 3.0 -vf \"scale='min(640,iw)':-2\" -pix_fmt yuv420p -c:a aac -b:a 96k -ar 44100 -ac 1" },
            // ---- animated images (made from video)
            new FormatSpec { Id = "gif",  Kind = TargetKind.Image, Extension = ".gif",  Label = "Animated GIF", Note = "Short loops from a video, with a tuned palette",
                Args = src => src == FileKind.Video
                    ? $"-vf \"{Loop},split[a][b];[a]palettegen[p];[b][p]paletteuse\" -loop 0"
                    : "" },
            new FormatSpec { Id = "apng", Kind = TargetKind.Image, Extension = ".png",  Label = "Animated PNG", Note = "Full colour loops with transparency",
                Args = src => src == FileKind.Video ? $"-vf \"{Loop}\" -plays 0 -f apng" : "-frames:v 1 -f apng" },
            // ---- audio
            new FormatSpec { Id = "mp3",  Kind = TargetKind.Audio, Extension = ".mp3",  Label = "MP3", Note = "Plays everywhere", Args = _ => "-vn -c:a libmp3lame -q:a 2" },
            new FormatSpec { Id = "m4a",  Kind = TargetKind.Audio, Extension = ".m4a",  Label = "AAC (M4A)", Note = "Small, good quality, Apple friendly", Args = _ => "-vn -c:a aac -b:a 192k" },
            new FormatSpec { Id = "aac",  Kind = TargetKind.Audio, Extension = ".aac",  Label = "AAC (raw)", Note = "A plain AAC stream", Args = _ => "-vn -c:a aac -b:a 192k -f adts" },
            new FormatSpec { Id = "opus", Kind = TargetKind.Audio, Extension = ".opus", Label = "Opus", Note = "Best quality for the size", Args = _ => "-vn -c:a libopus -b:a 160k" },
            new FormatSpec { Id = "ogg",  Kind = TargetKind.Audio, Extension = ".ogg",  Label = "Ogg Vorbis", Note = "Open format for games and Linux", Args = _ => "-vn -c:a libvorbis -q:a 6" },
            new FormatSpec { Id = "flac", Kind = TargetKind.Audio, Extension = ".flac", Label = "FLAC", Note = "Lossless and compressed", Args = _ => "-vn -c:a flac" },
            new FormatSpec { Id = "alac", Kind = TargetKind.Audio, Extension = ".m4a",  Label = "Apple Lossless", Note = "Lossless for Apple devices", Args = _ => "-vn -c:a alac" },
            new FormatSpec { Id = "wav",  Kind = TargetKind.Audio, Extension = ".wav",  Label = "WAV", Note = "Lossless and uncompressed", Args = _ => "-vn -c:a pcm_s16le" },
            new FormatSpec { Id = "aiff", Kind = TargetKind.Audio, Extension = ".aiff", Label = "AIFF", Note = "Lossless and uncompressed, Mac style", Args = _ => "-vn -c:a pcm_s16be" },
            new FormatSpec { Id = "wma",  Kind = TargetKind.Audio, Extension = ".wma",  Label = "Windows Media Audio", Note = "Old Windows players", Args = _ => "-vn -c:a wmav2 -b:a 192k" },
            new FormatSpec { Id = "ac3",  Kind = TargetKind.Audio, Extension = ".ac3",  Label = "Dolby Digital (AC-3)", Note = "Surround sound for home theatre", Args = _ => "-vn -c:a ac3 -b:a 448k" },
            new FormatSpec { Id = "mka",  Kind = TargetKind.Audio, Extension = ".mka",  Label = "Matroska audio", Note = "Copies the audio track without re-encoding", Args = _ => "-vn -map 0:a -c:a copy" },
            // ---- still images
            new FormatSpec { Id = "png",  Kind = TargetKind.Image, Extension = ".png",  Label = "PNG", Note = "Lossless with transparency", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "jpg",  Kind = TargetKind.Image, Extension = ".jpg",  Label = "JPEG", Note = "Photos; small and universal", Args = _ => "-frames:v 1 -q:v 2" },
            new FormatSpec { Id = "jpeg", Kind = TargetKind.Image, Extension = ".jpg",  Label = "JPEG", Note = "Same as :jpg", Args = _ => "-frames:v 1 -q:v 2" },
            new FormatSpec { Id = "webp", Kind = TargetKind.Image, Extension = ".webp", Label = "WebP", Note = "Small images for the web", Args = _ => "-frames:v 1 -quality 90" },
            new FormatSpec { Id = "avif", Kind = TargetKind.Image, Extension = ".avif", Label = "AVIF", Note = "Smallest photos; newer apps only", Args = _ => "-frames:v 1 -c:v libaom-av1 -still-picture 1 -crf 30" },
            new FormatSpec { Id = "jxl",  Kind = TargetKind.Image, Extension = ".jxl",  Label = "JPEG XL", Note = "Modern photo format, close to lossless", Args = _ => "-frames:v 1 -c:v libjxl -distance 1" },
            new FormatSpec { Id = "bmp",  Kind = TargetKind.Image, Extension = ".bmp",  Label = "Bitmap", Note = "Old apps that need raw pixels", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "tiff", Kind = TargetKind.Image, Extension = ".tiff", Label = "TIFF", Note = "Print and scanning, lossless", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "tga",  Kind = TargetKind.Image, Extension = ".tga",  Label = "Targa", Note = "Game textures and 3D tools", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "qoi",  Kind = TargetKind.Image, Extension = ".qoi",  Label = "QOI", Note = "Fast lossless images for games", Args = _ => "-frames:v 1 -c:v qoi" },
            new FormatSpec { Id = "ppm",  Kind = TargetKind.Image, Extension = ".ppm",  Label = "PPM", Note = "Plain pixel map for scripts", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "jp2",  Kind = TargetKind.Image, Extension = ".jp2",  Label = "JPEG 2000", Note = "Archives and medical imaging", Args = _ => "-frames:v 1 -c:v jpeg2000" },
            new FormatSpec { Id = "ico",  Kind = TargetKind.Image, Extension = ".ico",  Label = "Icon", Note = "256 square app icon with transparency",
                Args = _ => "-frames:v 1 -vf \"scale=256:256:force_original_aspect_ratio=decrease,pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0x00000000,format=rgba\"" },
        };

        /// <summary>The formats shown in lists: every format once (the :jpeg alias is left out).</summary>
        public static IEnumerable<FormatSpec> Listed => All.Where(f => f.Id != "jpeg");

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
