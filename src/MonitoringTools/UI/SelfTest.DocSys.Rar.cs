using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Core.DocTools;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    // RAR archives through the bundled full 7-Zip (7z.exe with 7z.dll). No RAR can be made without WinRAR, so every fixture is
    // built byte by byte from the documented formats (RarFixtures): stored RAR4 and RAR5 archives, a RAR4 with an escaping name,
    // RAR5 archives with encrypted data and with encrypted names, and a large stored RAR5 for Cancel.
    public static partial class SelfTest
    {
        private static readonly (string Name, byte[] Data)[] RarFiles =
        {
            ("readme.txt", Encoding.UTF8.GetBytes("RAR-README\r\n")),
            ("docs/notes.txt", Encoding.UTF8.GetBytes("RAR-NOTES in a subfolder\r\n")),
        };

        /// <summary>Null when every RarFiles entry is in <paramref name="dest"/> with the same SHA-256, and nothing was left behind.</summary>
        private static string SameRarTree(string dest)
        {
            foreach (var (name, data) in RarFiles)
            {
                var f = Path.Combine(dest, name.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(f)) return name + " was not written";
                var want = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
                if (DocSha(f) != want) return name + " has SHA-256 " + DocSha(f) + ", expected " + want;
            }
            if (Directory.EnumerateDirectories(dest, ".extracting-*").Any()) return "the hidden unpacking folder was left behind";
            if (Directory.EnumerateFiles(dest, "*.part", SearchOption.AllDirectories).Any()) return "a .part file was left behind";
            return null;
        }

        private static async Task RarDocChecksAsync()
        {
            if (!ArchiveTools.RarAvailable)
            {
                Check("archive dialog: RAR is read through the bundled full 7-Zip", () => "the full 7-Zip (7z.exe with 7z.dll) is missing from this build");
                return;
            }

            await CheckAsync("archive dialog: a stored RAR4 and a stored RAR5, built byte by byte, are listed through the full 7-Zip with their folder and extracted with matching SHA-256", async () =>
            {
                var t = DocTmp("rar-plain");
                foreach (var (tag, bytes) in new[] { ("rar4", RarFixtures.Rar4(RarFiles)), ("rar5", RarFixtures.Rar5(RarFiles)) })
                {
                    var arc = Path.Combine(t, tag + ".rar"); File.WriteAllBytes(arc, bytes);
                    var d = ExtractArchiveDialog.Open(arc);
                    try
                    {
                        await Pump(600);
                        if (d.State != ArchiveState.Ready) return tag + ": state " + d.State + ": " + Drive.TextOf(d);
                        if (d.Info.Format != "rar" || !d.Info.UseSevenZip) return tag + ": format " + d.Info.Format;
                        var listed = string.Join(", ", d.Info.Entries.Select(e => e.Path + ":" + e.Size));
                        if (d.Info.FileCount != 2 || RarFiles.Any(f => !d.Info.Entries.Any(e => e.Path == f.Name && e.Size == f.Data.Length && !e.Unsafe && !e.Encrypted))) return tag + ": listed " + listed;
                        if (!Drive.TextOf(d).Contains("2 of 2 selected")) return tag + ": the selection line is missing";
                        if (tag == "rar5") await DocAudit("archive rar ready", d);
                        var dest = Path.Combine(t, "out-" + tag);
                        var r = await ExtractAll(d, dest); if (r != null) return tag + ": " + r;
                        if (d.Result.Extracted != 2 || d.Result.Refused.Count != 0) return $"{tag}: extracted {d.Result.Extracted}, refused {d.Result.Refused.Count}";
                        var same = SameRarTree(dest); if (same != null) return tag + ": " + same;
                    }
                    finally { d?.Close(); }
                }
                return null;
            });

            await CheckAsync("archive dialog: a RAR4 entry named ..\\evil.txt is marked unsafe and refused, the safe entry is extracted, and nothing lands outside the destination", async () =>
            {
                var t = DocTmp("rar-slip");
                var arc = Path.Combine(t, "slip.rar");
                File.WriteAllBytes(arc, RarFixtures.Rar4(("ok.txt", Encoding.UTF8.GetBytes("SAFE")), ("..\\evil.txt", Encoding.UTF8.GetBytes("EVIL"))));
                var d = ExtractArchiveDialog.Open(arc);
                try
                {
                    await Pump(600);
                    if (d.State != ArchiveState.Ready) return "state " + d.State + ": " + Drive.TextOf(d);
                    if (!d.Info.Entries.Any(e => e.Path == "../evil.txt" && e.Unsafe)) return "the escaping name is not marked unsafe: " + string.Join(", ", d.Info.Entries.Select(e => e.Path + (e.Unsafe ? " (unsafe)" : "")));
                    if (!Drive.TextOf(d).Contains("unsafe path, will be refused")) return "the escaping path is not marked in the list";
                    var dest = Path.Combine(t, "out");
                    var r = await ExtractAll(d, dest); if (r != null) return r;
                    if (d.Result.Extracted != 1 || d.Result.Refused.Count != 1) return $"extracted {d.Result.Extracted}, refused {d.Result.Refused.Count}";
                    if (File.Exists(Path.Combine(t, "evil.txt")) || File.Exists(Path.Combine(dest, "evil.txt"))) return "the escaping entry was written";
                    if (File.ReadAllText(Path.Combine(dest, "ok.txt")) != "SAFE") return "the safe entry holds the wrong bytes";
                    if (!Drive.TextOf(d).Contains("1 unsafe path")) return "the Done card does not count the refused path";
                    return null;
                }
                finally { d?.Close(); }
            });

            await CheckAsync("archive dialog: a password RAR5 asks first; a wrong password extracts nothing and says so; the right one extracts; a RAR5 with encrypted names lists its files only after the password", async () =>
            {
                var t = DocTmp("rar-password");
                var arc = Path.Combine(t, "locked.rar"); File.WriteAllBytes(arc, RarFixtures.Rar5("rar-horse", false, RarFiles));
                var hidden = Path.Combine(t, "hidden-names.rar"); File.WriteAllBytes(hidden, RarFixtures.Rar5("rar-horse", true, RarFiles));
                foreach (var (file, namesLocked) in new[] { (arc, false), (hidden, true) })
                {
                    var tag = Path.GetFileName(file);
                    var d = ExtractArchiveDialog.Open(file);
                    try
                    {
                        await Pump(600);
                        if (d.State != ArchiveState.Password) return tag + ": state " + d.State + ": " + Drive.TextOf(d);
                        if (d.Info.NamesLocked != namesLocked) return tag + ": names locked is " + d.Info.NamesLocked;
                        if (namesLocked && (d.Info.FileCount != 0 || !Drive.TextOf(d).Contains("Even the file names are locked"))) return tag + ": the names-hidden state is wrong: " + Drive.TextOf(d);
                        if (!namesLocked && (d.Info.FileCount != 2 || d.Info.Entries.Any(e => !e.Encrypted))) return tag + ": listed " + d.Info.FileCount;
                        if (!namesLocked) await DocAudit("archive rar password", d);
                        var dest = Path.Combine(t, "out-" + tag);
                        d.SetDestinationForTest(dest);
                        d.StartForTest("wrong-horse"); await Pump(1500);
                        if (d.State != ArchiveState.Password) return tag + ": a wrong password left state " + d.State;
                        if (!Drive.TextOf(d).Contains("Wrong password for this archive. Nothing was extracted.")) return tag + ": no wrong-password message";
                        if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any()) return tag + ": a wrong password wrote something";
                        d.SetDestinationForTest(dest);
                        d.StartForTest("rar-horse");
                        if (!await UntilDocState(() => d.State == ArchiveState.Done, 30000)) return tag + ": never done with the right password, state " + d.State;
                        if (d.Result.Error != null || d.Result.WrongPassword) return tag + ": " + d.Result.Error;
                        if (d.Result.Extracted != 2) return tag + ": extracted " + d.Result.Extracted;
                        var same = SameRarTree(dest); if (same != null) return tag + ": " + same;
                    }
                    finally { d?.Close(); }
                }
                return null;
            });

            Check("archive engine: Cancel while the full 7-Zip unpacks a RAR leaves nothing in the destination, not even the hidden unpacking folder", () =>
            {
                var t = DocTmp("rar-cancel");
                var rnd = new Random(17);
                var files = Enumerable.Range(0, 4).Select(i => { var b = new byte[40_000_000]; rnd.NextBytes(b); return ($"blob{i}.bin", b); }).ToArray();
                var arc = Path.Combine(t, "big.rar");
                File.WriteAllBytes(arc, RarFixtures.Rar5(files));
                files = null;
                var info = ArchiveTools.Open(arc);
                if (info.Unsupported != null || info.FileCount != 4) return "listing: " + info.Unsupported + " / " + info.FileCount;
                var dest = Path.Combine(t, "out");
                using var cts = new CancellationTokenSource();
                int reports = 0;
                var r = ArchiveTools.Extract(info, info.Entries, dest, OverwriteRule.KeepBoth, null, new SyncProgress(p => { reports++; cts.Cancel(); }), cts.Token);
                if (reports == 0) return "no progress was reported before the end (extracted " + r.Extracted + ")";
                if (!r.Canceled) return "Canceled was not reported (extracted " + r.Extracted + ", error " + r.Error + ")";
                if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any()) return "left behind: " + string.Join(", ", Directory.EnumerateFileSystemEntries(dest).Select(Path.GetFileName));
                return null;
            });
        }
    }
}
