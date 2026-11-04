using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MonitoringTools.Core
{
    /// <summary>The outcome of writing the "Export everything" archive.</summary>
    public sealed record ExportAllResult(int Datasets, int Files, long Bytes);

    /// <summary>
    /// "Export everything": every dataset the app keeps, each written in all five formats, in one zip. Lock records are
    /// described without any PIN hash, password hash or authenticator secret. Clipboard history is optional because it can hold
    /// private text.
    /// </summary>
    public static class ExportAll
    {
        /// <summary>Datasets added to the ones in <see cref="Exporter.Datasets"/>.</summary>
        public static readonly (string Id, string Label)[] ExtraDatasets =
        {
            ("shortcuts", "Shortcuts"), ("regex-library", "Regex library"), ("looks", "Appearance and looks"),
            ("schedules", "Schedule rules"), ("locks", "Lock records (no secrets)"), ("dns", "DNS profiles")
        };

        private static string Folder(ExportFormat f) => f switch
        {
            ExportFormat.Json => "json", ExportFormat.Csv => "csv", ExportFormat.Markdown => "markdown", ExportFormat.Html => "html", _ => "text"
        };

        /// <summary>Every dataset id the archive holds, in order. Clipboard history is included only when asked for.</summary>
        public static List<string> Ids(bool includeClipboard)
            => Exporter.Datasets.Select(d => d.Id).Where(id => includeClipboard || id != "clipboard").Concat(ExtraDatasets.Select(d => d.Id)).ToList();

        /// <summary>Snapshots one dataset. Call on the UI thread, like <see cref="Exporter.Snapshot"/>.</summary>
        public static ExportTable Snapshot(string id)
        {
            switch (id)
            {
                case "shortcuts":
                {
                    var t = new ExportTable { Name = "Shortcuts", Columns = new[] { "Name", "Shell", "Show window", "Run elevated", "Description", "Script" } };
                    foreach (var s in Settings.Current.Shortcuts ?? new List<Shortcut>())
                    {
                        string body; try { body = Shortcuts.ReadScript(s); } catch { body = ""; }
                        t.Rows.Add(new[] { s.Name, s.Shell.ToString(), s.ShowWindow ? "yes" : "no", s.RunElevated ? "yes" : "no", s.Description ?? "", body });
                    }
                    return t;
                }
                case "regex-library":
                {
                    var t = new ExportTable { Name = "Regex library", Columns = new[] { "Name", "Pattern", "Options", "Replacement" } };
                    foreach (var p in Settings.Current.SavedPatterns ?? new List<SavedPattern>())
                        t.Rows.Add(new[] { p.Name, p.Pattern, p.Options.ToString(), p.Replacement ?? "" });
                    return t;
                }
                case "looks":
                {
                    var t = new ExportTable { Name = "Appearance and looks", Columns = new[] { "Kind", "Name", "Value" } };
                    var s = Settings.Current;
                    foreach (var (k, v) in new[]
                    {
                        ("Theme", s.ThemeMode), ("Seed colour", s.SeedColour), ("Text size percent", s.TextScalePercent.ToString()),
                        ("Compact density", s.CompactDensity ? "yes" : "no"), ("Reduce motion", s.MotionMode), ("High-contrast outlines", s.HighContrastOutlines ? "yes" : "no"),
                        ("Display name", s.DisplayName), ("Logo tint", s.LogoSource), ("Logo custom colour", s.LogoCustomHex), ("Logo glyph", s.LogoGlyph),
                        ("Logo custom image", string.IsNullOrEmpty(s.LogoImageFile) ? "none" : s.LogoImageFile)
                    }) t.Rows.Add(new[] { "setting", k, v ?? "" });
                    try
                    {
                        using var d = JsonDocument.Parse(ElementStyles.ExportJson());
                        foreach (var (section, kind) in new[] { ("Styles", "element look"), ("Presets", "preset") })
                            if (d.RootElement.TryGetProperty(section, out var obj) && obj.ValueKind == JsonValueKind.Object)
                                foreach (var p in obj.EnumerateObject()) t.Rows.Add(new[] { kind, p.Name, p.Value.GetRawText() });
                    }
                    catch (Exception ex) { Log.Warn("Looks export failed: " + ex.Message); }
                    return t;
                }
                case "schedules":
                {
                    var t = new ExportTable { Name = "Schedule rules", Columns = new[] { "Name", "Enabled", "Setting", "Value", "Start", "End", "Days", "From date", "To date" } };
                    foreach (var r in Settings.Current.ScheduleRules ?? new List<ScheduleRule>())
                        t.Rows.Add(new[] { r.Label ?? "", r.Enabled ? "yes" : "no", r.Setting, r.Value, r.Start, r.End,
                            r.Days == null || r.Days.Count == 0 ? "every day" : string.Join(" ", r.Days.OrderBy(d => (d + 6) % 7).Select(d => ((DayOfWeek)d).ToString().Substring(0, 3))), r.FromDate ?? "", r.ToDate ?? "" });
                    return t;
                }
                case "locks":
                {
                    // Only facts about each lock. The salts, hashes and authenticator secret are never read into the table.
                    var t = new ExportTable { Name = "Lock records (no secrets)", Columns = new[] { "Label", "Page", "Rule", "Open for minutes", "Created", "Uses PIN", "Uses password", "Uses authenticator" } };
                    foreach (var l in Locks.All)
                    {
                        var f = Locks.Factors(l.Policy);
                        t.Rows.Add(new[] { l.Label, l.PageId, Locks.Describe(l.Policy), l.Minutes.ToString(), l.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                            f.Contains(LockFactor.Pin) ? "yes" : "no", f.Contains(LockFactor.Password) ? "yes" : "no", f.Contains(LockFactor.Totp) ? "yes" : "no" });
                    }
                    return t;
                }
                case "dns":
                {
                    var t = new ExportTable { Name = "DNS profiles", Columns = new[] { "Name", "Primary", "Secondary" } };
                    foreach (var p in Settings.Current.DnsProfiles)
                        t.Rows.Add(new[] { p.Name, p.Primary, p.Secondary });
                    return t;
                }
                default: return Exporter.Snapshot(id);
            }
        }

        public static List<ExportTable> SnapshotAll(bool includeClipboard) => Ids(includeClipboard).Select(Snapshot).ToList();

        /// <summary>Writes every table in every format into a zip, replacing a file that already exists. Runs on any thread.</summary>
        public static ExportAllResult WriteZip(string path, IReadOnlyList<ExportTable> tables, IProgress<double> progress = null)
        {
            var tmp = path + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
            int files = 0, step = 0, total = tables.Count * Exporter.Formats.Length;
            using (var fs = new FileStream(tmp, FileMode.CreateNew))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var readme = new StringBuilder();
                readme.AppendLine("Monitoring Tools export, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                readme.AppendLine();
                readme.AppendLine("Each dataset is written in five formats: json, csv, markdown, html and text folders.");
                readme.AppendLine("Lock records describe each lock without any PIN, password or authenticator secret.");
                readme.AppendLine();
                foreach (var t in tables) readme.AppendLine($"{Slug(t.Name)}: {t.Rows.Count} rows");
                AddEntry(zip, "README.txt", readme.ToString(), false); files++;
                foreach (var t in tables)
                    foreach (var (format, _, ext) in Exporter.Formats)
                    {
                        AddEntry(zip, Folder(format) + "/" + Slug(t.Name) + ext, Exporter.Format(t, format), format == ExportFormat.Csv);
                        files++;
                        progress?.Report(++step / (double)Math.Max(1, total));
                    }
            }
            File.Move(tmp, path, true);
            return new ExportAllResult(tables.Count, files, new FileInfo(path).Length);
        }

        private static void AddEntry(ZipArchive zip, string name, string text, bool bom)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var s = e.Open();
            var bytes = new UTF8Encoding(bom).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
            s.Write(bytes, 0, bytes.Length);
        }

        public static string Slug(string name)
        {
            var sb = new StringBuilder();
            foreach (var c in name.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return string.Join("-", sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
