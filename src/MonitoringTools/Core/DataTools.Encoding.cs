using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MonitoringTools.Core
{
    internal enum LineEnds { None, Lf, CrLf, Cr, Mixed }

    /// <summary>What the detector found about one file.</summary>
    internal sealed class TextInfo
    {
        public string Path;
        public bool Binary;
        public string BinaryReason;
        public Encoding Encoding;          // null for binary files
        public string EncodingName;        // "UTF-8", "UTF-8 BOM", "UTF-16 LE", "Windows-1252 (guess)"
        public bool Bom;
        public bool Guess;                 // legacy code page chosen by elimination, not by evidence
        public bool Ascii;                 // every byte is below 0x80
        public int Crlf, Lf, Cr;
        public LineEnds Ends;
        public long Size;
        public bool HandPicked;
        public string DecodeError;         // set when the bytes are not valid in the encoding in use
        public int DecodeErrorPosition;
        public int DecodeErrorByte;
    }

    internal enum TargetEncoding { Utf8, Utf8Bom, Utf16 }
    internal enum TargetEnds { Keep, Lf, CrLf }

    internal sealed class TextConvertResult
    {
        public string Path;
        public bool Changed;
        public bool Failed;
        public string Message;        // "Windows-1252 → UTF-8, CRLF → LF"
        public string Backup;
    }

    /// <summary>
    /// Detects the encoding and line endings of text files and converts them. Detection order: byte-order mark, strict UTF-8 validation, UTF-16 by its
    /// NUL pattern, then the system ANSI code page, which is a guess and is said to be one. Conversion decodes strictly: a byte that is not valid in
    /// the detected encoding stops that file and nothing is written. Binary files are never touched.
    /// </summary>
    internal static class TextEngine
    {
        static TextEngine() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }

        private static readonly HashSet<string> BinaryExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".sys", ".msi", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".zip", ".7z", ".rar", ".gz", ".tar", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".mp3", ".mp4", ".mkv", ".avi", ".mov", ".wav", ".flac", ".ogg", ".iso", ".bin", ".dat", ".db", ".sqlite", ".pfx", ".p12", ".class", ".jar", ".so", ".o", ".obj", ".pdb", ".ttf", ".otf", ".woff", ".woff2",
        };

        private static readonly HashSet<string> TextExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".tsv", ".log", ".sh", ".md", ".json", ".xml", ".yaml", ".yml", ".ini", ".cfg", ".conf", ".bat", ".cmd", ".ps1", ".py", ".js", ".ts", ".css", ".html", ".htm", ".cs", ".java", ".c", ".cpp",
            ".h", ".sql", ".srt", ".vtt", ".toml", ".properties", ".gitignore", ".env", ".rst", ".tex", ".svg", ".cer", ".crt", ".pem",
        };

        /// <summary>Whether the file looks like one the dialog should open for (by name; the content check decides the rest).</summary>
        internal static bool IsTextName(string path)
        {
            var ext = Path.GetExtension(path ?? "");
            return ext.Length == 0 || TextExt.Contains(ext);
        }

        internal static readonly (string Label, int CodePage)[] HandChoices =
        {
            ("UTF-8", 65001), ("Windows-1252", 1252), ("ISO-8859-1", 28591), ("Windows-1250", 1250), ("Windows-1251", 1251), ("Shift-JIS", 932), ("GBK", 936), ("Big5", 950), ("EUC-KR", 949), ("UTF-16 LE", 1200), ("UTF-16 BE", 1201),
        };

        internal static Encoding Strict(int codePage) => Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        internal static TextInfo Analyse(string path, int? handCodePage = null)
        {
            var info = new TextInfo { Path = path };
            byte[] bytes;
            try { info.Size = new FileInfo(path).Length; bytes = File.ReadAllBytes(path); }
            catch (Exception ex) { info.Binary = true; info.BinaryReason = "Could not read the file: " + ex.Message; return info; }

            var ext = Path.GetExtension(path);
            if (BinaryExt.Contains(ext)) { info.Binary = true; info.BinaryReason = "This is a " + ext.TrimStart('.').ToUpperInvariant() + " file, not text. It is never changed here."; return info; }
            if (info.Size > 200L * 1024 * 1024) { info.Binary = true; info.BinaryReason = "This file is larger than 200 MB, which is more than the converter reads."; return info; }

            int bom = 0;
            if (handCodePage != null)
            {
                info.HandPicked = true;
                info.Encoding = Strict(handCodePage.Value);
                info.EncodingName = HandChoices.First(c => c.CodePage == handCodePage.Value).Label;
                if (handCodePage == 65001 && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { bom = 3; info.Bom = true; info.EncodingName = "UTF-8 BOM"; }
                else if (handCodePage == 1200 && bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { bom = 2; info.Bom = true; }
                else if (handCodePage == 1201 && bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { bom = 2; info.Bom = true; }
            }
            else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { bom = 3; info.Bom = true; info.Encoding = Strict(65001); info.EncodingName = "UTF-8 BOM"; }
            else if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0) { bom = 4; info.Bom = true; info.Encoding = Strict(12000); info.EncodingName = "UTF-32 LE"; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { bom = 2; info.Bom = true; info.Encoding = Strict(1200); info.EncodingName = "UTF-16 LE"; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { bom = 2; info.Bom = true; info.Encoding = Strict(1201); info.EncodingName = "UTF-16 BE"; }
            else
            {
                int nul = 0, nulOdd = 0, nulEven = 0, control = 0;
                for (int i = 0; i < Math.Min(bytes.Length, 65536); i++)
                {
                    var b = bytes[i];
                    if (b == 0) { nul++; if ((i & 1) == 1) nulOdd++; else nulEven++; }
                    else if (b < 32 && b != 9 && b != 10 && b != 13 && b != 12 && b != 27) control++;
                }
                int sample = Math.Min(bytes.Length, 65536);
                if (nul > 0 && sample >= 4 && (nulOdd > sample / 6 || nulEven > sample / 6) && Math.Abs(nulOdd - nulEven) > sample / 6)
                {
                    bool le = nulOdd > nulEven;
                    info.Encoding = Strict(le ? 1200 : 1201); info.EncodingName = le ? "UTF-16 LE" : "UTF-16 BE";
                    if (!TryDecode(info.Encoding, bytes, 0, out _, out _, out _)) { info.Binary = true; info.BinaryReason = "The bytes look like binary data, not text."; info.Encoding = null; return info; }
                }
                else if (nul > 0 || (sample > 0 && control * 10 > sample)) { info.Binary = true; info.BinaryReason = "The bytes look like binary data, not text. It is never changed here."; return info; }
                else if (TryDecode(Strict(65001), bytes, 0, out _, out _, out _)) { info.Encoding = Strict(65001); info.EncodingName = "UTF-8"; }
                else
                {
                    int ansi = 1252;
                    try { ansi = CultureInfo.CurrentCulture.TextInfo.ANSICodePage; if (ansi == 0 || ansi == 65001) ansi = 1252; } catch { }
                    info.Encoding = Strict(ansi); info.Guess = true;
                    info.EncodingName = (ansi == 1252 ? "Windows-1252" : "Code page " + ansi) + " (guess)";
                }
            }

            info.Ascii = bytes.Skip(bom).All(b => b < 0x80) && !(info.Encoding.CodePage is 1200 or 1201 or 12000);
            if (info.Ascii && info.EncodingName == "UTF-8") info.EncodingName = "UTF-8";
            if (!TryDecode(info.Encoding, bytes, bom, out var text, out var errPos, out var errByte))
            {
                info.DecodeError = "bad"; info.DecodeErrorPosition = errPos; info.DecodeErrorByte = errByte;
                return info;
            }
            CountEnds(info, text);
            return info;
        }

        private static bool TryDecode(Encoding enc, byte[] bytes, int skip, out string text, out int errPos, out int errByte)
        {
            try { text = enc.GetString(bytes, skip, bytes.Length - skip); errPos = 0; errByte = 0; return true; }
            catch (DecoderFallbackException ex)
            {
                text = null; errPos = skip + ex.Index + 1; errByte = ex.BytesUnknown != null && ex.BytesUnknown.Length > 0 ? ex.BytesUnknown[0] : 0; return false;
            }
        }

        private static void CountEnds(TextInfo info, string text)
        {
            int crlf = 0, lf = 0, cr = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; } else cr++; }
                else if (text[i] == '\n') lf++;
            }
            info.Crlf = crlf; info.Lf = lf; info.Cr = cr;
            int kinds = (crlf > 0 ? 1 : 0) + (lf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0);
            info.Ends = kinds == 0 ? LineEnds.None : kinds > 1 ? LineEnds.Mixed : crlf > 0 ? LineEnds.CrLf : lf > 0 ? LineEnds.Lf : LineEnds.Cr;
        }

        internal static string EndsName(LineEnds e) => e switch { LineEnds.Lf => "LF", LineEnds.CrLf => "CRLF", LineEnds.Cr => "CR", LineEnds.Mixed => "Mixed", _ => "No line ends" };

        internal static string TargetName(TargetEncoding t) => t switch { TargetEncoding.Utf8Bom => "UTF-8 BOM", TargetEncoding.Utf16 => "UTF-16 LE", _ => "UTF-8" };

        private static Encoding Writer(TargetEncoding t) => t switch
        {
            TargetEncoding.Utf8Bom => new UTF8Encoding(true, true),
            TargetEncoding.Utf16 => new UnicodeEncoding(false, true, true),
            _ => new UTF8Encoding(false, true),
        };

        /// <summary>The file's text decoded with the detected encoding, or null when it cannot be.</summary>
        internal static string Decode(TextInfo info)
        {
            if (info.Binary || info.DecodeError != null) return null;
            var bytes = File.ReadAllBytes(info.Path);
            int skip = info.Bom ? (info.EncodingName == "UTF-8 BOM" ? 3 : info.EncodingName == "UTF-32 LE" ? 4 : 2) : 0;
            return info.Encoding.GetString(bytes, skip, bytes.Length - skip);
        }

        internal static string Normalise(string text, TargetEnds ends)
        {
            if (ends == TargetEnds.Keep) return text;
            var eol = ends == TargetEnds.Lf ? "\n" : "\r\n";
            var sb = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; sb.Append(eol); }
                else if (c == '\n') sb.Append(eol);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Whether converting would change anything.</summary>
        internal static bool WouldChange(TextInfo i, TargetEncoding enc, TargetEnds ends)
        {
            if (i.Binary || i.DecodeError != null) return false;
            bool sameEnc = enc switch
            {
                TargetEncoding.Utf8 => i.EncodingName == "UTF-8" && !i.Bom,
                TargetEncoding.Utf8Bom => i.EncodingName == "UTF-8 BOM",
                _ => i.EncodingName == "UTF-16 LE" && i.Bom,
            };
            bool sameEnds = ends == TargetEnds.Keep || (ends == TargetEnds.Lf && (i.Ends == LineEnds.Lf || i.Ends == LineEnds.None)) || (ends == TargetEnds.CrLf && (i.Ends == LineEnds.CrLf || i.Ends == LineEnds.None));
            return !(sameEnc && sameEnds);
        }

        internal static string Describe(TextInfo i, TargetEncoding enc, TargetEnds ends)
        {
            var parts = new List<string>();
            var to = TargetName(enc);
            if (!WouldChange(i, enc, TargetEnds.Keep)) { }
            else parts.Add(i.EncodingName.Replace(" (guess)", "") + " → " + to);
            if (ends != TargetEnds.Keep && WouldChange(i, enc, ends) && (i.Ends == LineEnds.Mixed || (ends == TargetEnds.Lf && i.Ends != LineEnds.Lf && i.Ends != LineEnds.None) || (ends == TargetEnds.CrLf && i.Ends != LineEnds.CrLf && i.Ends != LineEnds.None)))
                parts.Add(i.Ends == LineEnds.Mixed ? "line ends made " + (ends == TargetEnds.Lf ? "LF" : "CRLF") : EndsName(i.Ends) + " → " + (ends == TargetEnds.Lf ? "LF" : "CRLF"));
            return string.Join(", ", parts);
        }

        /// <summary>Converts one file in place. The original is saved as a .bak file next to it first when <paramref name="backup"/> is set. Nothing is written when anything fails.</summary>
        internal static TextConvertResult Convert(TextInfo info, TargetEncoding enc, TargetEnds ends, bool backup)
        {
            var r = new TextConvertResult { Path = info.Path };
            if (info.Binary) { r.Failed = true; r.Message = info.BinaryReason; return r; }
            if (info.DecodeError != null)
            {
                r.Failed = true;
                r.Message = "Byte 0x" + info.DecodeErrorByte.ToString("X2") + " at position " + info.DecodeErrorPosition.ToString("N0", CultureInfo.InvariantCulture) + " is not valid in " + info.EncodingName.Replace(" (guess)", "") + ".";
                return r;
            }
            if (!WouldChange(info, enc, ends)) { r.Message = "Skipped, already matches"; return r; }
            string part = info.Path + ".part";
            try
            {
                var text = Normalise(Decode(info), ends);
                var w = Writer(enc);
                using (var fs = new FileStream(part, FileMode.Create, FileAccess.Write))
                {
                    var preamble = w.GetPreamble();
                    fs.Write(preamble, 0, preamble.Length);
                    var data = w.GetBytes(text);
                    fs.Write(data, 0, data.Length);
                }
                if (backup) { r.Backup = FileOps.Unique(info.Path + ".bak"); File.Copy(info.Path, r.Backup); }
                File.Move(part, info.Path, true);
                r.Changed = true;
                r.Message = Describe(info, enc, ends);
            }
            catch (Exception ex)
            {
                try { File.Delete(part); } catch { }
                r.Failed = true; r.Message = ex is EncoderFallbackException ? "A character cannot be written in " + TargetName(enc) + "." : ex.Message;
            }
            return r;
        }

        /// <summary>Puts the original back from its backup and removes the backup.</summary>
        internal static bool Restore(string path, string backup)
        {
            try { File.Copy(backup, path, true); File.Delete(backup); return true; } catch { return false; }
        }
    }
}
