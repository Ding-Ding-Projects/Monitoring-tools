using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// server.properties as a list of its own lines. Reading and writing it back without changes gives the same bytes;
    /// a change replaces only the value of that one key, so comments, blank lines, the order and keys this app does not
    /// know are kept exactly as they were written.
    /// </summary>
    public sealed class McProperties
    {
        private sealed class Line { public string Text; public string Key; }
        private readonly List<Line> _lines = new List<Line>();
        private string _newline = "\n";
        private bool _endsWithNewline = true;

        public static McProperties Parse(string text)
        {
            var p = new McProperties();
            text ??= "";
            p._newline = text.Contains("\r\n") ? "\r\n" : "\n";
            p._endsWithNewline = text.Length == 0 || text.EndsWith("\n", StringComparison.Ordinal);
            var body = p._endsWithNewline && text.Length > 0 ? text[..^(text.EndsWith("\r\n") ? 2 : 1)] : text;
            if (body.Length == 0 && text.Length == 0) return p;
            foreach (var raw in body.Split('\n'))
            {
                var t = raw.EndsWith("\r") ? raw[..^1] : raw;
                p._lines.Add(new Line { Text = t, Key = KeyOf(t) });
            }
            return p;
        }

        public static McProperties Load(string path) => Parse(File.Exists(path) ? File.ReadAllText(path) : "");

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, ToString(), new UTF8Encoding(false));
            File.Move(tmp, path, true);
        }

        /// <summary>The key of a "key=value" or "key:value" line, or null for a comment, a blank line or anything else.</summary>
        private static string KeyOf(string line)
        {
            var t = line.TrimStart();
            if (t.Length == 0 || t[0] == '#' || t[0] == '!') return null;
            int i = t.IndexOfAny(new[] { '=', ':' });
            return i <= 0 ? null : t[..i].Trim();
        }

        public IEnumerable<string> Keys => _lines.Where(l => l.Key != null).Select(l => l.Key).Distinct();
        public bool Has(string key) => _lines.Any(l => l.Key == key);

        public string Get(string key)
        {
            var l = _lines.LastOrDefault(x => x.Key == key);
            if (l == null) return null;
            var t = l.Text.TrimStart();
            int i = t.IndexOfAny(new[] { '=', ':' });
            return t[(i + 1)..];
        }

        /// <summary>Sets one key. An existing line keeps its position and its key spelling; a new key is added at the end.</summary>
        public void Set(string key, string value)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ");
            var l = _lines.LastOrDefault(x => x.Key == key);
            if (l == null) { _lines.Add(new Line { Text = key + "=" + value, Key = key }); return; }
            var lead = l.Text[..(l.Text.Length - l.Text.TrimStart().Length)];
            var t = l.Text.TrimStart();
            int i = t.IndexOfAny(new[] { '=', ':' });
            l.Text = lead + t[..(i + 1)] + value;
        }

        public Dictionary<string, string> ToDictionary() => Keys.ToDictionary(k => k, Get);

        public override string ToString()
        {
            var s = string.Join(_newline, _lines.Select(l => l.Text));
            return _lines.Count == 0 ? "" : s + (_endsWithNewline ? _newline : "");
        }
    }
}
