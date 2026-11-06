using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>SQL, regular expressions, XML and YAML, package names, Windows paths and tables.</summary>
    public static class Developer2
    {
        // ------------------------------------------------------------------ SQL

        private static readonly string[] SqlBreaks = { "SELECT", "FROM", "WHERE", "GROUP BY", "ORDER BY", "HAVING", "LIMIT", "LEFT JOIN", "RIGHT JOIN", "INNER JOIN", "OUTER JOIN", "JOIN", "UNION", "VALUES", "SET", "INSERT INTO", "UPDATE", "DELETE FROM" };

        public static SmartFact Sql(string t)
        {
            var s = t.Trim();
            if (!Regex.IsMatch(s, @"^(SELECT|WITH)\b[\s\S]+\bFROM\b|^(INSERT\s+INTO|UPDATE\s+\w[\w.\[\]""]*\s+SET|DELETE\s+FROM)\b", RegexOptions.IgnoreCase)) return null;
            var formatted = FormatSql(s);
            return new SmartFact("sql", "You copied SQL", Regex.Match(s, @"^\w+").Value.ToUpperInvariant() + " query")
                .Value("Formatted", formatted).Value("One line", Regex.Replace(s, @"\s+", " "))
                .Act("Copy formatted", SmartDo.Copy, formatted, primary: true);
        }

        public static string FormatSql(string s)
        {
            var one = Regex.Replace(s.Trim(), @"\s+", " ");
            foreach (var k in SqlBreaks.OrderByDescending(k => k.Length))
                one = Regex.Replace(one, @"(?<![\w.])" + k.Replace(" ", @"\s+") + @"(?!\w)", "\n" + k, RegexOptions.IgnoreCase);
            one = Regex.Replace(one, @"\b(AND|OR)\b", m => "\n  " + m.Value.ToUpperInvariant(), RegexOptions.IgnoreCase);
            one = Regex.Replace(one, @"\n(LEFT|RIGHT|INNER|OUTER)\n(JOIN)", "\n$1 $2");
            return Regex.Replace(one, @"[ \t]+\n", "\n").Trim();
        }

        /// <summary>Lines of values as an SQL IN (...) list, quoting anything that is not a number.</summary>
        public static string InList(string t)
        {
            var items = t.Split(new[] { '\r', '\n', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
            bool numbers = items.All(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            return "IN (" + string.Join(", ", items.Select(x => numbers ? x : "'" + x.Replace("'", "''") + "'")) + ")";
        }

        // ------------------------------------------------------------------ regular expressions

        public static SmartFact RegexText(string t)
        {
            var s = t.Trim();
            if (s.Length < 4 || s.Length > 400 || s.Contains('\n')) return null;
            var lit = Regex.Match(s, @"^/(.+)/([gimsuy]*)$");
            var body = lit.Success ? lit.Groups[1].Value : s;
            int signs = Regex.Matches(body, @"\\[dwsbDWSB]|\[[^\]]+\]|\(\?[:=!<]|[+*?]\)|\{\d+(,\d*)?\}|^\^|\$$").Count;
            if (signs < 2) return null;
            try { _ = new Regex(body); } catch { return null; }
            return new SmartFact("regex", "You copied a regular expression", body.Length > 50 ? body.Substring(0, 47) + "…" : body).Set("pattern", body)
                .Value("Pattern", body).Value("C# string", "@\"" + body.Replace("\"", "\"\"") + "\"").Value("JavaScript", "/" + body.Replace("/", "\\/") + "/")
                .Act("Explain and test it", SmartDo.Run, "regex:" + body, primary: true, tip: "Opens it in the regex builder");
        }

        // ------------------------------------------------------------------ XML

        public static SmartFact Xml(string t)
        {
            var s = t.Trim();
            if (!s.StartsWith("<") || !s.EndsWith(">") || s.Length < 7) return null;
            if (Regex.IsMatch(s, @"^<!DOCTYPE html|^<html", RegexOptions.IgnoreCase)) return null;
            XDocument doc;
            try { doc = XDocument.Parse(s); } catch { return null; }
            var pretty = doc.ToString();
            int count = doc.Descendants().Count();
            return new SmartFact("xml", "You copied XML", "<" + doc.Root.Name.LocalName + "> · " + count + " elements")
                .Value("Tidy", pretty).Value("One line", doc.ToString(SaveOptions.DisableFormatting))
                .Act("Copy tidy", SmartDo.Copy, pretty, primary: true);
        }

        // ------------------------------------------------------------------ JSON to YAML and back

        public static string JsonToYaml(string json)
        {
            using var d = JsonDocument.Parse(json);
            var sb = new StringBuilder(); Yaml(d.RootElement, sb, 0, false); return sb.ToString().TrimEnd();
        }

        private static void Yaml(JsonElement e, StringBuilder sb, int indent, bool inList)
        {
            var pad = new string(' ', indent);
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    bool first = true;
                    if (!e.EnumerateObject().Any()) { sb.Append("{}\n"); return; }
                    foreach (var p in e.EnumerateObject())
                    {
                        sb.Append(first && inList ? "" : pad).Append(Key(p.Name)).Append(':'); first = false;
                        if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array && Has(p.Value)) { sb.Append('\n'); Yaml(p.Value, sb, indent + 2, false); }
                        else { sb.Append(' '); Scalar(p.Value, sb); sb.Append('\n'); }
                    }
                    break;
                case JsonValueKind.Array:
                    if (!e.EnumerateArray().Any()) { sb.Append(pad).Append("[]\n"); return; }
                    foreach (var v in e.EnumerateArray())
                    {
                        sb.Append(pad).Append("- ");
                        if (v.ValueKind == JsonValueKind.Object && Has(v)) Yaml(v, sb, indent + 2, true);
                        else if (v.ValueKind == JsonValueKind.Array && Has(v)) { sb.Append('\n'); Yaml(v, sb, indent + 2, false); }
                        else { Scalar(v, sb); sb.Append('\n'); }
                    }
                    break;
                default: sb.Append(pad); Scalar(e, sb); sb.Append('\n'); break;
            }
        }

        private static bool Has(JsonElement e) => e.ValueKind == JsonValueKind.Object ? e.EnumerateObject().Any() : e.EnumerateArray().Any();
        private static string Key(string k) => Regex.IsMatch(k, @"^[A-Za-z_][\w\-]*$") ? k : "\"" + k.Replace("\"", "\\\"") + "\"";
        private static void Scalar(JsonElement e, StringBuilder sb)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    var s = e.GetString();
                    sb.Append(Regex.IsMatch(s, @"^[A-Za-z][\w .\-/]*$") && !Regex.IsMatch(s, @"^(true|false|null|yes|no|on|off)$", RegexOptions.IgnoreCase) ? s : JsonSerializer.Serialize(s));
                    break;
                case JsonValueKind.Object: sb.Append("{}"); break;
                case JsonValueKind.Array: sb.Append("[]"); break;
                default: sb.Append(e.GetRawText()); break;
            }
        }

        // ------------------------------------------------------------------ package names

        private static readonly (string Eco, Regex Rx)[] Packages =
        {
            ("npm", new Regex(@"^(?:npm (?:i|install|add)|yarn add|pnpm add)\s+(?<name>@?[\w.\-/]+?)(?:@(?<ver>[\w.\-^~]+))?$", RegexOptions.Compiled)),
            ("nuget", new Regex(@"^(?:dotnet add package|Install-Package)\s+(?<name>[\w.\-]+)(?:\s+(?:--version|-Version)\s+(?<ver>[\w.\-]+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
            ("pypi", new Regex(@"^(?:pip3? install|uv add|poetry add)\s+(?<name>[\w.\-]+)(?:==(?<ver>[\w.\-]+))?$", RegexOptions.Compiled)),
            ("npm", new Regex(@"^(?<name>@[\w.\-]+/[\w.\-]+|[a-z][\w.\-]*)@(?<ver>\^?~?\d[\w.\-]*)$", RegexOptions.Compiled)),
        };

        public static SmartFact Package(string t)
        {
            var s = t.Trim();
            foreach (var (eco, rx) in Packages)
            {
                var m = rx.Match(s);
                if (!m.Success) continue;
                var name = m.Groups["name"].Value; var ver = m.Groups["ver"].Value;
                string page = eco == "npm" ? "https://www.npmjs.com/package/" + name : eco == "nuget" ? "https://www.nuget.org/packages/" + name : "https://pypi.org/project/" + name + "/";
                return new SmartFact("package", "You copied a package", name + (ver.Length > 0 ? " " + ver : "") + " · " + eco)
                    .Set("lookup", eco + ":" + name).Set("ecosystem", eco).Set("name", name)
                    .Value("Name", name)
                    .Act("Open on " + (eco == "npm" ? "npm" : eco == "nuget" ? "NuGet" : "PyPI"), SmartDo.Open, page, primary: true);
            }
            return null;
        }

        // ------------------------------------------------------------------ Windows paths that are not on this PC

        public static SmartFact WindowsPath(string t)
        {
            var s = t.Trim().Trim('"');
            if (s.Contains('\n') || s.Length < 4 || s.Length > 260) return null;
            bool drive = Regex.IsMatch(s, @"^[A-Za-z]:\\[^<>:""|?*]*$"), unc = Regex.IsMatch(s, @"^\\\\[\w.\-$]+\\[^<>:""|?*]*$");
            if (!drive && !unc) return null;
            var f = new SmartFact("path", "You copied a Windows path", s);
            if (drive) f.Value("WSL", "/mnt/" + char.ToLowerInvariant(s[0]) + s.Substring(2).Replace('\\', '/'));
            f.Value("Forward slashes", s.Replace('\\', '/'))
             .Value("Escaped", s.Replace("\\", "\\\\"))
             .Value("File link", unc ? new Uri(s).AbsoluteUri : "file:///" + string.Join("/", s.Replace('\\', '/').Split('/').Select((seg, i) => i == 0 ? seg : Uri.EscapeDataString(seg))))
             .Value("Quoted", "\"" + s + "\"");
            return f;
        }

        // ------------------------------------------------------------------ tables (cells from Excel, Sheets, CSV)

        public static List<string[]> ReadTable(string t)
        {
            var lines = t.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            if (lines.Length < 2) return null;
            char sep = lines.All(l => l.Contains('\t')) ? '\t' : lines.All(l => l.Contains(',')) && !t.Contains('\t') ? ',' : '\0';
            if (sep == '\0') return null;
            var rows = lines.Select(l => (sep == ',' ? SplitCsv(l) : l.Split('\t')).Select(c => c.Trim()).ToArray()).ToList();
            int cols = rows[0].Length;
            if (cols < 2 || rows.Any(r => r.Length != cols)) return null;
            return rows;
        }

        private static string[] SplitCsv(string line)
        {
            var cells = new List<string>(); var sb = new StringBuilder(); bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q) { if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else if (c == '"') q = false; else sb.Append(c); }
                else if (c == '"') q = true; else if (c == ',') { cells.Add(sb.ToString()); sb.Clear(); } else sb.Append(c);
            }
            cells.Add(sb.ToString()); return cells.ToArray();
        }

        public static SmartFact Table(string t)
        {
            var rows = ReadTable(t);
            if (rows == null) return null;
            int cols = rows[0].Length;
            var f = new SmartFact("table", "You copied a table", rows.Count + " rows × " + cols + " columns");
            bool header = rows[0].Any(c => c.Length > 0 && !double.TryParse(c, NumberStyles.Any, CultureInfo.InvariantCulture, out _));
            var data = header ? rows.Skip(1).ToList() : rows;
            for (int c = 0; c < cols; c++)
            {
                var nums = data.Select(r => double.TryParse(r[c].Replace("$", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? (double?)v : null).ToList();
                if (nums.Count > 0 && nums.All(n => n.HasValue))
                {
                    var name = header ? rows[0][c] : "Column " + (c + 1);
                    f.Value(name + " sum", Units.Format(nums.Sum(n => n.Value)));
                    f.Value(name + " average", Units.Format(nums.Average(n => n.Value)));
                }
            }
            f.Value("Markdown", ToMarkdown(rows)).Value("CSV", string.Join(Environment.NewLine, rows.Select(r => string.Join(",", r.Select(Csv)))))
             .Value("JSON", ToJson(rows, header)).Value("HTML", "<table>" + string.Concat(rows.Select((r, i) => "<tr>" + string.Concat(r.Select(c => (i == 0 && header ? "<th>" : "<td>") + System.Net.WebUtility.HtmlEncode(c) + (i == 0 && header ? "</th>" : "</td>"))) + "</tr>")) + "</table>")
             .Act("Open as a table", SmartDo.Run, "table", primary: true);
            return f;
        }

        private static string Csv(string c) => c.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + c.Replace("\"", "\"\"") + "\"" : c;

        public static string ToMarkdown(List<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("| ").Append(string.Join(" | ", rows[0])).Append(" |\n");
            sb.Append('|').Append(string.Concat(rows[0].Select(_ => " --- |"))).Append('\n');
            foreach (var r in rows.Skip(1)) sb.Append("| ").Append(string.Join(" | ", r.Select(c => c.Replace("|", "\\|")))).Append(" |\n");
            return sb.ToString().TrimEnd();
        }

        public static string ToJson(List<string[]> rows, bool header)
        {
            if (!header) return JsonSerializer.Serialize(rows);
            var keys = new List<string>();
            foreach (var h in rows[0])
            {
                var k = h.Length > 0 ? h : "column"; var u = k;
                for (int n = 2; keys.Contains(u); n++) u = k + " " + n;
                keys.Add(u);
            }
            var list = rows.Skip(1).Select(r => keys.Select((k, i) => (k, r[i])).ToDictionary(x => x.k, x => x.Item2)).ToList();
            return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
    }
}
