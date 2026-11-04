using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    /// </summary>
    public static class PersonalWording
    {
        public const int MaxBytes = 256 * 1024;
        public const int MaxDepth = 8;
        public const int MaxKey = 160;
        public const int MaxValue = 1000;
        public const int SupportedVersion = 1;

        private static Dictionary<string, string> _map = new Dictionary<string, string>();
        private static Regex _matcher;
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

        public static void Clear()
        {
            try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { }
            Set(new Dictionary<string, string>(), persist: false);
        }

        private static void Set(Dictionary<string, string> map, bool persist, byte[] bytes = null)
        {
            if (persist && bytes != null) File.WriteAllBytes(CachePath, bytes);
            _map = map;
            _matcher = map.Count == 0 ? null : new Regex(
                @"(?<![\p{L}\p{N}])(" + string.Join("|", map.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)) + @")(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            Changed?.Invoke();
        }

        // -------------------------------------------------------------- applying

        /// <summary>Rewrites ordinary phrases in on-screen text, keeping the original capitalisation style.</summary>
        public static string Apply(string text)
        {
            if (_matcher == null || string.IsNullOrEmpty(text)) return text;
            try
            {
                return _matcher.Replace(text, m =>
                {
                    var key = _map.Keys.First(k => string.Equals(k, m.Value, StringComparison.OrdinalIgnoreCase));
                    var rep = _map[key];
                    if (m.Value.Length > 0 && char.IsUpper(m.Value[0]) && rep.Length > 0)
                        rep = char.ToUpper(rep[0], CultureInfo.CurrentCulture) + rep.Substring(1);
                    return rep;
                });
            }
            catch (RegexMatchTimeoutException) { return text; }
        }

        // -------------------------------------------------------------- validation

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
                            if (!seen.Peek().Add(reader.GetString())) { why = "The file repeats a key."; return false; }
                            break;
                    }
                }
            }
            catch (JsonException) { why = "The file is not valid JSON, or it is nested too deeply."; return false; }

            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { why = "The file must contain one JSON object."; return false; }
            foreach (var p in root.EnumerateObject())
                if (p.Name != "schemaVersion" && p.Name != "entries") { why = "The file has a field this version does not know."; return false; }
            if (!root.TryGetProperty("schemaVersion", out var ver) || ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out var v) || v != SupportedVersion)
            { why = $"Only schema version {SupportedVersion} is supported."; return false; }
            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Object)
            { why = "The file needs an \"entries\" object."; return false; }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries.EnumerateObject())
            {
                var key = e.Name;
                if (string.IsNullOrWhiteSpace(key) || key is "__proto__" or "constructor" or "prototype") { why = "The file has an unsafe or empty key."; return false; }
                if (new StringInfo(key).LengthInTextElements > MaxKey) { why = $"A key is longer than {MaxKey} characters."; return false; }
                if (e.Value.ValueKind != JsonValueKind.String) { why = "Every replacement must be text."; return false; }
                var val = e.Value.GetString();
                if (new StringInfo(val).LengthInTextElements > MaxValue) { why = $"A replacement is longer than {MaxValue} characters."; return false; }
                if (result.ContainsKey(key)) { why = "The file repeats a key."; return false; }
                result[key] = val;
            }
            if (result.Count == 0) { why = "The file has no entries."; return false; }
            map = result;
            why = null;
            return true;
        }
    }
}
