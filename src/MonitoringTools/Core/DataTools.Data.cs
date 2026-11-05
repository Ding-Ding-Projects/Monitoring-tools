using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace MonitoringTools.Core
{
    internal enum DataKind { Csv, Json, Yaml }

    /// <summary>A problem in a data file with the place it was found, 1-based.</summary>
    internal sealed class DataError : Exception
    {
        public int Line, Column;
        public DataError(string message, int line, int column) : base(message) { Line = line; Column = column; }
    }

    internal sealed class DataProbe
    {
        public string Path;
        public DataKind Format;
        public char Delimiter = ',';
        public bool Valid;
        public int ErrorLine, ErrorColumn;
        public string ErrorMessage;
        public int Rows, Columns;
        public bool HeaderFound;
        public string[] Header = Array.Empty<string>();
        public List<string[]> Preview = new List<string[]>();     // the first 500 rows of the table view
        public List<string> Warnings = new List<string>();
        public string Text = "";                                  // the start of the file as written
        public string Summary = "";
        public long Size;
        public int Levels, Keys;
        public JsonNode Tree;                                     // JSON and YAML only
        public string[] Context = Array.Empty<string>();          // three lines around an error
        public int ContextFirstLine;
        public bool TextTruncated;
    }

    internal sealed class ConvertStats { public int Items; public string Numbers = ""; public long Bytes; }

    /// <summary>
    /// Reads, checks and converts CSV, JSON and YAML. JSON uses System.Text.Json, YAML uses YamlDotNet, and CSV uses an own RFC 4180
    /// reader (quotes, embedded newlines, byte-order mark, delimiter detection). The table view shows the first 500 rows; conversion
    /// uses the whole file.
    /// </summary>
    internal static class DataEngine
    {
        internal const int PreviewRows = 500;
        private const long TreeLimit = 120L * 1024 * 1024;

        internal static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        internal static bool IsDataFile(string path) => Path.GetExtension(path ?? "").ToLowerInvariant() is ".csv" or ".tsv" or ".json" or ".yaml" or ".yml";

        internal static DataKind FormatOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => DataKind.Json, ".yaml" or ".yml" => DataKind.Yaml, _ => DataKind.Csv,
        };

        internal static string Name(DataKind f) => f switch { DataKind.Json => "JSON", DataKind.Yaml => "YAML", _ => "CSV" };
        internal static string Extension(DataKind f) => f switch { DataKind.Json => ".json", DataKind.Yaml => ".yaml", _ => ".csv" };

        // ------------------------------------------------------------------ CSV

        /// <summary>Reads rows from text. Throws <see cref="DataError"/> for a quote that never closes or text after a closing quote.</summary>
        internal static IEnumerable<string[]> ReadCsv(TextReader reader, char delimiter)
        {
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false, afterQuote = false, started = false;
            int line = 1, col = 0, quoteLine = 0, quoteCol = 0;
            int c, prev = -1;
            bool pendingCr = false;
            while ((c = reader.Read()) >= 0)
            {
                col++;
                if (pendingCr) { pendingCr = false; if (c == '\n') { continue; } }
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (reader.Peek() == '"') { reader.Read(); col++; field.Append('"'); }
                        else { quoted = false; afterQuote = true; }
                    }
                    else { field.Append((char)c); if (c == '\n') { line++; col = 0; } }
                    prev = c; continue;
                }
                if (c == '\r' || c == '\n')
                {
                    if (c == '\r') pendingCr = true;
                    row.Add(field.ToString()); field.Clear();
                    if (!(row.Count == 1 && row[0].Length == 0 && !started)) yield return row.ToArray();
                    row.Clear(); started = false; afterQuote = false; line++; col = 0; prev = c; continue;
                }
                if (c == delimiter) { row.Add(field.ToString()); field.Clear(); afterQuote = false; started = true; prev = c; continue; }
                if (afterQuote) throw new DataError("There is text after a closing quote.", line, col);
                if (c == '"')
                {
                    if (field.Length == 0 && (prev == -1 || prev == delimiter || prev == '\n' || prev == '\r')) { quoted = true; quoteLine = line; quoteCol = col; started = true; }
                    else field.Append('"');
                    prev = c; continue;
                }
                field.Append((char)c); started = true; prev = c;
            }
            if (quoted) throw new DataError("A quote opened here is never closed.", quoteLine, quoteCol);
            if (field.Length > 0 || row.Count > 0 || started) { row.Add(field.ToString()); yield return row.ToArray(); }
        }

        internal static char DetectDelimiter(string sample, string path)
        {
            if (Path.GetExtension(path ?? "").Equals(".tsv", StringComparison.OrdinalIgnoreCase)) return '\t';
            char best = ',';
            long bestScore = 0;
            foreach (var d in new[] { ',', ';', '\t', '|' })
            {
                try
                {
                    var rows = ReadCsv(new StringReader(sample), d).Take(30).ToList();
                    if (rows.Count == 0) continue;
                    var modal = rows.GroupBy(r => r.Length).OrderByDescending(g => g.Count()).First();
                    long score = modal.Key > 1 ? (long)modal.Count() * 1000 + modal.Key : 0;
                    if (score > bestScore) { best = d; bestScore = score; }
                }
                catch (DataError) { }
            }
            return best;
        }

        private static bool LooksLikeHeader(string[] first, string[] second)
        {
            if (first.Length == 0 || first.Any(string.IsNullOrWhiteSpace)) return false;
            if (first.Any(f => decimal.TryParse(f, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))) return false;
            if (first.Distinct(StringComparer.OrdinalIgnoreCase).Count() != first.Length) return false;
            return true;
        }

        private static string[] HeaderNames(string[] first, bool found, int width)
        {
            var names = new string[width];
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < width; i++)
            {
                var n = found && i < first.Length && !string.IsNullOrWhiteSpace(first[i]) ? first[i].Trim() : "column " + (i + 1);
                if (seen.TryGetValue(n, out var k)) { seen[n] = ++k; n = n + "_" + k; } else seen[n] = 1;
                names[i] = n;
            }
            return names;
        }

        // ------------------------------------------------------------------ probing

        internal static DataProbe Probe(string path, char? delimiter = null)
        {
            var p = new DataProbe { Path = path, Format = FormatOf(path) };
            try { p.Size = new FileInfo(path).Length; } catch { }
            string text;
            try
            {
                using var sr = new StreamReader(path, new UTF8Encoding(false, false), true);
                var buf = new char[200_000];
                int n = sr.ReadBlock(buf, 0, buf.Length);
                p.Text = new string(buf, 0, n);
                p.TextTruncated = !sr.EndOfStream;
                text = p.Text;
                if (p.Format != DataKind.Csv || p.Size < 60_000_000) { /* the whole text is read below when needed */ }
            }
            catch (Exception ex) { p.ErrorMessage = "The file could not be read: " + ex.Message; return p; }

            switch (p.Format)
            {
                case DataKind.Json: ProbeJson(p); break;
                case DataKind.Yaml: ProbeYaml(p); break;
                default: ProbeCsv(p, delimiter ?? DetectDelimiter(text, path)); break;
            }
            return p;
        }

        private static string ReadAll(string path)
        {
            using var sr = new StreamReader(path, new UTF8Encoding(false, false), true);
            return sr.ReadToEnd();
        }

        private static void SetContext(DataProbe p, string text, int line)
        {
            var lines = text.Split('\n');
            int first = Math.Max(1, line - 2);
            int last = Math.Min(lines.Length, line + 1);
            p.ContextFirstLine = first;
            p.Context = Enumerable.Range(first, Math.Max(0, last - first + 1)).Select(i => lines[i - 1].TrimEnd('\r')).ToArray();
        }

        private static void ProbeCsv(DataProbe p, char delimiter)
        {
            p.Delimiter = delimiter;
            try
            {
                using var sr = new StreamReader(p.Path, new UTF8Encoding(false, false), true);
                string[] first = null, second = null;
                var rows = new List<string[]>();
                int count = 0, maxWidth = 0, raggedFirst = 0, ragged = 0, modalWidth = 0;
                var widths = new Dictionary<int, int>();
                int line = 0;
                foreach (var r in ReadCsv(sr, delimiter))
                {
                    line++;
                    count++;
                    if (count == 1) first = r; else if (count == 2) second = r;
                    maxWidth = Math.Max(maxWidth, r.Length);
                    widths[r.Length] = widths.TryGetValue(r.Length, out var w) ? w + 1 : 1;
                    if (rows.Count < PreviewRows + 1) rows.Add(r);
                }
                if (count == 0) { p.Valid = true; p.Summary = "The file is empty."; return; }
                modalWidth = widths.OrderByDescending(kv => kv.Value).First().Key;
                p.HeaderFound = LooksLikeHeader(first, second);
                p.Header = HeaderNames(first, p.HeaderFound, maxWidth);
                p.Columns = maxWidth;
                p.Rows = p.HeaderFound ? count - 1 : count;
                var data = p.HeaderFound ? rows.Skip(1) : rows;
                p.Preview = data.Take(PreviewRows).Select(r => Pad(r, maxWidth)).ToList();
                ragged = widths.Where(kv => kv.Key != modalWidth).Sum(kv => kv.Value);
                if (ragged > 0) p.Warnings.Add(ragged + (ragged == 1 ? " row has" : " rows have") + " a different number of values than most rows (" + modalWidth + "). Missing values are left empty when converting.");
                p.Valid = true;
                string sep = delimiter switch { ',' => "Comma separated", ';' => "Semicolon separated", '\t' => "Tab separated", _ => "Bar separated" };
                p.Summary = sep + ", " + (p.HeaderFound ? "header row found" : "no header row found") + (ragged == 0 ? ", every row has " + maxWidth + " values." : ".");
                _ = raggedFirst;
            }
            catch (DataError e)
            {
                p.ErrorMessage = e.Message; p.ErrorLine = e.Line; p.ErrorColumn = e.Column;
                try { SetContext(p, ReadAll(p.Path), e.Line); } catch { }
            }
        }

        private static string[] Pad(string[] r, int width)
        {
            if (r.Length == width) return r;
            var x = new string[width];
            for (int i = 0; i < width; i++) x[i] = i < r.Length ? r[i] : "";
            return x;
        }

        private static void ProbeJson(DataProbe p)
        {
            string text = null;
            try
            {
                if (p.Size > TreeLimit) { p.ErrorMessage = "This file is larger than " + (TreeLimit / 1024 / 1024) + " MB, which is more than the viewer reads."; return; }
                text = ReadAll(p.Path);
                var node = ParseJson(text);
                p.Tree = node;
                p.Valid = true;
                Describe(p, node);
                p.Summary = SummaryOf(p, node, "JSON");
            }
            catch (DataError e)
            {
                p.ErrorMessage = e.Message; p.ErrorLine = e.Line; p.ErrorColumn = e.Column;
                if (text != null) SetContext(p, text, e.Line);
            }
            catch (Exception ex) { p.ErrorMessage = ex.Message; }
        }

        internal static JsonNode ParseJson(string text)
        {
            try
            {
                return JsonNode.Parse(text, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, MaxDepth = 256 });
            }
            catch (JsonException ex)
            {
                int line = (int)(ex.LineNumber ?? 0) + 1;
                int col = 1;
                var lines = text.Split('\n');
                if (ex.BytePositionInLine is long bytes && line - 1 < lines.Length)
                {
                    var l = lines[line - 1];
                    int used = 0, i = 0;
                    while (i < l.Length && used < bytes) { used += Encoding.UTF8.GetByteCount(l.Substring(i, char.IsHighSurrogate(l[i]) && i + 1 < l.Length ? 2 : 1)); i += char.IsHighSurrogate(l[i]) ? 2 : 1; }
                    col = i + 1;
                }
                var msg = ex.Message;
                foreach (var marker in new[] { " Path:", " LineNumber:" }) { int cut = msg.IndexOf(marker, StringComparison.Ordinal); if (cut > 0) msg = msg[..cut]; }
                throw new DataError(msg.Trim(), line, col);
            }
        }

        private static void ProbeYaml(DataProbe p)
        {
            string text = null;
            try
            {
                if (p.Size > TreeLimit) { p.ErrorMessage = "This file is larger than " + (TreeLimit / 1024 / 1024) + " MB, which is more than the viewer reads."; return; }
                text = ReadAll(p.Path);
                var node = ParseYaml(text, out var docs);
                p.Tree = node;
                p.Valid = true;
                if (docs > 1) p.Warnings.Add("The file holds " + docs + " YAML documents. Only the first is shown and converted.");
                Describe(p, node);
                bool tabs = text.Split('\n').Any(l => l.StartsWith("\t"));
                p.Summary = SummaryOf(p, node, "YAML") + (tabs ? "" : " No tabs used for indentation.");
            }
            catch (DataError e)
            {
                p.ErrorMessage = e.Message; p.ErrorLine = e.Line; p.ErrorColumn = e.Column;
                if (text != null && e.Line > 0) SetContext(p, text, e.Line);
            }
            catch (Exception ex) { p.ErrorMessage = ex.Message; }
        }

        internal static JsonNode ParseYaml(string text, out int documents)
        {
            var stream = new YamlStream();
            try { stream.Load(new StringReader(text)); }
            catch (YamlDotNet.Core.YamlException ex)
            {
                var msg = ex.Message;
                int at = msg.IndexOf("): ", StringComparison.Ordinal); if (at > 0) msg = msg[(at + 3)..];
                throw new DataError(msg.Trim(), (int)ex.Start.Line, (int)ex.Start.Column);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("same key"))
            {
                throw new DataError("A key appears twice in the same mapping.", 0, 0);
            }
            documents = stream.Documents.Count;
            if (documents == 0) return null;
            return FromYaml(stream.Documents[0].RootNode);
        }

        private static JsonNode FromYaml(YamlNode n)
        {
            switch (n)
            {
                case YamlMappingNode m:
                    var o = new JsonObject();
                    foreach (var kv in m.Children)
                    {
                        var key = kv.Key is YamlScalarNode ks ? ks.Value ?? "" : "(complex key)";
                        o[key] = FromYaml(kv.Value);
                    }
                    return o;
                case YamlSequenceNode s:
                    var a = new JsonArray();
                    foreach (var item in s.Children) a.Add(FromYaml(item));
                    return a;
                case YamlScalarNode sc:
                    return ScalarOf(sc);
                default:
                    return JsonValue.Create(n.ToString());
            }
        }

        private static readonly Regex YamlInt = new Regex(@"^[-+]?(0|[1-9][0-9]*)$", RegexOptions.Compiled);
        private static readonly Regex YamlFloat = new Regex(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$", RegexOptions.Compiled);

        private static JsonNode ScalarOf(YamlScalarNode sc)
        {
            var v = sc.Value ?? "";
            bool plain = sc.Style == YamlDotNet.Core.ScalarStyle.Plain || sc.Style == YamlDotNet.Core.ScalarStyle.Any;
            if (!plain) return JsonValue.Create(v);
            switch (v)
            {
                case "": case "~": case "null": case "Null": case "NULL": return null;
                case "true": case "True": case "TRUE": return JsonValue.Create(true);
                case "false": case "False": case "FALSE": return JsonValue.Create(false);
            }
            if (YamlInt.IsMatch(v) && long.TryParse(v, out var l)) return JsonValue.Create(l);
            if (YamlFloat.IsMatch(v) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && !double.IsInfinity(d)) return JsonValue.Create(d);
            return JsonValue.Create(v);
        }

        private static void Describe(DataProbe p, JsonNode root)
        {
            int keys = 0, levels = 0;
            void Walk(JsonNode n, int depth)
            {
                if (n is JsonObject || n is JsonArray) levels = Math.Max(levels, depth);
                if (n is JsonObject o) { keys += o.Count; foreach (var kv in o) Walk(kv.Value, depth + 1); }
                else if (n is JsonArray a) foreach (var x in a) Walk(x, depth + 1);
            }
            Walk(root, root is JsonObject || root is JsonArray ? 1 : 0);
            p.Keys = keys; p.Levels = levels;
            // table view: a list of records, a list of lists, or the top-level keys of an object
            if (root is JsonArray arr && arr.Count > 0 && arr.All(x => x is JsonObject))
            {
                var cols = new List<string>();
                foreach (var o in arr.OfType<JsonObject>().Take(2000)) foreach (var k in o.Select(kv => kv.Key)) if (!cols.Contains(k)) cols.Add(k);
                p.Header = cols.ToArray(); p.Columns = cols.Count; p.Rows = arr.Count; p.HeaderFound = true;
                p.Preview = arr.OfType<JsonObject>().Take(PreviewRows).Select(o => cols.Select(c => o.TryGetPropertyValue(c, out var v) ? Cell(v) : "").ToArray()).ToList();
            }
            else if (root is JsonArray arr2 && arr2.Count > 0 && arr2.All(x => x is JsonArray))
            {
                int w = arr2.OfType<JsonArray>().Max(r => r.Count);
                p.Header = Enumerable.Range(1, w).Select(i => "column " + i).ToArray(); p.Columns = w; p.Rows = arr2.Count;
                p.Preview = arr2.OfType<JsonArray>().Take(PreviewRows).Select(r => Pad(r.Select(Cell).ToArray(), w)).ToList();
            }
            else if (root is JsonObject obj)
            {
                p.Header = new[] { "key", "value" }; p.Columns = 2; p.Rows = obj.Count; p.HeaderFound = true;
                p.Preview = obj.Take(PreviewRows).Select(kv => new[] { kv.Key, Cell(kv.Value) }).ToList();
            }
            else if (root is JsonArray arr3)
            {
                p.Header = new[] { "value" }; p.Columns = 1; p.Rows = arr3.Count;
                p.Preview = arr3.Take(PreviewRows).Select(x => new[] { Cell(x) }).ToList();
            }
            else { p.Header = new[] { "value" }; p.Columns = 1; p.Rows = 1; p.Preview = new List<string[]> { new[] { Cell(root) } }; }
        }

        private static string Cell(JsonNode n)
        {
            if (n == null) return "";
            if (n is JsonObject o) return "{" + o.Count + (o.Count == 1 ? " key}" : " keys}");
            if (n is JsonArray a) return "[" + a.Count + (a.Count == 1 ? " item]" : " items]");
            var v = n.AsValue();
            if (v.TryGetValue<string>(out var s)) return s;
            return n.ToJsonString();
        }

        private static string SummaryOf(DataProbe p, JsonNode root, string kind)
        {
            string shape = root switch
            {
                JsonArray a => a.Count + (a.Count == 1 ? " item" : " items") + " at the top",
                JsonObject o => o.Count + (o.Count == 1 ? " key" : " keys") + " at the top",
                null => "empty value",
                _ => "a single value",
            };
            return p.Levels > 0
                ? $"{p.Levels} {(p.Levels == 1 ? "level" : "levels")}, {p.Keys} {(p.Keys == 1 ? "key" : "keys")} in all, {shape}."
                : shape + ".";
        }

        // ------------------------------------------------------------------ writing

        private static JsonSerializerOptions JsonOptions(bool pretty) => new JsonSerializerOptions { WriteIndented = pretty, Encoder = Encoder };

        internal static string ToJsonText(JsonNode n, bool pretty) => n == null ? "null" : n.ToJsonString(JsonOptions(pretty));

        internal static string ToYamlText(JsonNode n)
        {
            var ser = new SerializerBuilder().DisableAliases().WithQuotingNecessaryStrings().Build();
            return ser.Serialize(ToObject(n));
        }

        private static object ToObject(JsonNode n)
        {
            switch (n)
            {
                case null: return null;
                case JsonObject o: return o.ToDictionary(kv => kv.Key, kv => ToObject(kv.Value));
                case JsonArray a: return a.Select(ToObject).ToList();
                default:
                    var v = n.AsValue();
                    if (v.TryGetValue<string>(out var s)) return s;
                    if (v.TryGetValue<bool>(out var b)) return b;
                    if (v.TryGetValue<long>(out var l)) return l;
                    if (v.TryGetValue<double>(out var d)) return d;
                    return n.ToJsonString();
            }
        }

        private static string CsvQuote(string v, char d) => v.IndexOfAny(new[] { d, '"', '\r', '\n' }) >= 0 || (v.Length > 0 && (v[0] == ' ' || v[^1] == ' ')) ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

        internal static string ToCsvText(JsonNode root, char d, out int rows)
        {
            rows = 0;
            var sb = new StringBuilder();
            if (root is JsonArray arr && arr.Count > 0 && arr.All(x => x is JsonObject))
            {
                var cols = new List<string>();
                foreach (var o in arr.OfType<JsonObject>()) foreach (var k in o.Select(kv => kv.Key)) if (!cols.Contains(k)) cols.Add(k);
                sb.Append(string.Join(d, cols.Select(c => CsvQuote(c, d)))).Append("\r\n");
                foreach (var o in arr.OfType<JsonObject>())
                {
                    sb.Append(string.Join(d, cols.Select(c => CsvQuote(o.TryGetPropertyValue(c, out var v) ? CsvCell(v) : "", d)))).Append("\r\n");
                    rows++;
                }
                return sb.ToString();
            }
            if (root is JsonArray arr2 && arr2.Count > 0 && arr2.All(x => x is JsonArray))
            {
                foreach (var r in arr2.OfType<JsonArray>()) { sb.Append(string.Join(d, r.Select(x => CsvQuote(CsvCell(x), d)))).Append("\r\n"); rows++; }
                return sb.ToString();
            }
            throw new InvalidOperationException("A CSV table needs a list of records (objects) or a list of rows (lists). This file is not shaped like one.");
        }

        private static string CsvCell(JsonNode n) => n == null ? "" : n is JsonObject || n is JsonArray ? n.ToJsonString(JsonOptions(false)) : Cell(n);

        private static readonly Regex NumberText = new Regex(@"^-?(0|[1-9][0-9]{0,14})(\.[0-9]+)?$", RegexOptions.Compiled);
        private static readonly Regex IdLike = new Regex(@"(^|[ _\-])id$|[a-z]Id$|^(code|zip|zipcode|postcode|postal.*|phone.*|tel.*|sku|isbn|ean|upc|account.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>A column becomes numbers only when every filled value is a plain number without leading zeros and its name does not look like an identifier.</summary>
        private static bool[] NumericColumns(IEnumerable<string[]> rows, string[] header)
        {
            var numeric = Enumerable.Range(0, header.Length).Select(i => !IdLike.IsMatch(header[i])).ToArray();
            var seen = new bool[header.Length];
            foreach (var r in rows)
                for (int i = 0; i < header.Length && i < r.Length; i++)
                {
                    if (!numeric[i] || r[i].Length == 0) continue;
                    seen[i] = true;
                    if (!NumberText.IsMatch(r[i])) numeric[i] = false;
                }
            for (int i = 0; i < numeric.Length; i++) numeric[i] &= seen[i];
            return numeric;
        }

        private static string NumbersNote(string[] header, bool[] numeric)
        {
            var nums = header.Where((h, i) => numeric[i]).ToList();
            var text = header.Where((h, i) => !numeric[i]).ToList();
            if (nums.Count == 0) return "every value kept as text";
            string J(List<string> l) => l.Count == 1 ? l[0] : l.Count <= 4 ? string.Join(", ", l.Take(l.Count - 1)) + " and " + l[^1] : string.Join(", ", l.Take(4)) + " and " + (l.Count - 4) + " more";
            return J(nums) + " kept as numbers" + (text.Count > 0 ? "; " + J(text) + " kept as text" : "");
        }

        /// <summary>Converts a whole file to another format and returns the path written (never an existing file). CSV to JSON streams the rows.</summary>
        internal static string ConvertFile(DataProbe src, DataKind to, bool pretty, string outPath, ConvertStats stats)
        {
            var target = FileOps.Unique(outPath);
            var part = target + ".part";
            try
            {
                if (src.Format == DataKind.Csv)
                {
                    var header = src.Header; var skip = src.HeaderFound ? 1 : 0;
                    bool[] numeric;
                    using (var sr = new StreamReader(src.Path, new UTF8Encoding(false, false), true))
                        numeric = NumericColumns(ReadCsv(sr, src.Delimiter).Skip(skip), header);
                    stats.Numbers = NumbersNote(header, numeric);
                    if (to == DataKind.Json)
                    {
                        using var fs = new FileStream(part, FileMode.Create, FileAccess.Write);
                        using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = pretty, Encoder = Encoder });
                        using var sr = new StreamReader(src.Path, new UTF8Encoding(false, false), true);
                        w.WriteStartArray();
                        foreach (var r in ReadCsv(sr, src.Delimiter).Skip(skip))
                        {
                            w.WriteStartObject();
                            for (int i = 0; i < header.Length; i++)
                            {
                                var v = i < r.Length ? r[i] : "";
                                w.WritePropertyName(header[i]);
                                if (numeric[i] && v.Length > 0) w.WriteRawValue(v); else if (numeric[i]) w.WriteNullValue(); else w.WriteStringValue(v);
                            }
                            w.WriteEndObject();
                            stats.Items++;
                        }
                        w.WriteEndArray();
                    }
                    else if (to == DataKind.Yaml)
                    {
                        var arr = new JsonArray();
                        using (var sr = new StreamReader(src.Path, new UTF8Encoding(false, false), true))
                            foreach (var r in ReadCsv(sr, src.Delimiter).Skip(skip))
                            {
                                var o = new JsonObject();
                                for (int i = 0; i < header.Length; i++)
                                {
                                    var v = i < r.Length ? r[i] : "";
                                    o[header[i]] = numeric[i] ? (v.Length == 0 ? null : v.Contains('.') ? JsonValue.Create(double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)) : JsonValue.Create(long.Parse(v))) : JsonValue.Create(v);
                                }
                                arr.Add(o); stats.Items++;
                            }
                        File.WriteAllText(part, ToYamlText(arr), new UTF8Encoding(false));
                    }
                    else throw new InvalidOperationException("The file is already CSV.");
                }
                else
                {
                    var tree = src.Tree ?? throw new InvalidOperationException("The file has to be valid before it can be converted.");
                    string text;
                    switch (to)
                    {
                        case DataKind.Json: text = ToJsonText(tree, pretty); break;
                        case DataKind.Yaml: text = ToYamlText(tree); break;
                        default: text = ToCsvText(tree, src.Delimiter == '\t' ? '\t' : ',', out stats.Items); break;
                    }
                    if (to != DataKind.Csv) stats.Items = tree is JsonArray a ? a.Count : tree is JsonObject o ? o.Count : 1;
                    File.WriteAllText(part, text, new UTF8Encoding(false));
                }
                File.Move(part, target);
                stats.Bytes = new FileInfo(target).Length;
                return target;
            }
            catch { try { File.Delete(part); } catch { } throw; }
        }

        /// <summary>Rewrites the same file tidied (JSON indented, YAML re-emitted, CSV re-quoted) after saving a backup. Returns the backup path.</summary>
        internal static string FormatInPlace(DataProbe src, bool pretty)
        {
            if (!src.Valid) throw new InvalidOperationException("The file has to be valid before it can be formatted.");
            string text;
            switch (src.Format)
            {
                case DataKind.Json: text = ToJsonText(src.Tree, pretty); break;
                case DataKind.Yaml: text = ToYamlText(src.Tree); break;
                default:
                    var sb = new StringBuilder();
                    using (var sr = new StreamReader(src.Path, new UTF8Encoding(false, false), true))
                        foreach (var r in ReadCsv(sr, src.Delimiter)) sb.Append(string.Join(src.Delimiter, r.Select(v => CsvQuote(v, src.Delimiter)))).Append("\r\n");
                    text = sb.ToString();
                    break;
            }
            var backup = FileOps.Unique(src.Path + ".bak");
            File.Copy(src.Path, backup);
            var part = src.Path + ".part";
            try { File.WriteAllText(part, text, new UTF8Encoding(false)); File.Move(part, src.Path, true); }
            catch { try { File.Delete(part); } catch { } throw; }
            return backup;
        }
    }
}
