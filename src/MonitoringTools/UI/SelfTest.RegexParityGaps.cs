using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MonitoringTools.Core;
using MonitoringTools.Core.Archives;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    // Checks for the regex builder parity items closed after the first parity pass: the tracer's inline options, conditionals, class
    // subtraction and \G; the conditional guided editor and a real posted mouse drag in the Build tree; archive, binary and per-extension
    // file search; several patterns and the JSON automation entry on the command line.
    public static partial class SelfTest
    {
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr MouseLParam(Point p) => (IntPtr)((((int)Math.Round(p.Y)) << 16) | (((int)Math.Round(p.X)) & 0xFFFF));

        /// <summary>Posts a left-button drag from one client point to another to a window, the same messages a pointer would produce.</summary>
        private static async Task PostDragAsync(IntPtr hwnd, Point from, Point to, int steps = 6)
        {
            const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
            var MK_LBUTTON = (IntPtr)1;
            PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, MouseLParam(from)); await Task.Delay(60);
            PostMessage(hwnd, WM_LBUTTONDOWN, MK_LBUTTON, MouseLParam(from)); await Task.Delay(80);
            for (int k = 1; k <= steps; k++)
            {
                var p = new Point(from.X + (to.X - from.X) * k / steps, from.Y + (to.Y - from.Y) * k / steps);
                PostMessage(hwnd, WM_MOUSEMOVE, MK_LBUTTON, MouseLParam(p)); await Task.Delay(40);
            }
            PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, MouseLParam(to)); await Task.Delay(300);
        }

        private static void RegexParityGapChecks(string work)
        {
            // ================================================================ tracer
            Check("regex parity debug: inline options, conditionals, class subtraction and \\G trace, and every capture agrees with .NET", () =>
            {
                var cases = new (string Pattern, string Subject, RegexOptions Options, bool Match)[]
                {
                    ("(?i)abc", "xx ABC", RegexOptions.None, true), ("a(?i)b|C", "c", RegexOptions.None, true), ("(?i:a)b", "Ab AB", RegexOptions.None, true),
                    ("(?i)a(?-i)b", "AB Ab", RegexOptions.None, true), ("(?s)a.b", "a\nb", RegexOptions.None, true), ("(?m)^b", "a\nb", RegexOptions.None, true),
                    ("(?n)(a)(?<x>b)", "ab", RegexOptions.None, true), ("(a)(?n:(b))(c)", "abc", RegexOptions.None, true), ("(?-i)a", "A a", RegexOptions.IgnoreCase, true),
                    ("(a)?(?(1)b|c)", "c ab", RegexOptions.None, true), ("(?<q>\")?\\w+(?(q)\")", "say \"hi\" x", RegexOptions.None, true),
                    (@"(?(?=\d)\d+|[a-z]+)", "-- 42", RegexOptions.None, true), (@"(?(?!a)\w|a\d)", "a1", RegexOptions.None, true), ("(?(?<=x)y|z)", "xy", RegexOptions.None, true),
                    ("(a)?(?(1)b)c", "c", RegexOptions.None, true), ("[a-z-[aeiou]]+", "aeixyz", RegexOptions.None, true), ("[^a-z-[x]]", "x1", RegexOptions.None, true),
                    ("[a-z-[d-f-[e]]]+", "def", RegexOptions.None, true), ("(?i)[a-z-[A]]", "aB", RegexOptions.None, true),
                    (@"\Gab", "abab", RegexOptions.None, true), (@"\Gb", "ab", RegexOptions.None, false), (@"x|\Ga", "ya", RegexOptions.None, false),
                    (@"(a)(?(1)\1)", "aab", RegexOptions.None, true), ("(a)", "a", RegexOptions.ExplicitCapture, true),
                };
                var bad = new List<string>();
                foreach (var (pattern, subject, o, match) in cases)
                {
                    var r = RegexTrace.Run(pattern, o, subject);
                    if (r.Unsupported != null || r.Disagreement != null) { bad.Add(pattern + ": " + (r.Unsupported ?? r.Disagreement)); continue; }
                    if (!r.Verified || r.Matched != match || match && !r.CapturesVerified || r.Steps.Count < 3) bad.Add(pattern + ": verified=" + r.Verified + " matched=" + r.Matched + " captures=" + r.CapturesVerified + " steps=" + r.Steps.Count);
                }
                var cond = RegexTrace.Run("(a)?(?(1)b|c)", RegexOptions.None, "c");
                if (!cond.Steps.Any(s => s.Text.StartsWith("Conditional: group 1 has not captured anything"))) bad.Add("no step names the conditional's choice");
                var look = RegexTrace.Run(@"(?(?=\d)\d|x)", RegexOptions.None, "7");
                if (!look.Steps.Any(s => s.Text.Contains("look-ahead condition")) || !look.Steps.Any(s => s.Text.StartsWith("The condition holds"))) bad.Add("no step names the look-ahead condition");
                var opt = RegexTrace.Run("(?i)a", RegexOptions.None, "A");
                if (!opt.Steps.Any(s => s.Text.StartsWith("Inline options (?i)"))) bad.Add("no step names the inline options");
                var g = RegexTrace.Run(@"\Gb", RegexOptions.None, "ab");
                if (!g.Steps.Any(s => s.Text.Contains("start of this search (\\G)"))) bad.Add("no step names \\G");
                return bad.Count == 0 ? null : string.Join(" ; ", bad);
            });

            // ================================================================ conditional editor (core)
            Check("regex parity tree: a conditional is read into its condition and branches, and rewritten only when .NET accepts it", () =>
            {
                RegexTree.Node Cond(string p) => RegexTree.Walk(RegexTree.Parse(p).Root).First(n => n.Kind == RegexTree.NodeKind.Conditional);
                var p1 = "(a)?(?(1)b|c)";
                var c1 = Cond(p1);
                var parts = RegexTree.ReadConditional(p1, c1);
                if (parts == null || parts.Kind != "Group has matched" || parts.Test != "1" || parts.Yes != "b" || parts.No != "c" || !parts.HasNo) return "read: " + parts;
                var viaCondition = RegexTree.ReadConditional(p1, c1.Children[0]);
                if (viaCondition?.Test != "1") return "reading through the condition node";
                var la = RegexTree.ApplyConditional(p1, c1, "Look-ahead", @"\d", "x", "");
                if (la != @"(a)?(?(?=\d)x)") return "look-ahead rewrite: " + la;
                if (RegexTree.ApplyConditional(p1, c1, "Group has matched", "9", "b", "c") != null) return "a missing group was accepted";
                if (RegexTree.ApplyConditional(p1, c1, "Group has matched", "1", "a|b", "c") != null) return "a branch with its own | was accepted";
                if (RegexTree.ApplyConditional(p1, c1, "Negative look-behind", "", "b", "c") != null) return "an empty test was accepted";
                var p2 = "(?<q>\")?\\w+(?(?<!x)\"|[|])";
                var parts2 = RegexTree.ReadConditional(p2, Cond(p2));
                if (parts2 == null || parts2.Kind != "Negative look-behind" || parts2.Test != "x" || parts2.Yes != "\"" || parts2.No != "[|]") return "read look-behind: " + parts2;
                var named = RegexTree.ApplyConditional(p2, Cond(p2), "Group has matched", "q", "\"", "y");
                return named == "(?<q>\")?\\w+(?(q)\"|y)" ? null : "named group rewrite: " + named;
            });

            // ================================================================ files: archives, binary, file types
            Check("regex parity files: zip and 7z archives, binary files by byte with offsets and hex, and per-extension rules", () =>
            {
                var root = Path.Combine(work, "gap-files"); if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "notes.txt"), "a needle here\n");
                File.WriteAllBytes(Path.Combine(root, "data.bin"), new byte[] { 0, 1, 2, 0x6E, 0x65, 0x65, 0x64, 0x6C, 0x65, 0xFF, 0 });
                File.WriteAllBytes(Path.Combine(root, "wide.u16"), Encoding.Unicode.GetBytes("x needle\n"));   // UTF-16 LE without a byte order mark
                File.WriteAllText(Path.Combine(root, "skip.log"), "needle\n");
                using (var zip = ZipFile.Open(Path.Combine(root, "pack.zip"), ZipArchiveMode.Create))
                {
                    using (var w = new StreamWriter(zip.CreateEntry("inner/readme.md").Open())) w.Write("line one\nneedle in a zip\n");
                    using (var w = new StreamWriter(zip.CreateEntry("inner/other.txt").Open())) w.Write("nothing\n");
                }
                var bad = new List<string>();
                var rx = new Regex("needle");
                // plain search: archives off, binary skipped, no rules -> notes.txt and skip.log only; wide.u16 is skipped as binary
                var plain = RegexGrep.Search(rx, new RegexGrep.Options { Folder = root }, default);
                if (plain.FilesWithHits != 2) bad.Add("plain search found " + plain.FilesWithHits + " files");
                var rules = new List<RegexFileType> { new RegexFileType { Extension = "u16", Encoding = "UTF-16 LE" }, new RegexFileType { Extension = ".log", Skip = true } };
                var o = new RegexGrep.Options { Folder = root, SearchArchives = true, SkipBinary = false, BinaryMode = "Bytes", FileTypes = rules };
                var rep = RegexGrep.Search(rx, o, default);
                var names = rep.Files.Select(f => Path.GetFileName(f.Path.Replace("!/", "\\"))).OrderBy(x => x).ToList();
                if (string.Join(",", names) != "data.bin,notes.txt,readme.md,wide.u16") bad.Add("files with matches: " + string.Join(",", names) + " skipped: " + string.Join(" | ", rep.Skipped.Select(s => s.Reason)));
                var zipHit = rep.Files.FirstOrDefault(f => f.Archive != null);
                if (zipHit == null || !zipHit.Path.EndsWith("pack.zip!/inner/readme.md") || zipHit.Hits[0].Line != 2) bad.Add("zip entry: " + zipHit?.Path + " line " + zipHit?.Hits.FirstOrDefault()?.Line);
                var bin = rep.Files.FirstOrDefault(f => f.Binary);
                if (bin == null) bad.Add("no binary result");
                else
                {
                    var h = bin.Hits[0];
                    if (h.Offset != 3 || h.Hex != "00 01 02 6E 65 65 64 6C 65 FF 00" || h.Hex.Substring(h.HexStart, h.HexLength) != "6E 65 65 64 6C 65" || h.LineText != "...needle..") bad.Add($"binary hit: offset {h.Offset}, hex {h.Hex}, view {h.LineText}");
                }
                // a byte pattern finds raw bytes in the binary file
                var bytes = RegexGrep.Search(new Regex(@"\xFF\x00"), new RegexGrep.Options { Folder = root, SkipBinary = false, BinaryMode = "Bytes", Masks = "*.bin" }, default);
                if (bytes.TotalMatches != 1 || bytes.Files[0].Hits[0].Offset != 9) bad.Add("byte pattern: " + bytes.TotalMatches);
                var wide = rep.Files.FirstOrDefault(f => f.Path.EndsWith("wide.u16"));
                if (wide == null || wide.ForcedEncoding != "UTF-16 LE" || wide.Hits[0].Column != 3) bad.Add("forced encoding: " + wide?.Encoding);
                if (!rep.Skipped.Any(s => s.Path.EndsWith("skip.log") && s.Reason.Contains("file type setting"))) bad.Add("the .log rule did not skip");
                // replace never touches archive entries or binary results
                var plan = RegexGrep.Plan(rep);
                if (plan.Count != 2 || RegexGrep.NotReplaceable(rep) != 2 || plan.Any(x => x.Path.Contains("!/") || x.Path.EndsWith(".bin"))) bad.Add("plan: " + string.Join(",", plan.Select(x => Path.GetFileName(x.Path))));
                var oldOverride = Settings.DataDirOverride;
                Settings.DataDirOverride = Path.Combine(work, "gap-data");
                try
                {
                    var outc = RegexGrep.Apply(rx, "pin", plan, default);
                    var back = File.ReadAllBytes(Path.Combine(root, "wide.u16"));
                    if (outc.FilesChanged != 2 || Encoding.Unicode.GetString(back) != "x pin\n" || back.Length > 2 && back[0] == 0xFF && back[1] == 0xFE) bad.Add("replace with a forced encoding: " + outc.FilesChanged + " " + Encoding.Unicode.GetString(back));
                }
                finally { Settings.DataDirOverride = oldOverride; }
                // a 7z archive through the bundled 7-Zip
                var seven = ArchiveEngine.SevenZip;
                if (seven == null) bad.Add("7-Zip is not available in this build");
                else
                {
                    var src = Path.Combine(work, "gap-7z-src"); if (Directory.Exists(src)) Directory.Delete(src, true);
                    Directory.CreateDirectory(src);
                    File.WriteAllText(Path.Combine(src, "deep.txt"), "x\nfind the needle\n");
                    var arc = Path.Combine(root, "box.7z");
                    var outp = RxRun(seven, "a -t7z \"" + arc + "\" \"" + Path.Combine(src, "deep.txt") + "\"", null, out var code);
                    if (code != 0) bad.Add("could not make a 7z: " + outp.Trim());
                    else
                    {
                        var r7 = RegexGrep.Search(rx, new RegexGrep.Options { Folder = root, SearchArchives = true, Masks = "*.txt" }, default);
                        var hit = r7.Files.FirstOrDefault(f => f.Path.EndsWith("box.7z!/deep.txt"));
                        if (hit == null || hit.Hits[0].Line != 2) bad.Add("7z entry not found: " + string.Join(",", r7.Files.Select(f => f.Path)) + " / " + string.Join(" | ", r7.Skipped.Select(s => s.Reason)));
                        if (r7.Files.Any(f => f.Path.EndsWith("readme.md"))) bad.Add("the *.txt mask did not apply to the zip entries");
                        // a cap smaller than the archive leaves it unsearched and says so
                        var capped = RegexGrep.Search(rx, new RegexGrep.Options { Folder = root, SearchArchives = true, Masks = "*.txt", MaxArchiveBytes = 4 }, default);
                        if (capped.Files.Any(f => f.Archive != null) || !capped.Skipped.Any(s => s.Reason.Contains("unpacks to more than"))) bad.Add("archive size cap");
                    }
                }
                if (RegexGrep.NormalizeExtension("*.LOG") != ".log" || RegexGrep.NormalizeExtension("a b") != null) bad.Add("extension normalisation");
                return bad.Count == 0 ? null : string.Join(" ; ", bad);
            });

            // ================================================================ command line: several patterns and automation
            Check("regex parity cli: several patterns, a patterns file and the JSON automation entry run as real child processes", () =>
            {
                var exe = Environment.ProcessPath;
                if (exe == null || !exe.EndsWith("MonitoringTools.exe", StringComparison.OrdinalIgnoreCase)) return null;   // run only from the real executable
                var data = Path.Combine(work, "cli-gap-data"); if (Directory.Exists(data)) Directory.Delete(data, true);
                Directory.CreateDirectory(data);
                var env = new Dictionary<string, string> { ["MT_DATA_DIR"] = data };
                var f = Path.Combine(work, "cli-gap.txt");
                File.WriteAllText(f, "ant 12 elk 7\nbird\n");
                var bad = new List<string>();
                var m1 = RxRun(exe, "--regex --pattern \"\\d+\" --pattern \"elk|bird\" \"" + f + "\"", null, out var e1, env).Replace("\r", "");
                var want = f + ":1:5: [1] 12\n" + f + ":1:8: [2] elk\n" + f + ":1:12: [1] 7\n" + f + ":2:1: [2] bird\n";
                if (e1 != 0 || m1 != want) bad.Add("two patterns: exit " + e1 + " " + m1);
                var pf = Path.Combine(work, "cli-gap-patterns.txt");
                File.WriteAllText(pf, "# numbers and birds\n\\d+\n\nbird\n");
                var m2 = RxRun(exe, "--regex --patterns-file \"" + pf + "\" --json \"" + f + "\"", null, out var e2, env);
                try
                {
                    using var doc = JsonDocument.Parse(m2);
                    var rows = doc.RootElement.EnumerateArray().ToList();
                    if (e2 != 0 || rows.Count != 3 || rows[2].GetProperty("pattern").GetInt32() != 2 || rows[2].GetProperty("value").GetString() != "bird") bad.Add("patterns file json: " + m2);
                }
                catch (JsonException) { bad.Add("patterns file json is not JSON: " + m2); }
                var m3 = RxRun(exe, "--regex --pattern a --pattern \"(\" \"" + f + "\"", null, out var e3, env);
                if (e3 != 2 || !m3.Contains("Invalid pattern 2")) bad.Add("an invalid second pattern: " + e3 + " " + m3.Trim());
                var m4 = RxRun(exe, "--regex --pattern \"\\d\" --pattern e --replace # \"" + f + "\"", null, out var e4, env).Replace("\r", "");
                if (e4 != 0 || m4 != "ant ## #lk #\nbird\n") bad.Add("replace in order: " + m4);
                var m5 = RxRun(exe, "--regex --pattern a --pattern b --in-place --replace x \"" + f + "\"", null, out var e5, env);
                if (e5 != 2 || File.ReadAllText(f) != "ant 12 elk 7\nbird\n") bad.Add("--in-place with two patterns must be refused: " + e5);
                // automation: JSON request on standard input, JSON result on standard output
                var req = "{\"operation\":\"match\",\"patterns\":[{\"pattern\":\"(?<n>\\\\d+)\",\"name\":\"numbers\"},{\"pattern\":\"(\"}],\"text\":\"a 12\\nb 3\",\"files\":[" + JsonSerializer.Serialize(f) + "]}";
                var a1 = RxRun(exe, "--regex --automation", req, out var ea1, env);
                try
                {
                    using var doc = JsonDocument.Parse(a1);
                    var r = doc.RootElement;
                    var res = r.GetProperty("results");
                    var first = res[0];
                    var textIn = first.GetProperty("inputs")[0];
                    var m = textIn.GetProperty("matches")[1];
                    if (ea1 != 0 || !r.GetProperty("ok").GetBoolean() || first.GetProperty("name").GetString() != "numbers" || textIn.GetProperty("count").GetInt32() != 2
                        || m.GetProperty("line").GetInt32() != 2 || m.GetProperty("column").GetInt32() != 3 || m.GetProperty("groups")[0].GetProperty("name").GetString() != "n" || m.GetProperty("groups")[0].GetProperty("value").GetString() != "3"
                        || first.GetProperty("inputs")[1].GetProperty("count").GetInt32() != 2 || res[1].GetProperty("valid").GetBoolean() || res[1].GetProperty("error").GetString() == null)
                        bad.Add("automation match: exit " + ea1 + " " + a1);
                }
                catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is IndexOutOfRangeException) { bad.Add("automation match result: " + ex.Message + " " + a1); }
                var reqFile = Path.Combine(work, "cli-gap-request.json");
                File.WriteAllText(reqFile, "{\"operation\":\"replace\",\"pattern\":\"o\",\"options\":\"i\",\"replacement\":\"0\",\"text\":\"fOo\"}");
                var a2 = RxRun(exe, "--regex --request \"" + reqFile + "\"", null, out var ea2, env);
                if (ea2 != 0 || !a2.Contains("\"text\": \"f00\"") || !a2.Contains("\"replacements\": 2")) bad.Add("automation replace from a file: " + ea2 + " " + a2);
                var a3 = RxRun(exe, "--regex --automation", "{\"operation\":\"split\",\"pattern\":\",\\\\s*\",\"text\":\"a, b,c\"}", out var ea3, env);
                if (ea3 != 0 || !Regex.IsMatch(a3, "\"parts\": \\[\\s*\"a\",\\s*\"b\",\\s*\"c\"\\s*\\]")) bad.Add("automation split: " + a3);
                var a4 = RxRun(exe, "--regex --automation", "{\"operation\":\"validate\",\"patterns\":[\"a+\",\"[\"]}", out var ea4, env);
                if (ea4 != 1 || !a4.Contains("\"valid\": false")) bad.Add("automation validate: " + ea4 + " " + a4);
                var a5 = RxRun(exe, "--regex --automation", "{not json", out var ea5, env);
                if (ea5 != 2 || !a5.Contains("\"ok\": false")) bad.Add("a bad request: " + ea5 + " " + a5);
                var a6 = RxRun(exe, "--regex --automation", "{\"operation\":\"match\",\"pattern\":\"zzz\",\"text\":\"abc\"}", out var ea6, env);
                if (ea6 != 1 || !a6.Contains("\"matched\": false")) bad.Add("no match: " + ea6);
                return bad.Count == 0 ? null : string.Join(" ; ", bad);
            });
        }

        /// <summary>Saves the builder card as a PNG at 1.5x when a capture folder is given (MT_ONLY=regexgap writes them).</summary>
        private static void RxShot(FrameworkElement card, string shots, string name)
        {
            if (shots == null) return;
            card.UpdateLayout();
            // the whole scrolling content of the view, not only the part the card shows, so lower panels are in the picture too
            var content = Descend<ScrollViewer>(card).Select(s => s.Content as FrameworkElement).FirstOrDefault(c => c != null && c.ActualHeight > 200) ?? card;
            var bg = new System.Windows.Media.DrawingVisual();
            using (var dc = bg.RenderOpen())
            {
                dc.DrawRectangle(Ui.Res("B.Surface") ?? System.Windows.Media.Brushes.Black, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
                dc.DrawRectangle(new System.Windows.Media.VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            }
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(content.ActualWidth * 1.5), (int)(content.ActualHeight * 1.5), 144, 144, System.Windows.Media.PixelFormats.Pbgra32);
            bmp.Render(bg);
            Demo.SavePng(bmp, Path.Combine(shots, name + ".png"));
        }

        private static async Task RegexParityGapUiChecksAsync(SearchField f, RegexBuilderPopup b, FrameworkElement card, string work, string shots = null)
        {
            var settings = Settings.Current;
            f.OpenBuilder("Build"); await Task.Delay(400);
            await CheckAsync("regex parity ui build: the conditional editor changes the condition kind, test and branches through the real Build view", async () =>
            {
                f.State.SetQuery("(a)?(?(1)b|c)"); await Task.Delay(350);
                var cond = RegexTree.Walk(b.CurrentTree.Root).First(n => n.Kind == RegexTree.NodeKind.Conditional);
                b.PatternBox.Focus(); b.PatternBox.Select(cond.Start, cond.End - cond.Start); await Task.Delay(300);
                var test = FindBox(b.EditorPanel, "Condition test: a group name or number, or the look-around pattern");
                var yes = FindBox(b.EditorPanel, "Branch used when the condition holds");
                var no = FindBox(b.EditorPanel, "Branch used when it does not, empty for none");
                if (test == null || yes == null || no == null) return "no conditional editor: " + RxFirstText(card, t => t.StartsWith("Selected") || t.StartsWith("SELECTED"));
                if (test.Text != "1" || yes.Text != "b" || no.Text != "c") return "editor fields: " + test.Text + " / " + yes.Text + " / " + no.Text;
                RxShot(card, shots, "regex-build-conditional-editor");
                if (!Press(b.EditorPanel, "Swap branches")) return "no swap button";
                if (yes.Text != "c" || no.Text != "b") return "swap did not exchange the branches";
                if (!Press(b.EditorPanel, "Apply to the pattern")) return "no apply button";
                await Task.Delay(300);
                if (f.State.Query != "(a)?(?(1)c|b)") return "swap applied as " + f.State.Query;
                cond = RegexTree.Walk(b.CurrentTree.Root).First(n => n.Kind == RegexTree.NodeKind.Conditional);
                b.PatternBox.Focus(); b.PatternBox.Select(cond.Start, cond.End - cond.Start); await Task.Delay(300);
                if (!Press(b.EditorPanel, "Look-ahead")) return "no look-ahead chip";
                FindBox(b.EditorPanel, "Condition test: a group name or number, or the look-around pattern").Text = @"\d";
                FindBox(b.EditorPanel, "Branch used when the condition holds").Text = @"\d+";
                FindBox(b.EditorPanel, "Branch used when it does not, empty for none").Text = "";
                Press(b.EditorPanel, "Apply to the pattern"); await Task.Delay(300);
                if (f.State.Query != @"(a)?(?(?=\d)\d+)") return "look-ahead applied as " + f.State.Query;
                // a test naming a group the pattern does not have is refused, and the pattern is unchanged
                cond = RegexTree.Walk(b.CurrentTree.Root).First(n => n.Kind == RegexTree.NodeKind.Conditional);
                b.PatternBox.Focus(); b.PatternBox.Select(cond.Start, cond.End - cond.Start); await Task.Delay(300);
                Press(b.EditorPanel, "Group has matched");
                FindBox(b.EditorPanel, "Condition test: a group name or number, or the look-around pattern").Text = "7";
                Press(b.EditorPanel, "Apply to the pattern"); await Task.Delay(300);
                if (f.State.Query != @"(a)?(?(?=\d)\d+)") return "a missing group was applied: " + f.State.Query;
                return RxAllText(card).Contains("Not applied: the test must name a group") ? null : "no refusal message";
            });

            await CheckAsync("regex parity ui build: a real mouse drag posted to the builder window swaps two parts of the pattern", async () =>
            {
                f.State.SetQuery("a+b*c"); await Task.Delay(400);
                var kids = b.CurrentTree.Root.Children;
                var from = b.RowCentreInWindow(kids[0]);
                var to = b.RowCentreInWindow(kids[2]);
                var src = PresentationSource.FromVisual(card) as HwndSource;
                if (from == null || to == null || src == null) return "rows or window not found";
                var seen = new List<string>();
                System.Windows.Input.MouseButtonEventHandler down = (o, e) => seen.Add("down@" + e.GetPosition(card));
                System.Windows.Input.MouseEventHandler move = (o, e) => seen.Add("move@" + e.GetPosition(card));
                System.Windows.Input.MouseButtonEventHandler up = (o, e) => seen.Add("up@" + e.GetPosition(card));
                card.PreviewMouseLeftButtonDown += down; card.PreviewMouseMove += move; card.PreviewMouseLeftButtonUp += up;
                try { await PostDragAsync(src.Handle, from.Value, to.Value); await Task.Delay(300); }
                finally { card.PreviewMouseLeftButtonDown -= down; card.PreviewMouseMove -= move; card.PreviewMouseLeftButtonUp -= up; }
                if (f.State.Query != "b*ca+") return "the drag gave " + f.State.Query + " (from " + from + " to " + to + ", hwnd " + src.Handle + ", events: " + string.Join(" ", seen.Take(20)) + ")";
                // a press and release on the same row is a click, not a move
                kids = b.CurrentTree.Root.Children;
                var same = b.RowCentreInWindow(kids[1]).Value;
                await PostDragAsync(src.Handle, same, same, 1);
                if (f.State.Query != "b*ca+") return "a click moved a part: " + f.State.Query;
                // a part dropped on a row at another level is refused with a message
                f.State.SetQuery("(ab)c"); await Task.Delay(400);
                var grp = b.CurrentTree.Root.Children[0];
                var inner = RegexTree.Walk(grp).First(n => n.Kind == RegexTree.NodeKind.Literal);
                await PostDragAsync(src.Handle, b.RowCentreInWindow(inner).Value, b.RowCentreInWindow(b.CurrentTree.Root.Children[1]).Value);
                return f.State.Query == "(ab)c" ? null : "a cross-level drop moved: " + f.State.Query;
            });

            f.OpenBuilder("Debug"); await Task.Delay(400);
            await CheckAsync("regex parity ui debug: a conditional with inline options traces in the Debug view, checked against .NET", async () =>
            {
                f.State.SetQuery("(?i)(a)?(?(1)B|c)"); await Task.Delay(250);
                var subject = FindBox(card, "Subject text for the step-by-step trace");
                if (subject == null) return "no subject box";
                subject.Text = "xC ab"; await Task.Delay(400);
                var text = RxAllText(card);
                if (!text.Contains("Checked against the .NET engine: same result (match at 1, length 1)")) return "status: " + RxFirstText(card, t => t.Contains(".NET") || t.Contains("trace"));
                for (int i = 0; i < 12; i++) Press(card, "Next");
                await Task.Delay(200);
                RxShot(card, shots, "regex-debug-conditional-trace");
                return RxAllText(card).Contains("Step ") ? null : "no step text";
            });

            f.OpenBuilder("Files"); await Task.Delay(400);
            await CheckAsync("regex parity ui files: archive and binary chips, a file type rule and the search results in the Files view", async () =>
            {
                var root = Path.Combine(work, "ui-gap-files"); if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "notes.txt"), "needle\n");
                File.WriteAllBytes(Path.Combine(root, "data.bin"), new byte[] { 0, 0x6E, 0x65, 0x65, 0x64, 0x6C, 0x65, 0 });
                File.WriteAllBytes(Path.Combine(root, "wide.u16"), Encoding.Unicode.GetBytes("needle\n"));
                using (var zip = ZipFile.Open(Path.Combine(root, "pack.zip"), ZipArchiveMode.Create))
                using (var w = new StreamWriter(zip.CreateEntry("in/readme.md").Open())) w.Write("needle in zip\n");
                settings.RegexFileTypes = new List<RegexFileType>();
                f.State.SetQuery("needle");
                f.OpenBuilder("Files"); await Task.Delay(400);
                FindBox(card, "Folder to search").Text = root;
                FindBox(card, "Regular expression for file names").Text = "";
                if (!Press(card, "Search inside archives")) return "no archive chip";
                if (!Press(card, "Binary files as bytes")) return "no binary chip";
                FindBox(card, "Extension for a new file type rule, such as .log").Text = "u16";
                if (!Press(card, "Add file type")) return "no add button";
                await Task.Delay(250);
                if (settings.RegexFileTypes.Count != 1 || settings.RegexFileTypes[0].Extension != ".u16") return "rule not stored";
                Press(card, "Encoding: Auto"); await Task.Delay(200);
                Press(card, "Encoding: UTF-8"); await Task.Delay(200);
                if (settings.RegexFileTypes[0].Encoding != "UTF-16 LE" || FindButton(card, "Encoding: UTF-16 LE") == null) return "encoding cycle: " + settings.RegexFileTypes[0].Encoding;
                FindBox(card, "Extension for a new file type rule, such as .log").Text = ".U16";
                Press(card, "Add file type"); await Task.Delay(200);
                if (settings.RegexFileTypes.Count != 1 || !RxAllText(card).Contains("There is already a rule for .u16.")) return "a duplicate rule was added";
                Press(card, "Search files");
                for (int i = 0; i < 80 && !RxAllText(card).Contains("archive(s) opened"); i++) await Task.Delay(200);
                await Task.Delay(400); Window.GetWindow(card)?.UpdateLayout(); card.UpdateLayout();
                // rows built from inline runs: read their runs, not TextBlock.Text
                var text = RxAllText(card) + " | " + string.Join(" | ", Descend<TextBlock>(card).Where(t => t.Inlines.Count > 0).Select(t => new System.Windows.Documents.TextRange(t.ContentStart, t.ContentEnd).Text));
                if (!text.Contains("4 match(es) in 4 of 4 file(s) scanned · 1 archive(s) opened")) return "summary: " + RxFirstText(card, t => t.Contains("match(es) in"));
                if (!text.Contains("pack.zip!/in/readme.md") || !text.Contains("0x00000001") || !text.Contains("6E 65 65 64 6C 65") || !text.Contains("UTF-16 LE (file type setting)")) return "results: " + text[^Math.Min(text.Length, 600)..];
                RxShot(card, shots, "regex-files-archives-binary-types");
                Press(card, "Replace in files..."); await Task.Delay(300);
                if (!RxAllText(card).Contains("2 file(s) with matches inside archives or searched as bytes are not changed")) return "the preview does not say archive and binary matches stay unchanged";
                Press(card, "Back to results"); await Task.Delay(200);
                return null;
            });
        }

        /// <summary>MT_ONLY=regexgap: only the checks in this file, for a quick loop.</summary>
        internal static async Task RegexGapOnlyAsync(string dir)
        {
            var work = Path.Combine(dir, "regex-parity");
            Directory.CreateDirectory(work);
            RegexParityGapChecks(work);
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show(); await Task.Delay(900);
                cc.Navigate("overview", animate: false); await Task.Delay(400);
                var f = cc.PageSearch;
                f.State.SetRegex(true); f.State.SetQuery("a");
                Settings.Current.RegexDefaultTab = "Build";
                f.OpenBuilder("Build"); await Task.Delay(500);
                await RegexParityGapUiChecksAsync(f, f.Builder, f.BuilderCard, work, dir);
                try { f.State.SetQuery(""); f.State.SetRegex(false); f.Builder.Close(); } catch { }
            }
            finally { Settings.Current.RegexFileTypes = new List<RegexFileType>(); cc.Close(); }
        }

        /// <summary>
        /// MT_ONLY=regexdrag: opens the builder on the Build view with a+b*c and waits for an outside tool to drag the first tree row onto the third.
        /// It writes drag-ready.json with the builder window handle and the two row centres (client pixels), then records whether the pattern became b*ca+.
        /// </summary>
        internal static async Task RegexDragProbeAsync(string dir)
        {
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show(); await Task.Delay(900);
                cc.Navigate("overview", animate: false); await Task.Delay(400);
                var f = cc.PageSearch;
                f.State.SetRegex(true);
                f.State.SetQuery("a+b*c");
                Settings.Current.RegexDefaultTab = "Build";
                f.OpenBuilder("Build"); await Task.Delay(800);
                var b = f.Builder;
                var kids = b.CurrentTree.Root.Children;
                var from = b.RowCentreInWindow(kids[0]).Value; var to = b.RowCentreInWindow(kids[2]).Value;
                var src = (HwndSource)PresentationSource.FromVisual(f.BuilderCard);
                File.WriteAllText(Path.Combine(dir, "drag-ready.json"), JsonSerializer.Serialize(new { hwnd = src.Handle.ToInt64(), fromX = (int)from.X, fromY = (int)from.Y, toX = (int)to.X, toY = (int)to.Y, pattern = f.State.Query }));
                var seen = new List<string>();
                var card = f.BuilderCard;
                card.PreviewMouseLeftButtonDown += (o, e) => seen.Add("down@" + e.GetPosition(card));
                card.PreviewMouseMove += (o, e) => seen.Add("move@" + e.GetPosition(card));
                card.PreviewMouseLeftButtonUp += (o, e) => seen.Add("up@" + e.GetPosition(card));
                card.MouseLeave += (o, e) => seen.Add("leave");
                b.DragLog.Clear();
                await CheckAsync("regex parity ui drag: an outside mouse drag on the builder window swaps a+ and c", async () =>
                {
                    for (int i = 0; i < 600 && f.State.Query == "a+b*c"; i++) await Task.Delay(100);
                    return f.State.Query == "b*ca+" ? null : "pattern is " + f.State.Query + "; events: " + string.Join(" ", seen.Take(40)) + "; hook: " + string.Join(" ", b.DragLog);
                });
                File.WriteAllText(Path.Combine(dir, "drag-done.txt"), f.State.Query);
                await Task.Delay(10000);   // leave the result on screen for a capture
                try { f.State.SetQuery(""); f.State.SetRegex(false); b.Close(); } catch { }
            }
            finally { cc.Close(); }
        }
    }
}
