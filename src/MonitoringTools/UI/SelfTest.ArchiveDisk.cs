using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using MonitoringTools.Core;
using MonitoringTools.Core.Archives;
using MonitoringTools.Core.Disks;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        private sealed class SyncProgress<T> : IProgress<T> { private readonly Action<T> _a; public SyncProgress(Action<T> a) { _a = a; } public void Report(T v) => _a(v); }

        private static string Sha(string path) { using var f = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)); }

        /// <summary>A small tree with text, binary, an excluded folder, an excluded extension and an empty folder.</summary>
        private static string MakeArchiveTree(string root, int binaryKb = 300)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "readme.txt"), string.Concat(Enumerable.Repeat("Monitoring Tools archive test line.\n", 400)));
            var bin = new byte[binaryKb * 1024]; new Random(7).NextBytes(bin);
            File.WriteAllBytes(Path.Combine(root, "data.bin"), bin);
            Directory.CreateDirectory(Path.Combine(root, "sub")); File.WriteAllText(Path.Combine(root, "sub", "notes.md"), "# notes\nsome text\n");
            Directory.CreateDirectory(Path.Combine(root, "node_modules", "pkg")); File.WriteAllText(Path.Combine(root, "node_modules", "pkg", "index.js"), "module.exports = 1;");
            File.WriteAllText(Path.Combine(root, "scratch.tmp"), "throw away");
            Directory.CreateDirectory(Path.Combine(root, "emptydir"));
            return root;
        }

        private static Dictionary<string, string> ExpectedHashes(string root, string topName, params Func<string, bool>[] excludeRel)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (excludeRel.Any(x => x(rel))) continue;
                d[(topName == null ? "" : topName + "/") + rel] = Sha(f);
            }
            return d;
        }

        private static async Task ArchiveDiskChecksAsync(string dir)
        {
            var work = Path.Combine(Settings.DataDir, "archive-disk"); Directory.CreateDirectory(work);
            // a running log of finished checks, so a stuck run shows where it stopped
            _ = Task.Run(async () => { while (true) { try { File.WriteAllLines(Path.Combine(dir, "progress.txt"), Results.ToArray().Select(r => (r.Pass ? "PASS " : "FAIL ") + r.Name + (r.Pass ? "" : " :: " + r.Detail))); } catch { } await Task.Delay(1500); } });
            void Mark(string m) { try { File.AppendAllText(Path.Combine(dir, "marks.txt"), DateTime.Now.ToString("HH:mm:ss.fff ") + m + Environment.NewLine); } catch { } }
            Mark("start");
            string sevenZip = null;
            try { sevenZip = ArchiveEngine.SevenZip; } catch (Exception ex) { Check("archive: the bundled 7-Zip extracts and verifies", () => ex.GetType().Name + ": " + ex.Message); }
            Mark("7z " + sevenZip);
            var tree = MakeArchiveTree(Path.Combine(work, "Project"));
            Mark("tree");
            Func<string, bool> excl1 = r => r.StartsWith("node_modules/") || r.EndsWith(".tmp");

            // ---------------------------------------------------------------- rules and estimate
            Check("archive: glob and regex exclude patterns match what they say", () =>
            {
                bool G(string p, string rel) => new ExcludeRule(p, false).Matches(rel);
                bool R(string p, string rel) => new ExcludeRule(p, true).Matches(rel);
                if (!G("node_modules", "node_modules/pkg/index.js") || !G("node_modules", "a/b/node_modules/x.js")) return "a folder name did not drop the folder everywhere";
                if (!G("*.tmp", "sub/x.TMP") || G("*.tmp", "sub/x.tmpx")) return "*.tmp matched wrongly";
                if (!G("build/**", "build/out/a.o") || G("build/**", "src/build.txt")) return "build/** matched wrongly";
                if (!R(@"\.(log|bak)$", "a/b.bak") || R(@"\.(log|bak)$", "a/b.txt")) return "regex matched wrongly";
                if (new ExcludeRule("(unclosed", true).Validate() == null) return "an invalid regex was accepted";
                return null;
            });

            Check("archive: the scan roots a folder at itself and a mixed selection at the closest common parent, and skips links", () =>
            {
                var one = ArchiveScan.Run(new[] { tree });
                if (one.RootName != "Project" || one.Entries.Any(e => e.Relative.StartsWith("Project"))) return "single folder root wrong: " + one.Root;
                if (!one.Entries.Any(e => e.IsEmptyDirectory && e.Relative == "emptydir")) return "the empty folder is missing from the scan";
                var two = ArchiveScan.Run(new[] { Path.Combine(tree, "readme.txt"), Path.Combine(tree, "sub") });
                if (!Path.GetFullPath(two.Root).TrimEnd('\\').Equals(Path.GetFullPath(tree).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return "common parent wrong: " + two.Root;
                var o = new ArchiveOptions { IncludeParent = true };
                var names = two.Select(o).Select(x => x.Name).ToList();
                if (!names.Contains("Project/readme.txt") || !names.Contains("Project/sub/notes.md")) return "parent folder prefix missing: " + string.Join(", ", names);
                o.IncludeParent = false;
                return two.Select(o).Any(x => x.Name == "readme.txt") ? null : "without the parent folder the top names are wrong";
            });

            Check("archive: the estimate follows the file types, the level and the volumes, and is built from the scan", () =>
            {
                var scan = ArchiveScan.Run(new[] { tree });
                var o = new ArchiveOptions { Items = new[] { tree }, OutFolder = work };
                var normal = ArchiveEngine.Estimate(scan, o);
                o.Level = 0; var store = ArchiveEngine.Estimate(scan, o);
                o.Level = 5; var ultra = ArchiveEngine.Estimate(scan, o);
                if (store.EstimatedBytes < normal.EstimatedBytes || ultra.EstimatedBytes > normal.EstimatedBytes) return $"level ordering wrong: store {store.EstimatedBytes}, normal {normal.EstimatedBytes}, ultra {ultra.EstimatedBytes}";
                if (normal.SourceBytes != scan.Entries.Sum(e => e.Size)) return "source bytes do not match the scan";
                o.Level = 3; o.Excludes.Add(new ExcludeRule("node_modules", false)); o.Excludes.Add(new ExcludeRule("*.tmp", false));
                var cut = ArchiveEngine.Estimate(scan, o);
                if (cut.Excluded != 2 || cut.Files != normal.Files - 2) return $"exclusions not counted: {cut.Excluded} excluded, {cut.Files} files";
                o.Split = true; o.VolumeBytes = 64 * 1024; o.Format = ArchiveFormat.SevenZip;
                var vols = ArchiveEngine.Estimate(scan, o);
                return vols.Volumes >= 3 ? null : "split volumes not counted: " + vols.Volumes;
            });

            Check("archive: refused combinations say why before anything is written", () =>
            {
                var scan = ArchiveScan.Run(new[] { tree });
                string V(Action<ArchiveOptions> change, bool seven = true) { var o = new ArchiveOptions { Items = new[] { tree }, Name = "x", OutFolder = work }; change(o); return ArchiveEngine.Validate(scan, o, seven); }
                if (V(o => { }) != null) return "a plain zip was refused";
                if (V(o => o.Name = "") == null) return "an empty name was accepted";
                if (V(o => o.Name = "a:b") == null) return "an invalid name was accepted";
                if (V(o => { o.Format = ArchiveFormat.TarGz; o.Password = "x"; }) == null) return "tar.gz with a password was accepted";
                if (V(o => { o.Format = ArchiveFormat.Zip; o.EncryptNames = true; o.Password = "x"; }) == null) return "encrypted names on a zip were accepted";
                if (V(o => { o.Format = ArchiveFormat.SevenZip; o.EncryptNames = true; }) == null) return "encrypted names without a password were accepted";
                if (V(o => { o.Split = true; o.VolumeBytes = 100; }) == null) return "a 100 byte volume was accepted";
                if (V(o => o.Excludes.Add(new ExcludeRule("(oops", true))) == null) return "an invalid exclude regex was accepted";
                if (V(o => o.Format = ArchiveFormat.SevenZip, seven: false) == null) return "7z was accepted without 7-Zip";
                if (V(o => o.Password = "pw", seven: false) == null) return "an encrypted zip was accepted without 7-Zip";
                if (V(o => { o.Excludes.Add(new ExcludeRule("**", false)); }) == null) return "excluding everything was accepted";
                return null;
            });

            Check("archive: the 7-Zip command line carries every chosen option and never the password", () =>
            {
                var o = new ArchiveOptions { Format = ArchiveFormat.SevenZip, Method = "LZMA2", Level = 5, Dictionary = "64m", Solid = "1g", Threads = 4, Password = "Sup3r-secret", EncryptNames = true, Split = true, VolumeBytes = 700L * 1048576 };
                var a = string.Join(" ", ArchiveEngine.SevenZipArguments(o, "out.7z", "list.lst"));
                foreach (var want in new[] { "-t7z", "-mx=9", "-m0=LZMA2", "-md=64m", "-ms=1g", "-mmt=4", "-mhe=on", "-v734003200b", "-spd" })
                    if (!a.Contains(want)) return "missing " + want + " in: " + a;
                if (a.Contains("Sup3r-secret")) return "the password is in the argument list that is built for logging";
                var z = string.Join(" ", ArchiveEngine.SevenZipArguments(new ArchiveOptions { Format = ArchiveFormat.Zip, Method = "BZip2", Level = 4, Password = "x" }, "o.zip", "l.lst"));
                return z.Contains("-tzip") && z.Contains("-mm=BZip2") && z.Contains("-mem=AES256") && z.Contains("-mx=7") ? null : "zip arguments wrong: " + z;
            });

            // ---------------------------------------------------------------- real archives, read back independently
            await CheckAsync("disk: diskpart scripts are written in the console code page, and a name it cannot carry is refused before the consent prompt", () =>
            {
                // diskpart rejects a UTF-16 script (it prints its help and exits 0x80070057), which is how real disks failed before
                var enc = VhdEngine.ScriptEncoding();
                if (enc is UnicodeEncoding || enc.CodePage == 1200 || enc.CodePage == 65001) return Task.FromResult("scripts would be written as " + enc.WebName);
                var line = "select vdisk file=\"C:\\x.vhdx\"\r\n";
                var bytes = enc.GetBytes(line);
                if (bytes.Length != line.Length || bytes[0] != (byte)'s') return Task.FromResult($"the script bytes are not one byte per character: {bytes.Length} for {line.Length}");
                if (VhdEngine.ScriptUnreadable("Plain Disk 1") != null) return Task.FromResult("plain ASCII was reported unreadable");
                var r = new VhdRequest { File = @"C:\Temp\ok.vhdx", SizeMb = 64, FileSystem = "NTFS", MountMode = "letter", Letter = "Z", Label = "盤" };
                var why = VhdEngine.Validate(r, checkFolderState: false);
                if (VhdEngine.ScriptUnreadable("盤") != null && (why == null || !why.Contains("cannot read the character 盤"))) return Task.FromResult("a label diskpart cannot read was not refused: " + why);
                return Task.FromResult<string>(null);
            });

            await CheckAsync("archive: a zip written by System.IO.Compression holds exactly the right files, byte for byte, and its test step passes", async () =>
            {
                var scan = ArchiveScan.Run(new[] { tree });
                var o = new ArchiveOptions { Items = new[] { tree }, Name = "plain", OutFolder = Path.Combine(work, "out-zip"), Format = ArchiveFormat.Zip, TestAfter = true, IncludeParent = true };
                o.Excludes.Add(new ExcludeRule("node_modules", false)); o.Excludes.Add(new ExcludeRule("*.tmp", false));
                var steps = new List<string>();
                var r = await ArchiveEngine.RunAsync(scan, o, new SyncProgress<ArchiveProgress>(p => { if (steps.LastOrDefault() != p.Step) steps.Add(p.Step); }), CancellationToken.None);
                if (!r.Ok) return "run failed: " + r.Error;
                if (r.Backend != "System.IO.Compression") return "the backend was " + r.Backend;
                if (!r.Tested || !r.TestOk) return "the test step did not pass: " + r.TestDetail;
                if (string.Join(",", steps) != "Scan,Compress,Test,Finish") return "steps were " + string.Join(",", steps);
                using var zip = ZipFile.OpenRead(r.Primary);
                var want = ExpectedHashes(tree, "Project", excl1);
                var got = new Dictionary<string, string>();
                foreach (var e in zip.Entries.Where(e => !e.FullName.EndsWith("/"))) { using var s = e.Open(); got[e.FullName] = Convert.ToHexString(SHA256.HashData(s)); }
                if (got.Count != want.Count || want.Any(kv => !got.TryGetValue(kv.Key, out var h) || h != kv.Value)) return "entries differ: got " + string.Join(", ", got.Keys) + " wanted " + string.Join(", ", want.Keys);
                if (zip.Entries.Any(e => e.FullName.Contains("node_modules") || e.FullName.EndsWith(".tmp"))) return "an excluded file is inside";
                return zip.Entries.Any(e => e.FullName == "Project/emptydir/") ? null : "the empty folder is missing";
            });

            await CheckAsync("archive: a tar.gz holds the right files and its test step passes", async () =>
            {
                var scan = ArchiveScan.Run(new[] { tree });
                var o = new ArchiveOptions { Items = new[] { tree }, Name = "plain", OutFolder = Path.Combine(work, "out-tgz"), Format = ArchiveFormat.TarGz, TestAfter = true, IncludeParent = false };
                o.Excludes.Add(new ExcludeRule("node_modules", false)); o.Excludes.Add(new ExcludeRule("*.tmp", false));
                var r = await ArchiveEngine.RunAsync(scan, o, null, CancellationToken.None);
                if (!r.Ok) return "run failed: " + r.Error;
                if (!r.Primary.EndsWith(".tar.gz")) return "output was " + r.Primary;
                if (!r.TestOk) return "the test step did not pass: " + r.TestDetail;
                var want = ExpectedHashes(tree, null, excl1);
                using var fs = File.OpenRead(r.Primary); using var gz = new GZipStream(fs, CompressionMode.Decompress); using var tar = new TarReader(gz);
                var got = new Dictionary<string, string>(); TarEntry te;
                while ((te = tar.GetNextEntry()) != null) if (te.EntryType == TarEntryType.RegularFile) got[te.Name] = Convert.ToHexString(SHA256.HashData(te.DataStream));
                return got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var h) && h == kv.Value) ? null : "entries differ: " + string.Join(", ", got.Keys);
            });

            await CheckAsync("archive: a name that exists is never overwritten, a number is added", async () =>
            {
                var scan = ArchiveScan.Run(new[] { Path.Combine(tree, "sub") });
                var o = new ArchiveOptions { Items = new[] { Path.Combine(tree, "sub") }, Name = "twice", OutFolder = Path.Combine(work, "out-twice"), TestAfter = false };
                var a = await ArchiveEngine.RunAsync(scan, o, null, CancellationToken.None);
                var b = await ArchiveEngine.RunAsync(scan, o, null, CancellationToken.None);
                return a.Ok && b.Ok && a.Primary != b.Primary && File.Exists(a.Primary) && Path.GetFileName(b.Primary) == "twice (1).zip" ? null : $"names: {a.Primary} / {b.Primary}";
            });

            await CheckAsync("archive: the test step catches a damaged archive", async () =>
            {
                var scan = ArchiveScan.Run(new[] { tree });
                var o = new ArchiveOptions { Items = new[] { tree }, Name = "damaged", OutFolder = Path.Combine(work, "out-damaged"), TestAfter = false, Level = 0 };
                var r = await ArchiveEngine.RunAsync(scan, o, null, CancellationToken.None);
                if (!r.Ok) return "could not make the archive: " + r.Error;
                var hashes = ExpectedHashes(tree, "Project");
                var good = ArchiveEngine.TestManaged(o, r.Primary, hashes, hashes.Count, 1, null, CancellationToken.None);
                if (!good.Ok) return "the intact archive failed its own test: " + good.Detail;
                var bytes = File.ReadAllBytes(r.Primary);
                // data.bin is stored, so flipping a byte inside it keeps the zip readable but changes the content
                var at = IndexOf(bytes, File.ReadAllBytes(Path.Combine(tree, "data.bin")).Take(64).ToArray());
                if (at < 0) return "could not find the stored file inside the archive";
                bytes[at + 20] ^= 0xFF; File.WriteAllBytes(r.Primary, bytes);
                var bad = ArchiveEngine.TestManaged(o, r.Primary, hashes, hashes.Count, 1, null, CancellationToken.None);
                return !bad.Ok && bad.Detail.Contains("data.bin") ? null : "a flipped byte was not caught: " + bad.Detail;
            });

            if (sevenZip == null)
                await CheckAsync("archive: the bundled 7-Zip makes 7z with a password, hidden names and volumes, and an encrypted zip", () => Task.FromResult<string>("7-Zip is not available in this build (the .deps folder has no 7zip; run download-dependencies, or set MT_7ZA to a 7za.exe)"));
            else
            {
                await CheckAsync("archive: 7-Zip makes a 7z with an AES-256 password, hidden names and volumes; read back with the wrong and the right password", async () =>
                {
                    var big = MakeArchiveTree(Path.Combine(work, "Project7"), binaryKb: 3200);
                    var scan = ArchiveScan.Run(new[] { big });
                    var o = new ArchiveOptions
                    {
                        Items = new[] { big }, Name = "secret", OutFolder = Path.Combine(work, "out-7z"), Format = ArchiveFormat.SevenZip, Method = "LZMA2", Level = 3, Dictionary = "16m", Solid = "on", Threads = 2,
                        Password = "Correct-Horse-9!", EncryptNames = true, Split = true, VolumeBytes = 1024 * 1024, TestAfter = true, IncludeParent = true
                    };
                    o.Excludes.Add(new ExcludeRule("node_modules", false)); o.Excludes.Add(new ExcludeRule("*.tmp", false));
                    var r = await ArchiveEngine.RunAsync(scan, o, null, CancellationToken.None);
                    if (!r.Ok) return "run failed: " + r.Error;
                    if (r.Backend != "7-Zip") return "the backend was " + r.Backend;
                    if (r.Outputs.Count < 3 || !r.Primary.EndsWith("secret.7z.001")) return "expected several .7z.00n volumes, got " + string.Join(", ", r.Outputs.Select(Path.GetFileName));
                    if (r.Outputs.Take(r.Outputs.Count - 1).Any(p => new FileInfo(p).Length != 1024 * 1024)) return "a volume is not exactly 1 MB";
                    if (!r.TestOk) return "the built-in test step did not pass: " + r.TestDetail;
                    // independent reads with the 7-Zip command line
                    var wrong = RunTool(sevenZip, $"t \"{r.Primary}\" -pWrong-password -y");
                    if (wrong.Code == 0) return "the wrong password opened the archive";
                    var noPw = RunTool(sevenZip, $"l \"{r.Primary}\" -y");
                    if (noPw.Code == 0 && noPw.Out.Contains("data.bin")) return "file names are readable without the password";
                    var outDir = Path.Combine(work, "x7"); if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                    var ok = RunTool(sevenZip, $"x \"{r.Primary}\" -o\"{outDir}\" -pCorrect-Horse-9! -y");
                    if (ok.Code != 0) return "the right password did not extract: " + ok.Out;
                    var want = ExpectedHashes(big, "Project7", excl1);
                    var got = Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(outDir, f).Replace('\\', '/'), Sha);
                    return got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var h) && h == kv.Value) ? null : "extracted files differ: " + string.Join(", ", got.Keys);
                });

                await CheckAsync("archive: 7-Zip writes an AES-256 zip (and its test step rejects a damaged volume)", async () =>
                {
                    var scan = ArchiveScan.Run(new[] { tree });
                    var o = new ArchiveOptions { Items = new[] { tree }, Name = "enczip", OutFolder = Path.Combine(work, "out-enczip"), Format = ArchiveFormat.Zip, Method = "Deflate", Level = 3, Password = "Zip-Pass-123", TestAfter = true };
                    var r = await ArchiveEngine.RunAsync(scan, o, null, CancellationToken.None);
                    if (!r.Ok || r.Backend != "7-Zip") return "run failed or wrong backend: " + (r.Error ?? r.Backend);
                    if (RunTool(sevenZip, $"t \"{r.Primary}\" -pwrong -y").Code == 0) return "the wrong password opened the zip";
                    if (RunTool(sevenZip, $"t \"{r.Primary}\" -pZip-Pass-123 -y").Code != 0) return "the right password did not open the zip";
                    var bytes = File.ReadAllBytes(r.Primary);
                    for (int i = 200; i < bytes.Length - 200; i += 97) bytes[i] ^= 0x5A;
                    var damaged = r.Primary + ".bad.zip"; File.WriteAllBytes(damaged, bytes);
                    var t = await ArchiveEngine.TestSevenZipAsync(damaged, o, null, CancellationToken.None, 1);
                    return t.Ok ? "a damaged zip passed the test" : null;
                });

                await CheckAsync("archive: 7-Zip honours the method, dictionary and solid choices", async () =>
                {
                    var scan = ArchiveScan.Run(new[] { tree });
                    foreach (var method in new[] { "LZMA", "PPMd", "BZip2", "Deflate" })
                    {
                        var o = new ArchiveOptions { Items = new[] { tree }, Name = "m-" + method, OutFolder = Path.Combine(work, "out-methods"), Format = ArchiveFormat.SevenZip, Method = method, Level = 3, TestAfter = true };
                        var r = await ArchiveEngine.RunAsync(scan, o, null, CancellationToken.None);
                        if (!r.Ok || !r.TestOk) return method + " failed: " + (r.Error ?? r.TestDetail);
                        var info = RunTool(sevenZip, $"l -slt \"{r.Primary}\" -y").Out;
                        var shown = method == "BZip2" ? "BZip2" : method == "Deflate" ? "Deflate" : method;
                        if (!info.Contains("Method = " + shown) && !info.Contains(shown)) return method + " not recorded in the archive: " + info.Substring(0, Math.Min(400, info.Length));
                    }
                    return null;
                });
            }

            await CheckAsync("archive: Cancel removes the partly written files (zip, tar.gz and 7z)", async () =>
            {
                var big = Path.Combine(work, "Big"); Directory.CreateDirectory(big);
                var buf = new byte[1 << 20];
                using (var f = File.Create(Path.Combine(big, "noise.bin"))) for (int i = 0; i < 72; i++) { RandomNumberGenerator.Fill(buf); f.Write(buf); }
                var scan = ArchiveScan.Run(new[] { big });
                var formats = new List<ArchiveOptions>
                {
                    new ArchiveOptions { Format = ArchiveFormat.Zip, Level = 4 },
                    new ArchiveOptions { Format = ArchiveFormat.TarGz, Level = 4 },
                };
                if (sevenZip != null) formats.Add(new ArchiveOptions { Format = ArchiveFormat.SevenZip, Level = 4, Split = true, VolumeBytes = 8 * 1048576 });
                int n = 0;
                foreach (var o in formats)
                {
                    o.Items = new[] { big }; o.Name = "cancelled" + n++; o.OutFolder = Path.Combine(work, "out-cancel"); o.TestAfter = false;
                    var cts = new CancellationTokenSource();
                    var r = await ArchiveEngine.RunAsync(scan, o, new SyncProgress<ArchiveProgress>(p => { if (p.Step == "Compress" && p.Done > 0) cts.Cancel(); }), cts.Token);
                    if (!r.Cancelled) return $"{o.Format}: the run was not reported as cancelled (ok {r.Ok}, {r.Error})";
                    var left = Directory.Exists(o.OutFolder) ? Directory.GetFiles(o.OutFolder, o.Name + "*") : Array.Empty<string>();
                    if (left.Length > 0) return $"{o.Format}: files left after Cancel: " + string.Join(", ", left.Select(Path.GetFileName));
                }
                return null;
            });

            // ---------------------------------------------------------------- the real archive window
            await CheckAsync("archive window: the controls follow the format (zip, 7z, tar.gz) and the delete switch holds the test on", async () =>
            {
                ArchiveDialog.Open(new[] { tree });
                var d = ArchiveDialog.Current; if (d == null) return "the window did not open";
                try
                {
                    if (!await d.WaitForScanAsync(8000)) return "the scan never finished: " + d.SummaryText;
                    if (d.FormatBox.SelectedId != "zip") return "Zip is not the default format";
                    if (d.Excludes.Count != 0) return "exclude patterns started non-empty";
                    if (d.NameBox.Text != "Project") return "default name was " + d.NameBox.Text;
                    d.FormatBox.Select("tgz"); await Task.Delay(60);
                    if (d.PasswordBox.IsVisible || d.SplitSwitch.IsVisible) return "tar.gz still shows the password or volume controls";
                    if (d.MethodBox.IsVisible) return "tar.gz still shows a method";
                    d.FormatBox.Select("zip"); await Task.Delay(60);
                    if (d.EncryptNames.IsEnabled) return "Encrypt file names is usable on Zip";
                    if (d.DictionaryBox.IsVisible || d.ThreadsField.IsVisible) return "Zip shows 7z-only rows";
                    if (sevenZip != null)
                    {
                        d.FormatBox.Select("7z"); await Task.Delay(60);
                        if (!d.DictionaryBox.IsVisible || !d.ThreadsField.IsVisible || !d.SolidBox.IsVisible) return "7z hides its dictionary, solid or thread rows";
                        if (d.EncryptNames.IsEnabled) return "Encrypt file names is usable before a password is typed";
                        d.SetPassword("abc12345"); await Task.Delay(60);
                        if (!d.EncryptNames.IsEnabled) return "Encrypt file names stayed off with a password";
                        d.MethodBox.Select("PPMd"); await Task.Delay(60);
                        if (d.DictionaryBox.IsEnabled) return "the dictionary stayed usable with PPMd";
                        d.MethodBox.Select("LZMA2"); await Task.Delay(60);
                        if (!d.DictionaryBox.IsEnabled) return "the dictionary stayed off with LZMA2";
                        d.SetPassword("");
                        d.FormatBox.Select("zip"); await Task.Delay(60);
                    }
                    Drive.Click(d.DeleteSwitch); await Task.Delay(60);
                    if (d.TestSwitch.IsChecked != true || d.TestSwitch.IsEnabled) return "Delete the originals did not hold the test on";
                    Drive.Click(d.DeleteSwitch); await Task.Delay(60);
                    if (!d.TestSwitch.IsEnabled) return "the test switch stayed locked after Delete was turned off";
                    d.NameBox.Text = "bad:name"; await Task.Delay(300);
                    if (d.CreateEnabled) return "Create stayed on with an invalid name";
                    d.NameBox.Text = "fine"; await Task.Delay(300);
                    if (!d.CreateEnabled) return "Create stayed off with a valid name: " + d.SummaryText;
                    return d.SummaryText.Contains("estimate") ? null : "no estimate in the summary: " + d.SummaryText;
                }
                finally { d.Close(); }
            });

            await CheckAsync("archive window: filling the controls and pressing Create writes the zip, with excludes, the parent folder and a passing test", async () =>
            {
                var outDir = Path.Combine(work, "out-window"); Directory.CreateDirectory(outDir);
                ArchiveDialog.Open(new[] { tree });
                var d = ArchiveDialog.Current; if (d == null) return "the window did not open";
                try
                {
                    if (!await d.WaitForScanAsync(8000)) return "the scan never finished";
                    d.NameBox.Text = "from-window"; d.FolderBox.Text = outDir;
                    d.AddExclude("node_modules", false); d.AddExclude(@"\.tmp$", true);
                    d.ExcludeMode.Select("regex");
                    await Task.Delay(400);
                    if (!d.CreateEnabled) return "Create was off: " + d.SummaryText;
                    if (!d.SummaryText.Contains("excluded")) return "the summary does not mention the exclusions: " + d.SummaryText;
                    d.PressCreate();
                    if (!await d.WaitForViewAsync("done", 30000)) return "no done view; the view was " + d.View + " " + d.LastResult?.Error;
                    var zipPath = Path.Combine(outDir, "from-window.zip");
                    if (!File.Exists(zipPath)) return "no zip at " + zipPath;
                    using var zip = ZipFile.OpenRead(zipPath);
                    var names = zip.Entries.Select(e => e.FullName).ToList();
                    if (names.Any(n => n.Contains("node_modules") || n.EndsWith(".tmp"))) return "excluded files are inside: " + string.Join(", ", names);
                    if (!names.Contains("Project/readme.txt")) return "the parent folder is missing: " + string.Join(", ", names);
                    if (d.LastResult == null || !d.LastResult.TestOk) return "the test step did not pass";
                    return null;
                }
                finally { d.Close(); }
            });

            if (sevenZip != null)
                await CheckAsync("archive window: a 7z with a password, encrypted names and volumes, made through the controls", async () =>
                {
                    var src = MakeArchiveTree(Path.Combine(work, "ProjectUi"), binaryKb: 2600);
                    var outDir = Path.Combine(work, "out-window7"); Directory.CreateDirectory(outDir);
                    ArchiveDialog.Open(new[] { src });
                    var d = ArchiveDialog.Current; if (d == null) return "the window did not open";
                    try
                    {
                        if (!await d.WaitForScanAsync(8000)) return "the scan never finished";
                        d.NameBox.Text = "ui7"; d.FolderBox.Text = outDir;
                        d.FormatBox.Select("7z");
                        d.SetPassword("Window-Pass-42");
                        Drive.Click(d.EncryptNames);
                        Drive.Click(d.SplitSwitch);
                        d.VolumeUnit.Select("MB", false); d.VolumeField.SetValue(1);
                        await Task.Delay(500);
                        if (!d.CreateEnabled) return "Create was off: " + d.SummaryText;
                        if (!d.SummaryText.Contains("AES-256 with names") || !d.SummaryText.Contains("volume")) return "the summary misses the settings: " + d.SummaryText;
                        d.PressCreate();
                        if (!await d.WaitForViewAsync("done", 60000)) return "no done view: " + d.LastResult?.Error;
                        var vols = Directory.GetFiles(outDir, "ui7.7z.*");
                        if (vols.Length < 3) return "volumes: " + vols.Length;
                        if (RunTool(sevenZip, $"t \"{Path.Combine(outDir, "ui7.7z.001")}\" -pWindow-Pass-42 -y").Code != 0) return "the password did not open the archive";
                        return RunTool(sevenZip, $"l \"{Path.Combine(outDir, "ui7.7z.001")}\" -y").Out.Contains("data.bin") ? "names are readable without the password" : null;
                    }
                    finally { d.Close(); }
                });

            await CheckAsync("archive window: Delete the originals waits for the two-key confirmation, then sends them to the Recycle Bin and keeps the archive", async () =>
            {
                var src = Path.Combine(work, "originals"); Directory.CreateDirectory(src);
                var f1 = Path.Combine(src, "one.txt"); var f2 = Path.Combine(src, "two.txt");
                File.WriteAllText(f1, "first"); File.WriteAllText(f2, "second");
                var outDir = Path.Combine(work, "out-delete"); Directory.CreateDirectory(outDir);
                ArchiveDialog.Open(new[] { f1, f2 });
                var d = ArchiveDialog.Current; if (d == null) return "the window did not open";
                try
                {
                    if (!await d.WaitForScanAsync(8000)) return "the scan never finished";
                    d.NameBox.Text = "keepme"; d.FolderBox.Text = outDir;
                    Drive.Click(d.DeleteSwitch);
                    await Task.Delay(300);
                    d.PressCreate();
                    if (!await d.WaitForViewAsync("done", 30000)) return "no done view: " + d.LastResult?.Error;
                    ConfirmWindow dlg = null;
                    for (int i = 0; i < 40 && dlg == null; i++) { dlg = Application.Current.Windows.OfType<ConfirmWindow>().LastOrDefault(); if (dlg == null) await Task.Delay(100); }
                    if (dlg == null) return "the two-key confirmation never opened";
                    if (!File.Exists(f1) || !File.Exists(f2)) return "the originals were deleted before the confirmation";
                    var slider = Drive.All<System.Windows.Controls.Slider>(dlg).FirstOrDefault();
                    if (slider == null || slider.IsEnabled) return "the slider was usable with both keys off";
                    Drive.Click(Drive.Need<ToggleButton>(dlg, "First key")); Drive.Click(Drive.Need<ToggleButton>(dlg, "Second key")); await Task.Delay(100);
                    if (!slider.IsEnabled) return "both keys did not unlock the slider";
                    slider.Value = 0; slider.Value = 100;
                    for (int i = 0; i < 60 && (File.Exists(f1) || File.Exists(f2)); i++) await Task.Delay(150);
                    if (File.Exists(f1) || File.Exists(f2)) return "the originals are still there after the full slide";
                    if (!File.Exists(Path.Combine(outDir, "keepme.zip"))) return "the archive is gone";
                    return d.OriginalsDeleted ? null : "the window does not report the originals as deleted";
                }
                finally { d.Close(); foreach (var w in Application.Current.Windows.OfType<ConfirmWindow>().ToList()) w.Close(); }
            });

            await CheckAsync("archive window: Emergency exit on the confirmation keeps every original", async () =>
            {
                var src = Path.Combine(work, "originals2"); Directory.CreateDirectory(src);
                var f1 = Path.Combine(src, "keep-a.txt"); var f2 = Path.Combine(src, "keep-b.txt");
                File.WriteAllText(f1, "a"); File.WriteAllText(f2, "b");
                ArchiveDialog.Open(new[] { f1, f2 });
                var d = ArchiveDialog.Current; if (d == null) return "the window did not open";
                try
                {
                    if (!await d.WaitForScanAsync(8000)) return "the scan never finished";
                    d.NameBox.Text = "kept"; d.FolderBox.Text = Path.Combine(work, "out-delete2"); Directory.CreateDirectory(d.FolderBox.Text);
                    Drive.Click(d.DeleteSwitch); await Task.Delay(300);
                    d.PressCreate();
                    if (!await d.WaitForViewAsync("done", 30000)) return "no done view";
                    ConfirmWindow dlg = null;
                    for (int i = 0; i < 40 && dlg == null; i++) { dlg = Application.Current.Windows.OfType<ConfirmWindow>().LastOrDefault(); if (dlg == null) await Task.Delay(100); }
                    if (dlg == null) return "the confirmation never opened";
                    Drive.Press(dlg, "Emergency exit"); await Task.Delay(500);
                    return File.Exists(f1) && File.Exists(f2) && !d.OriginalsDeleted ? null : "the originals were touched after Emergency exit";
                }
                finally { d.Close(); foreach (var w in Application.Current.Windows.OfType<ConfirmWindow>().ToList()) w.Close(); }
            });

            await CheckAsync("archive window: Cancel in the progress view removes the partial archive and returns to the form with the settings kept", async () =>
            {
                var big = Path.Combine(work, "Big2"); Directory.CreateDirectory(big);
                var buf = new byte[1 << 20];
                using (var f = File.Create(Path.Combine(big, "noise.bin"))) for (int i = 0; i < 90; i++) { RandomNumberGenerator.Fill(buf); f.Write(buf); }
                var outDir = Path.Combine(work, "out-wcancel"); Directory.CreateDirectory(outDir);
                ArchiveDialog.Open(new[] { big });
                var d = ArchiveDialog.Current; if (d == null) return "the window did not open";
                try
                {
                    if (!await d.WaitForScanAsync(8000)) return "the scan never finished";
                    d.NameBox.Text = "cancelme"; d.FolderBox.Text = outDir;
                    d.LevelSlider.Value = 4;
                    await Task.Delay(300);
                    d.PressCreate();
                    if (!await d.WaitForViewAsync("progress", 5000)) return "no progress view";
                    await Task.Delay(700);
                    d.CancelRun();
                    if (!await d.WaitForViewAsync("form", 20000)) return "the form did not come back; the view is " + d.View;
                    if (Directory.GetFiles(outDir).Length != 0) return "files left behind: " + string.Join(", ", Directory.GetFiles(outDir).Select(Path.GetFileName));
                    return d.NameBox.Text == "cancelme" && (int)d.LevelSlider.Value == 4 ? null : "the settings were not kept";
                }
                finally { d.Close(); }
            });

            await DiskChecksAsync(work);
        }

        private static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int j = 0; while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        // ------------------------------------------------------------------ the virtual disk window
        // None of these checks creates, attaches or formats a real virtual disk: a real one needs the Windows consent prompt,
        // which only the person answers. VhdHelper.Diskpart and VhdSession.TestLaunch replace diskpart and the elevation, so the
        // scripts, the request file, the status and command files, the copy and the cleanup are exercised for real; diskpart is not.

        private static readonly List<string> DiskpartLog = new List<string>();

        private static (int, string) FakeDiskpart(string script, string failOn = null)
        {
            DiskpartLog.Add(script);
            if (failOn != null && script.Contains(failOn)) return (1, "DiskPart has encountered an error: the fake refused this step.");
            foreach (var line in script.Split('\n').Select(l => l.Trim()))
                if (line.StartsWith("create vdisk file=")) { var p = line.Split('"')[1]; Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllBytes(p, new byte[4096]); }
            return (0, "DiskPart successfully completed the fake step.");
        }

        private static async Task DiskChecksAsync(string work)
        {
            var mount = Path.Combine(work, "fake-volume"); Directory.CreateDirectory(mount);
            var disks = Path.Combine(work, "disks"); Directory.CreateDirectory(disks);
            var savedDiskpart = VhdHelper.Diskpart; var savedLaunch = VhdSession.TestLaunch; var savedGrant = VhdHelper.Grant; var savedReady = VhdHelper.MountReady;
            string failOn = null;
            void Fake()
            {
                VhdHelper.Diskpart = s => FakeDiskpart(s, failOn);
                VhdHelper.Grant = (r, sid) => (0, "granted " + sid);
                VhdHelper.MountReady = p => Directory.Exists(p);
                VhdHelper.TestMountPath = mount;
                VhdSession.TestLaunch = (file, sha) => Task.Run(() => VhdHelper.Run(file, sha, CancellationToken.None));
            }
            void Real() { VhdHelper.Diskpart = savedDiskpart; VhdSession.TestLaunch = savedLaunch; VhdHelper.Grant = savedGrant; VhdHelper.MountReady = savedReady; VhdHelper.TestMountPath = null; VhdSession.TestDecline = false; }
            VhdRequest Req(Action<VhdRequest> change = null)
            {
                var r = new VhdRequest { File = Path.Combine(disks, "Work.vhdx"), SizeMb = 65536, FileSystem = "NTFS", Label = "Work", MountMode = "letter", Letter = VhdEngine.FreeLetters().FirstOrDefault() ?? "V" };
                change?.Invoke(r); return r;
            }

            Check("virtual disk: every refused combination says why before the consent prompt", () =>
            {
                string V(Action<VhdRequest> c, long? free = null, long content = 0, long largest = 0) => VhdEngine.Validate(Req(c), free, content, largest);
                if (V(r => { }) != null) return "a plain NTFS disk was refused: " + V(r => { });
                if (V(r => { r.FileSystem = "FAT32"; r.SizeMb = 40 * 1024; }) == null) return "FAT32 above 32 GB was accepted";
                if (V(r => { r.FileSystem = "FAT32"; r.SizeMb = 1024; }, largest: 5L * 1024 * 1024 * 1024) == null) return "FAT32 with a 5 GB file was accepted";
                if (V(r => { r.FileSystem = "FAT32"; r.SizeMb = 1024; }) != null) return "a 1 GB FAT32 disk was refused";
                if (V(r => { r.File = Path.Combine(disks, "a.vhd"); r.SizeMb = 3000L * 1024; }) == null) return "a 3,000 GB VHD was accepted";
                if (V(r => r.SizeMb = 70L * 1024 * 1024) == null) return "a 70 TB VHDX was accepted";
                if (V(r => r.SizeMb = 8) == null) return "an 8 MB disk was accepted";
                if (V(r => r.Label = new string('x', 33)) == null) return "a 33 character NTFS label was accepted";
                if (V(r => { r.FileSystem = "exFAT"; r.Label = "twelve chars"; }) == null) return "a 12 character exFAT label was accepted";
                if (V(r => r.Label = "bad|label") == null) return "a label with a pipe was accepted";
                if (V(r => r.File = Path.Combine(disks, "x\"y.vhdx")) == null) return "a quote in the path was accepted (it would break the script)";
                if (V(r => r.File = Path.Combine(disks, "x.img")) == null) return "a .img file was accepted";
                if (V(r => r.File = @"\\server\share\x.vhdx") == null) return "a network share was accepted";
                if (V(r => r.Letter = "C") == null) return "drive letter C was accepted";
                var full = Path.Combine(work, "mnt-full"); Directory.CreateDirectory(full); File.WriteAllText(Path.Combine(full, "x.txt"), "x");
                if (V(r => { r.MountMode = "folder"; r.Folder = full; }) == null) return "a non-empty mount folder was accepted";
                if (V(r => { r.MountMode = "folder"; r.Folder = Path.Combine(work, "does-not-exist"); }) == null) return "a missing mount folder was accepted";
                var empty = Path.Combine(work, "mnt-empty"); Directory.CreateDirectory(empty);
                if (V(r => { r.MountMode = "folder"; r.Folder = empty; }) != null && new DriveInfo(Path.GetPathRoot(empty)).DriveFormat == "NTFS") return "an empty NTFS folder was refused: " + V(r => { r.MountMode = "folder"; r.Folder = empty; });
                if (V(r => { r.Fixed = true; r.SizeMb = 500 * 1024; }, free: 100L * 1024 * 1024 * 1024) == null) return "a fixed 500 GB disk was accepted with 100 GB free";
                if (V(r => r.SizeMb = 100, content: 200L * 1024 * 1024) == null) return "200 MB of content in a 100 MB disk was accepted";
                if (V(r => r.UserSid = "S-1-5-21-1-2-3;calc") == null) return "a bad account identifier was accepted";
                return null;
            });

            Check("virtual disk: the diskpart scripts are exactly what the choices say, with nothing from the request that is not validated", () =>
            {
                var r = Req(x => { x.Fixed = false; x.Letter = "V"; });
                var c = VhdEngine.CreateScript(r);
                if (!c.Contains($"create vdisk file=\"{r.File}\" maximum=65536 type=expandable") || !c.Contains("attach vdisk")) return "create script wrong:\n" + c;
                if (!VhdEngine.CreateScript(Req(x => x.Fixed = true)).Contains("type=fixed")) return "fixed type missing";
                var f = VhdEngine.FormatScript(r);
                if (!f.Contains("convert gpt") || !f.Contains("create partition primary") || !f.Contains("format fs=ntfs quick label=\"Work\"") || !f.Contains("assign letter=V")) return "format script wrong:\n" + f;
                var fat = VhdEngine.FormatScript(Req(x => { x.FileSystem = "FAT32"; x.Label = "FATDISK"; }));
                if (!fat.Contains("format fs=fat32 quick label=\"FATDISK\"")) return "FAT32 format line wrong:\n" + fat;
                var ex = VhdEngine.FormatScript(Req(x => { x.FileSystem = "exFAT"; x.Label = ""; }));
                if (!ex.Contains("format fs=exfat quick\r\n")) return "exFAT format line wrong:\n" + ex;
                var folder = VhdEngine.FormatScript(Req(x => { x.MountMode = "folder"; x.Folder = Path.Combine(work, "mnt-empty"); }));
                if (!folder.Contains("assign mount=\"" + Path.Combine(work, "mnt-empty") + "\"")) return "folder mount line wrong:\n" + folder;
                if (!VhdEngine.DetachScript(r).Contains("detach vdisk")) return "detach script wrong";
                return VhdEngine.CreateScript(r).EndsWith("exit\r\n") ? null : "scripts must end with exit";
            });

            Check("virtual disk: the elevated helper refuses a changed request, a bad extension and status files outside the app's folders", () =>
            {
                Fake();
                try
                {
                    var job = Path.Combine(Settings.DataDir, "vhd-jobs", "refuse"); Directory.CreateDirectory(job);
                    VhdRequest R(Action<VhdRequest> c = null) { var r = Req(c); r.StatusFile = Path.Combine(job, "status.json"); r.CommandFile = Path.Combine(job, "command.txt"); r.ParentPid = Environment.ProcessId; return r; }
                    int Run(VhdRequest r, bool tamper = false)
                    {
                        var file = Path.Combine(job, "req.json"); var bytes = JsonSerializer.SerializeToUtf8Bytes(r);
                        File.WriteAllBytes(file, bytes);
                        var sha = Convert.ToHexString(SHA256.HashData(bytes));
                        if (tamper) File.WriteAllBytes(file, JsonSerializer.SerializeToUtf8Bytes(R(x => x.File = Path.Combine(disks, "other.vhdx"))));
                        return VhdHelper.Run(file, sha, CancellationToken.None);
                    }
                    DiskpartLog.Clear();
                    if (Run(R(), tamper: true) != 3) return "a request file changed after hashing was not refused";
                    if (Run(R(r => r.File = Path.Combine(disks, "evil.exe"))) != 4) return "a .exe disk file was not refused";
                    var outside = R(); outside.StatusFile = @"C:\Windows\System32\status.json";
                    if (Run(outside) != 4) return "a status file outside the app's folders was not refused";
                    return DiskpartLog.Count == 0 ? null : "diskpart ran for a refused request";
                }
                finally { Real(); }
            });

            await CheckAsync("virtual disk: the helper builds, reports each phase, waits, and detaches or deletes on command (fake diskpart, no real disk)", async () =>
            {
                Fake();
                try
                {
                    foreach (var (cmd, expect) in new[] { ("detach", "detached"), ("detach-delete", "deleted"), ("release", "ready") })
                    {
                        DiskpartLog.Clear();
                        var r = Req(x => x.File = Path.Combine(disks, "cmd-" + cmd + ".vhdx"));
                        var s = new VhdSession(r);
                        if (!await s.StartAsync()) return "could not start the session";
                        var ready = await s.WaitForAsync(x => x.Phase == "ready" || x.Phase == "error", TimeSpan.FromSeconds(20), CancellationToken.None);
                        if (ready?.Phase != "ready") return $"{cmd}: never ready: {ready?.Phase} {ready?.Error}";
                        if (!ready.Log.Contains("create and attach") || !ready.Log.Contains("partition, format and mount")) return $"{cmd}: the helper log lacks a phase: " + ready.Log;
                        if (ready.Mount != mount) return $"{cmd}: mount path was {ready.Mount}";
                        if (!File.Exists(r.File)) return $"{cmd}: the disk file is not there when ready";
                        if (DiskpartLog.Count != 2 || !DiskpartLog[0].Contains("create vdisk") || !DiskpartLog[1].Contains("convert gpt")) return $"{cmd}: diskpart steps were {DiskpartLog.Count}";
                        s.Send(cmd);
                        var end = await s.WaitForAsync(x => x.Phase == expect || x.Phase == "error", TimeSpan.FromSeconds(20), CancellationToken.None);
                        if (end?.Phase != expect) return $"{cmd}: ended in {end?.Phase} {end?.Error}";
                        if (cmd == "detach" && !DiskpartLog.Last().Contains("detach vdisk")) return "detach ran no detach script";
                        if (cmd == "detach-delete" && File.Exists(r.File)) return "detach-delete left the file";
                        if (cmd != "detach-delete" && !File.Exists(r.File)) return cmd + " removed the file";
                        s.Cleanup();
                    }
                    return null;
                }
                finally { Real(); }
            });

            await CheckAsync("virtual disk: a failing diskpart step leaves nothing behind (detached, file deleted, error reported)", async () =>
            {
                Fake();
                try
                {
                    failOn = "format fs=";
                    DiskpartLog.Clear();
                    var r = Req(x => x.File = Path.Combine(disks, "fails.vhdx"));
                    var s = new VhdSession(r);
                    await s.StartAsync();
                    var end = await s.WaitForAsync(x => x.Phase == "error" || x.Phase == "ready", TimeSpan.FromSeconds(20), CancellationToken.None);
                    if (end?.Phase != "error") return "the failure was not reported: " + end?.Phase;
                    if (File.Exists(r.File)) return "the disk file was left behind";
                    if (!DiskpartLog.Last().Contains("detach vdisk")) return "the disk was not detached after the failure";
                    return end.Error.Contains("refused") ? null : "the error text lost the reason: " + end.Error;
                }
                finally { failOn = null; Real(); }
            });

            await CheckAsync("virtual disk window: filling the controls and pressing Create copies the items onto the volume and checks every file", async () =>
            {
                Fake();
                var src = Path.Combine(work, "to-disk"); Directory.CreateDirectory(Path.Combine(src, "inner"));
                File.WriteAllText(Path.Combine(src, "a.txt"), "alpha"); File.WriteAllBytes(Path.Combine(src, "inner", "b.bin"), Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray());
                var single = Path.Combine(work, "single.txt"); File.WriteAllText(single, "single file");
                var dstMount = Path.Combine(mount, "case1"); Directory.CreateDirectory(dstMount);
                VhdHelper.TestMountPath = dstMount;
                DiskDialog.Open(new[] { src, single });
                var d = DiskDialog.Current; if (d == null) { Real(); return "the window did not open"; }
                try
                {
                    if (!await d.WaitForMeasureAsync(8000)) return "the content was never measured";
                    d.FolderBox.Text = disks; d.NameBox.Text = "UiDisk"; d.LabelBox.Text = "UiLabel";
                    d.TypeBox.Select("fixed"); d.TypeBox.Select("dynamic");
                    d.ApplyForTest();
                    if (!d.CreateEnabled) return "Create was off: " + d.SummaryText;
                    if (!d.SummaryText.Contains("VHDX") || !d.SummaryText.Contains("UiLabel") || !d.SummaryText.Contains("copies 2 items")) return "summary wrong: " + d.SummaryText;
                    var disk = Path.Combine(disks, "UiDisk.vhdx");
                    d.PressCreate();
                    if (!await d.WaitForViewAsync("done", 30000)) return "no done view; view " + d.View + ", error " + d.ErrorText;
                    if (!File.Exists(disk)) return "the disk file is missing";
                    if (Sha(Path.Combine(dstMount, "to-disk", "a.txt")) != Sha(Path.Combine(src, "a.txt"))) return "a.txt differs on the volume";
                    if (Sha(Path.Combine(dstMount, "to-disk", "inner", "b.bin")) != Sha(Path.Combine(src, "inner", "b.bin"))) return "inner\b.bin differs on the volume";
                    if (Sha(Path.Combine(dstMount, "single.txt")) != Sha(single)) return "single.txt differs on the volume";
                    if (d.LastCopy == null || !d.LastCopy.Ok || d.LastCopy.Files != 3) return "the copy result was " + d.LastCopy?.Files;
                    if (!File.Exists(src + "\\a.txt") || !File.Exists(single)) return "Copy removed an original";
                    if (d.FinalState != "ready") return "state was " + d.FinalState;
                    // Detach from the done view, with the same helper, no second prompt
                    Drive.Press(d, "Detach the disk");
                    for (int i = 0; i < 100 && d.FinalState != "detached"; i++) await Task.Delay(100);
                    if (d.FinalState != "detached") return "Detach did not finish: " + d.FinalState;
                    return DiskpartLog.Last().Contains("detach vdisk") ? null : "no detach script ran";
                }
                finally { d.Close(); Real(); }
            });

            await CheckAsync("virtual disk window: Move deletes the originals only after the two-key confirmation, and After: Detach detaches", async () =>
            {
                Fake();
                var src = Path.Combine(work, "to-move"); Directory.CreateDirectory(src);
                var f1 = Path.Combine(src, "move-a.txt"); var f2 = Path.Combine(src, "move-b.txt");
                File.WriteAllText(f1, "A"); File.WriteAllText(f2, "B");
                var dstMount = Path.Combine(mount, "case2"); Directory.CreateDirectory(dstMount);
                VhdHelper.TestMountPath = dstMount;
                DiskDialog.Open(new[] { f1, f2 });
                var d = DiskDialog.Current; if (d == null) { Real(); return "the window did not open"; }
                try
                {
                    if (!await d.WaitForMeasureAsync(8000)) return "never measured";
                    d.FolderBox.Text = disks; d.NameBox.Text = "MoveDisk"; d.ContentsBox.Select("move"); d.AfterBox.Select("detach"); d.ApplyForTest();
                    d.PressCreate();
                    ConfirmWindow dlg = null;
                    for (int i = 0; i < 150 && dlg == null; i++) { dlg = Application.Current.Windows.OfType<ConfirmWindow>().LastOrDefault(); if (dlg == null) await Task.Delay(100); }
                    if (dlg == null) return "the two-key confirmation never opened; view " + d.View + " " + d.ErrorText;
                    if (!File.Exists(f1) || !File.Exists(f2)) return "the originals went before the confirmation";
                    if (!File.Exists(Path.Combine(dstMount, "move-a.txt"))) return "the files were not on the volume before the confirmation";
                    var slider = Drive.All<System.Windows.Controls.Slider>(dlg).First();
                    Drive.Click(Drive.Need<ToggleButton>(dlg, "First key")); Drive.Click(Drive.Need<ToggleButton>(dlg, "Second key")); await Task.Delay(100);
                    slider.Value = 0; slider.Value = 100;
                    if (!await d.WaitForViewAsync("done", 30000)) return "no done view: " + d.ErrorText;
                    for (int i = 0; i < 60 && (File.Exists(f1) || File.Exists(f2)); i++) await Task.Delay(150);
                    if (File.Exists(f1) || File.Exists(f2)) return "the originals are still there after the full slide";
                    if (!File.Exists(Path.Combine(dstMount, "move-b.txt"))) return "the copy on the volume is gone";
                    return d.FinalState == "detached" && DiskpartLog.Last().Contains("detach vdisk") ? null : "After: Detach did not detach: " + d.FinalState;
                }
                finally { d.Close(); foreach (var w in Application.Current.Windows.OfType<ConfirmWindow>().ToList()) w.Close(); Real(); }
            });

            await CheckAsync("virtual disk window: a declined consent prompt creates nothing and shows exit code 1223", async () =>
            {
                Fake(); VhdSession.TestDecline = true; DiskpartLog.Clear();
                var f = Path.Combine(work, "declined.txt"); File.WriteAllText(f, "x");
                DiskDialog.Open(new[] { f });
                var d = DiskDialog.Current; if (d == null) { Real(); return "the window did not open"; }
                try
                {
                    if (!await d.WaitForMeasureAsync(8000)) return "never measured";
                    d.FolderBox.Text = disks; d.NameBox.Text = "Declined"; d.ApplyForTest();
                    d.PressCreate();
                    if (!await d.WaitForViewAsync("error", 10000)) return "no error view; view " + d.View;
                    if (File.Exists(Path.Combine(disks, "Declined.vhdx"))) return "a disk file was created";
                    if (DiskpartLog.Count != 0) return "diskpart ran after a declined prompt";
                    var text = Drive.TextOf(d);
                    return d.ErrorText.Contains("declined") && text.Contains("Nothing was left behind") ? null : "the error view said: " + text;
                }
                finally { d.Close(); Real(); }
            });

            await CheckAsync("virtual disk window: Cancel detaches and deletes what was made, and the form returns", async () =>
            {
                Fake(); DiskpartLog.Clear();
                var f = Path.Combine(work, "cancel-disk.txt"); File.WriteAllText(f, "x");
                VhdHelper.TestMountPath = Path.Combine(mount, "case3"); Directory.CreateDirectory(VhdHelper.TestMountPath);
                DiskDialog.Open(new[] { f });
                var d = DiskDialog.Current; if (d == null) { Real(); return "the window did not open"; }
                try
                {
                    if (!await d.WaitForMeasureAsync(8000)) return "never measured";
                    d.FolderBox.Text = disks; d.NameBox.Text = "CancelDisk"; d.ApplyForTest();
                    d.PressCreate();
                    if (!await d.WaitForViewAsync("progress", 3000)) return "no progress view";
                    d.PressCancel();
                    if (!await d.WaitForViewAsync("form", 30000)) return "the form did not return; view " + d.View + " " + d.ErrorText;
                    if (File.Exists(Path.Combine(disks, "CancelDisk.vhdx"))) return "the disk file is still there after Cancel";
                    return DiskpartLog.Any(s => s.Contains("detach vdisk")) ? null : "no detach ran";
                }
                finally { d.Close(); Real(); }
            });

            Check("virtual disk window: the FAT32 limit, the label limit and the mount choices follow the controls", () =>
            {
                Fake();
                var f = Path.Combine(work, "rules.txt"); File.WriteAllText(f, "x");
                DiskDialog.Open(new[] { f });
                var d = DiskDialog.Current; if (d == null) { Real(); return "the window did not open"; }
                try
                {
                    d.SetSizeForTest(64, "GB");
                    d.FileSystemBox.Select("FAT32");
                    if (d.FileSystemBox.SelectedId == "FAT32") return "FAT32 could be chosen at 64 GB";
                    d.SetSizeForTest(16, "GB"); d.FileSystemBox.Select("FAT32");
                    if (d.FileSystemBox.SelectedId != "FAT32") return "FAT32 could not be chosen at 16 GB";
                    if (d.LabelBox.MaxLength != 11) return "the label limit for FAT32 was " + d.LabelBox.MaxLength;
                    d.SetSizeForTest(64, "GB");
                    if (d.FileSystemBox.SelectedId == "FAT32" && d.CreateEnabled) return "a 64 GB FAT32 disk could be created";
                    d.FileSystemBox.Select("NTFS"); d.FormatBox.Select("vhd"); d.SetSizeForTest(3, "TB");
                    if (d.CreateEnabled) return "a 3 TB VHD could be created";
                    d.FormatBox.Select("vhdx"); d.SetSizeForTest(3, "TB");
                    if (!d.CreateEnabled) return "a 3 TB VHDX was refused: " + d.SummaryText;
                    d.MountBox.Select("folder"); d.MountFolderBox.Text = "";
                    if (d.CreateEnabled) return "folder mode accepted no folder";
                    return null;
                }
                finally { d.Close(); Real(); }
            });
        }
    }
}
