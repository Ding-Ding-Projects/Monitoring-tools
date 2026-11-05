using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Sample text handling for the Test view: matching the whole text, each line or each page on its own,
    /// line-ending conversion, reading bytes in a chosen encoding and a hexadecimal dump of the sample.
    /// </summary>
    public static class RegexSample
    {
        public static readonly string[] Scopes = { "Whole text", "Each line", "Each page" };
        public static readonly string[] LineEndings = { "Keep", "LF", "CRLF" };
        public static readonly string[] Encodings = { "UTF-8", "UTF-16", "Latin-1" };

        /// <summary>Converts every line break in the text to the chosen style. Keep returns the text as it is.</summary>
        public static string NormalizeLineEndings(string text, string style)
        {
            text ??= "";
            if (style != "LF" && style != "CRLF") return text;
            var lf = text.Replace("\r\n", "\n").Replace('\r', '\n');
            return style == "LF" ? lf : lf.Replace("\n", "\r\n");
        }

        /// <summary>Describes the line endings found in a text, for display next to the sample.</summary>
        public static string DescribeLineEndings(string text)
        {
            text ??= "";
            int crlf = 0, lf = 0, cr = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; } else cr++; }
                else if (text[i] == '\n') lf++;
            }
            if (crlf + lf + cr == 0) return "no line breaks";
            var parts = new List<string>();
            if (crlf > 0) parts.Add(crlf + " CRLF");
            if (lf > 0) parts.Add(lf + " LF");
            if (cr > 0) parts.Add(cr + " CR");
            return string.Join(", ", parts);
        }

        public static Encoding EncodingByName(string name) => name switch
        {
            "UTF-16" => new UnicodeEncoding(false, false),
            "Latin-1" => Encoding.Latin1,
            _ => new UTF8Encoding(false),
        };

        /// <summary>Reads bytes as text in the named encoding. A byte order mark that disagrees with the choice wins, so nothing is read wrongly.</summary>
        public static string Decode(byte[] bytes, string encodingName, out string used)
        {
            bytes ??= Array.Empty<byte>();
            if (bytes.Length > RegexKit.MaxSampleLength) bytes = bytes[..RegexKit.MaxSampleLength];
            if (encodingName == null || encodingName == "Auto")
            {
                var tf = RegexGrep.Decode(bytes);
                used = tf.EncodingName;
                return tf.Text;
            }
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)) || bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                var tf = RegexGrep.Decode(bytes);
                used = tf.EncodingName + " (from the byte order mark)";
                return tf.Text;
            }
            used = encodingName;
            return EncodingByName(encodingName).GetString(bytes);
        }

        /// <summary>
        /// Runs a pattern over a sample the way the chosen scope says. In "Each line" and "Each page" the pattern is applied to every
        /// piece separately, so ^ and $ and \A and \z refer to that piece; reported positions are always positions in the whole sample.
        /// </summary>
        public static RegexKit.TestRun Run(Regex rx, string sample, string scope, int maxMatches = 500)
        {
            sample ??= "";
            if (scope != "Each line" && scope != "Each page") return RegexKit.Test(rx, sample, maxMatches);
            var matches = new List<RegexKit.MatchHit>();
            double ms = 0; bool timedOut = false, truncated = false;
            foreach (var (start, text) in Pieces(sample, scope))
            {
                var run = RegexKit.Test(rx, text, maxMatches - matches.Count);
                ms += run.Milliseconds; timedOut |= run.TimedOut; truncated |= run.Truncated;
                foreach (var m in run.Matches)
                    matches.Add(new RegexKit.MatchHit(m.Index + start, m.Length, m.Value, m.Groups.Select(g => new RegexKit.GroupHit(g.Name, g.Value, g.Success ? g.Index + start : g.Index, g.Length, g.Success)).ToList()));
                if (matches.Count >= maxMatches) { truncated = true; break; }
                if (timedOut) break;
            }
            return new RegexKit.TestRun(matches, ms, timedOut, truncated);
        }

        /// <summary>The pieces of a sample for a scope, each with the offset where it starts.</summary>
        public static List<(int Start, string Text)> Pieces(string sample, string scope)
        {
            sample ??= "";
            var list = new List<(int, string)>();
            char sep = scope == "Each page" ? '\f' : '\n';
            int pos = 0;
            while (pos <= sample.Length)
            {
                int next = sample.IndexOf(sep, pos);
                int end = next < 0 ? sample.Length : next;
                var piece = sample.Substring(pos, end - pos);
                if (sep == '\n' && piece.EndsWith('\r')) piece = piece[..^1];
                list.Add((pos, piece));
                if (next < 0) break;
                pos = next + 1;
            }
            return list;
        }

        /// <summary>Line and column (both starting at 1) of an index in a sample, counting a CRLF pair as one break.</summary>
        public static (int Line, int Column) LineColumn(string sample, int index)
        {
            sample ??= "";
            index = Math.Max(0, Math.Min(index, sample.Length));
            int line = 1, last = 0;
            for (int i = 0; i < index; i++)
                if (sample[i] == '\n') { line++; last = i + 1; }
            return (line, index - last + 1);
        }

        /// <summary>A classic hexadecimal dump: offset, sixteen bytes in hex and the printable characters, for the sample in an encoding.</summary>
        public static string HexDump(string sample, string encodingName, int maxBytes = 4096)
        {
            var bytes = EncodingByName(encodingName).GetBytes(sample ?? "");
            var sb = new StringBuilder();
            int n = Math.Min(bytes.Length, maxBytes);
            for (int off = 0; off < n; off += 16)
            {
                sb.Append(off.ToString("X8")).Append("  ");
                for (int i = 0; i < 16; i++)
                {
                    if (off + i < n) sb.Append(bytes[off + i].ToString("X2")).Append(' '); else sb.Append("   ");
                    if (i == 7) sb.Append(' ');
                }
                sb.Append(" |");
                for (int i = 0; i < 16 && off + i < n; i++) { var b = bytes[off + i]; sb.Append(b >= 32 && b < 127 ? (char)b : '.'); }
                sb.Append("|\n");
            }
            if (bytes.Length > n) sb.Append("... ").Append(bytes.Length - n).Append(" more byte(s) not shown\n");
            if (bytes.Length == 0) sb.Append("(empty)\n");
            return sb.ToString();
        }
    }
}
