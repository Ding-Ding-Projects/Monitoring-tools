using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Media
{
    public sealed class TrimPlan
    {
        public double Start, End;
        public bool Remove;       // cut the selection out instead of keeping it
        public bool Exact;        // re-encode so the cut lands on the exact frame
        public double Kept(double duration) => Remove ? Math.Max(0, duration - (End - Start)) : Math.Max(0, End - Start);
    }

    public sealed class TrimResult
    {
        public bool Ok, Cancelled;
        public string Error, Output, Method;
        public double StoppedAt;
    }

    /// <summary>Trim and cut for the Video trim dialog: keyframe scan, lossless copy cuts, exact re-encoded cuts, and joining two
    /// pieces when the middle is removed. Output is written to a .part file and renamed only when FFmpeg finished cleanly.</summary>
    public static class VideoTrimEngine
    {
        /// <summary>Times of every key frame, from the packet flags (no decoding, so it is quick even for a long file).</summary>
        public static async Task<List<double>> KeyframesAsync(string path, CancellationToken ct)
        {
            var list = new List<double>();
            var probe = MediaProc.Ffprobe;
            if (probe == null) return list;
            var r = await MediaProc.RunAsync(probe, $"-v error -select_streams v:0 -show_entries packet=pts_time,flags -of csv=p=0 {MediaProc.Quote(path)}", ct, timeoutMs: 120000);
            if (r.Code != 0) return list;
            foreach (var line in r.Out.Split('\n'))
            {
                var parts = line.Trim().Split(',');
                if (parts.Length >= 2 && parts[1].Contains('K') && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) list.Add(t);
            }
            list.Sort();
            return list;
        }

        public static bool OnKeyframe(IReadOnlyList<double> keys, double t, double tol = 0.04) => keys != null && keys.Any(k => Math.Abs(k - t) <= tol);

        /// <summary>The nearest key frame at or before t, or 0.</summary>
        public static double KeyframeAtOrBefore(IReadOnlyList<double> keys, double t) => keys == null || keys.Count == 0 ? 0 : keys.LastOrDefault(k => k <= t + 0.04);

        /// <summary>A cut copies the video without re-encoding when the point the kept piece starts at sits on a key frame (and
        /// the end sits on one or at the very end of the file).</summary>
        public static bool CanBeLossless(IReadOnlyList<double> keys, double duration, TrimPlan p)
        {
            if (keys == null || keys.Count == 0) return false;
            if (p.Remove)
            {
                bool head = p.Start <= 0.04, tail = p.End >= duration - 0.04;
                if (head && tail) return false;
                if (head) return OnKeyframe(keys, p.End);          // keeps [End, duration]
                if (tail) return true;                            // keeps [0, Start], which starts on the first frame
                return OnKeyframe(keys, p.End);                   // two pieces; the second starts at End
            }
            return (p.Start <= 0.04 || OnKeyframe(keys, p.Start)) && (OnKeyframe(keys, p.End) || p.End >= duration - 0.04);
        }

        private static string Inv(double v) => MediaProc.Inv(v, "0.###");

        public static string EncodeArgs(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant().TrimStart('.'))
            {
                case "webm": return "-c:v libvpx-vp9 -crf 30 -b:v 0 -deadline realtime -cpu-used 8 -c:a libopus";
                case "avi": return "-c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p -c:a copy";
                default: return "-c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p -c:a copy";
            }
        }

        private static string FilterEncodeArgs(string ext) =>
            (ext ?? "").TrimStart('.').ToLowerInvariant() == "webm"
                ? "-c:v libvpx-vp9 -crf 30 -b:v 0 -deadline realtime -cpu-used 8 -c:a libopus"
                : "-c:v libx264 -crf 18 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 192k";

        public static async Task<TrimResult> RunAsync(string src, string dest, TrimPlan plan, MediaInfo info, Action<double, string> progress, CancellationToken ct)
        {
            var result = new TrimResult { Output = dest };
            var part = dest + ".part";
            var ext = Path.GetExtension(dest);
            var container = MediaProc.ContainerFor(ext);
            if (container == null) { result.Error = "This file type cannot be written by the trim tool."; return result; }
            var tmp = Path.Combine(Path.GetTempPath(), "mt-trim-" + Guid.NewGuid().ToString("N"));
            try
            {
                // Removing from the very start or very end is the same as keeping the other side.
                double dur = info.Duration;
                var p = new TrimPlan { Start = Math.Clamp(plan.Start, 0, dur), End = Math.Clamp(plan.End, 0, dur), Remove = plan.Remove, Exact = plan.Exact };
                if (p.End - p.Start < 0.05) { result.Error = "The selection is too short. Move the handles apart."; return result; }
                if (p.Remove && p.Start <= 0.04) { p = new TrimPlan { Start = p.End, End = dur, Exact = p.Exact }; }
                else if (p.Remove && p.End >= dur - 0.04) { p = new TrimPlan { Start = 0, End = p.Start, Exact = p.Exact }; }
                if (!p.Remove && p.End - p.Start < 0.05) { result.Error = "Nothing would be left. Move the handles apart."; return result; }
                double total = p.Kept(dur);
                string audioMap = info.HasAudio ? "-map 0:a?" : "";

                FfResult r;
                if (!p.Remove)
                {
                    string args = p.Exact
                        ? $"-ss {Inv(p.Start)} -i {MediaProc.Quote(src)} -t {Inv(p.End - p.Start)} -map 0:v? {audioMap} {EncodeArgs(ext)} -f {container} {MediaProc.Quote(part)}"
                        : $"-ss {Inv(p.Start)} -i {MediaProc.Quote(src)} -t {Inv(p.End - p.Start)} -map 0:v? {audioMap} -c copy -avoid_negative_ts make_zero -f {container} {MediaProc.Quote(part)}";
                    result.Method = p.Exact ? "Exact cut, re-encoded" : "Lossless copy, cut at keyframes";
                    r = await MediaProc.RunFfmpegAsync(args, total, v => progress?.Invoke(v, p.Exact ? "Re-encoding the video, copying the audio" : "Copying video and audio, no re-encode"), ct);
                }
                else if (p.Exact)
                {
                    string f;
                    if (info.HasAudio)
                        f = $"[0:v]trim=start=0:end={Inv(p.Start)},setpts=PTS-STARTPTS[v0];[0:v]trim=start={Inv(p.End)},setpts=PTS-STARTPTS[v1];" +
                            $"[0:a]atrim=start=0:end={Inv(p.Start)},asetpts=PTS-STARTPTS[a0];[0:a]atrim=start={Inv(p.End)},asetpts=PTS-STARTPTS[a1];[v0][a0][v1][a1]concat=n=2:v=1:a=1[v][a]";
                    else
                        f = $"[0:v]trim=start=0:end={Inv(p.Start)},setpts=PTS-STARTPTS[v0];[0:v]trim=start={Inv(p.End)},setpts=PTS-STARTPTS[v1];[v0][v1]concat=n=2:v=1:a=0[v]";
                    var maps = info.HasAudio ? "-map [v] -map [a]" : "-map [v]";
                    result.Method = "Exact cut, re-encoded";
                    r = await MediaProc.RunFfmpegAsync($"-i {MediaProc.Quote(src)} -filter_complex \"{f}\" {maps} {FilterEncodeArgs(ext)} -f {container} {MediaProc.Quote(part)}", total, v => progress?.Invoke(v, "Re-encoding both pieces and joining them"), ct);
                }
                else
                {
                    // Lossless remove: copy the two pieces, then join them with the concat demuxer.
                    Directory.CreateDirectory(tmp);
                    var a = Path.Combine(tmp, "a" + ext); var b = Path.Combine(tmp, "b" + ext);
                    var cop = $"-map 0:v? {audioMap} -c copy -avoid_negative_ts make_zero";
                    result.Method = "Lossless copy, pieces joined at keyframes";
                    r = await MediaProc.RunFfmpegAsync($"-ss 0 -i {MediaProc.Quote(src)} -t {Inv(p.Start)} {cop} {MediaProc.Quote(a)}", p.Start, v => progress?.Invoke(v * 0.4, "Copying the part before the cut"), ct);
                    if (r.Ok) r = await MediaProc.RunFfmpegAsync($"-ss {Inv(p.End)} -i {MediaProc.Quote(src)} {cop} {MediaProc.Quote(b)}", dur - p.End, v => progress?.Invoke(0.4 + v * 0.5, "Copying the part after the cut"), ct);
                    if (r.Ok)
                    {
                        var list = Path.Combine(tmp, "list.txt");
                        File.WriteAllLines(list, new[] { "file '" + a.Replace('\\', '/') + "'", "file '" + b.Replace('\\', '/') + "'" });
                        r = await MediaProc.RunFfmpegAsync($"-f concat -safe 0 -i {MediaProc.Quote(list)} -c copy -f {container} {MediaProc.Quote(part)}", total, v => progress?.Invoke(0.9 + v * 0.1, "Joining the two pieces"), ct);
                    }
                }

                if (r.Cancelled) { result.Cancelled = true; result.Error = "Cancelled"; return result; }
                if (!r.Ok || !File.Exists(part) || new FileInfo(part).Length == 0)
                {
                    result.Error = MediaProc.Plain(r.Error);
                    result.StoppedAt = TryStoppedAt(r.Stderr);
                    return result;
                }
                File.Move(part, dest);
                result.Ok = true;
                return result;
            }
            catch (Exception ex) { result.Error = ex.Message; return result; }
            finally
            {
                try { if (File.Exists(part)) File.Delete(part); } catch { }
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            }
        }

        private static double TryStoppedAt(string stderr)
        {
            var m = System.Text.RegularExpressions.Regex.Matches(stderr ?? "", @"time=(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (m.Count == 0) return 0;
            var x = m[m.Count - 1];
            return int.Parse(x.Groups[1].Value) * 3600 + int.Parse(x.Groups[2].Value) * 60 + double.Parse(x.Groups[3].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>A strip of evenly spaced frames across the whole video, for the timeline.</summary>
        public static async Task<List<BitmapSource>> ThumbnailsAsync(string src, double duration, int count, int height, CancellationToken ct)
        {
            var list = new List<BitmapSource>();
            var dir = Path.Combine(Path.GetTempPath(), "mt-thumbs-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                double rate = count / Math.Max(duration, 0.1);
                var r = await MediaProc.RunFfmpegAsync($"-i {MediaProc.Quote(src)} -vf \"fps={Inv(rate)},scale=-2:{height}\" -frames:v {count} {MediaProc.Quote(Path.Combine(dir, "t%02d.png"))}", 0, null, ct);
                if (!r.Ok) return list;
                foreach (var f in Directory.GetFiles(dir, "t*.png").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(f); bi.EndInit(); bi.Freeze();
                    list.Add(bi);
                }
                while (list.Count > 0 && list.Count < count) list.Add(list[^1]);
            }
            catch { }
            finally { try { Directory.Delete(dir, true); } catch { } }
            return list;
        }
    }
}
