using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace MonitoringTools.Core
{
    /// <summary>
    /// A file to Base64 text written beside it, and that text back to the file, also beside it. Both directions stream,
    /// so a file of any size works; both write to a temporary file first and never overwrite anything, taking a numbered
    /// name when the name is taken; and both are checked by a SHA-256 round trip before they count as done.
    /// Kept free of any window code so the console checks can run it on any machine.
    /// </summary>
    public static class Base64Files
    {
        public const string Suffix = ".base64.txt";
        private const int ReadChunk = 3 * 64 * 1024;   // a multiple of 3, so every chunk encodes without padding

        public sealed class Result
        {
            public string Source;
            public string Output;
            public bool Ok;
            public bool Verified;
            public bool Cancelled;
            public string Error;
            public long InBytes;
            public long OutBytes;
            /// <summary>SHA-256 of the file as written, so an undo removes only a file nobody has changed since.</summary>
            public string WrittenSha256;
            /// <summary>For a decode, what the first bytes say the file is, such as "PDF document"; null when unknown.</summary>
            public string DetectedType;
        }

        /// <summary>True for a file this tool turns back into its original: name.ext.base64.txt, and the older .b64 and .base64.</summary>
        public static bool IsEncoded(string path) =>
            path != null && (path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) || path.EndsWith(".b64", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".base64", StringComparison.OrdinalIgnoreCase));

        /// <summary>Where the Base64 text of a file goes before a clash is resolved: right beside it.</summary>
        public static string EncodedName(string source) => source + Suffix;

        /// <summary>Where a decoded file goes before a clash is resolved: the text file's name without the Base64 ending.</summary>
        public static string DecodedName(string encoded)
        {
            foreach (var end in new[] { Suffix, ".base64", ".b64" })
                if (encoded.EndsWith(end, StringComparison.OrdinalIgnoreCase))
                {
                    var bare = encoded.Substring(0, encoded.Length - end.Length);
                    return Path.GetFileName(bare).Length == 0 ? bare + "decoded.bin" : bare;
                }
            return encoded + ".bin";
        }

        /// <summary>The path itself when it is free, or the first free numbered name beside it: "name (1).ext".</summary>
        public static string Unique(string target)
        {
            if (!File.Exists(target) && !Directory.Exists(target)) return target;
            var dir = Path.GetDirectoryName(target) ?? "";
            var name = Path.GetFileNameWithoutExtension(target);
            var ext = Path.GetExtension(target);
            int n = 1; string p;
            do { p = Path.Combine(dir, $"{name} ({n++}){ext}"); } while (File.Exists(p) || Directory.Exists(p));
            return p;
        }

        /// <summary>Writes the file's Base64 beside it and proves it decodes back to the same SHA-256.</summary>
        public static Result Encode(string source, CancellationToken ct = default, IProgress<double> progress = null)
        {
            var r = new Result { Source = source };
            string temp = null;
            try
            {
                var info = new FileInfo(source);
                if (!info.Exists) { r.Error = "The file is no longer there."; return r; }
                r.InBytes = info.Length;
                temp = TempBeside(source);
                byte[] sourceHash;
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan))
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
                using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    var buf = new byte[ReadChunk];
                    long done = 0;
                    int filled;
                    while ((filled = ReadFull(input, buf)) > 0)
                    {
                        if (ct.IsCancellationRequested) { r.Cancelled = true; r.Error = "Cancelled."; break; }
                        sha.AppendData(buf, 0, filled);
                        var text = System.Convert.ToBase64String(buf, 0, filled);
                        var bytes = Encoding.ASCII.GetBytes(text);
                        output.Write(bytes, 0, bytes.Length);
                        done += filled;
                        progress?.Report(r.InBytes == 0 ? 1 : done / (double)r.InBytes * 0.5);
                    }
                    sourceHash = sha.GetHashAndReset();
                }
                if (r.Cancelled) { TryDelete(temp); return r; }
                // the round trip: decode what was written and compare it with the original
                var back = DecodeStream(temp, null, ct, p => progress?.Report(0.5 + p * 0.5), out var error, out _);
                if (error != null) { TryDelete(temp); r.Error = error; r.Cancelled = ct.IsCancellationRequested; return r; }
                if (!CryptographicOperations.FixedTimeEquals(back, sourceHash)) { TryDelete(temp); r.Error = "The Base64 did not decode back to the same file, so nothing was kept."; return r; }
                r.Output = Place(temp, EncodedName(source));
                r.OutBytes = new FileInfo(r.Output).Length;
                r.WrittenSha256 = Sha256File(r.Output);
                r.Ok = r.Verified = true;
                return r;
            }
            catch (Exception ex) { if (temp != null) TryDelete(temp); r.Error = ex.Message; return r; }
        }

        /// <summary>Writes the original file back beside its Base64 text and proves it encodes back to the same text.</summary>
        public static Result Decode(string encoded, CancellationToken ct = default, IProgress<double> progress = null)
        {
            var r = new Result { Source = encoded };
            string temp = null;
            try
            {
                var info = new FileInfo(encoded);
                if (!info.Exists) { r.Error = "The file is no longer there."; return r; }
                r.InBytes = info.Length;
                var target = DecodedName(encoded);
                temp = TempBeside(target);
                byte[] canonical;
                string error;
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
                    DecodeStream(encoded, output, ct, p => progress?.Report(p * 0.6), out error, out canonical);
                if (error != null) { TryDelete(temp); r.Error = error; r.Cancelled = ct.IsCancellationRequested; return r; }
                if (new FileInfo(temp).Length == 0) { TryDelete(temp); r.Error = "There is no Base64 text in the file."; return r; }
                // the round trip: encode what was written and compare it with the text, in its standard form
                var again = EncodeHash(temp, ct, p => progress?.Report(0.6 + p * 0.4));
                if (ct.IsCancellationRequested) { TryDelete(temp); r.Cancelled = true; r.Error = "Cancelled."; return r; }
                if (!CryptographicOperations.FixedTimeEquals(again, canonical)) { TryDelete(temp); r.Error = "The decoded file did not encode back to the same text, so nothing was kept."; return r; }
                r.DetectedType = Sniff(temp);
                r.Output = Place(temp, target);
                r.OutBytes = new FileInfo(r.Output).Length;
                r.WrittenSha256 = Sha256File(r.Output);
                r.Ok = r.Verified = true;
                return r;
            }
            catch (Exception ex) { if (temp != null) TryDelete(temp); r.Error = ex.Message; return r; }
        }

        /// <summary>Encodes a file, or decodes it when it is a Base64 text file this tool knows.</summary>
        public static Result Run(string path, CancellationToken ct = default, IProgress<double> progress = null) =>
            IsEncoded(path) ? Decode(path, ct, progress) : Encode(path, ct, progress);

        public static string Sha256File(string path)
        {
            using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
            using var sha = SHA256.Create();
            return System.Convert.ToHexString(sha.ComputeHash(f));
        }

        // ------------------------------------------------------------------ decoding

        /// <summary>
        /// Decodes a Base64 text file into the output (or only hashes it when output is null) and returns the SHA-256 of
        /// the decoded bytes. Accepts the standard and URL-safe alphabets, line breaks and other white space, missing
        /// padding and a data: URL prefix. Anything else stops with the exact byte offset. Also gives the SHA-256 of the
        /// text in its standard form (standard alphabet, padded, no white space), for the decode round trip.
        /// </summary>
        public static byte[] DecodeStream(string path, Stream output, CancellationToken ct, Action<double> progress, out string error, out byte[] canonicalSha256)
        {
            error = null; canonicalSha256 = null;
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
            long total = Math.Max(1, input.Length);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var textSha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buf = new byte[1 << 16];
            var quad = new char[ReadChunk / 3 * 4];
            int q = 0;
            long offset = 0;
            bool padding = false, first = true;
            int read;
            while ((read = input.Read(buf, 0, buf.Length)) > 0)
            {
                if (ct.IsCancellationRequested) { error = "Cancelled."; return null; }
                int start = 0;
                if (first)
                {
                    first = false;
                    start = SkipPreamble(buf, read);
                }
                for (int i = start; i < read; i++)
                {
                    byte b = buf[i];
                    char c = (char)b;
                    if (c == ' ' || c == '\r' || c == '\n' || c == '\t' || c == '\f' || c == '\v') continue;
                    if (c == '=') { padding = true; continue; }
                    if (c == '-') c = '+'; else if (c == '_') c = '/';
                    bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/';
                    if (!ok || padding)
                    {
                        error = padding && ok ? $"There is Base64 text after the padding, at byte {offset + i:N0}." : $"Byte {offset + i:N0} is not Base64 text.";
                        return null;
                    }
                    quad[q++] = c;
                    if (q == quad.Length) { Flush(quad, q, output, sha, textSha); q = 0; }
                }
                offset += read;
                progress?.Invoke(offset / (double)total);
            }
            if (q % 4 == 1) { error = "The Base64 text ends one character short of a whole byte."; return null; }
            int full = q - q % 4;
            if (full > 0) Flush(quad, full, output, sha, textSha);
            if (q % 4 != 0)
            {
                var tail = new char[4];
                Array.Copy(quad, full, tail, 0, q - full);
                for (int i = q - full; i < 4; i++) tail[i] = '=';
                Flush(tail, 4, output, sha, textSha);
            }
            canonicalSha256 = textSha.GetHashAndReset();
            return sha.GetHashAndReset();
        }

        private static void Flush(char[] chars, int count, Stream output, IncrementalHash sha, IncrementalHash textSha)
        {
            var bytes = System.Convert.FromBase64CharArray(chars, 0, count);
            sha.AppendData(bytes);
            textSha.AppendData(Encoding.ASCII.GetBytes(System.Convert.ToBase64String(bytes)));
            output?.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Skips a byte order mark and a "data:...;base64," prefix at the start of the text.</summary>
        private static int SkipPreamble(byte[] buf, int read)
        {
            int start = read >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF ? 3 : 0;
            var head = Encoding.ASCII.GetString(buf, start, Math.Min(read - start, 512));
            int lead = head.Length - head.TrimStart().Length;
            if (head.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = head.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
                if (comma > 0) return start + comma + 8;
            }
            return start + lead;
        }

        /// <summary>SHA-256 of the file's Base64 text, as Encode would write it.</summary>
        private static byte[] EncodeHash(string path, CancellationToken ct, Action<double> progress)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buf = new byte[ReadChunk];
            long done = 0, total = Math.Max(1, input.Length);
            int filled;
            while ((filled = ReadFull(input, buf)) > 0)
            {
                if (ct.IsCancellationRequested) break;
                sha.AppendData(Encoding.ASCII.GetBytes(System.Convert.ToBase64String(buf, 0, filled)));
                done += filled;
                progress?.Invoke(done / (double)total);
            }
            return sha.GetHashAndReset();
        }

        // ------------------------------------------------------------------ helpers

        private static int ReadFull(Stream s, byte[] buf)
        {
            int total = 0, n;
            while (total < buf.Length && (n = s.Read(buf, total, buf.Length - total)) > 0) total += n;
            return total;
        }

        private static string TempBeside(string target) =>
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)) ?? "", "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".partial");

        /// <summary>Moves the finished temporary file to the first free name, never over an existing file.</summary>
        private static string Place(string temp, string target)
        {
            for (int attempt = 0; ; attempt++)
            {
                var dest = Unique(target);
                try { File.Move(temp, dest, overwrite: false); return dest; }
                catch (IOException) when (attempt < 5 && File.Exists(dest)) { /* taken a moment ago; take the next number */ }
            }
        }

        private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

        /// <summary>What a file's first bytes say it is, for the decode result; null when they say nothing.</summary>
        public static string Sniff(string path)
        {
            try
            {
                var head = new byte[16];
                int n;
                using (var f = File.OpenRead(path)) n = f.Read(head, 0, head.Length);
                bool Starts(params byte[] sig) { if (n < sig.Length) return false; for (int i = 0; i < sig.Length; i++) if (head[i] != sig[i]) return false; return true; }
                if (Starts(0x25, 0x50, 0x44, 0x46)) return "PDF document";
                if (Starts(0x89, 0x50, 0x4E, 0x47)) return "PNG image";
                if (Starts(0xFF, 0xD8, 0xFF)) return "JPEG image";
                if (Starts(0x47, 0x49, 0x46, 0x38)) return "GIF image";
                if (Starts(0x50, 0x4B, 0x03, 0x04)) return "ZIP archive or Office file";
                if (Starts(0x52, 0x49, 0x46, 0x46) && n >= 12 && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50) return "WebP image";
                if (Starts(0x4D, 0x5A)) return "Windows program";
                if (Starts(0x1F, 0x8B)) return "GZIP archive";
                if (Starts(0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)) return "7-Zip archive";
                if (n >= 8 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70) return "MP4 or QuickTime video";
                return null;
            }
            catch { return null; }
        }
    }
}
