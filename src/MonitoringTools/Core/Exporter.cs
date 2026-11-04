using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MonitoringTools.Clip;

namespace MonitoringTools.Core
{
    public enum ExportFormat { Json, Csv, Markdown, Html, Text }

    public sealed class ExportTable
    {
        public string Name;
        public string[] Columns;
        public List<string[]> Rows = new List<string[]>();
    }

    /// <summary>Turns the app's records into JSON, CSV, Markdown, HTML or plain text. Tables are snapshotted first so formatting can run off the UI thread.</summary>
    public static class Exporter
    {
        public static readonly (string Id, string Label)[] Datasets =
        {
            ("clipboard", "Clipboard history"), ("activity", "Activity"), ("settings", "Settings"),
            ("notifications", "Notifications"), ("history", "Settings history")
        };

        public static readonly (ExportFormat Format, string Label, string Ext)[] Formats =
        {
            (ExportFormat.Json, "JSON", ".json"), (ExportFormat.Csv, "CSV", ".csv"), (ExportFormat.Markdown, "Markdown", ".md"),
            (ExportFormat.Html, "HTML", ".html"), (ExportFormat.Text, "Plain text", ".txt")
        };

        public static string Extension(ExportFormat f) => Formats.First(x => x.Format == f).Ext;

        /// <summary>Takes a snapshot of one dataset. Call on the UI thread because clipboard history is bound to it.</summary>
        public static ExportTable Snapshot(string id)
        {
            switch (id)
            {
                case "clipboard":
                {
                    var t = new ExportTable { Name = "Clipboard history", Columns = new[] { "Time", "Kind", "Text" } };
                    var text = new HashSet<ClipKind> { ClipKind.Text, ClipKind.Markdown, ClipKind.Url, ClipKind.Email, ClipKind.Color, ClipKind.Json };
                    foreach (var it in App.History.Items.Where(i => text.Contains(i.Kind) && !string.IsNullOrEmpty(i.Text)))
                        t.Rows.Add(new[] { it.At.ToString("yyyy-MM-dd HH:mm:ss"), it.Kind.ToString(), it.Text });
                    return t;
                }
                case "activity":
                {
                    var t = new ExportTable { Name = "Activity", Columns = new[] { "Time", "Kind", "Title", "Detail", "Result" } };
                    foreach (var e in Activity.Recent(300))
                        t.Rows.Add(new[] { e.At.ToString("yyyy-MM-dd HH:mm:ss"), e.Kind.ToString(), e.Title ?? "", e.Detail ?? "", e.Ok ? "ok" : "failed" });
                    return t;
                }
                case "settings":
                {
                    var t = new ExportTable { Name = "Settings", Columns = new[] { "Setting", "Value" } };
                    using var d = JsonDocument.Parse(JsonSerializer.Serialize(Settings.Current));
                    foreach (var p in d.RootElement.EnumerateObject())
                        t.Rows.Add(new[] { SettingsHistory.Label(p.Name), p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText() });
                    return t;
                }
                case "notifications":
                {
                    var t = new ExportTable { Name = "Notifications", Columns = new[] { "Time", "Kind", "Title", "Detail" } };
                    foreach (var n in NotificationLog.All())
                        t.Rows.Add(new[] { n.At.ToString("yyyy-MM-dd HH:mm:ss"), n.Kind, n.DisplayTitle, n.Detail });
                    return t;
                }
                default:
                {
                    var t = new ExportTable { Name = "Settings history", Columns = new[] { "Time", "Summary" } };
                    foreach (var (_, s) in SettingsHistory.List())
                        t.Rows.Add(new[] { s.At.ToString("yyyy-MM-dd HH:mm:ss"), s.Summary });
                    return t;
                }
            }
        }

        /// <summary>Formats a snapshot on a worker thread, reporting progress from 0 to 1.</summary>
        public static Task<string> FormatAsync(ExportTable t, ExportFormat f, IProgress<double> progress)
            => Task.Run(() => Format(t, f, progress));

