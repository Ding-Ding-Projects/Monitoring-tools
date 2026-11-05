using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    /// <summary>Checksums of a file: SHA-256, SHA-1, MD5 and SHA-512 with a cancellable, progress-reporting read, and the comparison rules for a pasted value.</summary>
    internal static class HashEngine
    {
        internal static readonly string[] Algorithms = { "SHA-256", "SHA-1", "MD5", "SHA-512" };

        internal static HashAlgorithmName Name(string algorithm) => algorithm switch
        {
            "SHA-1" => HashAlgorithmName.SHA1,
            "MD5" => HashAlgorithmName.MD5,
            "SHA-512" => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA256,
        };

        /// <summary>Hex length of each algorithm's digest.</summary>
        internal static int HexLength(string algorithm) => algorithm switch { "SHA-1" => 40, "MD5" => 32, "SHA-512" => 128, _ => 64 };

        /// <summary>The algorithm whose digest has this many hex characters, or null when none does.</summary>
        internal static string AlgorithmForLength(int hexChars) => Algorithms.FirstOrDefault(a => HexLength(a) == hexChars);

        /// <summary>Reads the file once, 1 MB at a time, and feeds every requested algorithm. Cancelling throws OperationCanceledException and leaves nothing behind.</summary>
        internal static Task<Dictionary<string, string>> ComputeAsync(string path, IEnumerable<string> algorithms, IProgress<(long Done, long Total)> progress, CancellationToken ct)
        {
            var algos = algorithms.Distinct().ToList();
            return Task.Run(() =>
            {
                var hashers = algos.ToDictionary(a => a, a => IncrementalHash.CreateHash(Name(a)));
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20, FileOptions.SequentialScan);
                    long total = fs.Length, done = 0;
                    var buffer = new byte[1 << 20];
                    var last = DateTime.UtcNow;
                    int n;
                    while ((n = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        foreach (var h in hashers.Values) h.AppendData(buffer, 0, n);
                        done += n;
                        if ((DateTime.UtcNow - last).TotalMilliseconds >= 80) { progress?.Report((done, total)); last = DateTime.UtcNow; }
                    }
                    progress?.Report((done, total));
                    return hashers.ToDictionary(kv => kv.Key, kv => Convert.ToHexString(kv.Value.GetHashAndReset()).ToLowerInvariant());
                }
                finally { foreach (var h in hashers.Values) h.Dispose(); }
            }, ct);
        }

        /// <summary>The hash in groups of eight characters, the way it is shown on screen.</summary>
        internal static string Grouped(string hex) => string.Join(" ", Enumerable.Range(0, (hex.Length + 7) / 8).Select(i => hex.Substring(i * 8, Math.Min(8, hex.Length - i * 8))));

        internal sealed class Expected
        {
            public string Hex;          // lower-case hex, null when the text is not a checksum
            public string Problem;      // why it cannot be used
            public string FileName;     // when a whole "hash  filename" line was pasted
        }

        /// <summary>
        /// Reads a pasted checksum. Spaces, dashes, colons and capitals are ignored; a whole "hash  filename" or "hash *filename" line works
        /// too; a checksum file with several lines uses the line whose name matches <paramref name="fileName"/> when given, else the first.
        /// </summary>
        internal static Expected ParseExpected(string text, string fileName = null)
        {
            var e = new Expected();
            text = (text ?? "").Trim();
            if (text.Length == 0) { e.Problem = "Nothing pasted yet."; return e; }
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            string pick = lines[0];
            if (lines.Count > 1 && fileName != null)
                pick = lines.FirstOrDefault(l => l.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)) ?? lines[0];
            // "hash  name" / "hash *name" / "SHA256 (name) = hash" style lines
            var m = Regex.Match(pick, @"^([0-9A-Fa-f]{32}|[0-9A-Fa-f]{40}|[0-9A-Fa-f]{64}|[0-9A-Fa-f]{128})\s+\*?(.+)$");
            if (m.Success) { e.Hex = m.Groups[1].Value.ToLowerInvariant(); e.FileName = m.Groups[2].Value.Trim(); return e; }
            m = Regex.Match(pick, @"^(?:SHA-?\d+|MD5)\s*\((.+)\)\s*=\s*([0-9A-Fa-f]+)$", RegexOptions.IgnoreCase);
            if (m.Success) { e.Hex = m.Groups[2].Value.ToLowerInvariant(); e.FileName = m.Groups[1].Value; return e; }
            var clean = Regex.Replace(pick, @"[\s\-:]", "");
            if (clean.Length == 0 || !Regex.IsMatch(clean, "^[0-9A-Fa-f]+$")) { e.Problem = "That is not a checksum. A checksum has only the digits 0 to 9 and the letters a to f."; return e; }
            e.Hex = clean.ToLowerInvariant();
            return e;
        }

        /// <summary>The 1-based number of the first character where the two values differ, or 0 when they are equal.</summary>
        internal static int FirstDifference(string a, string b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) if (a[i] != b[i]) return i + 1;
            return a.Length == b.Length ? 0 : n + 1;
        }

        /// <summary>One line of a checksum file: "hash *name".</summary>
        internal static string FileLine(string hex, string fileName) => hex + " *" + fileName;
    }
}
