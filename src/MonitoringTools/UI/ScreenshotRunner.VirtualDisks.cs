using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Core.Disks;

namespace MonitoringTools.UI
{
    // Captures for the Virtual disks page. The disks are real: they are made, attached and read on the build machine, which
    // runs as an administrator, inside the throwaway data folder. A Quick pack is over too fast to catch part way, so its
    // progress and result views carry fixed numbers and their captions say "preview".
    public static partial class ScreenshotRunner
    {
        private static async Task VirtualDiskScenes(ControlCentreWindow cc)
        {
            var dir = Path.Combine(Settings.DataDir, "virtual-disk-capture");
            Directory.CreateDirectory(dir);
            string P(string name) => Path.Combine(dir, name);
            void ScrollTo(string text)
            {
                cc.UpdateLayout();
                var target = SelfTest.Descend<TextBlock>(cc).FirstOrDefault(t => t.Text == text);   // captures run in English
                if (target == null) { cc.Scroller.ScrollToTop(); return; }
                var top = target.TranslatePoint(new Point(0, 0), cc.Scroller);
                cc.Scroller.ScrollToVerticalOffset(Math.Max(0, cc.Scroller.VerticalOffset + top.Y - 24));
            }
            DiskJobStatus Run(DiskJob j) { try { return DiskJobs.Execute(j); } catch (Exception ex) { return new DiskJobStatus { Phase = "error", Error = ex.Message }; } }

            var photos = P("Photos 2026.vhdx");
            var baseImage = P("Base image.vhdx");
            var scratch = P("Scratch.avhdx");
            try
            {
                var made = await Task.Run(() =>
                {
                    var a = Run(new DiskJob { Op = "create", File = photos, Type = "dynamic", SizeBytes = 64 * DiskJobs.Gb, Init = "GPT", FileSystem = "NTFS", Label = "Photos", NoLetters = false, KeepAttached = true });
                    var b = Run(new DiskJob { Op = "create", File = baseImage, Type = "dynamic", SizeBytes = 40 * DiskJobs.Gb, Init = "GPT", FileSystem = "NTFS", Label = "Base", KeepAttached = false });
                    var c = b.Phase == "done" ? Run(new DiskJob { Op = "create", File = scratch, Type = "differencing", Other = baseImage, Init = "none" }) : b;
                    return a.Phase == "done" && c.Phase == "done";
                });
                if (!made) { Log.Warn("Virtual disk captures: the sample disks could not be made, so only the empty page is captured."); return; }

                cc.VdAdd(new[] { scratch, baseImage, photos });
                cc.VdTestShow(photos, null);
                for (int i = 0; i < 100 && !ControlCentreWindow.VdTestRead(new[] { photos, baseImage, scratch }); i++) await Task.Delay(100);
                for (int i = 0; i < 200 && !ControlCentreWindow.VdTestVolumesRead(photos); i++) await Task.Delay(100);
                cc.VdTestShow(photos, null);
                ScrollTo("Disk library");
                await Shot(cc, "vdisks-library", "Virtual disks: the library with an attached VHDX chosen, its figures and what is inside it", 900);

                cc.VdTestShow(photos, "grow");
                ScrollTo("Photos 2026.vhdx");
                await Shot(cc, "vdisks-grow", "Virtual disks: Grow, with the new size, the volume switch and the same steps as a PowerShell script", 800);

                cc.VdTestShow(scratch, "merge");
                ScrollTo("Scratch.avhdx");
                await Shot(cc, "vdisks-merge", "Virtual disks: a differencing child with its parent chain and the Merge panel", 800);

                // Quick pack from a sample folder
                var folder = P("Trip photos");
                Directory.CreateDirectory(Path.Combine(folder, "Day 1"));
                for (int i = 0; i < 6; i++) File.WriteAllBytes(Path.Combine(folder, i < 3 ? "" : "Day 1", $"IMG_{4400 + i}.jpg"), new byte[(i + 2) * 700_000]);
                cc.VdTestShow(null, null);
                await cc.VdTestPackAsync("ready", new[] { folder });
                ScrollTo("Quick pack");
                await Shot(cc, "vdisks-quickpack-plan", "Quick pack: the plan for a dropped folder, with the name, the size worked out line by line and the free space", 800);
                await cc.VdTestPackAsync("packing", new[] { folder });
                ScrollTo("Quick pack");
                await Shot(cc, "vdisks-quickpack-progress", "Quick pack progress (preview with fixed numbers): the disk grew by itself once and carried on", 600);
                await cc.VdTestPackAsync("done", new[] { folder }, photos);
                ScrollTo("Quick pack");
                await Shot(cc, "vdisks-quickpack-done", "Quick pack done (preview with fixed numbers): every file checked by SHA-256, the disk detached beside the originals", 600);
                cc.VdTestPackClear();

                cc.VdTestNew(new DiskJob { Op = "create", File = P("DevDrive.vhdx"), Type = "dynamic", SizeBytes = 80 * DiskJobs.Gb, Init = "GPT", FileSystem = "ReFS", Label = "Dev", DevDrive = true });
                ScrollTo("New virtual disk");
                await Shot(cc, "vdisks-new", "Virtual disks: New virtual disk from the Dev Drive preset, the checks before anything is written and the script", 800);
                cc.Scroller.ScrollToTop();
            }
            finally
            {
                cc.VdTestPackClear();
                await Task.Run(() => { foreach (var f in new[] { scratch, baseImage, photos }) { try { if (VirtDisk.Read(f).Attached) VirtDisk.Detach(f); } catch { } } });
            }
        }
    }
}
