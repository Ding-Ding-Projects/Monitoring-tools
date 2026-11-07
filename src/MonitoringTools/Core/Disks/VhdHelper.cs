using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core.Disks
{
    /// <summary>
    /// The elevated half of the virtual disk dialog. The app starts itself once with <c>--vhd-create &lt;request.json&gt; &lt;sha256&gt;</c>
    /// behind the Windows consent prompt. The helper builds the disk, writes its progress to a status file, then stays until the
    /// dialog tells it (through a command file) to detach, to detach and delete, or to leave the volume mounted. That keeps
    /// the whole job, including a Cancel or a Detach, to one consent prompt. It never answers the prompt itself.
    /// </summary>
    public static class VhdHelper
    {
        public const string Arg = "--vhd-create";

        /// <summary>Runs one diskpart script and returns its exit code and output. Tests replace it so no real disk is touched.</summary>
        public static Func<string, (int Code, string Output)> Diskpart = RunDiskpart;
        /// <summary>Tests point this at a throwaway folder that stands in for the new volume.</summary>
        public static string TestMountPath;
        /// <summary>Tests replace the check that the mount point came up.</summary>
        public static Func<string, bool> MountReady = path => { try { return Directory.Exists(path) && new DriveInfo(Path.GetPathRoot(path)).IsReady; } catch { return false; } };
        /// <summary>Tests replace the grant of write access to the person.</summary>
        public static Func<string, string, (int Code, string Output)> Grant = RunIcacls;

        // ------------------------------------------------------------ the entry point of the elevated process

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length < 3) return 2;
                return Run(args[1], args[2], CancellationToken.None);
            }
            catch (Exception ex) { Log.Warn("The virtual disk helper failed: " + ex.Message); return 1; }
        }

        public static int Run(string requestFile, string expectedSha256, CancellationToken ct)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(requestFile); } catch { return 2; }
            // The hash was passed on the command line, which cannot change after the process started, so a request file swapped
            // between the dialog writing it and this read is refused.
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase)) return 3;
            VhdRequest r;
            try { r = JsonSerializer.Deserialize<VhdRequest>(bytes); } catch { return 2; }
            var problem = VhdEngine.Validate(r);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var dataDir = Path.GetFullPath(Settings.DataDir);
            bool UnderKnown(string p) => p != null && (Path.GetFullPath(p).StartsWith(temp, StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(p).StartsWith(dataDir, StringComparison.OrdinalIgnoreCase));
            if (problem == null && (!UnderKnown(r.StatusFile) || !UnderKnown(r.CommandFile))) problem = "The status files are not in a place this helper writes to.";
            if (problem != null)
            {
                if (r?.StatusFile != null && UnderKnown(r.StatusFile)) Write(r, new VhdStatus { Phase = "error", Error = problem, ExitCode = 4 });
                return 4;
            }
            return Execute(r, ct);
        }

        // ------------------------------------------------------------ the job

        public static int Execute(VhdRequest r, CancellationToken ct)
        {
            var log = new StringBuilder();
            bool created = false, attached = false;
            void Status(string phase, string message, string error = null, int code = 0, string mount = null) =>
                Write(r, new VhdStatus { Phase = phase, Message = message, Error = error, ExitCode = code, Mount = mount, Log = log.ToString() });

            (bool Ok, string Error) Step(string script, string what)
            {
                var (code, output) = Diskpart(script);
                log.AppendLine("> " + what).AppendLine(output.Trim());
                bool failed = code != 0 || output.Contains("has encountered an error", StringComparison.OrdinalIgnoreCase) || output.Contains("Virtual Disk Service error", StringComparison.OrdinalIgnoreCase) || output.Contains("The system cannot find", StringComparison.OrdinalIgnoreCase);
                return failed ? (false, what + " failed (diskpart exit " + code + "): " + FirstLines(output)) : (true, null);
            }

            int Fail(string error)
            {
                // Every failure cleans up after itself: detach if it was attached, delete a file this run made.
                if (attached) { try { Step(VhdEngine.DetachScript(r), "detach after failure"); } catch { } }
                if (created) { try { if (File.Exists(r.File)) File.Delete(r.File); } catch { } }
                Status("error", "Nothing was left behind.", error, 5);
                return 5;
            }

            try
            {
                if (File.Exists(r.File)) return Fail("A file with that name already exists.");
                Status("started", "Permission given");
                var s1 = Step(VhdEngine.CreateScript(r), "create and attach");
                created = File.Exists(r.File) || s1.Ok;
                attached = s1.Ok;
                if (!s1.Ok) return Fail(s1.Error);
                if (!File.Exists(r.File)) return Fail("diskpart reported success but the disk file is not there.");
                Status("attached", "Created the file and attached it");
                var s2 = Step(VhdEngine.FormatScript(r), "partition, format and mount");
                if (!s2.Ok) return Fail(s2.Error);
                var root = TestMountPath ?? VhdEngine.MountRoot(r);
                var until = DateTime.UtcNow.AddSeconds(15);
                while (!MountReady(root) && DateTime.UtcNow < until) Thread.Sleep(250);
                if (!MountReady(root)) return Fail("The volume did not come up at " + root + ".");
                if ((r.FileSystem == "NTFS" || r.FileSystem == "ReFS") && !string.IsNullOrEmpty(r.UserSid))
                {
                    // A new NTFS or ReFS volume belongs to the administrators; hand the person who asked full control of what they put on it.
                    var (gc, go) = Grant(root, r.UserSid);
                    log.AppendLine("> grant write access").AppendLine(go.Trim());
                    if (gc != 0) return Fail("The new volume could not be opened up for your account: " + FirstLines(go));
                }
                Status("formatted", $"Formatted {r.FileSystem} as {(r.MountMode == "letter" ? r.Letter.ToUpperInvariant() + ":" : r.Folder)}", mount: root);
                Status("ready", "Ready", mount: root);
            }
            catch (Exception ex) { return Fail(ex.Message); }

            // Stay for the dialog's command. When the dialog's process disappears the volume is left mounted as it is.
            while (!ct.IsCancellationRequested)
            {
                string cmd = null;
                try { if (File.Exists(r.CommandFile)) cmd = File.ReadAllText(r.CommandFile).Trim().ToLowerInvariant(); } catch { }
                if (cmd == "detach" || cmd == "detach-delete")
                {
                    var d = Step(VhdEngine.DetachScript(r), "detach");
                    if (!d.Ok) { Status("error", "The disk could not be detached.", d.Error, 6, TestMountPath ?? VhdEngine.MountRoot(r)); return 6; }
                    attached = false;
                    if (cmd == "detach-delete") { try { File.Delete(r.File); } catch (Exception ex) { Status("error", "Detached, but the file could not be deleted.", ex.Message, 7); return 7; } Status("deleted", "Detached and deleted"); }
                    else Status("detached", "Detached");
                    return 0;
                }
                if (cmd == "release") { Status("ready", "Left mounted", mount: TestMountPath ?? VhdEngine.MountRoot(r)); return 0; }
                if (cmd == "fit")
                {
                    try { File.Delete(r.CommandFile); } catch { }
                    var fitted = FitTightly(r, log);
                    if (fitted.Error != null) { Status("fit-failed", "The disk keeps its size.", fitted.Error, 0, TestMountPath ?? VhdEngine.MountRoot(r)); continue; }
                    attached = false;
                    Status("fitted", fitted.Message);
                    Status("detached", "Detached");
                    return 0;
                }
                if (cmd != null && cmd.StartsWith("grow ") && long.TryParse(cmd.Substring(5), out var growMb))
                {
                    try { File.Delete(r.CommandFile); } catch { }
                    var g = GrowMounted(r, growMb, log);
                    if (g != null) Status("grow-failed", "The disk could not grow.", g, 0, TestMountPath ?? VhdEngine.MountRoot(r));
                    else { r.SizeMb = growMb; Status("grown", $"Grew to {VhdEngine.Bytes(growMb * 1048576L)}", mount: TestMountPath ?? VhdEngine.MountRoot(r)); }
                    continue;
                }
                if (!ParentAlive(r.ParentPid)) return 0;
                Thread.Sleep(250);
            }
            return 0;
        }

        /// <summary>Tests replace the growth so no real disk is touched; it returns null when it grew, or why it could not.</summary>
        public static Func<VhdRequest, long, string> TestGrow;

        /// <summary>
        /// Grows the mounted disk for a Quick pack that ran short: detach, grow the file, attach, extend the volume to the new
        /// end and put it back at the same drive letter or folder. The host drive must have the room; the size stays within
        /// the format's limit.
        /// </summary>
        private static string GrowMounted(VhdRequest r, long newMb, StringBuilder log)
        {
            if (TestGrow != null) return TestGrow(r, newMb);
            if (newMb <= r.SizeMb) return "The new size is not bigger than the disk.";
            if (r.Vhdx ? newMb > VhdEngine.MaxVhdxMb : newMb > VhdEngine.MaxVhdMb) return "The disk is at the largest size its format allows.";
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(r.File));
                long need = (newMb - r.SizeMb) * 1048576L;
                if (!r.Fixed) need = Math.Min(need, 64L * 1048576);   // a dynamic disk grows its file only as it fills
                if (drive.AvailableFreeSpace < need) return $"{drive.Name.TrimEnd('\\')} is {VhdEngine.Bytes(need - drive.AvailableFreeSpace)} short of the room the disk needs to grow.";
            }
            catch { }
            try
            {
                VirtDisk.Detach(r.File);
                log.AppendLine("> detached to grow");
                VirtDisk.Grow(r.File, (ulong)newMb * 1048576UL);
                log.AppendLine("> grew the file to " + newMb + " MB");
                VirtDisk.Attach(r.File, readOnly: false, noDriveLetters: true);
                log.AppendLine("> attached again");
            }
            catch (Exception ex) { return ex.Message; }
            string file = r.File.Replace("'", "''");
            string where = r.MountMode == "folder"
                ? $"Add-PartitionAccessPath -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -AccessPath '{r.Folder.Replace("'", "''")}'"
                : $"if ($p.DriveLetter -ne '{r.Letter.ToUpperInvariant()}') {{ Set-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -NewDriveLetter {r.Letter.ToUpperInvariant()} }}";
            var ps = string.Join("\n", new[]
            {
                "$ErrorActionPreference = 'Stop'",
                $"$d = Get-DiskImage -ImagePath '{file}' | Get-Disk",
                "$p = Get-Partition -DiskNumber $d.Number | Where-Object { $_.Type -ne 'Reserved' } | Sort-Object Size -Descending | Select-Object -First 1",
                "$max = (Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber).SizeMax",
                "if ($max -gt $p.Size) { Resize-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -Size $max }",
                "$p = Get-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber",
                where,
            });
            var (code, output) = PowerShell(ps);
            log.AppendLine("> extend the volume").AppendLine(output.Trim());
            if (code != 0) return "The disk grew, but its volume could not be extended: " + FirstLines(output);
            var root = VhdEngine.MountRoot(r);
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!MountReady(root) && DateTime.UtcNow < until) Thread.Sleep(250);
            return MountReady(root) ? null : "The disk grew, but its volume did not come back at " + root + ".";
        }

        /// <summary>
        /// Fits a packed disk tightly: shrink its volume to what it holds plus 64 MB, detach, and shrink the VHDX to its smallest
        /// safe size. Leaves the disk detached. A VHD cannot shrink, so it keeps its size.
        /// </summary>
        private static (string Message, string Error) FitTightly(VhdRequest r, StringBuilder log)
        {
            if (!r.Vhdx) return (null, "Only a VHDX can shrink.");
            string file = r.File.Replace("'", "''");
            var (code, output) = PowerShell(string.Join("\n", new[]
            {
                "$ErrorActionPreference = 'Stop'",
                $"$d = Get-DiskImage -ImagePath '{file}' | Get-Disk",
                "$p = Get-Partition -DiskNumber $d.Number | Where-Object { $_.Type -ne 'Reserved' } | Sort-Object Size -Descending | Select-Object -First 1",
                "$min = (Get-PartitionSupportedSize -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber).SizeMin",
                "$to = [math]::Ceiling($min / 1MB + 64) * 1MB",
                "if ($to -lt $p.Size) { Resize-Partition -DiskNumber $d.Number -PartitionNumber $p.PartitionNumber -Size $to }",
            }));
            log.AppendLine("> shrink the volume").AppendLine(output.Trim());
            if (code != 0) return (null, "Its volume could not shrink: " + FirstLines(output));
            try
            {
                var before = VirtDisk.Read(r.File).VirtualSize;
                VirtDisk.Detach(r.File);
                VirtDisk.Resize(r.File, 0, toSmallestSafe: true);
                var after = VirtDisk.Read(r.File).VirtualSize;
                return ($"Fitted tightly: {VhdEngine.Bytes((long)before)} down to {VhdEngine.Bytes((long)after)}.", null);
            }
            catch (Exception ex) { return (null, ex.Message); }
        }

        /// <summary>Runs a short PowerShell script with the Storage commands, as this elevated helper.</summary>
        public static (int Code, string Output) PowerShell(string script)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("-NoProfile"); psi.ArgumentList.Add("-NonInteractive"); psi.ArgumentList.Add("-ExecutionPolicy"); psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-EncodedCommand"); psi.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
            using var p = Process.Start(psi);
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(10 * 60 * 1000)) { try { p.Kill(true); } catch { } return (-1, "PowerShell did not finish in 10 minutes."); }
            return (p.ExitCode, o.Result + e.Result);
        }

        private static bool ParentAlive(int pid)
        {
            if (pid <= 0) return true;
            try { using var p = Process.GetProcessById(pid); return !p.HasExited; } catch { return false; }
        }

        private static string FirstLines(string s) => string.Join(" ", s.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(3));

        private static void Write(VhdRequest r, VhdStatus s)
        {
            s.Stamp = DateTime.UtcNow.Ticks;
            try
            {
                var tmp = r.StatusFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(s));
                File.Move(tmp, r.StatusFile, true);
            }
            catch { }
        }

        private static (int Code, string Output) RunDiskpart(string script)
        {
            var file = Path.Combine(Path.GetTempPath(), "mt-vhd-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                // diskpart reads a script in the console (OEM) code page; a UTF-16 script makes it print its help and fail.
                var enc = VhdEngine.ScriptEncoding();
                try { File.WriteAllBytes(file, enc.GetBytes(script)); }
                catch (EncoderFallbackException) { return (-2, "The disk path, folder or label has characters the Windows disk tools cannot read on this computer."); }
                var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "diskpart.exe"), "/s \"" + file + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = enc, StandardErrorEncoding = enc };
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(15 * 60 * 1000)) { try { p.Kill(true); } catch { } return (-1, "diskpart did not finish in 15 minutes"); }
                return (p.ExitCode, o.Result + e.Result);
            }
            finally { try { File.Delete(file); } catch { } }
        }

        private static (int Code, string Output) RunIcacls(string root, string sid)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "icacls.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add(root); psi.ArgumentList.Add("/grant"); psi.ArgumentList.Add("*" + sid + ":(OI)(CI)F");
            using var p = Process.Start(psi);
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            p.WaitForExit(60000);
            return (p.ExitCode, o.Result + e.Result);
        }
    }

    /// <summary>The normal app's side: write the request, start the elevated helper, follow its status file, send it commands.</summary>
    public sealed class VhdSession
    {
        /// <summary>Tests replace the elevation: it receives the request file and its hash and runs the helper in this process.</summary>
        public static Func<string, string, Task<int>> TestLaunch;
        /// <summary>Tests set this to behave as if the person declined the consent prompt (Windows error 1223).</summary>
        public static bool TestDecline;

        public VhdRequest Request { get; }
        public string Folder { get; }
        private Task<int> _task;
        private Process _process;
        public int? ExitCode { get; private set; }
        public bool Declined { get; private set; }
        public string LaunchError { get; private set; }
        public bool Started { get; private set; }

        public VhdSession(VhdRequest r)
        {
            Request = r;
            Folder = Path.Combine(Settings.DataDir, "vhd-jobs", Guid.NewGuid().ToString("N")[..12]);
            Directory.CreateDirectory(Folder);
            r.StatusFile = Path.Combine(Folder, "status.json");
            r.CommandFile = Path.Combine(Folder, "command.txt");
            r.ParentPid = Environment.ProcessId;
            try { r.UserSid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value; } catch { }
        }

        /// <summary>Starts the helper. Windows shows its consent prompt now; the answer is the person's alone.</summary>
        public async Task<bool> StartAsync()
        {
            var requestFile = Path.Combine(Folder, "request.json");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(Request);
            File.WriteAllBytes(requestFile, bytes);
            var sha = Convert.ToHexString(SHA256.HashData(bytes));
            if (TestDecline) { Declined = true; ExitCode = 1223; LaunchError = "The Windows consent prompt was declined or timed out."; return false; }
            if (TestLaunch != null)
            {
                _task = Task.Run(() => TestLaunch(requestFile, sha));
                _ = _task.ContinueWith(t => ExitCode = t.IsCompletedSuccessfully ? t.Result : 1);
                Started = true;
                return true;
            }
            try
            {
                var psi = new ProcessStartInfo(Environment.ProcessPath, $"{VhdHelper.Arg} \"{requestFile}\" {sha}") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                _process = Process.Start(psi);
                Started = _process != null;
                if (_process != null)
                    _task = Task.Run(() => { _process.WaitForExit(); ExitCode = _process.ExitCode; return _process.ExitCode; });
                return Started;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { Declined = true; ExitCode = 1223; LaunchError = "The Windows consent prompt was declined or timed out."; return false; }
            catch (Exception ex) { LaunchError = ex.Message; ExitCode = -1; return false; }
        }

        public VhdStatus ReadStatus()
        {
            try { return File.Exists(Request.StatusFile) ? JsonSerializer.Deserialize<VhdStatus>(File.ReadAllText(Request.StatusFile)) : null; }
            catch { return null; }
        }

        /// <summary>Waits until the status reaches one of the phases, the helper exits, or the time is up.</summary>
        public async Task<VhdStatus> WaitForAsync(Func<VhdStatus, bool> done, TimeSpan timeout, CancellationToken ct, Action<VhdStatus> onChange = null)
        {
            var until = DateTime.UtcNow + timeout; string last = null;
            while (DateTime.UtcNow < until && !ct.IsCancellationRequested)
            {
                var s = ReadStatus();
                if (s != null && s.Phase + s.Stamp != last) { last = s.Phase + s.Stamp; onChange?.Invoke(s); }
                if (s != null && done(s)) return s;
                if (ExitCode.HasValue && (s == null || s.Phase == "error" || ExitCode != 0)) return s ?? new VhdStatus { Phase = "error", Error = "The helper stopped (exit " + ExitCode + ") before it reported anything.", ExitCode = ExitCode.Value };
                await Task.Delay(120, CancellationToken.None);
            }
            return ReadStatus();
        }

        public void Send(string command)
        {
            try { File.WriteAllText(Request.CommandFile, command); } catch { }
        }

        public bool HelperAlive => _task != null && !_task.IsCompleted;

        public async Task<VhdStatus> DetachAsync(bool delete)
        {
            if (!HelperAlive) return ReadStatus();
            Send(delete ? "detach-delete" : "detach");
            return await WaitForAsync(s => s.Phase == "detached" || s.Phase == "deleted" || s.Phase == "error", TimeSpan.FromSeconds(60), CancellationToken.None);
        }

        public void Cleanup() { try { Directory.Delete(Folder, true); } catch { } }
    }
}
