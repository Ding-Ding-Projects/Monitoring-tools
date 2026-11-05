using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The automation entry of the regex engine: one JSON request in, one JSON result out
    /// (MonitoringTools.exe --regex --automation, request on standard input or with --request FILE).
    /// It runs one or more patterns over text and files and returns matches with groups, replacements,
    /// splits or a validity check. Nothing is written to disk. The format is described in the command line section of
    /// docs/features/search/regex-builder.md.
    /// </summary>
    public static class RegexAutomation
    {
        public const int DefaultMaxMatches = 1000;
        public const int MaxMatchesCap = 50000;
        public const int MaxPatterns = 100;

        // The result goes to a program, not a web page, so characters such as < and + stay as they are.
        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>Runs a request and returns the result document. Exit code 0 when anything matched (validate: every pattern valid), 1 when not, 2 for a bad request.</summary>
        public static string Handle(string requestJson, out int exit)
        {
            JsonNode req;
            try { req = JsonNode.Parse(requestJson ?? ""); }
            catch (JsonException ex) { exit = 2; return Fail("The request is not valid JSON: " + ex.Message); }
            if (req is not JsonObject o) { exit = 2; return Fail("The request must be a JSON object."); }

            var op = (Str(o, "operation") ?? "match").Trim().ToLowerInvariant();
            if (op != "match" && op != "replace" && op != "split" && op != "validate") { exit = 2; return Fail("Unknown operation \"" + op + "\". Use match, replace, split or validate."); }
            int maxMatches = DefaultMaxMatches;
            if (o["maxMatches"] is JsonValue mv && mv.TryGetValue<int>(out var mm)) maxMatches = Math.Clamp(mm, 1, MaxMatchesCap);

            // patterns: a "patterns" array, or a single "pattern" with "options" and "replacement"
            var specs = new List<(string Pattern, string Options, string Replacement, string Name, string Error)>();
            if (o["patterns"] is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JsonValue v && v.TryGetValue<string>(out var text)) specs.Add((text, Str(o, "options"), Str(o, "replacement"), null, null));
                    else if (item is JsonObject po)
                    {
                        var lib = Str(po, "library");
                        if (lib != null) specs.Add(FromLibrary(lib, po["parameters"] as JsonObject, Str(po, "options"), Str(po, "replacement"), Str(po, "name")));
                        else specs.Add((Str(po, "pattern"), Str(po, "options") ?? Str(o, "options"), Str(po, "replacement") ?? Str(o, "replacement"), Str(po, "name"), Str(po, "pattern") == null ? "This entry has no \"pattern\"." : null));
                    }
                    else { exit = 2; return Fail("Each entry of \"patterns\" must be a string or an object with \"pattern\"."); }
                }
            }
            else if (Str(o, "pattern") != null) specs.Add((Str(o, "pattern"), Str(o, "options"), Str(o, "replacement"), null, null));
            else if (Str(o, "library") != null) specs.Add(FromLibrary(Str(o, "library"), o["parameters"] as JsonObject, Str(o, "options"), Str(o, "replacement"), null));
            if (specs.Count == 0) { exit = 2; return Fail("Give \"pattern\", \"patterns\" or \"library\"."); }
            if (specs.Count > MaxPatterns) { exit = 2; return Fail("At most " + MaxPatterns + " patterns per request."); }

            // inputs: "text" and/or "files"
            var inputs = new List<(string Source, string Text, string Error)>();
            if (o["text"] is JsonValue tv && tv.TryGetValue<string>(out var inline)) inputs.Add(("(text)", inline, null));
            if (o["files"] is JsonArray files)
            {
                foreach (var f in files)
                {
                    if (f is not JsonValue fv || !fv.TryGetValue<string>(out var path)) { exit = 2; return Fail("Each entry of \"files\" must be a path string."); }
                    try { inputs.Add((path, RegexGrep.Decode(File.ReadAllBytes(path)).Text, null)); }
                    catch (Exception ex) { inputs.Add((path, null, ex.Message)); }
                }
            }
            if (inputs.Count == 0 && op != "validate") { exit = 2; return Fail("Give \"text\" or \"files\" to run the patterns on."); }

            bool anyMatch = false, allValid = true;
            var results = new JsonArray();
            for (int pi = 0; pi < specs.Count; pi++)
            {
                var sp = specs[pi];
                var r = new JsonObject { ["index"] = pi + 1, ["pattern"] = sp.Pattern, ["name"] = sp.Name };
                Regex rx = null;
                string err = sp.Error;
                if (err == null)
                {
                    if (!TryOptions(sp.Options, out var ro, out var optErr)) err = optErr;
                    else rx = RegexKit.TryCreate(sp.Pattern ?? "", ro, out err);
                }
                r["valid"] = rx != null;
                r["error"] = rx != null ? null : err;
                if (rx == null) { allValid = false; results.Add(r); continue; }
                r["groups"] = new JsonArray(rx.GetGroupNames().Select(n => (JsonNode)JsonValue.Create(n)).ToArray());
                if (op != "validate")
                {
                    var outs = new JsonArray();
                    foreach (var (source, text, readErr) in inputs)
                    {
                        var io = new JsonObject { ["source"] = source };
                        if (readErr != null) { io["error"] = readErr; outs.Add(io); continue; }
                        try
                        {
                            if (op == "match") anyMatch |= Matches(rx, text, maxMatches, io);
                            else if (op == "replace")
                            {
                                int count = 0;
                                var replaced = rx.Replace(text, m => { count++; return m.Result(sp.Replacement ?? ""); });
                                io["replacements"] = count; io["text"] = replaced;
                                anyMatch |= count > 0;
                            }
                            else
                            {
                                var parts = rx.Split(text);
                                io["parts"] = new JsonArray(parts.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
                                anyMatch |= parts.Length > 1;
                            }
                        }
                        catch (RegexMatchTimeoutException) { io["error"] = "Stopped at the 200 ms limit: the pattern backtracks too much on this input."; }
                        catch (Exception ex) when (ex is ArgumentException || ex is RegexParseException) { io["error"] = "Invalid replacement: " + ex.Message; }
                        outs.Add(io);
                    }
                    r["inputs"] = outs;
                }
                results.Add(r);
            }
            exit = op == "validate" ? (allValid ? 0 : 1) : anyMatch ? 0 : 1;
            var doc = new JsonObject { ["ok"] = true, ["error"] = null, ["operation"] = op, ["matched"] = op == "validate" ? allValid : anyMatch, ["results"] = results };
            return doc.ToJsonString(Indented);
        }

        private static bool Matches(Regex rx, string text, int max, JsonObject io)
        {
            var starts = new List<int> { 0 };
            for (int k = 0; k < text.Length; k++) if (text[k] == '\n') starts.Add(k + 1);
            var list = new JsonArray();
            int count = 0;
            var names = rx.GetGroupNames();
            foreach (Match m in rx.Matches(text))
            {
                count++;
                if (list.Count >= max) continue;
                int line = starts.BinarySearch(m.Index);
                if (line < 0) line = ~line - 1;
                var groups = new JsonArray();
                foreach (var name in names)
                {
                    if (name == "0") continue;
                    var g = m.Groups[name];
                    groups.Add(new JsonObject { ["name"] = name, ["number"] = rx.GroupNumberFromName(name), ["success"] = g.Success, ["index"] = g.Success ? g.Index : -1, ["length"] = g.Success ? g.Length : 0, ["value"] = g.Success ? g.Value : null });
                }
                list.Add(new JsonObject { ["index"] = m.Index, ["length"] = m.Length, ["line"] = line + 1, ["column"] = m.Index - starts[line] + 1, ["value"] = m.Value, ["groups"] = groups });
            }
            io["count"] = count;
            io["truncated"] = count > list.Count;
            io["matches"] = list;
            return count > 0;
        }

        private static (string, string, string, string, string) FromLibrary(string name, JsonObject parameters, string options, string replacement, string label)
        {
            Settings.Load();
            var saved = Settings.Current.SavedPatterns.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (saved == null) return (null, null, null, label ?? name, "No saved pattern is named \"" + name + "\".");
            var values = new Dictionary<string, string>();
            if (parameters != null) foreach (var kv in parameters) values[kv.Key] = kv.Value?.ToString() ?? "";
            var pattern = RegexLibrary.Expand(saved, values, out var unfilled);
            if (unfilled.Count > 0) return (pattern, null, null, label ?? saved.Name, "The saved pattern needs a value for: " + string.Join(", ", unfilled) + ".");
            return (pattern, options ?? OptionLetters((RegexOptions)saved.Options), replacement ?? saved.Replacement, label ?? saved.Name, null);
        }

        public static string OptionLetters(RegexOptions o) =>
            ((o & RegexOptions.IgnoreCase) != 0 ? "i" : "") + ((o & RegexOptions.Multiline) != 0 ? "m" : "") + ((o & RegexOptions.Singleline) != 0 ? "s" : "") +
            ((o & RegexOptions.IgnorePatternWhitespace) != 0 ? "x" : "") + ((o & RegexOptions.ExplicitCapture) != 0 ? "n" : "");

        /// <summary>Option letters i, m, s, x, n, written together ("im") or with commas ("i,m").</summary>
        public static bool TryOptions(string letters, out RegexOptions options, out string error)
        {
            options = RegexOptions.None; error = null;
            foreach (var c in (letters ?? "").Replace(",", "").Replace(" ", "").ToLowerInvariant())
            {
                switch (c)
                {
                    case 'i': options |= RegexOptions.IgnoreCase; break;
                    case 'm': options |= RegexOptions.Multiline; break;
                    case 's': options |= RegexOptions.Singleline; break;
                    case 'x': options |= RegexOptions.IgnorePatternWhitespace; break;
                    case 'n': options |= RegexOptions.ExplicitCapture; break;
                    default: error = "Unknown option '" + c + "'. Use i, m, s, x or n."; return false;
                }
            }
            return true;
        }

        private static string Str(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        private static string Fail(string message) =>
            new JsonObject { ["ok"] = false, ["error"] = message, ["results"] = new JsonArray() }.ToJsonString(Indented);
    }
}
