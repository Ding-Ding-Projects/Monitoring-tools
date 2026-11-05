using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Media
{
    /// <summary>What FFprobe says about one media file.</summary>
    public sealed class MediaInfo
    {
        public double Duration;
        public long Bytes;
        public int Width, Height;
        public double Fps;
        public bool HasVideo, HasAudio;
        public int SampleRate, Channels;
        public string VideoCodec, AudioCodec, Format;
        public long BitRate;
        public bool Ok;
        public string Error;
    }

    /// <summary>The outcome of one FFmpeg run.</summary>
    public sealed class FfResult
    {
        public bool Ok, Cancelled;
        public int Code;
        public string Error = "";
        public string Stderr = "";
    }

    /// <summary>Time text for the file tool dialogs: m:ss.sss with a millisecond tail, and the reverse for typed values.</summary>
    public static class MediaTime
    {
        public static string Format(double seconds, int decimals = 3)
        {
            if (seconds < 0 || double.IsNaN(seconds)) seconds = 0;
            long ms = (long)Math.Round(seconds * 1000);
            long h = ms / 3600000; ms %= 3600000;
            long m = ms / 60000; ms %= 60000;
            long s = ms / 1000; ms %= 1000;
            var frac = decimals <= 0 ? "" : "." + ms.ToString("000").Substring(0, Math.Min(3, decimals));
            return h > 0 ? $"{h}:{m:00}:{s:00}{frac}" : $"{m}:{s:00}{frac}";
        }

        /// <summary>Parses "m:ss.s", "h:mm:ss.sss", "12.5" or "90s". Returns false for anything else.</summary>
        public static bool TryParse(string text, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().TrimEnd('s', 'S').Replace(',', '.');
            var parts = text.Split(':');
            if (parts.Length > 3) return false;
            double total = 0;
            foreach (var p in parts)
            {
                if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0 || double.IsNaN(v) || double.IsInfinity(v)) return false;
                total = total * 60 + v;
            }
            seconds = total;
            return true;
        }
    }

    /// <summary>Shared plumbing for the file tool dialogs: running FFmpeg and FFprobe with progress and cancel, probing files,
    /// choosing names that never overwrite, and the container names FFmpeg needs when it writes to a .part file.</summary>
    public static class MediaProc
    {
        public static string Ffmpeg => Converter.FindFfmpeg();

        public static string Ffprobe
        {
            get
            {
                var f = Ffmpeg;
                if (f == null) return null;
                var p = Path.Combine(Path.GetDirectoryName(f), "ffprobe.exe");
                return File.Exists(p) ? p : null;
            }
        }

        public static string Inv(double v, string fmt = "0.###") => v.ToString(fmt, CultureInfo.InvariantCulture);

        /// <summary>The -f name for a container, so output can go to name.ext.part and be renamed when it is finished.</summary>
        public static string ContainerFor(string ext)
        {
            switch ((ext ?? "").TrimStart('.').ToLowerInvariant())
            {
                case "mp4": case "m4v": return "mp4";
                case "mov": return "mov";
                case "mkv": return "matroska";
                case "webm": return "webm";
                case "avi": return "avi";
                case "gif": return "gif";
                case "webp": return "webp";
                case "wav": return "wav";
                case "flac": return "flac";
                case "mp3": return "mp3";
                case "m4a": return "ipod";
                case "aac": return "adts";
                case "ogg": case "oga": return "ogg";
                default: return null;
            }
        }

        /// <summary>name + suffix + ext in the folder; a number is added when it exists. Never overwrites and never returns the source.</summary>
        public static string UniquePath(string folder, string name, string ext, string source = null)
        {
            var target = Path.Combine(folder, name + ext);
            int n = 1;
            while (File.Exists(target) || File.Exists(target + ".part") || (source != null && string.Equals(target, source, StringComparison.OrdinalIgnoreCase)))
                target = Path.Combine(folder, $"{name} ({n++}){ext}");
            return target;
        }

        public static string Quote(string path) => "\"" + path + "\"";

        // ------------------------------------------------------------ process running

        public static async Task<(int Code, string Out, string Err, bool Cancelled)> RunAsync(string exe, string args, CancellationToken ct,
            Action<string> onOut = null, Action<string> onErr = null, int timeoutMs = 0)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            var so = new StringBuilder(); var se = new StringBuilder();
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (s, e) => { if (e.Data == null) return; lock (so) { if (so.Length < 4_000_000) so.AppendLine(e.Data); } onOut?.Invoke(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data == null) return; lock (se) { if (se.Length < 4_000_000) se.AppendLine(e.Data); } onErr?.Invoke(e.Data); };
            p.Start();
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            bool cancelled = false;
            using var reg = ct.Register(() => { cancelled = true; try { if (!p.HasExited) p.Kill(true); } catch { } });
            using var timeout = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : null;
            timeout?.Token.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
            await p.WaitForExitAsync();
            p.WaitForExit();   // flushes the redirected readers
            string o, e2; lock (so) o = so.ToString(); lock (se) e2 = se.ToString();
            return (cancelled ? -1 : p.ExitCode, o, e2, cancelled);
        }

        /// <summary>Runs FFmpeg. progress receives 0 to 1 from the -progress stream when totalSeconds is known.</summary>
        public static async Task<FfResult> RunFfmpegAsync(string args, double totalSeconds, Action<double> progress, CancellationToken ct, bool wantInfoLog = false)
        {
            var ff = Ffmpeg;
            if (ff == null) return new FfResult { Error = "FFmpeg was not found. Install it from the control centre (Conversion)." };
            var full = $"-hide_banner -nostdin -y -loglevel {(wantInfoLog ? "info" : "error")} -progress pipe:1 -nostats {args}";
            Core.Log.Info("ffmpeg " + full);
            var tail = new Queue<string>();
            var r = await RunAsync(ff, full, ct, line =>
            {
                if (progress == null || totalSeconds <= 0) return;
                var kv = line.Split('=', 2);
                if (kv.Length == 2 && (kv[0] == "out_time_us" || kv[0] == "out_time_ms") && long.TryParse(kv[1], out var us) && us > 0)
                    progress(Math.Clamp(us / 1_000_000.0 / totalSeconds, 0, 0.999));
            }, line => { if (!string.IsNullOrWhiteSpace(line)) lock (tail) { tail.Enqueue(line.Trim()); while (tail.Count > 8) tail.Dequeue(); } });
            string t; lock (tail) t = string.Join(" ", tail);
            return new FfResult { Ok = r.Code == 0 && !r.Cancelled, Cancelled = r.Cancelled, Code = r.Code, Error = r.Cancelled ? "Cancelled" : (r.Code == 0 ? "" : (string.IsNullOrWhiteSpace(t) ? $"FFmpeg exited with code {r.Code}" : t)), Stderr = r.Err };
        }

        /// <summary>FFmpeg writing binary data to stdout (one picture, PCM samples).</summary>
        public static async Task<(bool Ok, byte[] Data, string Error)> RunBytesAsync(string args, CancellationToken ct, int timeoutMs = 60000)
        {
            var ff = Ffmpeg;
            if (ff == null) return (false, null, "FFmpeg was not found.");
            var psi = new ProcessStartInfo(ff, $"-hide_banner -nostdin -loglevel error {args}") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi);
            var err = p.StandardError.ReadToEndAsync();
            var ms = new MemoryStream();
            using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
            using var to = new CancellationTokenSource(timeoutMs);
            to.Token.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
            await p.StandardOutput.BaseStream.CopyToAsync(ms);
            await p.WaitForExitAsync();
            var e = await err;
            return (p.ExitCode == 0 && ms.Length > 0, ms.ToArray(), e.Trim());
        }

        // ------------------------------------------------------------ probing

        public static MediaInfo Probe(string path)
        {
            var info = new MediaInfo();
            var probe = Ffprobe;
            if (probe == null) { info.Error = "FFprobe was not found."; return info; }
            try
            {
                var psi = new ProcessStartInfo(probe, $"-v error -print_format json -show_format -show_streams {Quote(path)}") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(30000)) { try { p.Kill(true); } catch { } info.Error = "FFprobe timed out."; return info; }
                var json = o.Result;
                if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(json)) { info.Error = string.IsNullOrWhiteSpace(e.Result) ? "FFprobe could not read this file." : e.Result.Trim(); return info; }
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("format", out var fmt))
                {
                    info.Duration = Dbl(fmt, "duration");
                    info.Bytes = (long)Dbl(fmt, "size");
                    info.BitRate = (long)Dbl(fmt, "bit_rate");
                    info.Format = fmt.TryGetProperty("format_name", out var fn) ? fn.GetString() : null;
                }
                if (root.TryGetProperty("streams", out var streams))
                    foreach (var s in streams.EnumerateArray())
                    {
                        var type = s.TryGetProperty("codec_type", out var ct) ? ct.GetString() : "";
                        if (type == "video" && !info.HasVideo)
                        {
                            // A cover picture inside an audio file is a "video" stream with one frame; it is not a video.
                            bool attached = s.TryGetProperty("disposition", out var disp) && disp.TryGetProperty("attached_pic", out var ap) && ap.GetInt32() == 1;
                            if (attached) continue;
                            info.HasVideo = true;
                            info.Width = (int)Dbl(s, "width"); info.Height = (int)Dbl(s, "height");
                            info.VideoCodec = s.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;
                            info.Fps = Rate(s.TryGetProperty("avg_frame_rate", out var ar) ? ar.GetString() : null);
                            if (info.Fps <= 0) info.Fps = Rate(s.TryGetProperty("r_frame_rate", out var rr) ? rr.GetString() : null);
                            if (info.Duration <= 0) info.Duration = Dbl(s, "duration");
                        }
                        else if (type == "audio" && !info.HasAudio)
                        {
                            info.HasAudio = true;
                            info.SampleRate = (int)Dbl(s, "sample_rate"); info.Channels = (int)Dbl(s, "channels");
                            info.AudioCodec = s.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;
                            if (info.Duration <= 0) info.Duration = Dbl(s, "duration");
                        }
                    }
                if (info.Bytes <= 0) { try { info.Bytes = new FileInfo(path).Length; } catch { } }
                info.Ok = info.HasVideo || info.HasAudio;
                if (!info.Ok) info.Error = "There is no video or audio in this file.";
            }
            catch (Exception ex) { info.Error = ex.Message; }
            return info;
        }

        private static double Dbl(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return 0;
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return 0;
        }

        private static double Rate(string r)
        {
            if (string.IsNullOrEmpty(r)) return 0;
            var p = r.Split('/');
            if (p.Length == 2 && double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b > 0) return a / b;
            return double.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : 0;
        }

        /// <summary>One frame of the video as a picture, at most maxWidth pixels wide. Null when it cannot be read.</summary>
        public static async Task<BitmapSource> FrameAtAsync(string video, double seconds, int maxWidth, CancellationToken ct)
        {
            var r = await RunBytesAsync($"-ss {Inv(Math.Max(0, seconds))} -i {Quote(video)} -frames:v 1 -vf scale='min({maxWidth},iw)':-2 -f image2pipe -vcodec png -", ct);
            return r.Ok ? Decode(r.Data) : null;
        }

        public static BitmapSource Decode(byte[] data)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = new MemoryStream(data); bi.EndInit(); bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        /// <summary>The last meaningful sentence of an FFmpeg message, trimmed for a banner.</summary>
        public static string Plain(string ffmpegError)
        {
            if (string.IsNullOrWhiteSpace(ffmpegError)) return "FFmpeg stopped without saying why.";
            var t = ffmpegError.Trim();
            return t.Length > 240 ? t.Substring(t.Length - 240) : t;
        }
    }
}
