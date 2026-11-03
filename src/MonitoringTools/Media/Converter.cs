using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.Media
{
    public sealed class ConversionJob
    {
        public string Source { get; set; }
        public string Output { get; set; }
        public FormatSpec Format { get; set; }
        public double Progress { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan Position { get; set; }
        public double Speed { get; set; }
        public bool Done { get; set; }
        public bool Failed { get; set; }
        public string Error { get; set; }
        public long SourceBytes { get; set; }
        public long OutputBytes { get; set; }

        public event Action Changed;
        private Process _proc;
        private bool _cancelled;

        internal void Raise() => Application.Current?.Dispatcher.BeginInvoke(new Action(() => Changed?.Invoke()));
        internal void Attach(Process p) => _proc = p;

        public void Cancel()
        {
            _cancelled = true;
            try { if (_proc != null && !_proc.HasExited) _proc.Kill(true); } catch { }
        }
        public bool Cancelled => _cancelled;
    }

    public static class Converter
    {
        private static readonly Regex DurationRx = new Regex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        public static string FindFfmpeg()
        {
            var s = Settings.Current.FfmpegPath;
            if (!string.IsNullOrWhiteSpace(s) && File.Exists(s)) return s;

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
                Path.Combine(ServiceHost.InstallDir, "ffmpeg.exe"),
                Path.Combine(local, "Microsoft", "WinGet", "Links", "ffmpeg.exe"),
                @"C:\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "ffmpeg.exe"),
                @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try
                {
                    var p = Path.Combine(dir.Trim().Trim('"'), "ffmpeg.exe");
                    if (File.Exists(p)) return p;
                }
                catch { }
            }

            try
            {
                var pkgs = Path.Combine(local, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(pkgs))
                {
                    var hit = Directory.EnumerateFiles(pkgs, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (hit != null) return hit;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Installs FFmpeg with winget, then resolves the path.</summary>
        public static async Task<string> InstallFfmpegAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var p = Process.Start(new ProcessStartInfo("winget",
                        "install -e --id Gyan.FFmpeg --silent --accept-source-agreements --accept-package-agreements")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                }
                catch (Exception ex) { Log.Warn("winget failed: " + ex.Message); }
                return FindFfmpeg();
            });
        }

        public static string OutputPathFor(string source, FormatSpec f, string folderOverride = null)
        {
            var dir = folderOverride ?? Path.GetDirectoryName(source);
            var name = Path.GetFileNameWithoutExtension(source);
            var target = Path.Combine(dir, name + f.Extension);
            int n = 1;
            while (File.Exists(target) || string.Equals(target, source, StringComparison.OrdinalIgnoreCase))
                target = Path.Combine(dir, $"{name} ({n++}){f.Extension}");
            return target;
        }

        /// <summary>Starts a conversion and shows its toast. Entry point for ":mp4" commands and chip buttons.</summary>
        public static ConversionJob Start(string source, string formatId, string folderOverride = null)
        {
            var f = Formats.Find(formatId);
            if (f == null || !File.Exists(source)) return null;
            var job = new ConversionJob
            {
                Source = source, Format = f,
                Output = OutputPathFor(source, f, folderOverride),
                SourceBytes = new FileInfo(source).Length
            };
            App.Toasts.ShowJob(job);
            _ = RunAsync(job);
            return job;
        }

        /// <summary>Files a format can be made from: video targets take video, audio targets take audio and video, image targets take images.</summary>
        public static bool Compatible(string file, FormatSpec f)
        {
            var k = ClipItem.Classify(file);
            if (string.Equals(Path.GetExtension(file).TrimStart('.'), f.Extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)) return false;
            return f.Kind switch
            {
                TargetKind.Video => k == FileKind.Video || (f.Id == "mp4" && k == FileKind.Audio),
                TargetKind.Audio => k == FileKind.Audio || k == FileKind.Video,
                TargetKind.Image => k == FileKind.Image || (f.Id == "gif" && k == FileKind.Video),
                _ => false
            };
        }

        public static List<string> CompatibleIn(string folder, string formatId)
        {
            var f = Formats.Find(formatId);
            if (f == null) return new List<string>();
            try
            {
                return Directory.EnumerateFiles(folder).Where(p => Compatible(p, f))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch { return new List<string>(); }
        }

        /// <summary>Converts several files one after another under a single toast.</summary>
        public static void StartBatch(IList<string> files, string formatId, string label = null)
        {
            var f = Formats.Find(formatId);
            if (f == null) return;
            var list = files.Where(p => File.Exists(p) && Compatible(p, f)).ToList();
            var title = $"Batch to {f.Label}";
            var sub = label != null ? Path.GetFileName(label.TrimEnd('\\')) : $"{list.Count} files";
            if (list.Count == 0)
            {
                App.Toasts.ShowNotice(title, $"No files in {sub} can be converted to {f.Label}.", App.Toasts.Amber, 5, icon: UI.Icons.Bolt);
                return;
            }
            App.Toasts.ShowTask(title, sub, async report =>
            {
                int ok = 0;
                var errors = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    report($"{i + 1} of {list.Count} · {Path.GetFileName(list[i])}");
                    var job = new ConversionJob { Source = list[i], Format = f, Output = OutputPathFor(list[i], f), SourceBytes = new FileInfo(list[i]).Length };
                    await RunAsync(job);
                    if (job.Error == "ffmpeg-missing") return (false, "FFmpeg not found", "Install it from the control centre (Conversion), then copy the folder with :" + formatId + " again.");
                    if (job.Failed) errors.Add($"{Path.GetFileName(list[i])}: {UI.ActionProvider.Tail(job.Error, 1)}"); else ok++;
                }
                return (errors.Count == 0, $"Converted {ok} of {list.Count} to {f.Label}", errors.Count == 0 ? $"Saved next to the originals in {sub}." : string.Join("\n", errors));
            });
        }

        /// <summary>Saves a clipboard bitmap to disk, then converts when needed.</summary>
        public static string SaveBitmap(BitmapSource img, string formatId)
        {
            var folder = Settings.Current.ResolvedMediaFolder;
            var stamp = "Clipboard " + DateTime.Now.ToString("yyyy-MM-dd HHmmss");
            BitmapEncoder enc = formatId switch
            {
                "jpg" or "jpeg" => new JpegBitmapEncoder { QualityLevel = 92 },
                "bmp" => new BmpBitmapEncoder(),
                "tiff" => new TiffBitmapEncoder(),
                "gif" => new GifBitmapEncoder(),
                "png" => new PngBitmapEncoder(),
                _ => null
            };
            if (enc != null)
            {
                var ext = formatId == "jpeg" ? "jpg" : formatId;
                var path = Unique(Path.Combine(folder, $"{stamp}.{ext}"));
                enc.Frames.Add(BitmapFrame.Create(img));
                using var fs = File.Create(path);
                enc.Save(fs);
                return path;
            }
            // Formats WPF cannot encode go through a temp PNG and FFmpeg
            var tmp = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}.png");
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(img));
            using (var fs = File.Create(tmp)) png.Save(fs);
            var renamed = Unique(Path.Combine(Path.GetTempPath(), stamp + ".png"));
            File.Move(tmp, renamed);
            Start(renamed, formatId, folder);
            return null;
        }

        public static string SaveAudio(byte[] wav, string formatId)
        {
            var folder = Settings.Current.ResolvedMediaFolder;
            var stamp = "Clipboard audio " + DateTime.Now.ToString("yyyy-MM-dd HHmmss");
            if (formatId == "wav")
            {
                var path = Unique(Path.Combine(folder, stamp + ".wav"));
                File.WriteAllBytes(path, wav);
                return path;
            }
            var tmp = Unique(Path.Combine(Path.GetTempPath(), stamp + ".wav"));
            File.WriteAllBytes(tmp, wav);
            Start(tmp, formatId, folder);
            return null;
        }

        private static string Unique(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path); var name = Path.GetFileNameWithoutExtension(path); var ext = Path.GetExtension(path);
            int n = 1; string p;
            do { p = Path.Combine(dir, $"{name} ({n++}){ext}"); } while (File.Exists(p));
            return p;
        }

        public static async Task RunAsync(ConversionJob job)
        {
            var srcKind = ClipItem.Classify(job.Source);

            // Image to a WPF-encodable image format works without FFmpeg
            if (srcKind == FileKind.Image && new[] { "png", "jpg", "jpeg", "bmp", "tiff" }.Contains(job.Format.Id) && FindFfmpeg() == null)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var frame = BitmapFrame.Create(new Uri(job.Source), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        BitmapEncoder enc = job.Format.Id switch
                        {
                            "jpg" or "jpeg" => new JpegBitmapEncoder { QualityLevel = 92 },
                            "bmp" => new BmpBitmapEncoder(),
                            "tiff" => new TiffBitmapEncoder(),
                            _ => new PngBitmapEncoder()
                        };
                        enc.Frames.Add(frame);
                        using var fs = File.Create(job.Output);
                        enc.Save(fs);
                    });
                    Finish(job, null);
                }
                catch (Exception ex) { Finish(job, ex.Message); }
                return;
            }

            var ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
            {
                job.Failed = true; job.Error = "ffmpeg-missing"; job.Done = true; job.Raise();
                return;
            }

            var spec = job.Format.Args(srcKind);
            string args;
            if (spec.StartsWith("-f lavfi"))
            {
                // audio to video: black canvas input first, audio second
                var lavfi = spec.Substring(0, spec.IndexOf(" -shortest", StringComparison.Ordinal));
                var rest = spec.Substring(lavfi.Length);
                args = $"-hide_banner -nostdin -y {lavfi} -i \"{job.Source}\"{rest} -progress pipe:1 -nostats \"{job.Output}\"";
            }
            else
            {
                args = $"-hide_banner -nostdin -y -i \"{job.Source}\" {spec} -progress pipe:1 -nostats \"{job.Output}\"";
            }

            Log.Info("ffmpeg " + args);
            var psi = new ProcessStartInfo(ffmpeg, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };

            var errTail = new System.Collections.Generic.Queue<string>();
            try
            {
                using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    var m = DurationRx.Match(e.Data);
                    if (m.Success && job.Duration == TimeSpan.Zero)
                    {
                        job.Duration = new TimeSpan(0, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 0)
                                       + TimeSpan.FromSeconds(double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
                    }
                    lock (errTail) { errTail.Enqueue(e.Data); while (errTail.Count > 6) errTail.Dequeue(); }
                };
                p.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    var kv = e.Data.Split('=', 2);
                    if (kv.Length != 2) return;
                    switch (kv[0])
                    {
                        case "out_time_us":
                        case "out_time_ms": // ffmpeg reports microseconds under both keys
                            if (long.TryParse(kv[1], out var us) && us > 0)
                            {
                                job.Position = TimeSpan.FromTicks(us * 10);
                                if (job.Duration > TimeSpan.Zero)
                                    job.Progress = Math.Clamp(job.Position.TotalSeconds / job.Duration.TotalSeconds, 0, 0.999);
                            }
                            break;
                        case "speed":
                            if (double.TryParse(kv[1].TrimEnd('x'), NumberStyles.Float, CultureInfo.InvariantCulture, out var sp)) job.Speed = sp;
                            job.Raise();
                            break;
                    }
                };
                p.Start();
                job.Attach(p);
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                await p.WaitForExitAsync();

                if (job.Cancelled)
                {
                    try { if (File.Exists(job.Output)) File.Delete(job.Output); } catch { }
                    Finish(job, "Cancelled");
                }
                else if (p.ExitCode == 0 && File.Exists(job.Output)) Finish(job, null);
                else
                {
                    string tail; lock (errTail) tail = string.Join(" ", errTail);
                    Finish(job, string.IsNullOrWhiteSpace(tail) ? $"FFmpeg exited with code {p.ExitCode}" : tail);
                }
            }
            catch (Exception ex) { Finish(job, ex.Message); }
        }

        private static void Finish(ConversionJob job, string error)
        {
            job.Done = true;
            job.Failed = error != null;
            job.Error = error;
            if (!job.Failed)
            {
                job.Progress = 1;
                try { job.OutputBytes = new FileInfo(job.Output).Length; } catch { }
                if (job.Source.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
                    try { File.Delete(job.Source); } catch { }
            }
            Log.Info(job.Failed ? $"Conversion failed: {error}" : $"Converted to {job.Output}");
            job.Raise();
        }
    }
}
