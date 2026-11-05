using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.Core.Archives;
using MonitoringTools.Core.DocTools;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    // The archive formats read through the bundled 7-Zip (7z, xz, bz2, tar.xz, tar.bz2, AES zip), the Ask rule for names that
    // exist, every face of a font collection, and ejecting an image from the tray menu. Every fixture is made here: archives with
    // the bundled 7-Zip command line, the font collection copied from the system fonts folder (nothing is installed), and the
    // disc image built by the check, mounted for real and ejected through the tray row.
    public static partial class SelfTest
    {
        /// <summary>Runs the bundled 7za to make a fixture. Returns null when it worked, otherwise why not.</summary>
        private static string SevenMake(params string[] args)
        {
            var exe = ArchiveEngine.SevenZip;
            if (exe == null) return "the bundled 7-Zip is missing from this build";
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            p.StandardInput.Close();
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(120000)) { try { p.Kill(true); } catch { } return "7za did not finish making " + string.Join(" ", args.Take(3)); }
            return p.ExitCode == 0 ? null : "7za could not make the fixture: " + e.Result + o.Result;
        }

        private static string MakeSevenSource(string t)
        {
            var src = Path.Combine(t, "src");
            Directory.CreateDirectory(Path.Combine(src, "docs", "deep"));
            File.WriteAllText(Path.Combine(src, "readme.txt"), "README");
            File.WriteAllText(Path.Combine(src, "docs", "guide.md"), "GUIDE");
            File.WriteAllText(Path.Combine(src, "docs", "deep", "notes.txt"), "NOTES");
            return src;
        }

        private static async Task<string> ExtractAll(ExtractArchiveDialog d, string dest, string password = null)
        {
            d.SetDestinationForTest(dest);
            d.StartForTest(password);
            if (!await UntilDocState(() => d.State == ArchiveState.Done || (password != null && d.State == ArchiveState.Password && d.PasswordErrorBlock?.Visibility == Visibility.Visible), 30000)) return "never finished, state " + d.State;
            return d.State == ArchiveState.Done && d.Result.Error != null ? "error: " + d.Result.Error : null;
        }

        private static string SameTree(string dest)
        {
            foreach (var (rel, text) in new[] { ("readme.txt", "README"), (Path.Combine("docs", "guide.md"), "GUIDE"), (Path.Combine("docs", "deep", "notes.txt"), "NOTES") })
            {
                var f = Path.Combine(dest, rel);
                if (!File.Exists(f)) return rel + " was not written";
                if (File.ReadAllText(f) != text) return rel + " holds the wrong bytes";
            }
            if (Directory.EnumerateDirectories(dest, ".extracting-*").Any()) return "the hidden unpacking folder was left behind";
            if (Directory.EnumerateFiles(dest, "*.part", SearchOption.AllDirectories).Any()) return "a .part file was left behind";
            return null;
        }

        private static async Task SevenZipDocChecksAsync()
        {
            await CheckAsync("archive dialog: a 7z made by the bundled 7-Zip is listed with its tree and extracted through it, file for file", async () =>
            {
                var t = DocTmp("seven-plain"); var src = MakeSevenSource(t);
                var arc = Path.Combine(t, "bundle.7z");
                var err = SevenMake("a", "-t7z", arc, Path.Combine(src, "*")); if (err != null) return err;
                var d = ExtractArchiveDialog.Open(arc);
                try
                {
                    await Pump(600);
                    if (d.State != ArchiveState.Ready) return "state " + d.State + ": " + Drive.TextOf(d);
                    if (d.Info.Format != "7z" || !d.Info.UseSevenZip) return "format " + d.Info.Format;
                    if (d.Info.FileCount != 3 || !d.Info.Entries.Any(e => e.Path == "docs/deep/notes.txt" && e.Size == 5)) return "listed " + string.Join(", ", d.Info.Entries.Select(e => e.Path + ":" + e.Size));
                    if (!Drive.TextOf(d).Contains("3 of 3 selected")) return "the selection line is missing";
                    await DocAudit("archive 7z ready", d);
                    var dest = Path.Combine(t, "out");
                    var r = await ExtractAll(d, dest); if (r != null) return r;
                    if (d.Result.Extracted != 3) return "extracted " + d.Result.Extracted;
                    return SameTree(dest);
                }
                finally { d?.Close(); }
            });

            await CheckAsync("archive dialog: xz, bz2, tar.xz and tar.bz2 made by the bundled 7-Zip are listed and extracted, and a tar.xz path that escapes is refused", async () =>
            {
                var t = DocTmp("seven-streams");
                var one = Path.Combine(t, "notes.txt"); File.WriteAllText(one, "SINGLE-FILE");
                var xz = Path.Combine(t, "notes.txt.xz"); var bz = Path.Combine(t, "notes.txt.bz2");
                var err = SevenMake("a", "-txz", xz, one) ?? SevenMake("a", "-tbzip2", bz, one); if (err != null) return err;
                var tar = Path.Combine(t, "pack.tar");
                using (var w = new TarWriter(File.Create(tar), TarEntryFormat.Ustar, leaveOpen: false))
                {
                    void Add(string name, string text) => w.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(text)) });
                    Add("pack/a.txt", "TAR-A"); Add("pack/sub/b.txt", "TAR-B"); Add("../escape.txt", "ESCAPE");
                }
                var txz = Path.Combine(t, "pack.tar.xz"); var tbz = Path.Combine(t, "pack.tar.bz2");
                err = SevenMake("a", "-txz", txz, tar) ?? SevenMake("a", "-tbzip2", tbz, tar); if (err != null) return err;
                foreach (var (file, fmt) in new[] { (xz, "xz"), (bz, "bz2") })
                {
                    var d = ExtractArchiveDialog.Open(file);
                    try
                    {
                        await Pump(500);
                        if (d.State != ArchiveState.Ready || d.Info.Format != fmt || d.Info.FileCount != 1 || d.Info.Entries[0].Path != "notes.txt") return $"{fmt}: state {d.State}, format {d.Info.Format}, entries {string.Join(",", d.Info.Entries.Select(e => e.Path))}";
                        var dest = Path.Combine(t, "out-" + fmt);
                        var r = await ExtractAll(d, dest); if (r != null) return fmt + ": " + r;
                        if (File.ReadAllText(Path.Combine(dest, "notes.txt")) != "SINGLE-FILE") return fmt + ": content wrong";
                    }
                    finally { d?.Close(); }
                }
                foreach (var (file, fmt) in new[] { (txz, "tar.xz"), (tbz, "tar.bz2") })
                {
                    var d = ExtractArchiveDialog.Open(file);
                    try
                    {
                        await Pump(500);
                        if (d.State != ArchiveState.Ready || d.Info.Format != fmt || d.Info.FileCount != 3) return $"{fmt}: state {d.State}, format {d.Info.Format}, files {d.Info.FileCount}";
                        if (!Drive.TextOf(d).Contains("unsafe path, will be refused")) return fmt + ": the escaping path is not marked";
                        if (fmt == "tar.xz") await DocAudit("archive tar.xz ready", d);
                        var dest = Path.Combine(t, "out-" + fmt.Replace('.', '-'));
                        var r = await ExtractAll(d, dest); if (r != null) return fmt + ": " + r;
                        if (d.Result.Extracted != 2 || d.Result.Refused.Count != 1) return $"{fmt}: extracted {d.Result.Extracted}, refused {d.Result.Refused.Count}";
                        if (File.ReadAllText(Path.Combine(dest, "pack", "sub", "b.txt")) != "TAR-B" || File.Exists(Path.Combine(t, "escape.txt"))) return fmt + ": content or escape wrong";
                    }
                    finally { d?.Close(); }
                }
                return null;
            });

            await CheckAsync("archive dialog: a password 7z asks first; a wrong password extracts nothing and says so; the right one extracts; a 7z with encrypted names lists its files only after the password", async () =>
            {
                var t = DocTmp("seven-password"); var src = MakeSevenSource(t);
                var arc = Path.Combine(t, "locked.7z"); var hidden = Path.Combine(t, "hidden-names.7z");
                var err = SevenMake("a", "-t7z", "-pcorrect-horse", arc, Path.Combine(src, "*")) ?? SevenMake("a", "-t7z", "-pcorrect-horse", "-mhe=on", hidden, Path.Combine(src, "*"));
                if (err != null) return err;
                foreach (var (file, namesLocked) in new[] { (arc, false), (hidden, true) })
                {
                    var tag = Path.GetFileName(file);
                    var d = ExtractArchiveDialog.Open(file);
                    try
                    {
                        await Pump(600);
                        if (d.State != ArchiveState.Password) return tag + ": state " + d.State;
                        if (d.Info.NamesLocked != namesLocked) return tag + ": names locked is " + d.Info.NamesLocked;
                        if (namesLocked && (d.Info.FileCount != 0 || !Drive.TextOf(d).Contains("Even the file names are locked"))) return tag + ": the names-hidden state is wrong: " + Drive.TextOf(d);
                        if (!namesLocked && d.Info.FileCount != 3) return tag + ": listed " + d.Info.FileCount;
                        await DocAudit("archive 7z password " + (namesLocked ? "names hidden" : "names visible"), d);
                        var dest = Path.Combine(t, "out-" + tag);
                        d.SetDestinationForTest(dest);
                        d.StartForTest("wrong-horse"); await Pump(800);
                        if (d.State != ArchiveState.Password) return tag + ": a wrong password left state " + d.State;
                        if (!Drive.TextOf(d).Contains("Wrong password for this archive. Nothing was extracted.")) return tag + ": no wrong-password message";
                        if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any()) return tag + ": a wrong password wrote something";
                        d.SetDestinationForTest(dest);
                        d.StartForTest("correct-horse");
                        if (!await UntilDocState(() => d.State == ArchiveState.Done, 30000)) return tag + ": never done with the right password, state " + d.State;
                        if (d.Result.Error != null || d.Result.WrongPassword) return tag + ": " + d.Result.Error;
                        var same = SameTree(dest); if (same != null) return tag + ": " + same;
                    }
                    finally { d?.Close(); }
                }
                return null;
            });

            await CheckAsync("archive dialog: an AES-256 zip goes through the bundled 7-Zip: the password is asked, a wrong one is refused, the right one extracts, and a zip-slip name inside is refused", async () =>
            {
                var t = DocTmp("seven-aes"); var src = MakeSevenSource(t);
                Directory.CreateDirectory(Path.Combine(src, "xx")); File.WriteAllText(Path.Combine(src, "xx", "evil.txt"), "EVIL");
                var arc = Path.Combine(t, "aes.zip");
                var err = SevenMake("a", "-tzip", "-mem=AES256", "-pzip-secret", arc, Path.Combine(src, "*")); if (err != null) return err;
                // rename "xx/evil.txt" to "../evil.txt" in the local and central headers (same length; AES does not cover the name)
                var bytes = File.ReadAllBytes(arc);
                var from = Encoding.ASCII.GetBytes("xx/evil.txt"); var to = Encoding.ASCII.GetBytes("../evil.txt");
                int hits = 0;
                for (int i = 0; i + from.Length <= bytes.Length; i++)
                    if (bytes.AsSpan(i, from.Length).SequenceEqual(from)) { to.CopyTo(bytes, i); hits++; }
                if (hits < 2) return "the zip-slip name was not found in both headers (" + hits + ")";
                File.WriteAllBytes(arc, bytes);
                var d = ExtractArchiveDialog.Open(arc);
                try
                {
                    await Pump(600);
                    if (d.State != ArchiveState.Password || !d.Info.UseSevenZip) return "state " + d.State + ", through 7-Zip " + d.Info.UseSevenZip;
                    if (!d.Info.Entries.Any(e => e.Path == "../evil.txt" && e.Unsafe)) return "the escaping name is not marked unsafe";
                    var dest = Path.Combine(t, "out");
                    d.SetDestinationForTest(dest);
                    d.StartForTest("nope"); await Pump(800);
                    if (d.State != ArchiveState.Password || !Drive.TextOf(d).Contains("Wrong password for this archive. Nothing was extracted.")) return "a wrong AES password: state " + d.State;
                    d.SetDestinationForTest(dest);
                    d.StartForTest("zip-secret");
                    if (!await UntilDocState(() => d.State == ArchiveState.Done, 30000)) return "never done, state " + d.State;
                    if (d.Result.Error != null) return "error: " + d.Result.Error;
                    if (d.Result.Refused.Count != 1 || d.Result.Extracted != 3) return $"extracted {d.Result.Extracted}, refused {d.Result.Refused.Count}";
                    if (File.Exists(Path.Combine(t, "evil.txt")) || File.Exists(Path.Combine(dest, "evil.txt"))) return "the zip-slip entry was written";
                    if (!Drive.TextOf(d).Contains("1 unsafe path")) return "the Done card does not count the refused path";
                    return SameTree(dest);
                }
                finally { d?.Close(); }
            });

            Check("archive engine: Cancel while 7-Zip unpacks a 7z leaves nothing in the destination, not even the hidden unpacking folder", () =>
            {
                var t = DocTmp("seven-cancel");
                var src = Path.Combine(t, "src"); Directory.CreateDirectory(src);
                var rnd = new Random(11);
                for (int i = 0; i < 4; i++) { var b = new byte[6_000_000]; rnd.NextBytes(b); File.WriteAllBytes(Path.Combine(src, $"blob{i}.bin"), b); }
                var arc = Path.Combine(t, "big.7z");
                var err = SevenMake("a", "-t7z", "-mx=1", arc, Path.Combine(src, "*")); if (err != null) return err;
                var info = ArchiveTools.Open(arc);
                if (info.Unsupported != null || info.FileCount != 4) return "listing: " + info.Unsupported + " / " + info.FileCount;
                var dest = Path.Combine(t, "out");
                using var cts = new CancellationTokenSource();
                var r = ArchiveTools.Extract(info, info.Entries, dest, OverwriteRule.KeepBoth, null, new SyncProgress(p => cts.Cancel()), cts.Token);
                if (!r.Canceled) return "Canceled was not reported (extracted " + r.Extracted + ", error " + r.Error + ")";
                if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any()) return "left behind: " + string.Join(", ", Directory.EnumerateFileSystemEntries(dest).Select(Path.GetFileName));
                return null;
            });
        }

        // ------------------------------------------------------------------ Ask

        private static async Task AskRuleDocChecksAsync()
        {
            await CheckAsync("archive dialog: Ask stops at each name that exists and waits; Replace, Skip and Keep both act on that file, the switch applies one answer to the rest, and Cancel during the question stops cleanly", async () =>
            {
                var t = DocTmp("zip-ask");
                var zip = Path.Combine(t, "ask.zip");
                using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
                    foreach (var n in new[] { "a.txt", "b.txt", "c.txt", "d.txt", "e.txt" }) { var e = z.CreateEntry(n); using var w = new StreamWriter(e.Open()); w.Write("ZIP-" + n); }
                var dest = Path.Combine(t, "out");
                void Reset()
                {
                    if (Directory.Exists(dest)) Directory.Delete(dest, true);
                    Directory.CreateDirectory(dest);
                    foreach (var n in new[] { "a.txt", "b.txt", "c.txt", "d.txt" }) File.WriteAllText(Path.Combine(dest, n), "MINE");
                }
                Reset();
                var d = ExtractArchiveDialog.Open(zip);
                try
                {
                    await Pump(400);
                    d.SetRuleForTest(OverwriteRule.Ask); d.SetDestinationForTest(dest);
                    d.StartForTest();
                    if (!await UntilDocState(() => d.State == ArchiveState.Conflict)) return "no question for a.txt, state " + d.State;
                    var text = Drive.TextOf(d);
                    if (!text.Contains("a.txt already exists") || !text.Contains("In the folder") || !text.Contains("In the archive")) return "the question does not name the file: " + text;
                    await DocAudit("archive ask", d);
                    await Pump(300);
                    if (d.State != ArchiveState.Conflict) return "the extraction did not wait for the answer";
                    Drive.Press(d, "Replace the file in the folder");
                    if (!await UntilDocState(() => d.State == ArchiveState.Conflict && Drive.TextOf(d).Contains("b.txt already exists"))) return "no question for b.txt, state " + d.State;
                    Drive.Press(d, "Skip this file");
                    if (!await UntilDocState(() => d.State == ArchiveState.Conflict && Drive.TextOf(d).Contains("c.txt already exists"))) return "no question for c.txt, state " + d.State;
                    Drive.Press(d, "Do the same for every other file that already exists");
                    Drive.Press(d, "Keep both files");
                    if (!await UntilDocState(() => d.State == ArchiveState.Done)) return "d.txt was asked about although the switch was on, state " + d.State;
                    var r = d.Result;
                    string Read(string n) => File.Exists(Path.Combine(dest, n)) ? File.ReadAllText(Path.Combine(dest, n)) : null;
                    if (Read("a.txt") != "ZIP-a.txt") return "Replace did not replace a.txt";
                    if (Read("b.txt") != "MINE" || File.Exists(Path.Combine(dest, "b (1).txt"))) return "Skip did not leave b.txt alone";
                    if (Read("c.txt") != "MINE" || Read("c (1).txt") != "ZIP-c.txt") return "Keep both did not number c.txt";
                    if (Read("d.txt") != "MINE" || Read("d (1).txt") != "ZIP-d.txt") return "the switch did not apply Keep both to d.txt";
                    if (Read("e.txt") != "ZIP-e.txt") return "e.txt (new) was not written";
                    if (r.Skipped != 1 || r.Renamed.Count != 2 || r.Extracted != 4) return $"skipped {r.Skipped}, renamed {r.Renamed.Count}, extracted {r.Extracted}";
                }
                finally { d?.Close(); }
                // Cancel while the question is open
                Reset();
                d = ExtractArchiveDialog.Open(zip);
                try
                {
                    await Pump(400);
                    d.SetRuleForTest(OverwriteRule.Ask); d.SetDestinationForTest(dest);
                    d.StartForTest();
                    if (!await UntilDocState(() => d.State == ArchiveState.Conflict)) return "no question in the cancel run";
                    Drive.Press(d, "Cancel the extraction");
                    if (!await UntilDocState(() => d.State == ArchiveState.Done)) return "Cancel during the question did not finish, state " + d.State;
                    if (!d.Result.Canceled) return "Cancel during the question was not reported as stopped";
                    if (File.ReadAllText(Path.Combine(dest, "a.txt")) != "MINE" || File.Exists(Path.Combine(dest, "e.txt"))) return "Cancel during the question still changed files";
                    if (Directory.EnumerateFiles(dest, "*.part").Any()) return "a .part file was left";
                }
                finally { d?.Close(); }
                return null;
            });
        }

        // ------------------------------------------------------------------ font collections

        private static string FindSystemCollection()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var n in new[] { "cambria.ttc", "msgothic.ttc", "mingliub.ttc", "simsun.ttc", "msjh.ttc", "YuGothR.ttc" })
            {
                var p = Path.Combine(dir, n);
                if (File.Exists(p) && FontTools.FaceCount(p) > 1) return p;
            }
            return Directory.EnumerateFiles(dir, "*.ttc").FirstOrDefault(p => FontTools.FaceCount(p) > 1);
        }

        private static async Task FontCollectionDocChecksAsync()
        {
            await CheckAsync("font dialog: a font collection (.ttc) lists every face, previews the face picked from the file itself, and Install takes all faces under one value (install step recorded, nothing installed)", async () =>
            {
                var sys = FindSystemCollection(); if (sys == null) return "no font collection in the system fonts folder to copy";
                var t = DocTmp("font-ttc"); var f = Path.Combine(t, Path.GetFileName(sys)); File.Copy(sys, f);
                int count = FontTools.FaceCount(f);
                var installs = new List<FontPlan>(); bool installed = false;
                FontTools.InstalledHook = _ => installed;
                FontTools.InstallHook = p => { installs.Add(p); installed = true; return null; };
                FontTools.UninstallHook = p => { installed = false; return null; };
                var d = FontInstallDialog.Open(f);
                try
                {
                    await Pump(600);
                    if (d.State != FontState.Ready) return "state " + d.State + ": " + Drive.TextOf(d);
                    if (d.Facts.Faces.Count != count || count < 2) return $"faces read {d.Facts.Faces.Count}, header says {count}";
                    foreach (var face in d.Facts.Faces) { DocFontNames.Add(face.Family); DocFontNames.Add(face.Display); }
                    var text = Drive.TextOf(d);
                    if (!text.Contains($"{count} faces")) return "the summary or the Faces row does not count the faces: " + text;
                    foreach (var face in d.Facts.Faces)
                    {
                        if (!text.Contains(face.Display)) return "face " + face.Display + " is not listed";
                        if (Drive.Find<ButtonBase>(d, "Preview the face " + face.Display) == null) return "no chip for " + face.Display;
                    }
                    await DocAudit("font collection", d);
                    // pick the last face: the preview must come from that face of the copied file
                    var last = d.Facts.Faces[^1];
                    Drive.Press(d, "Preview the face " + last.Display); await Pump(300);
                    if (d.FaceIndex != last.Index) return "picking a face did not switch the preview";
                    if (!d.PreviewUsesFile) return "the specimens do not use the face from the file";
                    var tf = new Typeface(d.PreviewFamily, last.Style, last.WpfWeight, FontStretches.Normal);
                    if (!tf.TryGetGlyphTypeface(out var gt) || !gt.FontUri.LocalPath.StartsWith(d.PreviewDir, StringComparison.OrdinalIgnoreCase)) return "the picked face resolves to " + (gt?.FontUri?.ToString() ?? "nothing");
                    var fam = gt.Win32FamilyNames.TryGetValue(System.Globalization.CultureInfo.GetCultureInfo("en-US"), out var en) ? en : gt.Win32FamilyNames.Values.FirstOrDefault();
                    if (!string.Equals(fam, last.Family, StringComparison.OrdinalIgnoreCase)) return "the preview shows family " + fam + ", expected " + last.Family;
                    if (!Drive.TextOf(d).Contains(last.Family)) return "the Family row did not follow the picked face";
                    // one plan for every face, under one registry value
                    var plan = d.Plan;
                    if (!plan.ValueName.EndsWith("(TrueType)") || d.Facts.Faces.Select(x => x.Display).Distinct().Any(x => !plan.ValueName.Contains(x)) || !plan.ValueName.Contains(" & ")) return "value name " + plan.ValueName;
                    Drive.Press(d, "Install"); await Pump(300);
                    if (installs.Count != 1 || d.State != FontState.Installed) return "install: " + installs.Count + " / " + d.State;
                    if (!Drive.TextOf(d).Contains($"All {count} faces are installed for you")) return "installed card: " + Drive.TextOf(d);
                    await DocAudit("font collection installed", d);
                    return null;
                }
                finally { d?.Close(); FontTools.InstalledHook = null; FontTools.InstallHook = null; FontTools.UninstallHook = null; }
            });
        }

        // ------------------------------------------------------------------ the tray menu

        private static async Task TrayEjectDocChecksAsync()
        {
            await CheckAsync("tray menu: an image this app mounted for real is listed with its drive letter and an Eject row; Eject dismounts it through Windows, the open disc window goes back to not mounted, and the row disappears", async () =>
            {
                var t = DocTmp("iso-tray");
                var iso = Path.Combine(t, "tray-test.iso");
                BuildIso(iso, "TRAY_EJECT", ("HELLO.TXT", Encoding.ASCII.GetBytes("FROM-THE-TRAY")));
                TrayMenuWindow menu = null; DiscImageDialog d = null;
                try
                {
                    var none = new TrayMenuWindow();
                    try { if (Drive.Find<ButtonBase>(none, "Eject tray-test.iso", visibleOnly: false) != null) return "the tray lists the image before it was mounted"; }
                    finally { none.Close(); }
                    var res = await Task.Run(() => DiscImageTools.Mount(iso));
                    if (!res.Ok) return "Windows did not mount the image built for this check: " + res.Error;
                    d = DiscImageDialog.Open(iso);
                    if (!await UntilDocState(() => d.State == DiscState.Mounted, 20000)) return "the disc window did not open as mounted, state " + d.State;
                    menu = new TrayMenuWindow();
                    menu.ShowAtCursor(); await Pump(400);
                    var row = Drive.Find<ButtonBase>(menu, "Eject tray-test.iso", visibleOnly: false);
                    if (row == null) return "the tray has no Eject row for the mounted image";
                    if (!Drive.TextOf(row).Contains(res.Drive)) return "the row does not show the drive " + res.Drive + ": " + Drive.TextOf(row);
                    await DocAudit("tray with a mounted image", menu);
                    Drive.Click(row);
                    if (!await UntilDocState(() => DiscImageTools.MountedSnapshot().All(m => m.Image != iso), 40000)) return "the image is still in the mounted list after Eject";
                    var left = await Task.Run(() => DiscImageTools.AttachedDrive(iso));
                    if (left != null) return "Windows still has the image attached as " + left;
                    if (!await UntilDocState(() => d.State == DiscState.Ready, 5000)) return "the open disc window still says mounted, state " + d.State;
                    var after = new TrayMenuWindow();
                    try { if (Drive.Find<ButtonBase>(after, "Eject tray-test.iso", visibleOnly: false) != null) return "the Eject row is still in the tray after ejecting"; }
                    finally { after.Close(); }
                    return null;
                }
                finally
                {
                    try { menu?.Close(); } catch { }
                    d?.Close();
                    if (DiscImageTools.AttachedDrive(iso) != null) DiscImageTools.Dismount(iso);
                }
            });
        }
    }
}