        public static string Format(ExportTable t, ExportFormat f, IProgress<double> progress = null)
        {
            var sb = new StringBuilder();
            int n = t.Rows.Count;
            void Tick(int i) { if (progress != null && (i % 200 == 0 || i == n)) progress.Report(n == 0 ? 1 : (double)i / n); }

            switch (f)
            {
                case ExportFormat.Json:
                {
                    using var ms = new System.IO.MemoryStream();
                    using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                    {
                        w.WriteStartArray();
                        for (int i = 0; i < n; i++)
                        {
                            w.WriteStartObject();
                            for (int c = 0; c < t.Columns.Length; c++) w.WriteString(t.Columns[c], t.Rows[i][c]);
                            w.WriteEndObject();
                            Tick(i + 1);
                        }
                        w.WriteEndArray();
                    }
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
                case ExportFormat.Csv:
                    sb.Append(string.Join(",", t.Columns.Select(Csv))).Append("\r\n");
                    for (int i = 0; i < n; i++) { sb.Append(string.Join(",", t.Rows[i].Select(Csv))).Append("\r\n"); Tick(i + 1); }
                    return sb.ToString();
                case ExportFormat.Markdown:
                    sb.Append("# ").AppendLine(t.Name).AppendLine();
                    sb.Append("| ").Append(string.Join(" | ", t.Columns)).AppendLine(" |");
                    sb.Append("|").Append(string.Concat(t.Columns.Select(_ => " --- |"))).AppendLine();
                    for (int i = 0; i < n; i++) { sb.Append("| ").Append(string.Join(" | ", t.Rows[i].Select(Md))).AppendLine(" |"); Tick(i + 1); }
                    return sb.ToString();
                case ExportFormat.Html:
                    sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>").Append(Html(t.Name)).Append("</title><style>")
                      .Append("body{font:14px/1.5 system-ui,Segoe UI,sans-serif;margin:24px;color:#1b1f23;background:#fff}")
                      .Append("table{border-collapse:collapse;width:100%}th,td{border:1px solid #d0d7de;padding:6px 10px;text-align:left;vertical-align:top;white-space:pre-wrap;word-break:break-word}")
                      .Append("th{background:#f3f4f6}tr:nth-child(even) td{background:#fafbfc}</style></head><body><h1>").Append(Html(t.Name)).Append("</h1><table><thead><tr>")
                      .Append(string.Concat(t.Columns.Select(c => "<th>" + Html(c) + "</th>"))).Append("</tr></thead><tbody>");
                    for (int i = 0; i < n; i++) { sb.Append("<tr>").Append(string.Concat(t.Rows[i].Select(c => "<td>" + Html(c) + "</td>"))).Append("</tr>"); Tick(i + 1); }
                    sb.Append("</tbody></table></body></html>");
                    return sb.ToString();
                default:
                    sb.AppendLine(t.Name).AppendLine(new string('=', Math.Max(4, t.Name.Length)));
                    for (int i = 0; i < n; i++)
                    {
                        for (int c = 0; c < t.Columns.Length; c++) sb.Append(t.Columns[c]).Append(": ").AppendLine(t.Rows[i][c]);
                        sb.AppendLine();
                        Tick(i + 1);
                    }
                    return sb.ToString();
            }
        }

        private static string Csv(string v)
        {
            v ??= "";
            // A cell that starts like a formula is prefixed so spreadsheets show it as text instead of running it.
            if (v.Length > 0 && "=+-@\t\r".IndexOf(v[0]) >= 0) v = "'" + v;
            return v.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        private static string Md(string v) => (v ?? "").Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r\n", "<br>").Replace("\n", "<br>").Replace("\r", "<br>");
        private static string Html(string v) => WebUtility.HtmlEncode(v ?? "");
    }
}
