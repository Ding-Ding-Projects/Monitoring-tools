using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Code snippets for a pattern in several languages, for matching, replacing and splitting. Every
    /// snippet comes from a template; the built-in templates can be edited and the edited text is kept
    /// per language and kind in the app settings. A template is plain text with {{name}} placeholders:
    /// {{literal}} the pattern as a string literal of the language, {{flags}} the language's option
    /// expression, {{replacement}} the translated replacement as a string literal, {{timeout}} the limit
    /// in milliseconds and {{notes}} the comment lines about anything that could not be translated.
    /// </summary>
    public static class RegexCode
    {
        public static readonly string[] Languages = { "C#", "PowerShell", "Python", "JavaScript", "Java" };
        public static readonly string[] Kinds = { "Match", "Replace", "Split" };
        public static readonly string[] Placeholders = { "literal", "flags", "replacement", "timeout", "notes" };

        public static string Key(string language, string kind) => language + "|" + kind;

        /// <summary>The built-in template for a language and kind.</summary>
        public static string DefaultTemplate(string language, string kind)
        {
            switch (language)
            {
                case "PowerShell":
                    return "{{notes}}$rx = [regex]::new({{literal}}, {{flags}}, [timespan]::FromMilliseconds({{timeout}}))\n" + kind switch
                    {
                        "Replace" => "$result = $rx.Replace($text, {{replacement}})\n$result",
                        "Split" => "$rx.Split($text)",
                        _ => "$rx.Matches($text) | ForEach-Object { $_.Value }",
                    };
                case "Python":
                    return "import re\n{{notes}}rx = re.compile({{literal}}{{flags}})\n" + kind switch
                    {
                        "Replace" => "result = rx.sub({{replacement}}, text)\nprint(result)",
                        "Split" => "for part in rx.split(text):\n    print(part)",
                        _ => "for m in rx.finditer(text):\n    print(m.group(0))",
                    };
                case "JavaScript":
                    return "{{notes}}const rx = new RegExp({{literal}}, {{flags}});\n" + kind switch
                    {
                        "Replace" => "const result = text.replace(rx, {{replacement}});\nconsole.log(result);",
                        "Split" => "for (const part of text.split(rx)) console.log(part);",
                        _ => "for (const m of text.matchAll(rx)) console.log(m[0]);",
                    };
                case "Java":
                    return "{{notes}}Pattern rx = Pattern.compile({{literal}}{{flags}});\n" + kind switch
                    {
                        "Replace" => "String result = rx.matcher(text).replaceAll({{replacement}});\nSystem.out.println(result);",
                        "Split" => "for (String part : rx.split(text))\n    System.out.println(part);",
                        _ => "Matcher m = rx.matcher(text);\nwhile (m.find())\n    System.out.println(m.group());",
                    };
                default:
                    return "{{notes}}var rx = new Regex({{literal}},\n    {{flags}},\n    TimeSpan.FromMilliseconds({{timeout}}));\n" + kind switch
                    {
                        "Replace" => "var result = rx.Replace(text, {{replacement}});\nConsole.WriteLine(result);",
                        "Split" => "foreach (var part in rx.Split(text))\n    Console.WriteLine(part);",
                        _ => "foreach (Match m in rx.Matches(text))\n    Console.WriteLine(m.Value);",
                    };
            }
        }

        /// <summary>The template in force: the user's edited text when one is stored, otherwise the built-in one.</summary>
        public static string TemplateFor(string language, string kind)
        {
            var custom = Settings.Current?.RegexCodeTemplates;
            return custom != null && custom.TryGetValue(Key(language, kind), out var t) && !string.IsNullOrWhiteSpace(t) ? t : DefaultTemplate(language, kind);
        }

        public static bool IsCustom(string language, string kind) =>
            Settings.Current?.RegexCodeTemplates != null && Settings.Current.RegexCodeTemplates.TryGetValue(Key(language, kind), out var t) && !string.IsNullOrWhiteSpace(t);

        public static void SaveTemplate(string language, string kind, string text)
        {
            var s = Settings.Current;
            if (s == null) return;
            s.RegexCodeTemplates ??= new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(text) || text == DefaultTemplate(language, kind)) s.RegexCodeTemplates.Remove(Key(language, kind));
            else s.RegexCodeTemplates[Key(language, kind)] = text.Length > 4000 ? text[..4000] : text;
        }

        /// <summary>Placeholders in a template that this builder does not know, so an editing slip is visible instead of silently printed.</summary>
        public static List<string> UnknownPlaceholders(string template) =>
            Regex.Matches(template ?? "", @"\{\{\s*([A-Za-z_]\w*)\s*\}\}").Select(m => m.Groups[1].Value).Where(n => !Placeholders.Contains(n)).Distinct().ToList();

        public static string Render(string language, string kind, string pattern, RegexOptions options, string replacement, string template = null)
        {
            pattern ??= ""; replacement ??= "";
            template ??= TemplateFor(language, kind);
            var notes = new List<string>();
            string literal, flags, repl;
            switch (language)
            {
                case "PowerShell":
                {
                    var f = new List<string>();
                    if ((options & RegexOptions.IgnoreCase) != 0) f.Add("IgnoreCase");
                    foreach (var (flag, _, _) in RegexKit.OptionDefs) if ((options & flag) != 0) f.Add(flag.ToString());
                    literal = "'" + pattern.Replace("'", "''") + "'";
                    flags = f.Count == 0 ? "'None'" : "'" + string.Join(", ", f) + "'";
                    repl = "'" + replacement.Replace("'", "''") + "'";
                    break;
                }
                case "Python":
                {
                    var c = RegexFlavours.Convert("Python", pattern, options);
                    var f = new List<string>();
                    if (c.Flags.Contains('i')) f.Add("re.IGNORECASE");
                    if (c.Flags.Contains('m')) f.Add("re.MULTILINE");
                    if (c.Flags.Contains('s')) f.Add("re.DOTALL");
                    if (c.Flags.Contains('x')) f.Add("re.VERBOSE");
                    if (c.Pattern != pattern) notes.Add("The pattern was converted from .NET syntax (group names, anchors).");
                    notes.AddRange(c.Limits);
                    var rl = new List<string>();
                    repl = PyString(RegexFlavours.ConvertReplacement("Python", replacement, rl));
                    notes.AddRange(rl);
                    literal = PyRaw(c.Pattern);
                    flags = f.Count > 0 ? ", " + string.Join(" | ", f) : "";
                    break;
                }
                case "JavaScript":
                {
                    var c = RegexFlavours.Convert("JavaScript", pattern, options);
                    if (c.Pattern != pattern) notes.Add("The pattern was converted from .NET syntax (anchors).");
                    notes.AddRange(c.Limits);
                    notes.Add("JavaScript has no match time limit; add your own guard for untrusted text.");
                    var rl = new List<string>();
                    repl = Json(RegexFlavours.ConvertReplacement("JavaScript", replacement, rl));
                    notes.AddRange(rl);
                    literal = Json(c.Pattern);
                    flags = "\"" + c.Flags + (kind == "Split" ? "" : "g") + "\"";
                    break;
                }
                case "Java":
                {
                    var c = RegexFlavours.Convert("Java", pattern, options);
                    notes.AddRange(c.Limits);
                    notes.Add("Java has no match time limit; add your own guard for untrusted text.");
                    var rl = new List<string>();
                    repl = Json(RegexFlavours.ConvertReplacement("Java", replacement, rl));
                    notes.AddRange(rl);
                    literal = Json(c.Pattern);
                    var f = new List<string>();
                    if (c.Flags.Contains('i')) f.Add("Pattern.CASE_INSENSITIVE");
                    if (c.Flags.Contains('m')) f.Add("Pattern.MULTILINE");
                    if (c.Flags.Contains('s')) f.Add("Pattern.DOTALL");
                    if (c.Flags.Contains('x')) f.Add("Pattern.COMMENTS");
                    flags = f.Count > 0 ? ", " + string.Join(" | ", f) : "";
                    break;
                }
                default:
                {
                    var f = new List<string>();
                    if ((options & RegexOptions.IgnoreCase) != 0) f.Add("RegexOptions.IgnoreCase");
                    foreach (var (flag, _, _) in RegexKit.OptionDefs) if ((options & flag) != 0) f.Add("RegexOptions." + flag);
                    literal = "@\"" + pattern.Replace("\"", "\"\"") + "\"";
                    flags = f.Count == 0 ? "RegexOptions.None" : string.Join(" | ", f);
                    repl = "@\"" + replacement.Replace("\"", "\"\"") + "\"";
                    break;
                }
            }
            var comment = language == "Python" ? "# " : language == "PowerShell" ? "# " : "// ";
            var noteText = notes.Count == 0 ? "" : string.Join("\n", notes.Distinct().Select(n => comment + n)) + "\n";
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["literal"] = literal, ["flags"] = flags, ["replacement"] = repl, ["timeout"] = ((int)RegexKit.Timeout.TotalMilliseconds).ToString(), ["notes"] = noteText,
            };
            return Regex.Replace(template, @"\{\{\s*([A-Za-z_]\w*)\s*\}\}", m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        }

        private static string Json(string s) => System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        private static string PyString(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        /// <summary>A Python raw string literal that survives quotes and a trailing backslash.</summary>
        private static string PyRaw(string p)
        {
            if (!p.Contains('"') && !p.EndsWith("\\")) return "r\"" + p + "\"";
            if (!p.Contains('\'') && !p.EndsWith("\\")) return "r'" + p + "'";
            if (!p.Contains("\"\"\"") && !p.EndsWith("\\") && !p.EndsWith("\"")) return "r\"\"\"" + p + "\"\"\"";
            return "(\"" + string.Concat(p.Select(ch => ch == '\\' ? "\\\\" : ch == '"' ? "\\\"" : ch == '\n' ? "\\n" : ch.ToString())) + "\")";
        }
    }
}
