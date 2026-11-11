using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Optional personal wording: a local JSON file that maps ordinary phrases to the user's own
    /// words for on-screen text. The app ships no mappings at all. A file is validated in full
    /// (size, schema version, depth, duplicate keys, lengths, string values only, no unknown
    /// fields) before anything is applied, and a rejected file changes nothing. The validated copy
    /// is cached locally and revalidated on every load. Nothing here is logged, exported or sent.
    ///
    /// Format: { "schemaVersion": 1, "entries": { "ordinary phrase": "replacement", ... } }
    /// Also accepted: other top-level fields (names, dates, notes) are ignored; an entry may be an object with a
    /// "replacement", "value" or "to" text; keys that differ only in case are separate entries (the exact case wins).
    /// </summary>
    public static class PersonalWording
    {
        public const int MaxBytes = 4 * 1024 * 1024;
        public const int MaxDepth = 8;
        public const int MaxKey = 160;
        public const int MaxValue = 1000;
        public const int SupportedVersion = 1;
        public const int NewestVersion = 2;

        private static Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Dictionary<string, string> _folded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static int[] _lengths = Array.Empty<int>();
        public static event Action Changed;

        public static int Count => _map.Count;
        /// <summary>True when a wording file is loaded and School mode is not leaving personal wording out.</summary>
        public static bool Active => _map.Count > 0 && !Modes.Blocks(ModeFeature.PersonalVocabulary);

        /// <summary>Tells listeners to re-apply the wording, for example after School mode switched on or off.</summary>
        internal static void Reapply() => Changed?.Invoke();
        private static string CachePath => Path.Combine(Settings.DataDir, "wording.json");

        /// <summary>The headline for the celebration: the file's own value for "private vocabulary", else a neutral one.</summary>
        public static string Headline =>
            _map.TryGetValue("private vocabulary", out var v) && !string.IsNullOrWhiteSpace(v) ? v : "Personal wording applied";

        // -------------------------------------------------------------- loading

        public static void LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return;
                var bytes = File.ReadAllBytes(CachePath);
                if (Validate(bytes, out var map, out _)) Set(map, persist: false);
                else { File.Delete(CachePath); Set(new Dictionary<string, string>(), persist: false); }   // fail closed to shipped wording
            }
            catch { Set(new Dictionary<string, string>(), persist: false); }
        }

        /// <summary>Validates and applies a file. Returns a short user-facing result; never the payload.</summary>
        public static bool TryApply(string path, out string message)
        {
            if (Modes.Blocks(ModeFeature.PersonalVocabulary)) { message = Modes.SchoolName + " is on, so personal wording is left out until it is off."; return false; }
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) { message = "That file could not be found."; return false; }
                if (info.Length > MaxBytes) { message = $"That file is larger than {MaxBytes / 1024} KiB, so nothing was changed."; return false; }
                var bytes = File.ReadAllBytes(path);
                if (!Validate(bytes, out var map, out var why)) { message = why + " Nothing was changed."; return false; }
                Set(map, persist: true, bytes: bytes);
                message = $"Personal wording applied: {map.Count} phrases.";
                return true;
            }
            catch (Exception) { message = "That file could not be read, so nothing was changed."; return false; }
        }

        /// <summary>True when the path names a JSON file that passes validation. Reads nothing larger than the cap
        /// and says nothing about why another file failed, so copying an unrelated JSON file stays silent.</summary>
        public static bool IsWordingFile(string path)
        {
            try
            {
                if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) return false;
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0 || info.Length > MaxBytes) return false;
                return Validate(File.ReadAllBytes(path), out _, out _);
            }
            catch { return false; }
        }

        /// <summary>
        /// True when a JSON file is meant as a wording file (it has "entries" and "schemaVersion" near the top), whether or not it
        /// passes. A copied file like that which fails is reported with the reason instead of being ignored.
        /// </summary>
        public static bool LooksLikeWordingFile(string path)
        {
            try
            {
                if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) return false;
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0 || info.Length > 16 * 1024 * 1024) return false;
                using var f = File.OpenRead(path);
                var buf = new byte[(int)Math.Min(info.Length, 64 * 1024)];
                var n = f.Read(buf, 0, buf.Length);
                var head = Encoding.UTF8.GetString(buf, 0, n);
                return head.Contains("\"entries\"") && head.Contains("\"schemaVersion\"");
            }
            catch { return false; }
        }

        /// <summary>
        /// The file a copied piece of text names, when it names one existing file: a plain path, a path in quotes as
        /// File Explorer's "Copy as path" gives it, or a file:/// address. Null otherwise.
        /// </summary>
        public static string PathFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim();
            if (t.Contains('\n')) return null;
            if (t.Length >= 2 && (t[0] == '"' && t[^1] == '"' || t[0] == '\'' && t[^1] == '\'')) t = t.Substring(1, t.Length - 2).Trim();
            try
            {
                if (t.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(t, UriKind.Absolute, out var u) && u.IsFile) t = u.LocalPath;
                t = Environment.ExpandEnvironmentVariables(t);
                if (t.Length < 4 || t.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(t)) return null;
                return File.Exists(t) ? Path.GetFullPath(t) : null;
            }
            catch { return null; }
        }

        /// <summary>Self-test only: drops the words from memory without touching the saved copy, as a restart does.</summary>
        internal static void ForgetForTest() { _map = new Dictionary<string, string>(StringComparer.Ordinal); _folded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); _lengths = Array.Empty<int>(); }

        public static void Clear()
        {
            try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { }
            Set(new Dictionary<string, string>(), persist: false);
        }

        private static void Set(Dictionary<string, string> map, bool persist, byte[] bytes = null)
        {
            if (persist && bytes != null) File.WriteAllBytes(CachePath, bytes);
            _map = new Dictionary<string, string>(map, StringComparer.Ordinal);
            // case-folded lookup: the first spelling seen for each folded key answers when the exact case is not in the file
            var folded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in map.Keys) if (!folded.ContainsKey(k)) folded[k] = k;
            _folded = folded;
            _lengths = map.Keys.Select(k => k.Length).Distinct().OrderByDescending(n => n).ToArray();
            Changed?.Invoke();
        }

        // -------------------------------------------------------------- applying

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c);

        /// <summary>
        /// Rewrites ordinary phrases in on-screen text, keeping the original capitalisation style. The longest phrase wins
        /// at each place, phrases match whole words only, and the scan is linear in the text (no pattern, no timeout),
        /// so a file with thousands of phrases applies as fast as a small one.
        /// </summary>
        public static string Apply(string text)
        {
            if (_lengths.Length == 0 || string.IsNullOrEmpty(text)) return text;
            StringBuilder sb = null;
            int i = 0, copied = 0;
            while (i < text.Length)
            {
                // the same word edges as before: no letter or digit just before the phrase or just after it
                bool startOk = i == 0 || !IsWordChar(text[i - 1]);
                string hitKey = null; int hitLen = 0;
                if (startOk)
                {
                    foreach (var len in _lengths)
                    {
                        if (i + len > text.Length) continue;
                        if (i + len < text.Length && IsWordChar(text[i + len])) continue;
                        var cand = text.Substring(i, len);
                        if (_map.ContainsKey(cand)) { hitKey = cand; hitLen = len; break; }
                        if (_folded.TryGetValue(cand, out var k)) { hitKey = k; hitLen = len; break; }
                    }
                }
                if (hitKey == null) { i++; continue; }
                sb ??= new StringBuilder(text.Length + 16);
                sb.Append(text, copied, i - copied);
                var written = text.Substring(i, hitLen);
                var rep = _map[hitKey];
                if (written.Length > 0 && char.IsUpper(written[0]) && rep.Length > 0 && !char.IsUpper(rep[0]))
                    rep = char.ToUpper(rep[0], CultureInfo.CurrentCulture) + rep.Substring(1);
                sb.Append(rep);
                i += hitLen; copied = i;
            }
            if (sb == null) return text;
            sb.Append(text, copied, text.Length - copied);
            return sb.ToString();
        }

        // -------------------------------------------------------------- validation

        private static string Short(string s) => s.Length > 40 ? s.Substring(0, 40) + "…" : s;

        public static bool Validate(byte[] bytes, out Dictionary<string, string> map, out string why)
        {
            map = null;
            if (bytes.Length > MaxBytes) { why = $"The file is larger than {MaxBytes / 1024} KiB."; return false; }
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'); }
            catch { why = "The file is not UTF-8 text."; return false; }

            // A reader pass catches what JsonDocument tolerates: duplicate keys and excessive depth.
            try
            {
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), new JsonReaderOptions { MaxDepth = MaxDepth, CommentHandling = JsonCommentHandling.Disallow });
                var seen = new Stack<HashSet<string>>();
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject: seen.Push(new HashSet<string>(StringComparer.Ordinal)); break;
                        case JsonTokenType.EndObject: seen.Pop(); break;
                        case JsonTokenType.PropertyName:
                            if (!seen.Peek().Add(reader.GetString())) { why = $"The file repeats the key \"{Short(reader.GetString())}\" exactly."; return false; }
                            break;
                    }
                }
            }
            catch (JsonException) { why = "The file is not valid JSON, or it is nested too deeply."; return false; }

            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { why = "The file must contain one JSON object."; return false; }
            // Other top-level fields (a name, a date, notes, a generator) are left alone; only "entries" is read.
            if (root.TryGetProperty("schemaVersion", out var ver))
            {
                if (ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out var v) || v < SupportedVersion || v > NewestVersion)
                { why = $"Schema version {(ver.ValueKind == JsonValueKind.Number ? ver.GetRawText() : "?")} is not supported; this version reads {SupportedVersion} to {NewestVersion}."; return false; }
            }
            else { why = "The file needs a \"schemaVersion\" number."; return false; }
            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Object)
            { why = "The file needs an \"entries\" object."; return false; }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in entries.EnumerateObject())
            {
                var key = e.Name;
                if (string.IsNullOrWhiteSpace(key)) { why = "The file has an empty key."; return false; }
                if (key == "__proto__") continue;
                if (new StringInfo(key).LengthInTextElements > MaxKey) { why = $"The key \"{Short(key)}\" is longer than {MaxKey} characters."; return false; }
                string val = null;
                if (e.Value.ValueKind == JsonValueKind.String) val = e.Value.GetString();
                else if (e.Value.ValueKind == JsonValueKind.Object)
                    foreach (var name in new[] { "replacement", "value", "to", "text" })
                        if (e.Value.TryGetProperty(name, out var inner) && inner.ValueKind == JsonValueKind.String) { val = inner.GetString(); break; }
                if (val == null) { why = $"The entry \"{Short(key)}\" has no text to show (it must be text, or an object with a \"replacement\")."; return false; }
                if (new StringInfo(val).LengthInTextElements > MaxValue) { why = $"The replacement for \"{Short(key)}\" is longer than {MaxValue} characters."; return false; }
                if (result.TryGetValue(key, out var had) && had != val) { why = $"The file gives \"{Short(key)}\" two different replacements."; return false; }
                result[key] = val;
            }
            if (result.Count == 0) { why = "The file has no entries."; return false; }
            map = result;
            why = null;
            return true;
        }
    }
}
