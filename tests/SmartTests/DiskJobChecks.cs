using System;
using System.Collections.Generic;
using System.IO;
using MonitoringTools.Core.Disks;

namespace SmartTests
{
    public static partial class Program
    {
        /// <summary>The virtual disk manager's checks: format limits, never overwriting, and honest scripts.</summary>
        private static void DiskJobChecks()
        {
            var root = Path.GetTempPath();
            string P(string name) => Path.Combine(root, name);
            var there = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { P("old.vhdx"), P("base.vhdx"), P("disc.iso"), P("old.vhd") };
            bool Exists(string p) => there.Contains(p);
            long gb = DiskJobs.Gb, mb = DiskJobs.Mb;
            DiskJob Create(string name, long size, string type = "dynamic") => new DiskJob { Op = "create", File = P(name), SizeBytes = size, Type = type };

            Check("disk create a normal VHDX", DiskJobs.Validate(Create("new.vhdx", 64 * gb), Exists) == null);
            Check("disk create refuses under 3 MB", DiskJobs.Validate(Create("tiny.vhdx", 2 * mb), Exists)?.Contains("3 MB") == true);
            Check("disk create allows exactly 3 MB", DiskJobs.Validate(Create("three.vhdx", 3 * mb), Exists) == null);
            Check("disk create refuses a VHD over 2,040 GB", DiskJobs.Validate(Create("big.vhd", 2041 * gb), Exists)?.Contains("2,040 GB") == true);
            Check("disk create allows a VHD of 2,040 GB", DiskJobs.Validate(Create("edge.vhd", 2040 * gb), Exists) == null);
            Check("disk create refuses a VHDX over 64 TB", DiskJobs.Validate(Create("huge.vhdx", 64L * 1024 * gb + 1), Exists)?.Contains("64 TB") == true);
            Check("disk create never overwrites", DiskJobs.Validate(Create("old.vhdx", 64 * gb), Exists)?.Contains("nothing is ever overwritten") == true);
            Check("disk create needs a full path", DiskJobs.Validate(new DiskJob { Op = "create", File = "new.vhdx", SizeBytes = gb }, Exists) != null);
            Check("disk create refuses a network share", DiskJobs.Validate(new DiskJob { Op = "create", File = @"\\server\share\a.vhdx", SizeBytes = gb }, Exists) != null);
            Check("disk create refuses an ISO", DiskJobs.Validate(Create("new.iso", gb), Exists) != null);
            var block = Create("blocks.vhdx", gb); block.BlockSizeMb = 48;
            Check("disk block size must be a power of two", DiskJobs.Validate(block, Exists)?.Contains("power of two") == true);
            block.BlockSizeMb = 256;
            Check("disk block size of 256 MB is fine", DiskJobs.Validate(block, Exists) == null);
            var vhdBlock = Create("blocks.vhd", gb); vhdBlock.BlockSizeMb = 32;
            Check("disk block size is VHDX only", DiskJobs.Validate(vhdBlock, Exists) != null);
            var sector = Create("s.vhdx", gb); sector.LogicalSector = 1024;
            Check("disk sector sizes are 512 or 4096", DiskJobs.Validate(sector, Exists) != null);
            var dev = Create("dev.vhdx", 40 * gb); dev.FileSystem = "ReFS"; dev.DevDrive = true;
            Check("disk Dev Drive needs 50 GB", DiskJobs.Validate(dev, Exists)?.Contains("50 GB") == true);
            var child = new DiskJob { Op = "create", File = P("child.vhdx"), Type = "differencing", Other = P("base.vhdx") };
            Check("disk differencing child of an existing parent", DiskJobs.Validate(child, Exists) == null);
            child.Other = P("missing.vhdx");
            Check("disk differencing needs its parent", DiskJobs.Validate(child, Exists) != null);

            Check("disk grow refuses a VHD past 2,040 GB", DiskJobs.Validate(new DiskJob { Op = "grow", File = P("old.vhd"), SizeBytes = 2100 * gb }, Exists) != null);
            Check("disk shrink is VHDX only", DiskJobs.Validate(new DiskJob { Op = "shrink", File = P("old.vhd") }, Exists) != null);
            Check("disk an ISO cannot grow", DiskJobs.Validate(new DiskJob { Op = "grow", File = P("disc.iso"), SizeBytes = gb }, Exists) != null);
            Check("disk convert never overwrites", DiskJobs.Validate(new DiskJob { Op = "convert", File = P("old.vhd"), Other = P("old.vhdx"), Type = "dynamic" }, Exists) != null);
            Check("disk convert to a new name", DiskJobs.Validate(new DiskJob { Op = "convert", File = P("old.vhd"), Other = P("new.vhdx"), Type = "dynamic" }, Exists) == null);
            Check("disk attach an ISO", DiskJobs.Validate(new DiskJob { Op = "attach", File = P("disc.iso"), ReadOnly = true }, Exists) == null);
            Check("disk a missing file is said plainly", DiskJobs.Validate(new DiskJob { Op = "attach", File = P("gone.vhdx") }, Exists)?.Contains("is not there") == true);

            var (ps, dp) = DiskJobs.Scripts(new DiskJob { Op = "grow", File = P("old.vhdx"), SizeBytes = 80 * gb }, hyperV: false);
            Check("disk script says when Hyper-V is missing", ps.Contains("needs the Hyper-V commands") && !ps.Contains("Resize-VHD -Path"));
            Check("disk script gives the DiskPart size in MB", dp.Contains("expand vdisk maximum=81920"));
            var (ps2, _) = DiskJobs.Scripts(new DiskJob { Op = "grow", File = P("old.vhdx"), SizeBytes = 80 * gb }, hyperV: true);
            Check("disk script uses Resize-VHD with Hyper-V", ps2.Contains("Resize-VHD -Path") && ps2.Contains("-SizeBytes 85899345920"));
            var (ps3, _) = DiskJobs.Scripts(new DiskJob { Op = "attach", File = P("it's.vhdx") }, false);
            Check("disk script quotes a path with an apostrophe", ps3.Contains("'" + P("it''s.vhdx") + "'"));
        }
    }
}
