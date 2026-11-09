using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core;
using MonitoringTools.Core.Disks;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Real virtual disk operations on this computer, in a throwaway folder: create, attach, detach, grow, shrink, compact,
    /// convert, differencing and merge, a new identifier, and a Quick pack that has to grow on its own, with a clash and a
    /// cancel. It needs administrator rights, which the build machine has; it is run with MT_ONLY=vdisk.
    /// </summary>
    public static partial class SelfTest
    {
        /// <summary>
        /// Opens the Virtual disks page in each state a person meets (a disk chosen, each action panel, a differencing child,
        /// an attached disk with its volumes, every Quick pack stage and the New virtual disk card) and checks that no English is
        /// left in Cantonese, and that both languages show in bilingual mode.
        /// </summary>
        private static async Task<string> VirtualDiskPageLanguageAsync(string disk, string kid, string folder, string newBase)
        {
            var made = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "create", File = kid, Type = "differencing", Other = disk, Init = "none" }));
            if (made.Phase != "done") return "the differencing child was not made: " + made.Error;
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            cc.Show();
            await Task.Delay(900);
            var left = new List<string>();
            try
            {
                async Task Read(params string[] paths)
                {
                    cc.VdAdd(paths); cc.VdTestShow(paths[0], null);
                    for (int i = 0; i < 100 && !ControlCentreWindow.VdTestRead(paths); i++) await Task.Delay(100);
                }
                await Read(kid, disk);
                var states = new List<(string Where, Func<Task> Show)>
                {
                    ("a disk", async () => { cc.VdTestShow(disk, null); await Task.Delay(300); }),
                };
                foreach (var a in new[] { "attach", "grow", "shrink", "compact", "convert", "reset-id" })
                    states.Add(("action " + a, async () => { cc.VdTestShow(disk, a); await Task.Delay(300); }));
                foreach (var a in new string[] { null, "merge", "set-parent" })
                    states.Add(("child " + (a ?? "details"), async () => { cc.VdTestShow(kid, a); await Task.Delay(300); }));
                foreach (var phase in new[] { "ready", "packing", "done", "partial", "cancelled", "stopped" })
                    states.Add(("quick pack " + phase, async () => { await cc.VdTestPackAsync(phase, new[] { folder }, phase == "done" || phase == "partial" ? disk : null); await Task.Delay(300); }));
                states.Add(("new dev drive", async () => { cc.VdTestPackClear(); cc.VdTestNew(new DiskJob { Op = "create", File = newBase + ".vhdx", Type = "dynamic", SizeBytes = 80 * DiskJobs.Gb, Init = "GPT", FileSystem = "ReFS", Label = "Dev", DevDrive = true }); await Task.Delay(300); }));
                states.Add(("new child", async () => { cc.VdTestNew(new DiskJob { Op = "create", File = newBase + "-child.vhdx", Type = "differencing", Init = "none" }); await Task.Delay(300); }));
                states.Add(("new too big", async () => { cc.VdTestNew(new DiskJob { Op = "create", File = newBase + ".vhd", Type = "fixed", SizeBytes = 3000 * DiskJobs.Gb, Init = "GPT", FileSystem = "FAT32", Label = "Old" }); await Task.Delay(300); }));

                foreach (var mode in new[] { LanguageMode.Cantonese, LanguageMode.Bilingual })
                {
                    LanguageAudit.Begin(mode);
                    try
                    {
                        foreach (var (where, show) in states) { await show(); LanguageAudit.Scan(mode + " vdisks: " + where, cc); }
                        // attached, so the details show what is inside it and the detach panel
                        var att = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "attach", File = disk, NoLetters = true }));
                        if (att.Phase == "done")
                        {
                            await Read(disk);
                            for (int i = 0; i < 200 && !ControlCentreWindow.VdTestVolumesRead(disk); i++) await Task.Delay(100);
                            cc.VdTestShow(disk, null); await Task.Delay(400);
                            LanguageAudit.Scan(mode + " vdisks: attached", cc);
                            cc.VdTestShow(disk, "detach"); await Task.Delay(300);
                            LanguageAudit.Scan(mode + " vdisks: action detach", cc);
                            await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "detach", File = disk }));
                            await Read(disk);
                        }
                        else left.Add("attach for the volumes view: " + att.Error);
                        left.AddRange(LanguageAudit.Findings.Select(f => "\"" + f.Text.Replace("\n", " / ") + "\" (" + f.Where + ", " + f.Kind + ")"));
                    }
                    finally { LanguageAudit.End(); }
                }
            }
            finally { cc.VdTestPackClear(); cc.Close(); }
            return left.Count == 0 ? null : left.Count + " left: " + string.Join(" | ", left.Take(80));
        }

        private static async Task VirtualDiskChecksAsync(string dir)
        {
            var root = Path.Combine(Path.GetTempPath(), "mt-vdisk-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(root);
            string P(string name) => Path.Combine(root, name);
            long mb = DiskJobs.Mb;
            Check("vdisk: the Windows virtual disk service is here", () => VirtDisk.Available ? null : "virtdisk.dll did not load");
            try
            {
                await CheckAsync("vdisk: create a dynamic VHDX and read it back", async () =>
                {
                    var j = new DiskJob { Op = "create", File = P("a.vhdx"), Type = "dynamic", SizeBytes = 256 * mb, Init = "GPT", FileSystem = "NTFS", Label = "Test", NoLetters = true, KeepAttached = false };
                    var s = await Task.Run(() => DiskJobs.Execute(j));
                    if (s.Phase != "done") return s.Error + " " + s.Log;
                    var i = VirtDisk.Read(j.File);
                    if (i.Error != null) return i.Error;
                    if (i.Format != "VHDX" || i.Type != "dynamic") return $"read {i.Format} {i.Type}";
                    if (i.VirtualSize != 256UL * (ulong)mb) return "virtual size " + i.VirtualSize;
                    if (i.Attached) return "it should be detached";
                    return i.SmallestSafeSize == null ? "no smallest safe size" : null;
                });
                await CheckAsync("vdisk: attach read-only, list it, detach", async () =>
                {
                    var s = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "attach", File = P("a.vhdx"), ReadOnly = true, NoLetters = true }));
                    if (s.Phase != "done") return s.Error;
                    try
                    {
                        var i = VirtDisk.Read(P("a.vhdx"));
                        if (!i.Attached || i.DiskNumber == null) return "not attached after attach: " + i.PhysicalPath;
                        // The temporary folder can be named in its short 8.3 form (RUNNER~1) while Windows lists the long form, so
                        // the disk is recognised by its own uniquely named folder and file.
                        var listed = VirtDisk.Attached();
                        var tail = Path.DirectorySeparatorChar + Path.Combine(Path.GetFileName(root), "a.vhdx");
                        if (!listed.Any(x => x.EndsWith(tail, StringComparison.OrdinalIgnoreCase)))
                            return $"the attached list does not have it (Windows error {VirtDisk.LastAttachedError}): " + string.Join(", ", listed);
                        var again = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "attach", File = P("a.vhdx") }));
                        if (again.Phase != "done" || !again.Message.Contains("already attached")) return "a second attach was not recognised: " + again.Message + again.Error;
                        var d = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "detach", File = P("a.vhdx") }));
                        return d.Phase == "done" && !VirtDisk.Read(P("a.vhdx")).Attached ? null : "detach: " + d.Error;
                    }
                    finally
                    {
                        // A failed step above must not leave the disk attached for every check after it.
                        if (VirtDisk.Read(P("a.vhdx")).Attached) await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "detach", File = P("a.vhdx") }));
                    }
                });
                await CheckAsync("vdisk: grow and extend its volume", async () =>
                {
                    var s = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "grow", File = P("a.vhdx"), SizeBytes = 512 * mb, Volume = true }));
                    if (s.Phase != "done") return s.Error + " " + s.Log;
                    var i = VirtDisk.Read(P("a.vhdx"));
                    return i.VirtualSize == 512UL * (ulong)mb && !i.Attached ? null : $"size {i.VirtualSize}, attached {i.Attached}";
                });
                await CheckAsync("vdisk: shrink to the smallest safe size", async () =>
                {
                    var s = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "shrink", File = P("a.vhdx"), Volume = true }));
                    if (s.Phase != "done") return s.Error + " " + s.Log;
                    var i = VirtDisk.Read(P("a.vhdx"));
                    return i.VirtualSize < 512UL * (ulong)mb ? null : "still " + i.VirtualSize;
                });
                await CheckAsync("vdisk: compact, putting it back detached", async () =>
                {
                    var s = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "compact", File = P("a.vhdx"), Quick = true }));
                    return s.Phase == "done" && !VirtDisk.Read(P("a.vhdx")).Attached ? null : s.Error + " " + s.Log;
                });
                await CheckAsync("vdisk: convert VHDX to VHD beside it, the original unchanged", async () =>
                {
                    var before = File.GetLastWriteTimeUtc(P("a.vhdx"));
                    var s = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "convert", File = P("a.vhdx"), Other = P("a.vhd"), Type = "dynamic" }));
                    if (s.Phase != "done") return s.Error + " " + s.Log;
                    var i = VirtDisk.Read(P("a.vhd"));
                    if (i.Format != "VHD") return "converted to " + i.Format;
                    return File.GetLastWriteTimeUtc(P("a.vhdx")) == before ? null : "the original changed";
                });
                await CheckAsync("vdisk: a differencing child reads its parent, then merges into it", async () =>
                {
                    var c = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "create", File = P("child.vhdx"), Type = "differencing", Other = P("a.vhdx"), Init = "none" }));
                    if (c.Phase != "done") return "create child: " + c.Error;
                    var i = VirtDisk.Read(P("child.vhdx"));
                    if (i.Type != "differencing" || i.ParentResolved == false || !i.ParentPaths.Any(x => x.EndsWith("a.vhdx", StringComparison.OrdinalIgnoreCase))) return $"child reads {i.Type}, parent {string.Join(";", i.ParentPaths)}";
                    var m = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "merge", File = P("child.vhdx") }));
                    return m.Phase == "done" ? null : "merge: " + m.Error;
                });
                await CheckAsync("vdisk: a new identifier", async () =>
                {
                    var before = VirtDisk.Read(P("a.vhdx")).Identifier;
                    var s = await Task.Run(() => DiskJobs.Execute(new DiskJob { Op = "reset-id", File = P("a.vhdx") }));
                    if (s.Phase != "done") return s.Error;
                    var after = VirtDisk.Read(P("a.vhdx")).Identifier;
                    return after != null && after != before ? null : $"identifier {before} then {after}";
                });
                Check("vdisk: the format limits refuse before anything is written", () =>
                {
                    var j = new DiskJob { Op = "create", File = P("big.vhd"), Type = "dynamic", SizeBytes = 2041 * DiskJobs.Gb };
                    return DiskJobs.Validate(j) != null && !File.Exists(j.File) ? null : "a 2,041 GB VHD was allowed";
                });

                // Quick pack: a source bigger than the starting size, so the disk has to grow on its own
                var src = P("Photos");
                Directory.CreateDirectory(Path.Combine(src, "2026-06 Lisbon"));
                var rnd = new Random(3);
                var hashes = new Dictionary<string, string>();
                for (int i = 0; i < 6; i++)
                {
                    var f = Path.Combine(src, i < 3 ? "" : "2026-06 Lisbon", $"IMG_{4400 + i}.CR3");
                    var data = new byte[24 * mb]; rnd.NextBytes(data);
                    File.WriteAllBytes(f, data);
                    hashes[Path.GetRelativePath(src, f)] = Convert.ToHexString(SHA256.HashData(data));
                }
                File.WriteAllText(Path.Combine(src, "IMG_4400.CR3") + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
                File.WriteAllText(P("Photos.vhdx"), "taken");   // a clash, so the pack must be numbered
                await CheckAsync("vdisk: Quick pack grows by itself, checks every file and never overwrites", async () =>
                {
                    var walk = PackCopy.Walk(new[] { src });
                    var plan = QuickPackPlan.For(new[] { src }, entries: walk.Select(w => (w.Size, w.Folder)));
                    if (!plan.Numbered || !plan.File.EndsWith("Photos (1).vhdx")) return "the clash was not numbered: " + plan.File;
                    var letters = VhdEngine.FreeLetters();
                    var req = new VhdRequest { File = plan.File, SizeMb = 96, Fixed = false, FileSystem = "NTFS", Label = "Photos", MountMode = "letter", Letter = letters.Last() };
                    var saved = VhdSession.TestLaunch;
                    VhdSession.TestLaunch = (file, sha) => Task.Run(() => VhdHelper.Run(file, sha, CancellationToken.None));
                    try
                    {
                        var session = new VhdSession(req);
                        if (!await session.StartAsync()) return "the helper did not start: " + session.LaunchError;
                        var ready = await session.WaitForAsync(s => s.Phase == "ready" || s.Phase == "error", TimeSpan.FromMinutes(5), CancellationToken.None);
                        if (ready?.Phase != "ready") return "not ready: " + ready?.Error + " " + ready?.Log;
                        long current = req.SizeMb * mb; int grew = 0;
                        var copy = new PackCopy
                        {
                            Grow = async left =>
                            {
                                long to = QuickPackPlan.GrowTo(current, left);
                                session.Send("grow " + to / mb);
                                var g = await session.WaitForAsync(s => s.Phase == "grown" || s.Phase == "grow-failed" || s.Phase == "error", TimeSpan.FromMinutes(5), CancellationToken.None);
                                if (g?.Phase != "grown") return g?.Error ?? "no answer";
                                current = to; grew++;
                                return null;
                            }
                        };
                        var r = await copy.RunAsync(walk, VhdEngine.MountRoot(req), null, CancellationToken.None);
                        if (!r.Ok) { await session.DetachAsync(delete: true); return "pack: " + r.Error + " skipped " + string.Join(";", r.Skipped.Select(x => x.Reason)); }
                        if (grew == 0) { await session.DetachAsync(delete: true); return "it never had to grow"; }
                        var mark = Path.Combine(VhdEngine.MountRoot(req), "Photos", "IMG_4400.CR3") + ":Zone.Identifier";
                        if (!File.Exists(mark) || !File.ReadAllText(mark).Contains("ZoneId=3")) { await session.DetachAsync(delete: true); return "the mark of the web did not come along"; }
                        var d = await session.DetachAsync(delete: false);
                        if (d?.Phase != "detached") return "detach: " + d?.Error;
                        var info = VirtDisk.Read(req.File);
                        if (info.Attached) return "still attached";
                        if (File.ReadAllText(P("Photos.vhdx")) != "taken") return "the file that was already there changed";
                        foreach (var (rel, h) in hashes) if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(src, rel)))) != h) return "a source changed: " + rel;
                        return r.Verified == hashes.Count ? null : $"{r.Verified} of {hashes.Count} verified";
                    }
                    finally { VhdSession.TestLaunch = saved; }
                });
                await CheckAsync("vdisk: a cancelled Quick pack leaves no new file and the sources untouched", async () =>
                {
                    var walk = PackCopy.Walk(new[] { src });
                    var plan = QuickPackPlan.For(new[] { src }, entries: walk.Select(w => (w.Size, w.Folder)));
                    var req = new VhdRequest { File = plan.File, SizeMb = 512, Fixed = false, FileSystem = "NTFS", Label = "Photos", MountMode = "letter", Letter = VhdEngine.FreeLetters().Last() };
                    var saved = VhdSession.TestLaunch;
                    VhdSession.TestLaunch = (file, sha) => Task.Run(() => VhdHelper.Run(file, sha, CancellationToken.None));
                    try
                    {
                        var session = new VhdSession(req);
                        if (!await session.StartAsync()) return "the helper did not start";
                        var ready = await session.WaitForAsync(s => s.Phase == "ready" || s.Phase == "error", TimeSpan.FromMinutes(5), CancellationToken.None);
                        if (ready?.Phase != "ready") return "not ready: " + ready?.Error;
                        using var cts = new CancellationTokenSource();
                        var copy = new PackCopy();
                        var progress = new Progress<PackProgress>(pr => { if (pr.Done > 8 * mb) cts.Cancel(); });
                        var r = await copy.RunAsync(walk, VhdEngine.MountRoot(req), progress, cts.Token);
                        if (!r.Cancelled) cts.Cancel();
                        var d = await session.DetachAsync(delete: true);
                        if (File.Exists(req.File)) return "the new VHDX is still there";
                        foreach (var (rel, h) in hashes) if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(src, rel)))) != h) return "a source changed: " + rel;
                        return d?.Phase == "deleted" ? null : "the helper said " + d?.Phase + " " + d?.Error;
                    }
                    finally { VhdSession.TestLaunch = saved; }
                });
                await CheckAsync("vdisk: every state of the page reads in Cantonese and in both languages", () => VirtualDiskPageLanguageAsync(P("a.vhdx"), P("kid.vhdx"), src, P("new")));
            }
            finally
            {
                foreach (var f in Directory.Exists(root) ? Directory.GetFiles(root, "*.vhd*") : Array.Empty<string>())
                    try { if (VirtDisk.Read(f).Attached) VirtDisk.Detach(f); } catch { }
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
