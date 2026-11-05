using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // The accessibility and Material 3 audit checks. One tour of every page, window and state runs per theme (dark, then light); the
    // findings are then judged by named checklist rows, and the full list is written to audit-ui.json beside selftest.json.
    public static partial class SelfTest
    {
        private static bool _auditRan;

        private static async Task AuditTourAsync(string dir)
        {
            if (_auditRan) return;
            _auditRan = true;
            UiAudit.Reset();
            var savedMode = Settings.Current.ThemeMode; var savedSeed = Settings.Current.SeedColour;
            try
            {
                foreach (var mode in new[] { "dark", "light" })
                {
                    Settings.Current.ThemeMode = mode; Settings.Current.SeedColour = "mint"; Appearance.Apply();
                    UiAudit.Begin(mode);
                    try { await LanguageAudit.RunAsync(dir); }
                    finally { UiAudit.End(); }
                }
            }
            finally { Settings.Current.ThemeMode = savedMode; Settings.Current.SeedColour = savedSeed; Appearance.Apply(); }
            UiAudit.Write(dir, "audit-ui.json");
            var t = UiAudit.Tally;
            string N(string k) => t.TryGetValue(k, out var v) ? v.ToString() : "0";
            NoteForReport("audit-ui", $"{N("scans")} scans in two themes: {N("controls")} control sightings, {N("named-checked")} names, {N("size-checked")} sizes, {N("tab-checked")} tab stops, {N("traversals")} traversals ({N("traversal-steps")} steps, {N("traversal-unavailable")} unavailable), {N("contrast-checked")} contrast pairs ({N("contrast-skipped-backdrop")} skipped on a gradient or picture), {N("text-checked")} text sightings, {N("shape-checked")} corners, {N("brush-literal-checked")} literal brushes");
        }

        private static string AuditVerdict(params string[] checks)
        {
            var bad = UiAudit.Findings.Where(f => checks.Contains(f.Check)).ToList();
            if (bad.Count == 0) return null;
            var groups = bad.GroupBy(f => f.Check + ": " + f.Detail).OrderByDescending(g => g.Count()).Take(12)
                .Select(g => g.Count() + " x " + g.Key + " (e.g. " + g.First().Control + " in " + g.First().Where + ")");
            return bad.Count + " findings: " + string.Join(" | ", groups) + " | see audit-ui.json";
        }

        internal static async Task AuditChecksAsync(string dir)
        {
            await CheckAsync("audit (tour): every page, window and state is walked in the dark and the light theme", async () =>
            {
                await AuditTourAsync(dir);
                var t = UiAudit.Tally;
                if (!t.TryGetValue("scans", out var scans) || scans < 100) return "only " + (t.TryGetValue("scans", out var s0) ? s0 : 0) + " scans were collected, so the audit is not covering the app";
                if (!t.TryGetValue("controls", out var c) || c < 2000) return "only " + (t.TryGetValue("controls", out var c0) ? c0 : 0) + " control sightings were collected";
                var errors = UiAudit.Findings.Where(f => f.Check == "audit error").ToList();
                return errors.Count == 0 ? null : errors.Count + " scans failed: " + string.Join(" | ", errors.Take(5).Select(e => e.Detail));
            });
            await CheckAsync("accessibility: every control a person can press has an accessible name", async () => { await AuditTourAsync(dir); return AuditVerdict("accessible name"); });
            await CheckAsync("accessibility: every enabled control is a tab stop and a real Tab traversal reaches it", async () =>
            {
                await AuditTourAsync(dir);
                if (!UiAudit.Tally.TryGetValue("traversals", out var n) || n < 20) return "only " + (UiAudit.Tally.TryGetValue("traversals", out var n0) ? n0 : 0) + " real Tab traversals ran (focus could not be moved)";
                return AuditVerdict("keyboard reach");
            });
            await CheckAsync("accessibility: no target is smaller than 24 by 24 unless it has the space around it", async () => { await AuditTourAsync(dir); return AuditVerdict("target size"); });
            await CheckAsync("accessibility: text contrast is at least 4.5:1 (3:1 for large text) on the real background, in both themes, and every palette pair in all five seeds passes", async () =>
            {
                await AuditTourAsync(dir);
                var pal = UiAudit.PaletteContrast(out int pairs);
                if (pal.Count > 0) return pal.Count + " palette pairs fail: " + string.Join(" | ", pal.Take(12));
                NoteForReport("audit-palette-pairs", pairs + " palette pairs checked across 5 seeds in light and dark");
                return AuditVerdict("contrast");
            });
            await CheckAsync("material 3: colours come from theme roles, not literals", async () => { await AuditTourAsync(dir); return AuditVerdict("colour roles"); });
            await CheckAsync("material 3: font sizes sit on the type scale and the typeface is Roboto", async () => { await AuditTourAsync(dir); return AuditVerdict("type scale", "typeface"); });
            await CheckAsync("material 3: corner radii sit on the shape scale", async () => { await AuditTourAsync(dir); return AuditVerdict("shape scale"); });
            await CheckAsync("material 3: every interactive style has hover, pressed, focus and disabled state layers", () =>
            {
                var problems = UiAudit.StyleStateReport(out var table);
                NoteForReport("audit-state-layers", table.Replace("\r", "").Replace("\n", "; "));
                return Task.FromResult(problems.Count == 0 ? null : problems.Count + " gaps: " + string.Join(" | ", problems));
            });
        }
    }
}
