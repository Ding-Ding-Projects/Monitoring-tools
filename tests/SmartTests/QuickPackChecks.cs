using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonitoringTools.Core.Disks;

namespace SmartTests
{
    public static partial class Program
    {
        /// <summary>Quick pack: where the VHDX goes, what it is called, and how big it starts.</summary>
        private static void QuickPackChecks()
        {
            string P(params string[] parts) => Path.Combine(parts);
            var root = Path.GetTempPath();
            var photos = P(root, "Archive", "2026", "Photos");
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool Exists(string p) => taken.Contains(p);

            var one = QuickPackPlan.For(new[] { photos }, Exists);
            Check("quick pack names a folder after itself", one.File == P(root, "Archive", "2026", "Photos.vhdx"), one.File);
            Check("quick pack puts it beside the dropped folder", one.Folder == P(root, "Archive", "2026"));

            taken.Add(P(root, "Archive", "2026", "Photos.vhdx"));
            var clash = QuickPackPlan.For(new[] { photos }, Exists);
            Check("quick pack numbers a clash and never overwrites", clash.File == P(root, "Archive", "2026", "Photos (1).vhdx") && clash.Numbered, clash.File);
            taken.Add(P(root, "Archive", "2026", "Photos (1).vhdx"));
            Check("quick pack keeps counting", QuickPackPlan.For(new[] { photos }, Exists).File.EndsWith("Photos (2).vhdx"));

            var file = QuickPackPlan.For(new[] { P(root, "Docs", "report.pdf") }, Exists);
            Check("quick pack names a single file without its extension", Path.GetFileName(file.File) == "report.vhdx", file.File);

            var many = QuickPackPlan.For(new[] { P(root, "Docs", "a.txt"), P(root, "Docs", "b.txt") }, Exists);
            Check("quick pack names several items after their folder", Path.GetFileName(many.File) == "Docs.vhdx" && !many.MixedFolders, many.File);

            var mixed = QuickPackPlan.For(new[] { P(root, "One", "a.txt"), P(root, "Two", "b.txt") }, Exists);
            Check("quick pack uses the first item's folder for mixed folders", mixed.Folder == P(root, "One") && mixed.MixedFolders, mixed.Folder);

            var sized = QuickPackPlan.For(new[] { photos }, Exists, new List<(long, bool)> { (1, false), (4096, false), (4097, false), (0, true) });
            Check("quick pack counts files and folders", sized.Files == 3 && sized.Folders == 1);
            Check("quick pack rounds every file up to whole clusters", sized.RoundingBytes == 4095 + 0 + 4095, sized.RoundingBytes.ToString());
            Check("quick pack counts a record for every entry", sized.RecordBytes == 3 * 1024 + 4096);
            Check("quick pack starts with at least 64 MB of headroom", sized.HeadroomBytes == 64L * 1024 * 1024);
            Check("quick pack start size covers everything", sized.StartBytes >= sized.ContentBytes + sized.RoundingBytes + sized.RecordBytes + sized.TableBytes + sized.HeadroomBytes && sized.StartBytes % (1024 * 1024) == 0);

            long gb = 1024L * 1024 * 1024;
            Check("quick pack grows by at least 1 GB", QuickPackPlan.GrowTo(10 * gb, 1) >= 11 * gb);
            Check("quick pack grows for what is left plus a tenth", QuickPackPlan.GrowTo(10 * gb, 20 * gb) >= 10 * gb + 22 * gb);
            Check("quick pack never grows past 64 TB", QuickPackPlan.GrowTo(QuickPackPlan.MaxVhdxBytes, 10 * gb) == QuickPackPlan.MaxVhdxBytes);
            Check("quick pack label keeps letters and stays short", QuickPackPlan.LabelFor("Photos: summer/2026") == "Photos summer2026" && QuickPackPlan.LabelFor(new string('x', 40)).Length == 32);
        }
    }
}
