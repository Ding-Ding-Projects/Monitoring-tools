using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitoringTools.Core.Disks
{
    public static partial class DiskJobs
    {
        // ------------------------------------------------------------------ running it, elevated

        /// <summary>The entry point of the elevated helper: <c>--vdisk-job &lt;request.json&gt; &lt;sha256&gt;</c>.</summary>
        public static int Main(string[] args)
        {
            if (args.Length < 3) return 2;
            byte[] bytes;
            try { bytes = System.IO.File.ReadAllBytes(args[1]); } catch { return 2; }
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(args[2], StringComparison.OrdinalIgnoreCase)) return 3;
            DiskJob j;
            try { j = JsonSerializer.Deserialize<DiskJob>(bytes); } catch { return 2; }
            var dataDir = Path.GetFullPath(Settings.DataDir);
            if (j?.StatusFile == null || !Path.GetFullPath(j.StatusFile).StartsWith(dataDir, StringComparison.OrdinalIgnoreCase)) return 4;
            var problem = Validate(j);
            if (problem != null) { Write(j, new DiskJobStatus { Phase = "error", Error = problem }); return 4; }
            var status = Execute(j);
            Write(j, status);
            return status.Phase == "done" ? 0 : 5;
        }

        private static void Write(DiskJob j, DiskJobStatus s)
        {
            try { var tmp = j.StatusFile + ".tmp"; System.IO.File.WriteAllText(tmp, JsonSerializer.Serialize(s)); System.IO.File.Move(tmp, j.StatusFile, true); } catch { }
        }

        /// <summary>Does the job. Runs in the elevated helper; on a failure the disk is put back in the state it was found in.</summary>
        public static DiskJobStatus Execute(DiskJob j)
        {
            var log = new StringBuilder();
            const string Service = "Windows virtual disk service";
            const string ServiceAndStorage = "Windows virtual disk service, then the Storage commands";
            DiskJobStatus Done(string message, string route) => new DiskJobStatus { Phase = "done", Message = message, Route = route, Log = log.ToString() };
            DiskJobStatus Fail(string error, string route = Service) => new DiskJobStatus { Phase = "error", Error = error, Route = route, Log = log.ToString() };
            string Ps(string script, string what)
            {
                var (code, output) = VhdHelper.PowerShell("$ErrorActionPreference = 'Stop'\n" + script);
                log.AppendLine("> " + what).AppendLine(output.Trim());
                return code == 0 ? null : "Could not " + what + ": " + string.Join(" ", output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(3));
            }
            string DiskVar = $"$d = Get-DiskImage -ImagePath {Q(j.File)} | Get-Disk\n$p = Get-Partition -DiskNumber $d.Number | Where-Object {{ $_.Type -ne 'Reserved' }} | Sort-Object Offset | Select-Object -Last 1\n";
            try
            {
                var before = VirtDisk.Read(j.File);
                switch (j.Op)
                {
                    case "attach":
                        if (before.Attached) return Done("It was already attached at " + before.PhysicalPath + ".", Service);
                        VirtDisk.Attach(j.File, j.ReadOnly, j.NoLetters);
                        return Done(j.ReadOnly ? "Attached read-only." : "Attached.", Service);
                    case "detach":
                        if (!before.Attached) return Done("It was not attached.", Service);
                        VirtDisk.Detach(j.File);
                        return Done("Detached.", Service);
                    case "grow":
                    {
                        if ((ulong)j.SizeBytes <= before.VirtualSize) return Fail("The new size is not bigger than " + VhdEngine.Bytes((long)before.VirtualSize) + ".");
                        bool was = before.Attached;
                        if (was) { VirtDisk.Detach(j.File); log.AppendLine("> detached to grow"); }
                        try { VirtDisk.Grow(j.File, (ulong)j.SizeBytes); log.AppendLine("> grew"); }
                        catch (Exception ex) { if (was) { try { VirtDisk.Attach(j.File, false, false); } catch { } } return Fail(ex.Message); }
                        if (!j.Volume) { if (was) VirtDisk.Attach(j.File, false, false); return Done("Grown to " + VhdEngine.Bytes(j.SizeBytes) + ".", Service); }
                        VirtDisk.Attach(j.File, false, noDriveLetters: !was);
                        var err = Ps(DiskVar + "Resize-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -Size (Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber).SizeMax", "extend the volume");
                        if (!was) { try { VirtDisk.Detach(j.File); } catch { } }
                        return err == null ? Done("Grown to " + VhdEngine.Bytes(j.SizeBytes) + " and its volume extended.", ServiceAndStorage) : Fail("The disk grew, but its volume was not extended. " + err, ServiceAndStorage);
                    }
                    case "shrink":
                    {
                        bool was = before.Attached;
                        if (j.Volume)
                        {
                            if (!was) VirtDisk.Attach(j.File, false, noDriveLetters: true);
                            var err = Ps(DiskVar + "$min = (Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber).SizeMin\nResize-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -Size ([math]::Ceiling($min / 1MB + 64) * 1MB)", "shrink the volume");
                            if (err != null) { if (!was) { try { VirtDisk.Detach(j.File); } catch { } } return Fail(err, ServiceAndStorage); }
                        }
                        if (was || j.Volume) { VirtDisk.Detach(j.File); log.AppendLine("> detached to shrink"); }
                        try { VirtDisk.Resize(j.File, 0, toSmallestSafe: true); }
                        catch (Exception ex) { if (was) { try { VirtDisk.Attach(j.File, false, false); } catch { } } return Fail(ex.Message); }
                        var after = VirtDisk.Read(j.File);
                        if (was) VirtDisk.Attach(j.File, false, false);
                        return Done($"Shrunk from {VhdEngine.Bytes((long)before.VirtualSize)} to {VhdEngine.Bytes((long)after.VirtualSize)}, its smallest safe size.", j.Volume ? ServiceAndStorage : Service);
                    }
                    case "compact":
                    {
                        if (before.Type == "fixed") return Fail("A fixed disk has no unused blocks to return. Convert it to dynamically expanding first.");
                        bool was = before.Attached;
                        long sizeBefore = (long)before.PhysicalSize;
                        if (was) VirtDisk.Detach(j.File);
                        string mode;
                        try
                        {
                            // attached read-only, Windows can ask the file system which blocks are free; that is the full compact
                            VirtDisk.Attach(j.File, readOnly: true, noDriveLetters: true);
                            try { VirtDisk.Compact(j.File, j.Quick); mode = j.Quick ? "Quick" : "Full scan"; }
                            finally { try { VirtDisk.Detach(j.File); } catch { } }
                        }
                        catch (VDiskException)
                        {
                            VirtDisk.Compact(j.File, quick: true);
                            mode = "Quick, on the detached file";
                        }
                        if (was) VirtDisk.Attach(j.File, false, false);
                        long sizeAfter = (long)VirtDisk.Read(j.File).PhysicalSize;
                        return Done($"Compacted ({mode}). On disk {VhdEngine.Bytes(sizeBefore)} before, {VhdEngine.Bytes(sizeAfter)} after.", Service);
                    }
                    case "merge":
                        if (before.Type != "differencing") return Fail("Only a differencing disk can merge into its parent.");
                        if (before.Attached) return Fail("Detach it first. A merge needs the whole chain detached.");
                        VirtDisk.MergeIntoParent(j.File);
                        return Done("Merged into " + Path.GetFileName(before.ParentPaths.FirstOrDefault() ?? "its parent") + ". This child no longer counts as a disk.", Service);
                    case "reset-id":
                        if (before.Attached) return Fail("Detach it first. Its identifier changes only while it is not in use.");
                        var g = VirtDisk.ResetIdentifier(j.File);
                        return Done("New identifier " + g + ".", Service);
                    case "set-parent":
                        if (before.Attached) return Fail("Detach it first.");
                        VirtDisk.SetParent(j.File, j.Other);
                        return Done("Now points at " + Path.GetFileName(j.Other) + ".", Service);
                    case "convert":
                        if (before.Attached) return Fail("Detach it first. A disk converts only while it is not in use.");
                        VirtDisk.Create(new VirtDisk.CreateSpec { Path = j.Other, SourcePath = j.File, Fixed = j.Type == "fixed" });
                        var made = VirtDisk.Read(j.Other);
                        if (made.Error != null || made.VirtualSize != before.VirtualSize) { try { System.IO.File.Delete(j.Other); } catch { } return Fail("The new file did not come out the same size, so it was removed. " + made.Error); }
                        return Done($"Converted to {made.Format}, {made.Type}, beside it as {Path.GetFileName(j.Other)}. The original is unchanged.", Service);
                    case "create":
                    {
                        var spec = new VirtDisk.CreateSpec
                        {
                            Path = j.File, SizeBytes = (ulong)j.SizeBytes, Fixed = j.Type == "fixed",
                            ParentPath = j.Type == "differencing" ? j.Other : null, SourcePath = j.Type == "copy" ? j.Other : null,
                            BlockSizeBytes = (uint)(j.BlockSizeMb * Mb), LogicalSectorBytes = (uint)j.LogicalSector, PhysicalSectorBytes = (uint)j.PhysicalSector,
                        };
                        VirtDisk.Create(spec);
                        log.AppendLine("> created");
                        if (j.Init == "none" || j.Type == "differencing" || j.Type == "copy")
                        {
                            if (j.KeepAttached && j.Init != "none") VirtDisk.Attach(j.File, false, j.NoLetters);
                            return Done("Created " + Path.GetFileName(j.File) + ".", Service);
                        }
                        VirtDisk.Attach(j.File, false, noDriveLetters: true);
                        var format = j.DevDrive ? "-DevDrive" : "-FileSystem " + j.FileSystem;
                        var err = Ps($"$d = Get-DiskImage -ImagePath {Q(j.File)} | Get-Disk\nInitialize-Disk -Number $d.Number -PartitionStyle {j.Init}\n" +
                                     $"New-Partition -DiskNumber $d.Number -UseMaximumSize{(j.NoLetters ? "" : " -AssignDriveLetter")} | Format-Volume {format}{(string.IsNullOrEmpty(j.Label) ? "" : " -NewFileSystemLabel " + Q(j.Label))} -Confirm:$false | Out-Null",
                            "prepare the disk");
                        if (err != null)
                        {
                            try { VirtDisk.Detach(j.File); } catch { }
                            try { System.IO.File.Delete(j.File); } catch { }
                            return Fail(err + " The new file was removed, so nothing was left behind.", ServiceAndStorage);
                        }
                        if (!j.KeepAttached) VirtDisk.Detach(j.File);
                        return Done($"Created {Path.GetFileName(j.File)}, {j.Init}, {(j.DevDrive ? "Dev Drive" : j.FileSystem)}" + (j.KeepAttached ? ", attached." : ", detached."), ServiceAndStorage);
                    }
                }
                return Fail("The request is not one this version understands.");
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        /// <summary>
        /// Runs a job through the elevated helper: Windows asks for permission once. Declining it leaves everything as it was
        /// and reports the step as not done. Tests replace <see cref="TestRun"/> so no prompt or disk is involved.
        /// </summary>
        public static Func<DiskJob, DiskJobStatus> TestRun;

        public static async Task<DiskJobStatus> RunElevatedAsync(DiskJob j)
        {
            if (TestRun != null) return TestRun(j);
            var folder = Path.Combine(Settings.DataDir, "vdisk-jobs", Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(folder);
            try
            {
                j.StatusFile = Path.Combine(folder, "status.json");
                var request = Path.Combine(folder, "request.json");
                var bytes = JsonSerializer.SerializeToUtf8Bytes(j);
                System.IO.File.WriteAllBytes(request, bytes);
                var sha = Convert.ToHexString(SHA256.HashData(bytes));
                Process p;
                try
                {
                    p = Process.Start(new ProcessStartInfo(Environment.ProcessPath, $"{Arg} \"{request}\" {sha}") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    return new DiskJobStatus { Phase = "declined", Error = "Windows asked for permission and it was not given, so nothing was changed." };
                }
                if (p == null) return new DiskJobStatus { Phase = "error", Error = "The helper did not start." };
                await Task.Run(() => p.WaitForExit());
                try
                {
                    var s = JsonSerializer.Deserialize<DiskJobStatus>(System.IO.File.ReadAllText(j.StatusFile));
                    if (s != null) return s;
                }
                catch { }
                return new DiskJobStatus { Phase = "error", Error = $"The helper stopped (exit {p.ExitCode}) before it reported anything." };
            }
            finally { try { Directory.Delete(folder, true); } catch { } }
        }
    }
}
