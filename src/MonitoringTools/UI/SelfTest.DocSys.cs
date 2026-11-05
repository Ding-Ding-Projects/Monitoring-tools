using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Core.DocTools;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    // The archive, subtitle, font and disc image dialogs: each is driven through its real window on files the check creates.
    // No font is installed (the install, uninstall and "is it installed" steps are replaced by a recorder), and a disc image is
    // mounted for real only when the image was built here and is dismounted again in a finally block.
    public static partial class SelfTest
    {
        private static readonly List<string> DocLanguageGaps = new List<string>();

        private static string DocTmp(string name)
        {
            var p = Path.Combine(Settings.DataDir, "docsys", name);
            try { if (Directory.Exists(p)) Directory.Delete(p, true); } catch { }
            Directory.CreateDirectory(p);
            return p;
        }

        private static string Sha(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)); }

        private static async Task DocAudit(string where, Window w)
        {
            var saved = Settings.Current.LanguageMode;
            try
            {
                Settings.Current.LanguageMode = LanguageMode.Cantonese; Language.Refresh();
                await Task.Delay(450);
                LanguageAudit.Reset(); LanguageAudit.Scan(where, w);
                foreach (var f in LanguageAudit.Findings.Where(x => !DocTestData(x.Text)).Take(15)) DocLanguageGaps.Add(where + ": \"" + f.Text.Replace("\n", " / ") + "\"");
            }
            finally { Settings.Current.LanguageMode = saved; Language.Refresh(); await Task.Delay(200); }
        }

        // Words the check itself put into the files (cue text, folder names, a mock mount reason), the sample specimen, and what the font
        // file says about itself: data the person owns, not wording of the app.
        private static bool DocTestData(string text)
        {
            foreach (var k in new[] { "Aa Gg 123", "Quick brown fox", "Sphinx of black quartz", "Microsoft supplied font", "Welcome back to the show", "Today we are fixing", "First, the hinge", "Now the latch", "Line that runs long", "Starts before the last", "network share that does not allow mounting", "SOURCES", "assets", "project", "src" })
                if (text.Contains(k) && !text.Contains("Collapse") && !text.Contains("Expand")) return true;
            return text == "Fine" || text.StartsWith("Arial ·") || text.StartsWith("Arial (") || text.Contains("Arial · Regular");
        }

        private static async Task Pump(int ms = 250) { await Task.Delay(ms); }

        private static async Task<bool> UntilDocState(Func<bool> done, int ms = 15000) => await WaitFor(done, ms);

        // ------------------------------------------------------------------ a small ISO 9660 writer

        internal static void BuildIso(string path, string label, params (string Path, byte[] Data)[] files)
        {
            const int S = 2048;
            var dirs = files.Where(f => f.Path.Contains('/')).Select(f => f.Path.Split('/')[0]).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            int rootSec = 20; int Sec(string d) => 21 + dirs.IndexOf(d);
            int next = 21 + dirs.Count;
            var extents = new Dictionary<string, int>();
            foreach (var f in files) { extents[f.Path] = next; next += Math.Max(1, (f.Data.Length + S - 1) / S); }
            int total = next;
            var img = new byte[total * S];
            void PutLE32(int off, uint v) { BitConverter.GetBytes(v).CopyTo(img, off); }
            void Both32(byte[] b, int off, uint v) { BitConverter.GetBytes(v).CopyTo(b, off); var be = BitConverter.GetBytes(v); Array.Reverse(be); be.CopyTo(b, off + 4); }
            void Both16(byte[] b, int off, ushort v) { BitConverter.GetBytes(v).CopyTo(b, off); var be = BitConverter.GetBytes(v); Array.Reverse(be); be.CopyTo(b, off + 2); }
            byte[] Rec(int extent, int size, byte flags, byte[] name)
            {
                int len = 33 + name.Length + (name.Length % 2 == 0 ? 1 : 0);
                var r = new byte[len]; r[0] = (byte)len;
                Both32(r, 2, (uint)extent); Both32(r, 10, (uint)size);
                r[18] = 126; r[19] = 1; r[20] = 1;
                r[25] = flags; Both16(r, 28, 1); r[32] = (byte)name.Length; name.CopyTo(r, 33);
                return r;
            }
            void WriteDir(int sector, int selfSec, int parentSec, IEnumerable<byte[]> children)
            {
                int p = sector * S;
                foreach (var rec in new[] { Rec(selfSec, S, 2, new byte[] { 0 }), Rec(parentSec, S, 2, new byte[] { 1 }) }.Concat(children)) { rec.CopyTo(img, p); p += rec.Length; }
            }
            byte[] Name(string n) => Encoding.ASCII.GetBytes(n);
            var rootKids = new List<(string name, byte[] rec)>();
            foreach (var d in dirs) rootKids.Add((d, Rec(Sec(d), S, 2, Name(d))));
            foreach (var f in files.Where(f => !f.Path.Contains('/'))) rootKids.Add((f.Path, Rec(extents[f.Path], f.Data.Length, 0, Name(f.Path + ";1"))));
            WriteDir(rootSec, rootSec, rootSec, rootKids.OrderBy(k => k.name, StringComparer.Ordinal).Select(k => k.rec));
            foreach (var d in dirs)
            {
                var kids = files.Where(f => f.Path.StartsWith(d + "/")).Select(f => (name: f.Path[(d.Length + 1)..], rec: Rec(extents[f.Path], f.Data.Length, 0, Name(f.Path[(d.Length + 1)..] + ";1"))));
                WriteDir(Sec(d), Sec(d), rootSec, kids.OrderBy(k => k.name, StringComparer.Ordinal).Select(k => k.rec));
            }
            foreach (var f in files) f.Data.CopyTo(img, extents[f.Path] * S);

            // path tables: root, then each directory (parent is the root, number 1)
            byte[] PathTable(bool little)
            {
                var ms = new MemoryStream();
                void Entry(int ext, int parent, byte[] name)
                {
                    ms.WriteByte((byte)name.Length); ms.WriteByte(0);
                    var e = BitConverter.GetBytes((uint)ext); var pa = BitConverter.GetBytes((ushort)parent);
                    if (!little) { Array.Reverse(e); Array.Reverse(pa); }
                    ms.Write(e); ms.Write(pa); ms.Write(name);
                    if (name.Length % 2 == 1) ms.WriteByte(0);
                }
                Entry(rootSec, 1, new byte[] { 0 });
                foreach (var d in dirs) Entry(Sec(d), 1, Name(d));
                return ms.ToArray();
            }
            var lt = PathTable(true); var mt = PathTable(false);
            lt.CopyTo(img, 18 * S); mt.CopyTo(img, 19 * S);

            // primary volume descriptor, then the terminator
            var pvd = new byte[S];
            pvd[0] = 1; Encoding.ASCII.GetBytes("CD001").CopyTo(pvd, 1); pvd[6] = 1;
            for (int i = 8; i < 72; i++) pvd[i] = 0x20;
            Encoding.ASCII.GetBytes(label.PadRight(32)).CopyTo(pvd, 40);
            Both32(pvd, 80, (uint)total); Both16(pvd, 120, 1); Both16(pvd, 124, 1); Both16(pvd, 128, S);
            Both32(pvd, 132, (uint)lt.Length);
            BitConverter.GetBytes(18u).CopyTo(pvd, 140);
            var m = BitConverter.GetBytes(19u); Array.Reverse(m); m.CopyTo(pvd, 148);
            Rec(rootSec, S, 2, new byte[] { 0 }).CopyTo(pvd, 156);
            for (int i = 190; i < 813; i++) pvd[i] = 0x20;
            foreach (var off in new[] { 813, 830, 847, 864 }) { Encoding.ASCII.GetBytes("2026010100000000").CopyTo(pvd, off); pvd[off + 16] = 0; }
            pvd[881] = 1;
            pvd.CopyTo(img, 16 * S);
            var term = new byte[S]; term[0] = 255; Encoding.ASCII.GetBytes("CD001").CopyTo(term, 1); term[6] = 1; term.CopyTo(img, 17 * S);
            File.WriteAllBytes(path, img);
        }

        // ------------------------------------------------------------------ the checks

        private static async Task DocSysChecksAsync(string dir)
        {
            DocLanguageGaps.Clear();
            // --- the toast buttons
            await CheckAsync("docsys toast buttons: a copied zip, subtitle, font and disc image each get the matching button, and it opens the matching dialog", async () =>
            {
                var t = DocTmp("toast");
                var zip = Path.Combine(t, "a.zip"); using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) z.CreateEntry("x.txt");
                var srt = Path.Combine(t, "a.srt"); File.WriteAllText(srt, "1\r\n00:00:01,000 --> 00:00:02,000\r\nHi\r\n");
                var iso = Path.Combine(t, "a.iso"); BuildIso(iso, "TOAST", ("HELLO.TXT", Encoding.ASCII.GetBytes("hello")));
                var fontSrc = FindSystemFont(); if (fontSrc == null) return "no system font to copy";
                var ttf = Path.Combine(t, "a.ttf"); File.Copy(fontSrc, ttf);
                var cases = new (string File, string Label, Func<Window> Current)[]
                {
                    (zip, "Extract…", () => ExtractArchiveDialog.Current), (srt, "Subtitle timing…", () => SubtitleTimingDialog.Current),
                    (ttf, "Preview font…", () => FontInstallDialog.Current), (iso, "Disc image…", () => DiscImageDialog.Current)
                };
                FontTools.InstalledHook = _ => false;
                try
                {
                    foreach (var c in cases)
                    {
                        var actions = ActionProvider.For(ClipItem.FromFiles(new[] { c.File }));
                        var mine = actions.Where(a => a.Label == c.Label).ToList();
                        if (mine.Count != 1) return $"expected one \"{c.Label}\" button for {Path.GetFileName(c.File)}, found {mine.Count}: " + string.Join(" | ", actions.Select(a => a.Label));
                        mine[0].Run(); await Pump(500);
                        var w = c.Current();
                        if (w == null || !w.IsVisible) return $"the \"{c.Label}\" button opened no window";
                        w.Close();
                    }
                    var two = ActionProvider.For(ClipItem.FromFiles(new[] { zip, srt }));
                    if (two.Any(a => a.Label == "Extract…" || a.Label == "Subtitle timing…")) return "several files must not get a single-file dialog button";
                    var txt = Path.Combine(t, "a.txt"); File.WriteAllText(txt, "x");
                    if (ActionProvider.For(ClipItem.FromFiles(new[] { txt })).Any(a => a.Label is "Extract…" or "Subtitle timing…" or "Preview font…" or "Disc image…")) return "a plain text file got a dialog button";
                    return null;
                }
                finally { FontTools.InstalledHook = null; }
            });

            await ArchiveDocChecksAsync();
            await SubtitleDocChecksAsync();
            await FontDocChecksAsync();
            await DiscDocChecksAsync();

            Check("language: every visible string in the archive, subtitle, font and disc image dialogs has a Cantonese entry", () =>
                DocLanguageGaps.Count == 0 ? null : DocLanguageGaps.Count + " untranslated: " + string.Join(" | ", DocLanguageGaps.Take(500)));
        }

        private static string FindSystemFont()
        {
            foreach (var n in new[] { "arial.ttf", "segoeui.ttf", "calibri.ttf", "tahoma.ttf", "verdana.ttf" })
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), n);
                if (File.Exists(p)) return p;
            }
            return Directory.EnumerateFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "*.ttf").FirstOrDefault();
        }

        // ------------------------------------------------------------------ archives

        private static async Task ArchiveDocChecksAsync()
        {
            await CheckAsync("archive dialog: a zip is listed with a tree, selection and search; Extract writes the chosen files, refuses every zip-slip path and says so", async () =>
            {
                var t = DocTmp("zip");
                var zip = Path.Combine(t, "project-v2.zip");
                using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
                {
                    void Add(string name, string text) { var e = z.CreateEntry(name); using var w = new StreamWriter(e.Open()); w.Write(text); }
                    Add("project/src/main.cs", "MAIN"); Add("project/src/util.cs", "UTIL"); Add("project/assets/logo.svg", "<svg/>"); Add("notes.txt", "NOTES");
                    Add("../evil.txt", "EVIL1"); Add("..\\evil2.txt", "EVIL2"); Add("C:/evil3.txt", "EVIL3");
                }
                var dest = Path.Combine(t, "out");
                var d = ExtractArchiveDialog.Open(zip);
                try
                {
                    await Pump(500);
                    if (d.State != ArchiveState.Ready) return "state is " + d.State;
                    if (d.Info.FileCount != 7) return "listed " + d.Info.FileCount + " files, expected 7";
                    if (d.Info.Entries.Count(e => e.Unsafe) != 3) return "unsafe entries flagged: " + d.Info.Entries.Count(e => e.Unsafe);
                    if (d.Tree.SelectedCount != 7) return "everything should start selected: " + d.Tree.SelectedCount;
                    var text = Drive.TextOf(d);
                    if (!text.Contains("7 of 7 selected")) return "the selection line is missing: " + text[..Math.Min(300, text.Length)];
                    if (!text.Contains("unsafe path, will be refused")) return "unsafe entries are not marked in the list";
                    // a folder's check toggles everything under it, and shows mixed when only some are on
                    d.Tree.ToggleForTest("project/assets");
                    if (d.Tree.FolderState("project/assets") != false || d.Tree.SelectedCount != 6) return "unchecking a folder did not clear its files";
                    d.Tree.ToggleForTest("project/src/util.cs");
                    if (d.Tree.FolderState("project/src") != null) return "a folder with some files off should be mixed";
                    d.Tree.ToggleForTest("project/src/util.cs");
                    // search
                    d.Search.State.SetQuery("util"); await Pump(200);
                    if (d.Tree.MatchCount != 1) return "search for util matched " + d.Tree.MatchCount;
                    d.Search.State.SetQuery(""); await Pump(150);
                    d.Tree.ToggleForTest("project/assets");
                    await DocAudit("archive ready", d);
                    d.SetDestinationForTest(dest);
                    d.StartForTest();
                    if (!await UntilDocState(() => d.State == ArchiveState.Done)) return "never reached Done, state " + d.State;
                    await DocAudit("archive done", d);
                    var r = d.Result;
                    if (r.Error != null) return "error: " + r.Error;
                    if (r.Extracted != 4 || r.Refused.Count != 3) return $"extracted {r.Extracted} refused {r.Refused.Count}";
                    if (File.ReadAllText(Path.Combine(dest, "project", "src", "main.cs")) != "MAIN" || File.ReadAllText(Path.Combine(dest, "notes.txt")) != "NOTES") return "content wrong";
                    if (File.Exists(Path.Combine(dest, "..", "evil.txt")) || File.Exists(Path.Combine(t, "evil2.txt")) || File.Exists(Path.Combine(dest, "evil.txt")) || File.Exists("C:\\evil3.txt")) return "a zip-slip entry was written";
                    if (Directory.EnumerateFiles(dest, "*.part", SearchOption.AllDirectories).Any()) return "a .part file was left behind";
                    var done = Drive.TextOf(d);
                    if (!done.Contains("Extracted 4 files") || !done.Contains("3 unsafe paths")) return "the Done card does not say what happened: " + done[..Math.Min(400, done.Length)];
                    if (Drive.Find<System.Windows.Controls.Primitives.ButtonBase>(d, "Delete the archive") == null || Drive.Find<System.Windows.Controls.Primitives.ButtonBase>(d, "Open folder") == null) return "the Done actions are missing";
                    return null;
                }
                finally { d?.Close(); }
            });

            await CheckAsync("archive dialog: Keep both numbers a name that exists, Replace overwrites it and Skip leaves it", async () =>
            {
                var t = DocTmp("zip-rules");
                var zip = Path.Combine(t, "r.zip");
                using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) { var e = z.CreateEntry("a.txt"); using var w = new StreamWriter(e.Open()); w.Write("FROM-ZIP"); }
                var dest = Path.Combine(t, "out"); Directory.CreateDirectory(dest);
                var target = Path.Combine(dest, "a.txt");
                foreach (var (rule, expectMain, expectRenamed, expectSkipped) in new[] { (OverwriteRule.KeepBoth, "MINE", 1, 0), (OverwriteRule.Replace, "FROM-ZIP", 0, 0), (OverwriteRule.Skip, "MINE", 0, 1) })
                {
                    File.WriteAllText(target, "MINE");
                    foreach (var f in Directory.GetFiles(dest, "a (*.txt")) File.Delete(f);
                    var d = ExtractArchiveDialog.Open(zip);
                    try
                    {
                        await Pump(300);
                        d.SetDestinationForTest(dest); d.SetRuleForTest(rule);
                        d.StartForTest();
                        if (!await UntilDocState(() => d.State == ArchiveState.Done)) return rule + ": never done";
                        var r = d.Result;
                        if (File.ReadAllText(target) != expectMain) return $"{rule}: a.txt holds {File.ReadAllText(target)}";
                        if (r.Renamed.Count != expectRenamed || r.Skipped != expectSkipped) return $"{rule}: renamed {r.Renamed.Count} skipped {r.Skipped}";
                        if (rule == OverwriteRule.KeepBoth && File.ReadAllText(Path.Combine(dest, "a (1).txt")) != "FROM-ZIP") return "Keep both did not write a (1).txt";
                    }
                    finally { d?.Close(); }
                }
                return null;
            });

            await CheckAsync("archive dialog: a password zip asks for the password, a wrong one extracts nothing and says so, the right one extracts", async () =>
            {
                var t = DocTmp("zip-pw");
                var zip = Path.Combine(t, "taxes-2024.zip");
                ArchiveTools.WritePasswordZip(zip, "hunter2", ("a.txt", Encoding.UTF8.GetBytes("SECRET-A")), ("dir/b.txt", Encoding.UTF8.GetBytes("SECRET-B")));
                var dest = Path.Combine(t, "out");
                var d = ExtractArchiveDialog.Open(zip);
                try
                {
                    await Pump(400);
                    if (d.State != ArchiveState.Password) return "state is " + d.State + " for an encrypted zip";
                    var text = Drive.TextOf(d);
                    if (!text.Contains("This archive needs a password")) return "no password banner: " + text;
                    await DocAudit("archive password", d);
                    d.SetDestinationForTest(dest);
                    d.StartForTest("wrong-password");
                    await Pump(300);
                    if (d.State != ArchiveState.Password) return "a wrong password left state " + d.State;
                    if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any()) return "a wrong password extracted something";
                    if (!Drive.TextOf(d).Contains("Wrong password for this archive. Nothing was extracted.")) return "no wrong-password message: " + Drive.TextOf(d);
                    await DocAudit("archive wrong password", d);
                    d.SetDestinationForTest(dest);
                    d.StartForTest("hunter2");
                    if (!await UntilDocState(() => d.State == ArchiveState.Done)) return "never done after the right password, state " + d.State + ": " + d.Result?.Error;
                    if (File.ReadAllText(Path.Combine(dest, "a.txt")) != "SECRET-A" || File.ReadAllText(Path.Combine(dest, "dir", "b.txt")) != "SECRET-B") return "decrypted content is wrong";
                    return null;
                }
                finally { d?.Close(); }
            });

            await CheckAsync("archive dialog: tar, tar.gz and gz are listed and extracted, a tar path that escapes the folder is refused, and 7z reports that it needs 7-Zip", async () =>
            {
                var t = DocTmp("tar");
                byte[] B(string s) => Encoding.UTF8.GetBytes(s);
                void WriteTar(Stream s, bool evil)
                {
                    using var w = new TarWriter(s, TarEntryFormat.Ustar, leaveOpen: false);
                    void Add(string name, string text) { w.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(B(text)) }); }
                    Add("docs/readme.txt", "README"); Add("docs/deep/more.txt", "MORE"); if (evil) Add("../escape.txt", "ESCAPE");
                }
                var tar = Path.Combine(t, "plain.tar"); WriteTar(File.Create(tar), true);
                var tgz = Path.Combine(t, "bundle.tar.gz"); WriteTar(new GZipStream(File.Create(tgz), CompressionLevel.Optimal), false);
                var gz = Path.Combine(t, "notes.txt.gz"); using (var o = new GZipStream(File.Create(gz), CompressionLevel.Optimal)) o.Write(B("GZIPPED"));
                var seven = Path.Combine(t, "old.7z"); File.WriteAllBytes(seven, new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0, 4, 0, 0, 0, 0 });
                // tar with an escaping path
                var d1 = ExtractArchiveDialog.Open(tar);
                try
                {
                    await Pump(300);
                    if (d1.State != ArchiveState.Ready || d1.Info.FileCount != 3) return $"tar: state {d1.State}, files {d1.Info.FileCount}";
                    var o1 = Path.Combine(t, "out-tar"); d1.SetDestinationForTest(o1); d1.StartForTest();
                    if (!await UntilDocState(() => d1.State == ArchiveState.Done)) return "tar never done";
                    if (d1.Result.Extracted != 2 || d1.Result.Refused.Count != 1) return $"tar extracted {d1.Result.Extracted} refused {d1.Result.Refused.Count}";
                    if (File.ReadAllText(Path.Combine(o1, "docs", "deep", "more.txt")) != "MORE" || File.Exists(Path.Combine(t, "escape.txt"))) return "tar content or escape wrong";
                }
                finally { d1?.Close(); }
                var d2 = ExtractArchiveDialog.Open(tgz);
                try
                {
                    await Pump(300);
                    if (d2.State != ArchiveState.Ready || d2.Info.Format != "tar.gz" || d2.Info.FileCount != 2) return $"tar.gz: state {d2.State}, format {d2.Info.Format}, files {d2.Info.FileCount}";
                    var o2 = Path.Combine(t, "out-tgz"); d2.SetDestinationForTest(o2); d2.StartForTest();
                    if (!await UntilDocState(() => d2.State == ArchiveState.Done)) return "tar.gz never done";
                    if (File.ReadAllText(Path.Combine(o2, "docs", "readme.txt")) != "README") return "tar.gz content wrong";
                }
                finally { d2?.Close(); }
                var d3 = ExtractArchiveDialog.Open(gz);
                try
                {
                    await Pump(300);
                    if (d3.State != ArchiveState.Ready || d3.Info.FileCount != 1 || d3.Info.Entries[0].Path != "notes.txt") return $"gz: state {d3.State}, files {d3.Info.FileCount}";
                    var o3 = Path.Combine(t, "out-gz"); d3.SetDestinationForTest(o3); d3.StartForTest();
                    if (!await UntilDocState(() => d3.State == ArchiveState.Done)) return "gz never done";
                    if (File.ReadAllText(Path.Combine(o3, "notes.txt")) != "GZIPPED") return "gz content wrong";
                }
                finally { d3?.Close(); }
                var d4 = ExtractArchiveDialog.Open(seven);
                try
                {
                    await Pump(300);
                    if (d4.State != ArchiveState.CannotRead) return "7z state is " + d4.State;
                    var text = Drive.TextOf(d4);
                    if (!text.Contains("needs 7-Zip")) return "the 7z state does not say 7-Zip is needed: " + text;
                    await DocAudit("archive cannot read", d4);
                }
                finally { d4?.Close(); }
                return null;
            });

            Check("archive engine: Cancel stops before any file is written, removes the .part file and reports Canceled", () =>
            {
                var t = DocTmp("zip-cancel");
                var zip = Path.Combine(t, "c.zip");
                using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) { var e = z.CreateEntry("big.bin", CompressionLevel.NoCompression); using var s = e.Open(); s.Write(new byte[3_000_000]); }
                var info = ArchiveTools.Open(zip);
                using var cts = new CancellationTokenSource(); cts.Cancel();
                var dest = Path.Combine(t, "out");
                var r = ArchiveTools.Extract(info, info.Entries, dest, OverwriteRule.KeepBoth, null, null, cts.Token);
                if (!r.Canceled) return "Canceled was not reported";
                if (Directory.Exists(dest) && Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories).Any()) return "files were left behind: " + string.Join(", ", Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories));
                // and a cancel during a file leaves no .part
                using var cts2 = new CancellationTokenSource();
                var prog = new Progress<ExtractProgress>();
                var r2 = ArchiveTools.Extract(info, info.Entries, dest, OverwriteRule.KeepBoth, null, new SyncProgress(p => cts2.Cancel()), cts2.Token);
                if (!r2.Canceled) return "a cancel during a file was not reported";
                if (Directory.Exists(dest) && Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories).Any()) return "a half written file was left: " + string.Join(", ", Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories));
                return null;
            });
        }

        private sealed class SyncProgress : IProgress<ExtractProgress> { private readonly Action<ExtractProgress> _a; public SyncProgress(Action<ExtractProgress> a) { _a = a; } public void Report(ExtractProgress p) => _a(p); }

        // ------------------------------------------------------------------ subtitles

        private const string SampleSrt =
            "1\r\n00:00:04,200 --> 00:00:06,800\r\nWelcome back to the show.\r\n\r\n" +
            "2\r\n00:00:07,000 --> 00:00:10,400\r\nToday we are fixing the garden gate.\r\n\r\n" +
            "3\r\n00:00:10,900 --> 00:00:13,300\r\nFirst, the hinge.\r\n\r\n" +
            "4\r\n00:00:20,000 --> 00:00:22,000\r\nNow the latch.\r\n";

        private static async Task SubtitleDocChecksAsync()
        {
            await CheckAsync("subtitle dialog: Shift moves every cue by the typed seconds, never changes the original, and a move before zero is blocked with the cue named", async () =>
            {
                var t = DocTmp("srt-shift");
                var src = Path.Combine(t, "episode-04.srt"); File.WriteAllText(src, SampleSrt);
                var before = Sha(src);
                var d = SubtitleTimingDialog.Open(src);
                try
                {
                    await Pump(400);
                    if (d.File.Cues.Count != 4) return "parsed " + d.File.Cues.Count + " cues";
                    if (!d.SummaryText.Contains("4 cues")) return "summary: " + d.SummaryText;
                    d.SetShift("-10"); await Pump(150);
                    if (d.SaveEnabled) return "a move before zero was allowed";
                    if (!d.StatusText.Contains("Cue 1 would start before")) return "no named cue in the status: " + d.StatusText;
                    d.SetShift("+2.400"); await Pump(150);
                    if (!d.SaveEnabled) return "a fine shift was blocked: " + d.StatusText;
                    if (!d.StatusText.Contains("4 cues change")) return "status: " + d.StatusText;
                    await DocAudit("subtitle shift", d);
                    d.SaveNow(); await Pump(200);
                    var outPath = d.SavedPath;
                    if (outPath == null || Path.GetFileName(outPath) != "episode-04.shifted.srt") return "saved as " + outPath;
                    var saved = SubtitleTools.Read(outPath);
                    var orig = SubtitleTools.Read(src);
                    for (int i = 0; i < 4; i++) if (saved.Cues[i].Start != orig.Cues[i].Start + 2400 || saved.Cues[i].End != orig.Cues[i].End + 2400) return "cue " + (i + 1) + " is wrong";
                    if (Sha(src) != before) return "the original file was changed";
                    if (!File.ReadAllText(outPath).Contains("00:00:06,600 --> 00:00:09,200")) return "the file does not carry the shifted first cue";
                    return null;
                }
                finally { d?.Close(); }
            });

            await CheckAsync("subtitle dialog: Stretch maps the first and last cue linearly, the frame rate presets fill the two times, and the result is what the formula says", async () =>
            {
                var t = DocTmp("srt-stretch");
                var src = Path.Combine(t, "stretch.srt"); File.WriteAllText(src, SampleSrt);
                var d = SubtitleTimingDialog.Open(src);
                try
                {
                    await Pump(400);
                    d.SetAnchors("00:00:05,100", "00:00:25,000"); await Pump(150);
                    if (!d.SaveEnabled) return "stretch blocked: " + d.StatusText;
                    var note = Drive.TextOf(d);
                    if (!note.Contains("Speed change: \u00d7 ")) return "no speed factor shown: " + note;
                    await DocAudit("subtitle stretch", d);
                    d.SaveNow(); await Pump(200);
                    var saved = SubtitleTools.Read(d.SavedPath); var orig = SubtitleTools.Read(src);
                    long t1 = 4200, t2 = 20000, a = 5100, b = 25000;
                    long Map(long x) => a + (long)Math.Round((x - t1) * (double)(b - a) / (t2 - t1));
                    for (int i = 0; i < 4; i++) if (saved.Cues[i].Start != Map(orig.Cues[i].Start) || saved.Cues[i].End != Map(orig.Cues[i].End)) return $"cue {i + 1}: start {saved.Cues[i].Start} expected {Map(orig.Cues[i].Start)}";
                    if (saved.Cues[0].Start != 5100 || saved.Cues[3].Start != 25000) return "the anchors did not land";
                    if (!Path.GetFileName(d.SavedPath).EndsWith(".stretched.srt")) return "name: " + d.SavedPath;
                }
                finally { d?.Close(); }
                // frame rate preset
                var d2 = SubtitleTimingDialog.Open(src);
                try
                {
                    await Pump(400); d2.ShowMode(1); await Pump(200);
                    Drive.Press(d2, "Frame rate 23.976 to 25"); await Pump(200);
                    d2.SaveNow(); await Pump(200);
                    var saved = SubtitleTools.Read(d2.SavedPath);
                    long exp1 = (long)Math.Round(4200 * 23.976 / 25.0), exp4 = (long)Math.Round(20000 * 23.976 / 25.0);
                    if (saved.Cues[0].Start != exp1 || saved.Cues[3].Start != exp4) return $"preset gave {saved.Cues[0].Start} and {saved.Cues[3].Start}, expected {exp1} and {exp4}";
                }
                finally { d2?.Close(); }
                return null;
            });

            await CheckAsync("subtitle dialog: Convert writes WebVTT and SubRip correctly, drops VTT cue settings and lists them, and keeps the cue text", async () =>
            {
                var t = DocTmp("srt-convert");
                var src = Path.Combine(t, "episode-04.srt"); File.WriteAllText(src, SampleSrt);
                var d = SubtitleTimingDialog.Open(src);
                try
                {
                    await Pump(400); d.SetConvertTo(SubtitleFormat.Vtt); await Pump(200);
                    await DocAudit("subtitle convert", d);
                    d.SaveNow(); await Pump(200);
                    var vtt = File.ReadAllText(d.SavedPath);
                    if (Path.GetFileName(d.SavedPath) != "episode-04.vtt") return "name " + d.SavedPath;
                    if (!vtt.StartsWith("WEBVTT") || !vtt.Contains("00:00:04.200 --> 00:00:06.800") || vtt.Contains("00:00:04,200")) return "vtt text wrong: " + vtt[..Math.Min(120, vtt.Length)];
                    if (!vtt.Contains("Today we are fixing the garden gate.")) return "cue text lost";
                }
                finally { d?.Close(); }
                var srcVtt = Path.Combine(t, "with-settings.vtt");
                File.WriteAllText(srcVtt, "WEBVTT\n\nNOTE a note\n\n1\n00:00:01.000 --> 00:00:03.000 position:10% align:start\nHello <b>there</b>\n\n2\n00:01:05.500 --> 00:01:07.000\nSecond\n");
                var d2 = SubtitleTimingDialog.Open(srcVtt);
                try
                {
                    await Pump(400);
                    if (d2.File.Cues.Count != 2 || d2.File.Format != SubtitleFormat.Vtt) return "vtt parsed " + d2.File.Cues.Count + " cues";
                    d2.SetConvertTo(SubtitleFormat.Srt); await Pump(200);
                    if (!Drive.TextOf(d2).Contains("Dropped and listed afterwards")) return "dropped settings were not listed";
                    d2.SaveNow(); await Pump(200);
                    var srt = File.ReadAllText(d2.SavedPath);
                    if (srt.Contains("WEBVTT") || srt.Contains("position:") || !srt.Contains("00:00:01,000 --> 00:00:03,000") || !srt.Contains("00:01:05,500 --> 00:01:07,000") || !srt.Contains("Hello <b>there</b>")) return "srt text wrong: " + srt;
                    if (!srt.StartsWith("1\r\n")) return "srt numbering missing";
                }
                finally { d2?.Close(); }
                return null;
            });

            await CheckAsync("subtitle dialog: overlapping and unreadable cues are listed, Fix overlaps and Skip broken cue clear them, and the file can still be shifted first", async () =>
            {
                var t = DocTmp("srt-problems");
                var src = Path.Combine(t, "episode-09.srt");
                File.WriteAllText(src, "1\r\n00:11:02,100 --> 00:11:05,900\r\nLine that runs long\r\n\r\n2\r\n00:11:04,300 --> 00:11:06,000\r\nStarts before the last one ends\r\n\r\n3\r\n00:16:xx,xxx --> 00:16:41,200\r\nTime cannot be read\r\n\r\n4\r\n00:17:00,000 --> 00:17:02,000\r\nFine\r\n");
                var d = SubtitleTimingDialog.Open(src);
                try
                {
                    await Pump(400);
                    var probs = SubtitleTools.Problems(d.File);
                    if (probs.Count != 2 || probs.Count(p => p.Kind == "overlap") != 1 || probs.Count(p => p.Kind == "broken") != 1) return "problems: " + string.Join(",", probs.Select(p => p.Kind));
                    var text = Drive.TextOf(d);
                    if (!text.Contains("1 cue overlaps and 1 is broken")) return "banner: " + text[..Math.Min(300, text.Length)];
                    await DocAudit("subtitle problems", d);
                    d.SetShift("+1"); await Pump(150);
                    if (!d.SaveEnabled) return "a file with problems should still be shiftable";
                    Drive.Press(d, "Fix overlaps"); await Pump(250);
                    if (SubtitleTools.Problems(d.File).Any(p => p.Kind == "overlap")) return "overlap remains";
                    if (d.File.Cues[0].End != 11 * 60000 + 4299) return "the earlier cue should end 1 ms before the next starts, got " + d.File.Cues[0].End;
                    Drive.Press(d, "Skip broken cue"); await Pump(250);
                    if (d.File.Cues.Count != 3 || SubtitleTools.Problems(d.File).Count != 0) return "after skipping: " + d.File.Cues.Count + " cues";
                    if (Drive.TextOf(d).Contains("overlap")) return "the problem banner is still shown";
                    return null;
                }
                finally { d?.Close(); }
            });
        }

        // ------------------------------------------------------------------ fonts

        private static async Task FontDocChecksAsync()
        {
            var tmpFont = FindSystemFont();
            await CheckAsync("font dialog: the preview uses the font from the file at four sizes, reads family and glyphs from it, and installs nothing", async () =>
            {
                if (tmpFont == null) return "no system font to copy";
                var t = DocTmp("font"); var f = Path.Combine(t, Path.GetFileName(tmpFont)); File.Copy(tmpFont, f);
                var realTarget = Path.Combine(FontTools.FontsFolder, Path.GetFileName(f));
                bool existedBefore = File.Exists(realTarget);
                var installs = new List<FontPlan>(); FontTools.InstallHook = p => { installs.Add(p); return null; };
                FontTools.InstalledHook = _ => false;
                var d = FontInstallDialog.Open(f);
                try
                {
                    await Pump(500);
                    if (d.State != FontState.Ready) return "state " + d.State + ": " + Drive.TextOf(d);
                    if (string.IsNullOrWhiteSpace(d.Facts.Family) || d.Facts.Glyphs < 100 || !d.Facts.Scripts.Contains("Latin")) return $"facts: {d.Facts.Family}, {d.Facts.Glyphs} glyphs, {string.Join("/", d.Facts.Scripts)}";
                    if (d.SpecimenBlocks.Count != 4 || !d.SpecimenBlocks.Select(b => b.FontSize).SequenceEqual(new double[] { 72, 36, 18, 12 })) return "the specimens are not 72, 36, 18 and 12";
                    if (!d.PreviewUsesFile) return "the preview did not take the font family from the file";
                    var tf = new Typeface(d.PreviewFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    if (!tf.TryGetGlyphTypeface(out var gt) || !gt.FontUri.LocalPath.StartsWith(d.PreviewDir, StringComparison.OrdinalIgnoreCase)) return "the preview font resolves to " + (gt?.FontUri?.LocalPath ?? "nothing") + ", not the copy of the file";
                    d.SetSampleForTest("Quick brown fox"); await Pump(150);
                    if (!d.SpecimenBlocks[1].Text.Contains("Quick brown fox")) return "editing the sample text did not change the specimens";
                    var text = Drive.TextOf(d);
                    foreach (var must in new[] { "Family", "Glyphs", "Licence", "Install for this user only", "The preview does not install anything." }) if (!text.Contains(must)) return "missing " + must;
                    await DocAudit("font ready", d);
                    if (installs.Count != 0) return "the preview installed something";
                    if (File.Exists(realTarget) && !existedBefore) return "a font file appeared in the user fonts folder";
                    // the install plan: current user only, no elevation
                    var plan = d.Plan;
                    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (!plan.TargetFile.StartsWith(Path.Combine(local, "Microsoft", "Windows", "Fonts"), StringComparison.OrdinalIgnoreCase)) return "target is " + plan.TargetFile;
                    if (plan.TargetFile.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase)) return "the plan points into the Windows folder";
                    if (plan.RegistryHive != "HKEY_CURRENT_USER" || plan.RegistryPath != @"Software\Microsoft\Windows NT\CurrentVersion\Fonts") return "registry target " + plan.RegistryHive + "\\" + plan.RegistryPath;
                    if (!plan.ValueName.EndsWith("(TrueType)") || !plan.ValueName.StartsWith(d.Facts.Family)) return "value name " + plan.ValueName;
                    return null;
                }
                finally { d?.Close(); FontTools.InstallHook = null; FontTools.InstalledHook = null; }
            });

            await CheckAsync("font dialog: Install goes through the per-user install step and shows where, Uninstall reverses it, and an already installed font offers Uninstall (steps recorded, nothing really installed)", async () =>
            {
                if (tmpFont == null) return "no system font to copy";
                var t = DocTmp("font-install"); var f = Path.Combine(t, Path.GetFileName(tmpFont)); File.Copy(tmpFont, f);
                var log = new List<string>(); bool installed = false;
                FontTools.InstalledHook = _ => installed;
                FontTools.InstallHook = p => { log.Add("install " + p); installed = true; return null; };
                FontTools.UninstallHook = p => { log.Add("uninstall " + p); installed = false; return null; };
                try
                {
                    var d = FontInstallDialog.Open(f);
                    try
                    {
                        await Pump(400);
                        Drive.Press(d, "Install"); await Pump(300);
                        if (d.State != FontState.Installed || log.Count != 1 || !log[0].StartsWith("install ")) return "after Install: " + d.State + " / " + string.Join(";", log);
                        var text = Drive.TextOf(d);
                        if (!text.Contains("is installed for you") || !text.Contains("Your user fonts folder") || !text.Contains("Open fonts settings")) return "done card: " + text;
                        await DocAudit("font installed", d);
                        Drive.Press(d, "Uninstall"); await Pump(300);
                        if (log.Count != 2 || !log[1].StartsWith("uninstall ")) return "uninstall not recorded: " + string.Join(";", log);
                    }
                    finally { d?.Close(); }
                    installed = true;
                    var d2 = FontInstallDialog.Open(f);
                    try
                    {
                        await Pump(400);
                        if (d2.State != FontState.AlreadyInstalled) return "an installed font opened in state " + d2.State;
                        if (!Drive.TextOf(d2).Contains("This font is already installed") || Drive.Find<System.Windows.Controls.Primitives.ButtonBase>(d2, "Uninstall") == null) return "already-installed card: " + Drive.TextOf(d2);
                        await DocAudit("font already installed", d2);
                    }
                    finally { d2?.Close(); }
                    return null;
                }
                finally { FontTools.InstalledHook = null; FontTools.InstallHook = null; FontTools.UninstallHook = null; }
            });

            await CheckAsync("font dialog: a damaged font and a WOFF file say they cannot be used and change nothing", async () =>
            {
                var t = DocTmp("font-bad");
                var broken = Path.Combine(t, "broken-font.otf"); File.WriteAllBytes(broken, Enumerable.Range(0, 400).Select(i => (byte)(i * 7)).ToArray());
                var woff = Path.Combine(t, "web.woff"); File.WriteAllBytes(woff, Encoding.ASCII.GetBytes("wOFF" + new string('x', 200)));
                FontTools.InstallHook = p => throw new InvalidOperationException("an unreadable font reached the installer");
                try
                {
                    foreach (var (file, must) in new[] { (broken, "This is not a usable font file"), (woff, "WOFF is a web font format") })
                    {
                        var d = FontInstallDialog.Open(file);
                        try
                        {
                            await Pump(400);
                            if (d.State != FontState.Unreadable) return Path.GetFileName(file) + " state " + d.State;
                            var text = Drive.TextOf(d);
                            if (!text.Contains(must) && !text.Contains("The font tables are damaged")) return "message: " + text;
                            if (Drive.Find<System.Windows.Controls.Primitives.ButtonBase>(d, "Install") != null) return "an unreadable font offers Install";
                            if (Drive.Find<System.Windows.Controls.Primitives.ButtonBase>(d, "Copy details") == null) return "no Copy details";
                            if (file == broken) await DocAudit("font unreadable", d);
                        }
                        finally { d?.Close(); }
                    }
                    return null;
                }
                finally { FontTools.InstallHook = null; }
            });
        }

        // ------------------------------------------------------------------ disc images

        private static async Task DiscDocChecksAsync()
        {
            Check("disc image engine: an ISO built here is read back with its folders, files and sizes, and each chosen file extracts byte for byte", () =>
            {
                var t = DocTmp("iso-engine");
                var big = new byte[5000]; new Random(3).NextBytes(big);
                var iso = Path.Combine(t, "disc.iso");
                BuildIso(iso, "MY_DISC", ("SETUP.EXE", Encoding.ASCII.GetBytes("MZ-SETUP-EXE")), ("AUTORUN.INF", Encoding.ASCII.GetBytes("[autorun]")), ("SOURCES/INSTALL.WIM", big), ("BOOT/BOOT.SDI", Encoding.ASCII.GetBytes("SDI")));
                var vol = DiscImageTools.ReadIso(iso);
                if (vol.Label != "MY_DISC") return "label " + vol.Label;
                var paths = vol.Entries.Select(e => e.Path + (e.IsDirectory ? "/" : "")).OrderBy(x => x).ToList();
                var want = new[] { "AUTORUN.INF", "BOOT/", "BOOT/BOOT.SDI", "SETUP.EXE", "SOURCES/", "SOURCES/INSTALL.WIM" };
                if (!paths.SequenceEqual(want)) return "entries: " + string.Join(", ", paths);
                if (vol.Entries.First(e => e.Path == "SOURCES/INSTALL.WIM").Size != 5000) return "size wrong";
                var dest = Path.Combine(t, "out");
                var r = DiscImageTools.Extract(iso, vol.Entries.Where(e => e.Path is "SOURCES/INSTALL.WIM" or "SETUP.EXE"), dest, null, CancellationToken.None);
                if (r.Error != null || r.Extracted != 2) return "extract: " + r.Error + " / " + r.Extracted;
                if (!File.ReadAllBytes(Path.Combine(dest, "SOURCES", "INSTALL.WIM")).SequenceEqual(big)) return "INSTALL.WIM bytes differ";
                if (File.ReadAllText(Path.Combine(dest, "SETUP.EXE")) != "MZ-SETUP-EXE") return "SETUP.EXE differs";
                var notIso = Path.Combine(t, "junk.iso"); File.WriteAllBytes(notIso, new byte[40000]);
                try { DiscImageTools.ReadIso(notIso); return "a file of zeros was read as an ISO"; } catch (InvalidDataException) { }
                return null;
            });

            await CheckAsync("disc image dialog: Mount, Eject and a refused mount follow the mount step (steps replaced by a recorder), the tree browses and extracts without mounting, and closing never ejects", async () =>
            {
                var t = DocTmp("iso-flow");
                var iso = Path.Combine(t, "Win-install.iso");
                BuildIso(iso, "WIN_INSTALL", ("SETUP.EXE", Encoding.ASCII.GetBytes("MZ-SETUP")), ("AUTORUN.INF", Encoding.ASCII.GetBytes("[autorun]")), ("SOURCES/INSTALL.WIM", new byte[3000]));
                var calls = new List<string>(); bool fail = false; string attached = null;
                DiscImageTools.StatusHook = _ => attached;
                DiscImageTools.MountHook = p => { calls.Add("mount"); Thread.Sleep(400); return fail ? new MountResult { Error = "The file is on a network share that does not allow mounting." } : new MountResult { Ok = true, Drive = "Z:" }; };
                DiscImageTools.DismountHook = p => { calls.Add("dismount"); return null; };
                try
                {
                    var d = DiscImageDialog.Open(iso);
                    try
                    {
                        await Pump(500);
                        if (d.State != DiscState.Ready || !d.Browsable) return "state " + d.State + " browsable " + d.Browsable;
                        if (d.Tree.FileCount != 3) return "tree shows " + d.Tree.FileCount + " files";
                        d.Tree.ToggleForTest("SETUP.EXE");
                        if (d.Tree.SelectedCount != 1 || !Drive.TextOf(d).Contains("1 file selected")) return "selection: " + d.Tree.SelectedCount;
                        await DocAudit("disc ready", d);
                        // Extract selected without mounting
                        var dest = Path.Combine(t, "out"); d.ExtractForTest(dest);
                        if (!await UntilDocState(() => d.State == DiscState.Extracted)) return "extract never finished";
                        if (File.ReadAllText(Path.Combine(dest, "SETUP.EXE")) != "MZ-SETUP" || d.Result.Extracted != 1) return "extracted wrong";
                        if (calls.Count != 0) return "extracting called the mount step";
                        Drive.Press(d, "Back to the image"); await Pump(300);
                        // Mount
                        Drive.Press(d, "Mount as a drive"); await Pump(120);
                        if (d.State != DiscState.Mounting || !Drive.TextOf(d).Contains("Attaching the image as a drive")) return "no mounting state: " + d.State;
                        await DocAudit("disc mounting", d);
                        if (!await UntilDocState(() => d.State == DiscState.Mounted)) return "never mounted, state " + d.State;
                        var text = Drive.TextOf(d);
                        if (!text.Contains("Mounted as drive Z:") || !text.Contains("Leaving this window open does not unmount it.") || Drive.Find<System.Windows.Controls.Primitives.ButtonBase>(d, "Open drive Z:") == null) return "mounted card: " + text;
                        await DocAudit("disc mounted", d);
                        // closing does not eject
                        var before = calls.Count(c => c == "dismount");
                        d.Close(); await Pump(150);
                        if (calls.Count(c => c == "dismount") != before) return "closing the window ejected the image";
                        // reopen: an attached image shows as mounted, and Eject reverses it
                        attached = "Z:";
                        d = DiscImageDialog.Open(iso); await Pump(900);
                        if (d.State != DiscState.Mounted) return "an attached image opened as " + d.State;
                        Drive.Press(d, "Eject"); await Pump(300);
                        if (d.State != DiscState.Ready || calls.Count(c => c == "dismount") != before + 1) return "eject: state " + d.State;
                        // a refused mount
                        attached = null; fail = true;
                        Drive.Press(d, "Mount as a drive");
                        if (!await UntilDocState(() => d.State == DiscState.CouldNotMount)) return "a refused mount ended in " + d.State;
                        text = Drive.TextOf(d);
                        if (!text.Contains("Windows could not mount this image") || !text.Contains("network share")) return "failure card: " + text;
                        await DocAudit("disc could not mount", d);
                        Drive.Press(d, "Browse without mounting"); await Pump(300);
                        if (d.State != DiscState.Ready || d.Tree.FileCount != 3) return "Browse without mounting led to " + d.State;
                    }
                    finally { d?.Close(); }
                    return null;
                }
                finally { DiscImageTools.MountHook = null; DiscImageTools.DismountHook = null; DiscImageTools.StatusHook = null; }
            });

            await CheckAsync("disc image: the built image really mounts for this user through Windows, lists its files on the new drive, and ejects again (dismounted in a finally block)", async () =>
            {
                var t = DocTmp("iso-real");
                var iso = Path.Combine(t, "real.iso");
                BuildIso(iso, "REAL_MOUNT", ("HELLO.TXT", Encoding.ASCII.GetBytes("HELLO-FROM-ISO")), ("DOCS/NOTE.TXT", Encoding.ASCII.GetBytes("NOTE")));
                try
                {
                    var res = await Task.Run(() => DiscImageTools.Mount(iso));
                    if (!res.Ok) return "Windows did not mount the image built for this check: " + res.Error;
                    var root = res.Drive + "\\";
                    if (!Directory.Exists(root)) return "the drive " + res.Drive + " does not exist";
                    var names = Directory.GetFileSystemEntries(root).Select(Path.GetFileName).ToList();
                    if (!names.Any(n => n.Equals("HELLO.TXT", StringComparison.OrdinalIgnoreCase))) return "drive holds: " + string.Join(", ", names);
                    if (File.ReadAllText(Path.Combine(root, "HELLO.TXT")) != "HELLO-FROM-ISO") return "file content on the drive is wrong";
                    var again = await Task.Run(() => DiscImageTools.AttachedDrive(iso));
                    if (again != res.Drive) return "AttachedDrive says " + again + ", expected " + res.Drive;
                    return null;
                }
                finally
                {
                    var err = DiscImageTools.Dismount(iso);
                    var left = DiscImageTools.AttachedDrive(iso);
                    if (left != null) Log.Warn("The self-test image is still attached as " + left + " (" + err + ")");
                }
            });

            await CheckAsync("disc image dialog: a mount that Windows really refuses shows Could not mount with the reason and still lets the tree browse", async () =>
            {
                var t = DocTmp("iso-refuse");
                var junk = Path.Combine(t, "backup.iso"); File.WriteAllBytes(junk, new byte[100_000]);
                var d = DiscImageDialog.Open(junk);
                try
                {
                    await Pump(600);
                    if (d.Browsable) return "a file of zeros should not be browsable";
                    if (!Drive.TextOf(d).Contains("No file list for this image")) return "no note about the missing list: " + Drive.TextOf(d);
                    Drive.Press(d, "Mount as a drive");
                    if (!await UntilDocState(() => d.State == DiscState.CouldNotMount, 70000)) return "ended in " + d.State;
                    var text = Drive.TextOf(d);
                    if (!text.Contains("Windows could not mount this image") || !text.Contains("WHAT WAS NOT CHANGED")) return "card: " + text;
                    if (DiscImageTools.AttachedDrive(junk) != null) { DiscImageTools.Dismount(junk); return "a failed mount left the image attached"; }
                    return null;
                }
                finally { d?.Close(); }
            });
        }
    }
}
