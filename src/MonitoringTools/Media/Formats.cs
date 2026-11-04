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
        /// <summary>One plain sentence: what the format is good for.</summary>
        public string Description = "";
        /// <summary>What the output keeps or loses, for the convert dialog (for example "Lossless" or "Small, some quality loss").</summary>
        public string Quality = "";
        public bool Lossless;
        /// <summary>Which kinds of file can become this format. Empty means the format's own kind only.</summary>
        public FileKind[] Takes = Array.Empty<FileKind>();
        /// <summary>One plain line on when to pick this format, shown in the format list on the Conversion page.</summary>
        public string Note = "";
        /// <summary>Kinds of source file this format can be made from, in plain words ("video, audio").</summary>
        public string From => string.Join(", ", Formats.Sources(this).Select(k => k == FileKind.Image ? "image" : k.ToString().ToLowerInvariant()));
        /// <summary>A short note shown for some sources, for example "Takes the first frame" when a video becomes a picture.</summary>
        public Func<FileKind, string> SourceNote = _ => null;
        /// <summary>Hidden from the dialog and the suggestion chips (an alias kept so old ":jpeg" commands still work).</summary>
        public bool Alias;
    }

    public static class Formats
    {
        private const string H264 = "-c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p";
        private static readonly FileKind[] VideoOnly = { FileKind.Video };
        private static readonly FileKind[] AudioAndVideo = { FileKind.Audio, FileKind.Video };
        private static readonly FileKind[] Pictures = { FileKind.Image, FileKind.Video };
        private static string FrameNote(FileKind k) => k == FileKind.Video ? "Takes the first frame of the video." : null;
        private static string AudioNote(FileKind k) => k == FileKind.Video ? "Keeps only the sound." : null;

        public static readonly List<FormatSpec> All = new List<FormatSpec>
        {
            // ---- video
            new FormatSpec { Id = "mp4",  Kind = TargetKind.Video, Extension = ".mp4",  Note = "Plays everywhere; the safe default", Label = "MP4 (H.264)", Takes = AudioAndVideo,
                Description = "Plays almost everywhere: phones, browsers, TVs and editors.", Quality = "Good quality, medium size",
                SourceNote = k => k == FileKind.Audio ? "Makes a video with a black picture, for sites that only take video." : null,
                Args = src => src == FileKind.Audio
                    ? "-f lavfi -i color=c=black:s=1280x720:r=30 -shortest -c:v libx264 -tune stillimage -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart"
                    : $"{H264} -c:a aac -b:a 192k -movflags +faststart" },
            new FormatSpec { Id = "hevc", Kind = TargetKind.Video, Extension = ".mp4", Note = "About half the size of H.264 at the same quality", Label = "MP4 (H.265 / HEVC)", Takes = VideoOnly,
                Description = "About half the size of H.264 at the same quality. Newer phones, TVs and Windows with the HEVC extension play it.",
                Quality = "Good quality, small size, slow to make",
                Args = _ => "-c:v libx265 -preset medium -crf 24 -pix_fmt yuv420p -tag:v hvc1 -c:a aac -b:a 192k -movflags +faststart" },
            new FormatSpec { Id = "av1", Kind = TargetKind.Video, Extension = ".mp4", Note = "Smallest files; newer players only", Label = "MP4 (AV1)", Takes = VideoOnly,
                Description = "The newest open video codec: very small files. Modern browsers and recent hardware play it.",
                Quality = "Good quality, smallest size, slowest to make",
                Args = _ => "-c:v libsvtav1 -preset 8 -crf 32 -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart" },
            new FormatSpec { Id = "mov",  Kind = TargetKind.Video, Extension = ".mov",  Note = "For Apple apps and video editors", Label = "QuickTime (H.264)", Takes = VideoOnly,
                Description = "Apple's container; opens in Photos, iMovie and Final Cut.", Quality = "Good quality, medium size",
                Args = _ => $"{H264} -c:a aac -b:a 192k" },
            new FormatSpec { Id = "mov-prores", Kind = TargetKind.Video, Extension = ".mov", Note = "For editing; large but smooth to scrub", Label = "QuickTime (ProRes 422)", Takes = VideoOnly,
                Description = "An editing format: smooth to scrub in editors, no quality lost to recompression.", Quality = "Near lossless, very large",
                Args = _ => "-c:v prores_ks -profile:v 2 -pix_fmt yuv422p10le -c:a pcm_s16le" },
            new FormatSpec { Id = "mkv",  Kind = TargetKind.Video, Extension = ".mkv",  Note = "Copies every stream without re-encoding", Label = "Matroska (copy)", Takes = VideoOnly, Lossless = true,
                Description = "Moves every stream, subtitle and chapter into an MKV without recompressing.", Quality = "Lossless, instant",
                Args = _ => "-map 0 -c copy" },
            new FormatSpec { Id = "webm", Kind = TargetKind.Video, Extension = ".webm", Note = "For the web and chat apps", Label = "WebM (VP9)", Takes = VideoOnly,
                Description = "Open web video for browsers and sites.", Quality = "Good quality, small size",
                Args = _ => "-c:v libvpx-vp9 -crf 32 -b:v 0 -row-mt 1 -c:a libopus -b:a 128k" },
            new FormatSpec { Id = "avi",  Kind = TargetKind.Video, Extension = ".avi",  Note = "Old players and devices", Label = "AVI (MPEG-4)", Takes = VideoOnly,
                Description = "For old players and devices.", Quality = "Fair quality, large",
                Args = _ => "-c:v mpeg4 -q:v 3 -c:a libmp3lame -b:a 192k" },
            new FormatSpec { Id = "m4v",  Kind = TargetKind.Video, Extension = ".m4v",  Note = "Apple style MP4 for TVs and players", Label = "M4V (H.264)", Takes = VideoOnly,
                Description = "MP4 under the name iTunes and Apple TV expect.", Quality = "Good quality, medium size",
                Args = _ => $"{H264} -c:a aac -b:a 192k -movflags +faststart -f mp4" },
            new FormatSpec { Id = "ts",   Kind = TargetKind.Video, Extension = ".ts",   Note = "Broadcast and recording tools", Label = "MPEG transport stream", Takes = VideoOnly,
                Description = "Broadcast and streaming format; survives being cut or interrupted.", Quality = "Good quality, medium size",
                Args = _ => $"{H264} -c:a aac -b:a 192k -f mpegts" },
            new FormatSpec { Id = "mpg",  Kind = TargetKind.Video, Extension = ".mpg",  Note = "DVD tools and old hardware players", Label = "MPEG-2", Takes = VideoOnly,
                Description = "DVD-era video for old players and set-top boxes.", Quality = "Fair quality, large",
                Args = _ => "-c:v mpeg2video -q:v 3 -c:a mp2 -b:a 224k -f mpeg" },
            new FormatSpec { Id = "wmv",  Kind = TargetKind.Video, Extension = ".wmv",  Note = "Old Windows players and slide shows", Label = "Windows Media Video", Takes = VideoOnly,
                Description = "For old Windows software and PowerPoint versions.", Quality = "Fair quality, medium size",
                Args = _ => "-c:v wmv2 -q:v 3 -c:a wmav2 -b:a 192k" },
            new FormatSpec { Id = "flv",  Kind = TargetKind.Video, Extension = ".flv",  Note = "Old streaming tools", Label = "Flash video (H.264)", Takes = VideoOnly,
                Description = "For older streaming tools that still ask for FLV.", Quality = "Good quality, medium size",
                Args = _ => $"{H264} -c:a aac -b:a 160k -f flv" },
            new FormatSpec { Id = "3gp",  Kind = TargetKind.Video, Extension = ".3gp",  Note = "Old phones; small and low resolution", Label = "3GP (small phone video)", Takes = VideoOnly,
                Description = "Tiny files for basic phones and slow connections.", Quality = "Low quality, very small",
                Args = _ => "-c:v libx264 -profile:v baseline -level 3.0 -crf 28 -vf \"scale='min(640,iw)':-2\" -pix_fmt yuv420p -c:a aac -b:a 64k -ac 1" },
            new FormatSpec { Id = "ogv",  Kind = TargetKind.Video, Extension = ".ogv",  Note = "Fully open video for wikis and Linux", Label = "Ogg video (Theora)", Takes = VideoOnly,
                Description = "A fully open format some wikis and Linux tools prefer.", Quality = "Fair quality, medium size",
                Args = _ => "-c:v libtheora -q:v 7 -c:a libvorbis -q:a 5" },

            // ---- audio
            new FormatSpec { Id = "mp3",  Kind = TargetKind.Audio, Extension = ".mp3",  Note = "Plays everywhere", Label = "MP3", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "The audio file every device plays.", Quality = "Very good quality, small", Args = _ => "-vn -c:a libmp3lame -q:a 2" },
            new FormatSpec { Id = "wav",  Kind = TargetKind.Audio, Extension = ".wav",  Note = "Lossless and uncompressed", Label = "WAV", Takes = AudioAndVideo, SourceNote = AudioNote, Lossless = true,
                Description = "Uncompressed audio for editors and samplers.", Quality = "Lossless, very large", Args = _ => "-vn -c:a pcm_s16le" },
            new FormatSpec { Id = "flac", Kind = TargetKind.Audio, Extension = ".flac", Note = "Lossless and compressed", Label = "FLAC", Takes = AudioAndVideo, SourceNote = AudioNote, Lossless = true,
                Description = "Lossless audio at about half the size of WAV.", Quality = "Lossless, large", Args = _ => "-vn -c:a flac" },
            new FormatSpec { Id = "m4a",  Kind = TargetKind.Audio, Extension = ".m4a",  Note = "Small, good quality, Apple friendly", Label = "AAC (M4A)", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "Apple and phone friendly audio.", Quality = "Very good quality, small", Args = _ => "-vn -c:a aac -b:a 192k" },
            new FormatSpec { Id = "alac", Kind = TargetKind.Audio, Extension = ".m4a",  Note = "Lossless for Apple devices", Label = "Apple Lossless (ALAC)", Takes = AudioAndVideo, SourceNote = AudioNote, Lossless = true,
                Description = "Lossless audio that Apple Music and iPhones play.", Quality = "Lossless, large", Args = _ => "-vn -c:a alac" },
            new FormatSpec { Id = "aac",  Kind = TargetKind.Audio, Extension = ".aac",  Note = "A plain AAC stream", Label = "AAC (raw)", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "Plain AAC stream for players and tools that want .aac.", Quality = "Very good quality, small", Args = _ => "-vn -c:a aac -b:a 192k -f adts" },
            new FormatSpec { Id = "ogg",  Kind = TargetKind.Audio, Extension = ".ogg",  Note = "Open format for games and Linux", Label = "Ogg Vorbis", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "Open audio used by games and Linux.", Quality = "Very good quality, small", Args = _ => "-vn -c:a libvorbis -q:a 6" },
            new FormatSpec { Id = "opus", Kind = TargetKind.Audio, Extension = ".opus", Note = "Best quality for the size", Label = "Opus", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "The best quality for its size; great for voice and music.", Quality = "Very good quality, very small", Args = _ => "-vn -c:a libopus -b:a 160k" },
            new FormatSpec { Id = "aiff", Kind = TargetKind.Audio, Extension = ".aiff", Note = "Lossless and uncompressed, Mac style", Label = "AIFF", Takes = AudioAndVideo, SourceNote = AudioNote, Lossless = true,
                Description = "Uncompressed audio that Apple software prefers.", Quality = "Lossless, very large", Args = _ => "-vn -c:a pcm_s16be" },
            new FormatSpec { Id = "wma",  Kind = TargetKind.Audio, Extension = ".wma",  Note = "Old Windows players", Label = "Windows Media Audio", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "For old Windows players and car stereos.", Quality = "Good quality, small", Args = _ => "-vn -c:a wmav2 -b:a 192k" },
            new FormatSpec { Id = "ac3",  Kind = TargetKind.Audio, Extension = ".ac3",  Note = "Surround sound for home theatre", Label = "Dolby Digital (AC-3)", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "Surround sound for DVDs and home cinema; keeps up to 5.1 channels.", Quality = "Good quality, medium size", Args = _ => "-vn -c:a ac3 -b:a 448k" },
            new FormatSpec { Id = "wv",   Kind = TargetKind.Audio, Extension = ".wv",   Note = "Lossless with good compression", Label = "WavPack", Takes = AudioAndVideo, SourceNote = AudioNote, Lossless = true,
                Description = "Lossless audio with good compression, used for archives.", Quality = "Lossless, large", Args = _ => "-vn -c:a wavpack" },
            new FormatSpec { Id = "mka",  Kind = TargetKind.Audio, Extension = ".mka",  Note = "Copies the audio track without re-encoding", Label = "Matroska audio (copy)", Takes = AudioAndVideo, SourceNote = AudioNote, Lossless = true,
                Description = "Keeps every audio track exactly as it is, in one MKA file.", Quality = "Lossless, instant", Args = _ => "-vn -map 0:a -c copy" },
            new FormatSpec { Id = "mp2",  Kind = TargetKind.Audio, Extension = ".mp2",  Note = "Broadcast and old radio software", Label = "MPEG audio layer 2", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "For broadcast systems and old radio software.", Quality = "Good quality, medium size", Args = _ => "-vn -c:a mp2 -b:a 256k" },
            new FormatSpec { Id = "amr",  Kind = TargetKind.Audio, Extension = ".amr",  Note = "Voice for old phones", Label = "AMR (phone voice)", Takes = AudioAndVideo, SourceNote = AudioNote,
                Description = "Voice-only format for old phones; mono at 8 kHz.", Quality = "Voice quality, tiny", Args = _ => "-vn -ar 8000 -ac 1 -c:a libopencore_amrnb -b:a 12.2k" },

            // ---- pictures
            new FormatSpec { Id = "gif",  Kind = TargetKind.Image, Extension = ".gif",  Note = "Short loops from a video, with a tuned palette", Label = "Animated GIF", Takes = Pictures,
                Description = "Short silent loops that play anywhere, including chat apps.", Quality = "256 colours, large for video",
                SourceNote = k => k == FileKind.Video ? "Up to 15 frames a second, at most 720 pixels wide." : null,
                Args = src => src == FileKind.Video
                    ? "-vf \"fps=15,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse\" -loop 0"
                    : "" },
            new FormatSpec { Id = "apng", Kind = TargetKind.Image, Extension = ".png",  Note = "Full colour loops with transparency", Label = "Animated PNG", Takes = Pictures,
                Description = "Animation with full colour and transparency; browsers play it.", Quality = "Full colour, large",
                Args = src => src == FileKind.Video ? "-vf \"fps=15,scale='min(720,iw)':-2:flags=lanczos\" -plays 0 -f apng" : "-frames:v 1 -f apng" },
            new FormatSpec { Id = "webp-anim", Kind = TargetKind.Image, Extension = ".webp", Note = "Animations much smaller than GIF", Label = "Animated WebP", Takes = VideoOnly,
                Description = "Animation much smaller than a GIF, with full colour.", Quality = "Good quality, small",
                Args = _ => "-vf \"fps=15,scale='min(720,iw)':-2:flags=lanczos\" -c:v libwebp_anim -quality 75 -loop 0" },
            new FormatSpec { Id = "png",  Kind = TargetKind.Image, Extension = ".png",  Note = "Lossless with transparency", Label = "PNG", Takes = Pictures, SourceNote = FrameNote, Lossless = true,
                Description = "Sharp pictures, screenshots and transparency.", Quality = "Lossless", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "jpg",  Kind = TargetKind.Image, Extension = ".jpg",  Note = "Photos; small and universal", Label = "JPEG", Takes = Pictures, SourceNote = FrameNote,
                Description = "Photos, small and universal.", Quality = "Very good quality, small", Args = _ => "-frames:v 1 -q:v 2" },
            new FormatSpec { Id = "jpeg", Kind = TargetKind.Image, Extension = ".jpg",  Note = "Same as :jpg", Label = "JPEG", Takes = Pictures, SourceNote = FrameNote, Alias = true,
                Description = "Photos, small and universal.", Quality = "Very good quality, small", Args = _ => "-frames:v 1 -q:v 2" },
            new FormatSpec { Id = "webp", Kind = TargetKind.Image, Extension = ".webp", Note = "Small images for the web", Label = "WebP", Takes = Pictures, SourceNote = FrameNote,
                Description = "Web pictures smaller than JPEG, with transparency.", Quality = "Very good quality, very small", Args = _ => "-frames:v 1 -quality 90" },
            new FormatSpec { Id = "avif", Kind = TargetKind.Image, Extension = ".avif", Note = "Smallest photos; newer apps only", Label = "AVIF", Takes = Pictures, SourceNote = FrameNote,
                Description = "The newest web picture format: the smallest files.", Quality = "Very good quality, smallest", Args = _ => "-frames:v 1 -c:v libaom-av1 -still-picture 1 -crf 30" },
            new FormatSpec { Id = "jxl",  Kind = TargetKind.Image, Extension = ".jxl",  Note = "Modern photo format, close to lossless", Label = "JPEG XL", Takes = Pictures, SourceNote = FrameNote,
                Description = "A modern successor to JPEG; small and high quality. Newer apps open it.", Quality = "Very good quality, very small", Args = _ => "-frames:v 1 -c:v libjxl -distance 1" },
            new FormatSpec { Id = "bmp",  Kind = TargetKind.Image, Extension = ".bmp",  Note = "Old apps that need raw pixels", Label = "Bitmap", Takes = Pictures, SourceNote = FrameNote, Lossless = true,
                Description = "Uncompressed pictures for old Windows software.", Quality = "Lossless, very large", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "tiff", Kind = TargetKind.Image, Extension = ".tiff", Note = "Print and scanning, lossless", Label = "TIFF", Takes = Pictures, SourceNote = FrameNote, Lossless = true,
                Description = "For printing, scanning and archives.", Quality = "Lossless, large", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "jp2",  Kind = TargetKind.Image, Extension = ".jp2",  Note = "Archives and medical imaging", Label = "JPEG 2000", Takes = Pictures, SourceNote = FrameNote,
                Description = "Used in medical imaging, film and digital archives.", Quality = "Very good quality, small", Args = _ => "-frames:v 1 -c:v libopenjpeg" },
            new FormatSpec { Id = "tga",  Kind = TargetKind.Image, Extension = ".tga",  Note = "Game textures and 3D tools", Label = "Targa", Takes = Pictures, SourceNote = FrameNote, Lossless = true,
                Description = "Textures for games and 3D tools.", Quality = "Lossless, large", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "qoi",  Kind = TargetKind.Image, Extension = ".qoi",  Note = "Fast lossless images for games", Label = "QOI", Takes = Pictures, SourceNote = FrameNote, Lossless = true,
                Description = "A very fast lossless format some games and tools use.", Quality = "Lossless, medium size", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "pcx",  Kind = TargetKind.Image, Extension = ".pcx",  Note = "Very old paint software", Label = "PCX", Takes = Pictures, SourceNote = FrameNote, Lossless = true,
                Description = "For very old paint and DOS-era software.", Quality = "Lossless, large", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "ppm",  Kind = TargetKind.Image, Extension = ".ppm",  Note = "Plain pixel map for scripts", Label = "PPM", Takes = Pictures, SourceNote = FrameNote, Lossless = true,
                Description = "A plain pixel format for image-processing scripts.", Quality = "Lossless, very large", Args = _ => "-frames:v 1" },
            new FormatSpec { Id = "ico",  Kind = TargetKind.Image, Extension = ".ico",  Note = "256 square app icon with transparency", Label = "Icon", Takes = Pictures, SourceNote = FrameNote,
                Description = "A Windows icon, 256 by 256 with transparency.", Quality = "Resized to 256 pixels",
                Args = _ => "-frames:v 1 -vf \"scale=256:256:force_original_aspect_ratio=decrease,pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0x00000000,format=rgba\"" },
        };

        public static bool IsKnown(string id) => Find(id) != null;
        public static FormatSpec Find(string id) => All.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Every format shown to people (aliases left out), in table order.</summary>
        public static IEnumerable<FormatSpec> Visible => All.Where(f => !f.Alias);
        /// <summary>The formats in the Conversion page's list (the same as <see cref="Visible"/>).</summary>
        public static IEnumerable<FormatSpec> Listed => Visible;

        /// <summary>The source kinds a format accepts.</summary>
        public static FileKind[] Sources(FormatSpec f) => f.Takes.Length > 0 ? f.Takes
            : new[] { f.Kind == TargetKind.Video ? FileKind.Video : f.Kind == TargetKind.Audio ? FileKind.Audio : FileKind.Image };

        /// <summary>Every format a file of this kind and extension can become, for the convert dialog.</summary>
        public static List<FormatSpec> TargetsFor(FileKind kind, string currentExt)
        {
            var cur = (currentExt ?? "").TrimStart('.').ToLowerInvariant();
            if (cur == "jpeg") cur = "jpg";
            if (cur == "tif") cur = "tiff";
            return Visible.Where(f => Sources(f).Contains(kind) && !(f.Id == cur && f.Extension.TrimStart('.') == cur)).ToList();
        }

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
