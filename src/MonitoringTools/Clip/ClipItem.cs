using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Clip
{
    public enum ClipKind { Text, Markdown, Url, Email, Color, Json, Files, Image, Audio, Command, Install }
    public enum FileKind { Other, Folder, Video, Audio, Image, Document, Archive, Code }

    public enum CommandKind { Convert, Push, Custom }

    public sealed class QuickCommand
    {
        public CommandKind Kind { get; set; }
        public string SourcePath { get; set; }   // the text before the colon (a path for convert and push)
        public string Format { get; set; }       // the word after the colon
    }

    public sealed class ClipItem
    {
        public ClipKind Kind { get; set; }
        public string Text { get; set; }
        public string[] Files { get; set; } = Array.Empty<string>();
        public BitmapSource Image { get; set; }
        public byte[] Audio { get; set; }
        public QuickCommand Command { get; set; }
        /// <summary>A copied choco or winget install command, read strictly (Kind is Install).</summary>
        public MonitoringTools.Core.InstallCommand Install { get; set; }
        /// <summary>The copied text was edited in the toast; Text holds the edit and OriginalText the copy.</summary>
        public bool Edited { get; set; }
        public string OriginalText { get; set; }
        public DateTime At { get; set; } = DateTime.Now;
        public string Signature { get; set; }
        /// <summary>The copied text wrapped paths in quotes (Explorer "Copy as path" does this).</summary>
        public bool HadQuotes { get; set; }
        /// <summary>True when the paths came from text rather than a file drop.</summary>
        public bool IsPathText { get; set; }

        public string FirstFile => Files.Length > 0 ? Files[0] : null;
        public FileKind FirstFileKind => FirstFile == null ? FileKind.Other : Classify(FirstFile);

        // ------------------------------------------------------------ file kinds

        public static readonly HashSet<string> VideoExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".flv", ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".3g2", ".ogv",
          ".vob", ".mxf", ".f4v", ".m2v", ".asf", ".divx", ".rm", ".rmvb", ".y4m", ".hevc", ".h264", ".h265" };
        public static readonly HashSet<string> AudioExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff", ".aif", ".alac", ".amr",
          ".ac3", ".eac3", ".mka", ".mp2", ".caf", ".dts", ".wv", ".ape", ".au", ".oga", ".m4b", ".tta", ".spx" };
        public static readonly HashSet<string> ImageExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".heic", ".heif", ".avif",
          ".jxl", ".tga", ".qoi", ".ppm", ".pgm", ".pbm", ".pcx", ".dds", ".exr", ".hdr", ".jp2", ".j2k", ".psd", ".sgi", ".apng", ".jfif" };
        private static readonly HashSet<string> DocExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".rtf", ".csv" };
        private static readonly HashSet<string> ArchiveExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".xz", ".bz2" };
        private static readonly HashSet<string> CodeExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".js", ".ts", ".py", ".json", ".xml", ".xaml", ".html", ".css", ".cpp", ".h", ".java", ".go", ".rs", ".ps1", ".bat", ".sh", ".yml", ".yaml" };

        public static FileKind Classify(string path)
        {
            if (Directory.Exists(path)) return FileKind.Folder;
            var ext = Path.GetExtension(path);
            if (VideoExt.Contains(ext)) return FileKind.Video;
            if (AudioExt.Contains(ext)) return FileKind.Audio;
            if (ImageExt.Contains(ext)) return FileKind.Image;
            if (DocExt.Contains(ext)) return FileKind.Document;
            if (ArchiveExt.Contains(ext)) return FileKind.Archive;
            if (CodeExt.Contains(ext)) return FileKind.Code;
            return FileKind.Other;
        }

        // ------------------------------------------------------------ text classification

        // "C:\clip.mov:mp4", "\"C:\my clip.mov\":mp4", "C:\clip.mov :mp4"
        private static readonly Regex CommandRx = new Regex(@"^\s*""?(?<path>.*?)""?\s*:(?<fmt>[A-Za-z][A-Za-z0-9_-]{1,31})\s*$", RegexOptions.Compiled);
        private static readonly Regex UrlRx = new Regex(@"^(https?://|www\.)[^\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EmailRx = new Regex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex ColorRx = new Regex(@"^(#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*[\d.]+\s*)?\))$", RegexOptions.Compiled);
        private static readonly Regex MdRx = new Regex(@"(^|\n)\s{0,3}(#{1,6}\s|[-*+]\s|\d+\.\s|>\s|```)|\*\*[^*\n]+\*\*|\[[^\]\n]+\]\([^)\s]+\)|`[^`\n]+`|(^|\n)\|.+\|", RegexOptions.Compiled);

        public static ClipItem FromText(string raw)
        {
            var item = new ClipItem { Text = raw, Kind = ClipKind.Text, Signature = "t:" + raw.GetHashCode() };
            var t = raw.Trim();
            if (t.Length == 0) return item;

            var cmd = TryCommand(t);
            if (cmd != null)
            {
                item.Kind = ClipKind.Command; item.Command = cmd;
                if (cmd.Kind != CommandKind.Custom || File.Exists(cmd.SourcePath) || Directory.Exists(cmd.SourcePath))
                    item.Files = string.IsNullOrEmpty(cmd.SourcePath) ? Array.Empty<string>() : new[] { cmd.SourcePath };
                return item;
            }

            if (!t.Contains('\n'))
            {
                if (UrlRx.IsMatch(t)) { item.Kind = ClipKind.Url; item.Text = t; return item; }
                if (EmailRx.IsMatch(t)) { item.Kind = ClipKind.Email; item.Text = t; return item; }
                if (ColorRx.IsMatch(t)) { item.Kind = ClipKind.Color; item.Text = t; return item; }
            }

            var paths = AsPaths(t);
            if (paths != null)
            {
                item.Kind = ClipKind.Files; item.Files = paths; item.IsPathText = true; item.HadQuotes = t.Contains('"');
                return item;
            }

            if ((t.StartsWith("{") && t.EndsWith("}")) || (t.StartsWith("[") && t.EndsWith("]")))
            {
                try { using (JsonDocument.Parse(t)) { } item.Kind = ClipKind.Json; return item; } catch { }
            }

            if (MdRx.Matches(t).Count >= 1 && (t.Contains('\n') || t.Contains("**") || t.Contains("](")))
                item.Kind = ClipKind.Markdown;
            return item;
        }

        public static QuickCommand TryCommand(string t)
        {
            if (t.Contains('\n')) return null;
            var m = CommandRx.Match(t);
            if (!m.Success) return null;
            var path = m.Groups["path"].Value.Trim().Trim('"');
            var fmt = m.Groups["fmt"].Value.ToLowerInvariant();

            // User shortcuts win, so a custom ":mp4" can replace the built-in one
            if (Core.Shortcuts.Find(fmt) != null)
                return new QuickCommand { Kind = CommandKind.Custom, SourcePath = path, Format = fmt };

            if (fmt == "push")
            {
                if (path.Length < 3) return null;
                try { if (!Directory.Exists(path) && !File.Exists(path)) return null; } catch { return null; }
                return new QuickCommand { Kind = CommandKind.Push, SourcePath = Path.GetFullPath(path), Format = fmt };
            }

            if (!Media.Formats.IsKnown(fmt)) return null;
            // a folder converts every compatible file inside it
            if (path.Length < 3 || (!File.Exists(path) && !Directory.Exists(path))) return null;
            return new QuickCommand { Kind = CommandKind.Convert, SourcePath = Path.GetFullPath(path), Format = fmt };
        }

        private static string[] AsPaths(string t)
        {
            var lines = t.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(l => l.Trim().Trim('"')).Where(l => l.Length > 0).ToArray();
            if (lines.Length == 0 || lines.Length > 50) return null;
            foreach (var l in lines)
            {
                if (l.Length < 3 || l.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return null;
                if (!(l.Length > 2 && l[1] == ':' ) && !l.StartsWith(@"\\")) return null;
                try { if (!File.Exists(l) && !Directory.Exists(l)) return null; } catch { return null; }
            }
            return lines;
        }

        public static ClipItem FromFiles(string[] files) =>
            new ClipItem { Kind = ClipKind.Files, Files = files, Signature = "f:" + string.Join("|", files).GetHashCode() };

        public static ClipItem FromImage(BitmapSource img) =>
            new ClipItem { Kind = ClipKind.Image, Image = img, Signature = $"i:{img.PixelWidth}x{img.PixelHeight}:{DateTime.Now.Ticks / TimeSpan.TicksPerSecond}" };

        public static ClipItem FromAudio(byte[] wav) =>
            new ClipItem { Kind = ClipKind.Audio, Audio = wav, Signature = "a:" + wav.Length };

        // ------------------------------------------------------------ display helpers

        public string Preview
        {
            get
            {
                switch (Kind)
                {
                    case ClipKind.Files: return string.Join(Environment.NewLine, Files.Take(4)) + (Files.Length > 4 ? $"{Environment.NewLine}+{Files.Length - 4} more" : "");
                    case ClipKind.Image: return Image == null ? "Image" : $"Image {Image.PixelWidth} × {Image.PixelHeight}";
                    case ClipKind.Audio: return "Audio clip";
                    default: return Text ?? "";
                }
            }
        }
    }
}
