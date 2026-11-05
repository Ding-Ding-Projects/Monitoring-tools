using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace MonitoringTools.Core.DocTools
{
    public sealed class IsoEntry
    {
        public string Path; public bool IsDirectory; public long Size; public long Extent; public DateTime? Modified;
        public string Name => Path.Contains('/') ? Path[(Path.LastIndexOf('/') + 1)..] : Path;
    }

    public sealed class MountResult { public bool Ok; public string Drive; public string Error; public bool Canceled; }

    /// <summary>
    /// Disc images: a built-in ISO 9660 / Joliet reader for browsing and extracting without mounting, and per-user mounting
    /// through the Windows Storage cmdlets (Mount-DiskImage and Dismount-DiskImage, read-only, no elevation for an ISO).
    /// </summary>
    public static class DiscImageTools
    {
        public static readonly string[] Extensions = { ".iso", ".img", ".vhd", ".vhdx" };
        public static bool IsDiscImage(string path) => Extensions.Contains(System.IO.Path.GetExtension(path ?? "").ToLowerInvariant());
        public static bool CanBrowse(string path) => System.IO.Path.GetExtension(path ?? "").Equals(".iso", StringComparison.OrdinalIgnoreCase);

        /// <summary>Images this app mounted and has not ejected (the tray menu can list them).</summary>
        public static readonly List<string> MountedByApp = new List<string>();

        /// <summary>Checks replace the real mount, dismount and status calls.</summary>
        internal static Func<string, MountResult> MountHook;
        internal static Func<string, string> DismountHook;
        internal static Func<string, string> StatusHook;

        // ------------------------------------------------------------------ ISO 9660 and Joliet

        public sealed class IsoVolume { public string Label; public bool Joliet; public List<IsoEntry> Entries = new List<IsoEntry>(); public long TotalBytes; }

        public static IsoVolume ReadIso(string file)
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var sec = new byte[2048];
            long pvd = -1, svd = -1;
            for (int i = 16; i < 64; i++)
            {
                if ((i + 1) * 2048L > fs.Length) { if (i == 16) throw new InvalidDataException("this is not an ISO 9660 disc image"); break; }
                fs.Seek(i * 2048L, SeekOrigin.Begin); fs.ReadExactly(sec, 0, 2048);
                if (Encoding.ASCII.GetString(sec, 1, 5) != "CD001") { if (i == 16) throw new InvalidDataException("this is not an ISO 9660 disc image"); break; }
                if (sec[0] == 255) break;
                if (sec[0] == 1 && pvd < 0) pvd = i;
                if (sec[0] == 2 && sec[88] == 0x25 && sec[89] == 0x2F && (sec[90] == 0x40 || sec[90] == 0x43 || sec[90] == 0x45)) svd = i;
            }
            if (pvd < 0 && svd < 0) throw new InvalidDataException("this is not an ISO 9660 disc image");
            long which = svd >= 0 ? svd : pvd; bool joliet = svd >= 0;
            fs.Seek(which * 2048L, SeekOrigin.Begin); fs.ReadExactly(sec, 0, 2048);
            var vol = new IsoVolume { Joliet = joliet };
            vol.Label = (joliet ? Encoding.BigEndianUnicode.GetString(sec, 40, 32) : Encoding.ASCII.GetString(sec, 40, 32)).Trim().TrimEnd('\0').Trim();
            uint rootExtent = BitConverter.ToUInt32(sec, 156 + 2), rootSize = BitConverter.ToUInt32(sec, 156 + 10);
            int count = 0;
            void Walk(uint extent, uint size, string prefix, int depth)
            {
                if (depth > 32 || count > 200_000) return;
                var data = new byte[size];
                fs.Seek(extent * 2048L, SeekOrigin.Begin);
                int got = fs.Read(data, 0, data.Length);
                int p = 0;
                while (p < got)
                {
                    int len = data[p];
                    if (len == 0) { p = (p / 2048 + 1) * 2048; continue; }
                    if (p + len > got || len < 34) break;
                    int nameLen = data[p + 32];
                    var raw = new byte[nameLen]; Array.Copy(data, p + 33, raw, 0, nameLen);
                    bool dir = (data[p + 25] & 2) != 0;
                    if (!(nameLen == 1 && (raw[0] == 0 || raw[0] == 1)))
                    {
                        var name = joliet ? Encoding.BigEndianUnicode.GetString(raw) : Encoding.ASCII.GetString(raw);
                        int semi = name.IndexOf(';'); if (semi >= 0) name = name[..semi];
                        if (!joliet && name.EndsWith(".")) name = name[..^1];
                        var path = prefix + name;
                        uint ext = BitConverter.ToUInt32(data, p + 2), sz = BitConverter.ToUInt32(data, p + 10);
                        DateTime? when = null;
                        try { when = new DateTime(1900 + data[p + 18], Math.Max(1, (int)data[p + 19]), Math.Max(1, (int)data[p + 20]), data[p + 21] % 24, data[p + 22] % 60, data[p + 23] % 60); } catch { }
                        vol.Entries.Add(new IsoEntry { Path = path, IsDirectory = dir, Size = dir ? 0 : sz, Extent = ext, Modified = when });
                        count++;
                        if (dir) Walk(ext, sz, path + "/", depth + 1);
                    }
                    p += len;
                }
            }
            Walk(rootExtent, rootSize, "", 0);
            vol.TotalBytes = vol.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
            return vol;
        }

        /// <summary>Copies the chosen files out of an ISO without mounting it. Each is written under .part and renamed; a name that exists gets a number.</summary>
        public static ExtractResult Extract(string file, IEnumerable<IsoEntry> picked, string dest, IProgress<ExtractProgress> progress, CancellationToken ct)
        {
            var res = new ExtractResult { Destination = dest };
            var list = picked.Where(e => !e.IsDirectory).ToList();
            var prog = new ExtractProgress { Total = list.Count, TotalBytes = list.Sum(e => e.Size) };
            try
            {
                Directory.CreateDirectory(dest);
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                foreach (var e in list)
                {
                    ct.ThrowIfCancellationRequested();
                    prog.Done++;
                    if (!ArchiveTools.TrySafePath(dest, e.Path, out var full)) { res.Refused.Add(e.Path); continue; }
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
                    var target = full;
                    if (File.Exists(target)) { target = FileOps.Unique(full); res.Renamed.Add(System.IO.Path.GetFileName(full) + " → " + System.IO.Path.GetFileName(target)); }
                    var part = target + ".part";
                    try
                    {
                        fs.Seek(e.Extent * 2048L, SeekOrigin.Begin);
                        using (var o = File.Create(part))
                        {
                            var buf = new byte[81920]; long left = e.Size;
                            while (left > 0)
                            {
                                ct.ThrowIfCancellationRequested();
                                int n = fs.Read(buf, 0, (int)Math.Min(buf.Length, left));
                                if (n <= 0) throw new EndOfStreamException("the image ends before " + e.Path + " does");
                                o.Write(buf, 0, n); left -= n; prog.Bytes += n; res.Bytes += n;
                                progress?.Report(new ExtractProgress { Done = prog.Done, Total = prog.Total, Current = e.Path, Bytes = prog.Bytes, TotalBytes = prog.TotalBytes });
                            }
                        }
                        if (File.Exists(target)) File.Delete(target);
                        File.Move(part, target);
                        res.Extracted++;
                    }
                    finally { try { if (File.Exists(part)) File.Delete(part); } catch { } }
                }
            }
            catch (OperationCanceledException) { res.Canceled = true; }
            catch (Exception ex) { res.Error = ex.Message; }
            return res;
        }

        // ------------------------------------------------------------------ mounting

        private static (int Code, string Out, string Err) RunPowerShell(string script, string image, int timeoutMs, CancellationToken ct)
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            psi.Environment["MT_IMG"] = image;
            psi.Environment.Remove("PSModulePath");     // a PowerShell 7 module path makes Windows PowerShell load the wrong storage module
            using var p = Process.Start(psi);
            p.StandardInput.WriteLine("$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; try { " + script + " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 3 }");
            p.StandardInput.Close();
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            var sw = Stopwatch.StartNew();
            while (!p.WaitForExit(200))
            {
                if (ct.IsCancellationRequested || sw.ElapsedMilliseconds > timeoutMs) { try { p.Kill(true); } catch { } return (-1, "", ct.IsCancellationRequested ? "canceled" : "Windows did not answer in time."); }
            }
            return (p.ExitCode, o.Result, e.Result.Trim());
        }

        /// <summary>Mounts the image read-only for this user and returns its drive letter.</summary>
        public static MountResult Mount(string image, CancellationToken ct = default)
        {
            if (MountHook != null) return MountHook(image);
            var (code, o, e) = RunPowerShell("$i = Mount-DiskImage -ImagePath $env:MT_IMG -Access ReadOnly -PassThru; $v = $i | Get-Volume; if (-not $v -or -not $v.DriveLetter) { throw 'The image attached but has no drive letter.' }; Write-Output ('LETTER=' + $v.DriveLetter)", image, 60000, ct);
            if (e == "canceled") { Dismount(image); return new MountResult { Canceled = true }; }
            var line = o.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith("LETTER="));
            if (code == 0 && line != null)
            {
                lock (MountedByApp) if (!MountedByApp.Contains(image)) MountedByApp.Add(image);
                return new MountResult { Ok = true, Drive = line["LETTER=".Length..] + ":" };
            }
            // a failure that left the image attached without a letter must not leave it attached
            Dismount(image);
            return new MountResult { Error = string.IsNullOrWhiteSpace(e) ? "Windows did not say why." : e };
        }

        /// <summary>Detaches the image. Returns null on success, otherwise the reason.</summary>
        public static string Dismount(string image)
        {
            if (DismountHook != null) return DismountHook(image);
            var (code, _, e) = RunPowerShell("Dismount-DiskImage -ImagePath $env:MT_IMG | Out-Null", image, 30000, default);
            lock (MountedByApp) MountedByApp.Remove(image);
            return code == 0 ? null : (string.IsNullOrWhiteSpace(e) ? "Windows did not say why." : e);
        }

        /// <summary>The drive letter ("E:") when the image is already attached, otherwise null.</summary>
        public static string AttachedDrive(string image)
        {
            if (StatusHook != null) return StatusHook(image);
            try
            {
                var (code, o, _) = RunPowerShell("$d = Get-DiskImage -ImagePath $env:MT_IMG; if ($d.Attached) { $v = $d | Get-Volume; if ($v -and $v.DriveLetter) { Write-Output ('LETTER=' + $v.DriveLetter) } }", image, 15000, default);
                var line = o.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith("LETTER="));
                return code == 0 && line != null ? line["LETTER=".Length..] + ":" : null;
            }
            catch { return null; }
        }
    }
}
