using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Media
{
    public enum ClipFormat { Gif, WebP, Mp4 }

    public sealed class GifOptions
    {
        public ClipFormat Format;
        public double From, To;
        public int Width = 480, Fps = 15;
        public bool Loop = true;
        public long TargetBytes;      // 0 means no target
    }

    public sealed class GifAttempt
    {
        public int Width, Fps;
        public long Bytes;
        public bool Ok;
        public override string ToString() => $"{Width} px · {Fps} fps";
    }

    public sealed class GifResult
    {
        public bool Ok, Cancelled, TargetTooSmall;
        public string Error, Output;
        public int Width, Fps;
        public long Bytes;
        public double Seconds;
        public List<GifAttempt> Tried = new List<GifAttempt>();
    }

    /// <summary>Video to GIF, animated WebP or a short silent MP4. A GIF is two passes (a palette built for this clip, then the
    /// encode with it). With a target size the width and frame rate step down together until the result fits.</summary>
    public static class GifEngine
    {
        public static readonly int[] Widths = { 480, 400, 320, 240, 160 };
        public static readonly int[] Rates = { 15, 12, 10, 8 };

        public static string ExtensionFor(ClipFormat f) => f == ClipFormat.Gif ? ".gif" : f == ClipFormat.WebP ? ".webp" : ".mp4";

        /// <summary>The settings tried in order: the ones asked for, then the ladder entries below them.</summary>
        public static List<(int Width, int Fps)> Ladder(int width, int fps)
        {
            var l = new List<(int, int)> { (width, fps) };
            for (int i = 0; i < Widths.Length; i++)
                if (Widths[i] < width) l.Add((Widths[i], Math.Min(fps, Rates[Math.Min(i, Rates.Length - 1)])));
            // a ladder entry only counts when it is smaller than the one before
            var outList = new List<(int, int)>();
            foreach (var e in l) if (outList.Count == 0 || e.Item1 < outList[^1].Item1) outList.Add(e);
            return outList;
        }

        private static string Inv(double v) => MediaProc.Inv(v, "0.###");

        private static string Scale(int w, ClipFormat f) => f == ClipFormat.Mp4 ? $"scale={w & ~1}:-2:flags=lanczos" : $"scale={w}:-1:flags=lanczos";

        /// <summary>Makes one file with the given settings into outFile (a temp or .part path). Returns the FFmpeg result.</summary>
        public static async Task<FfResult> EncodeAsync(string src, GifOptions o, int width, int fps, string outFile, Action<double, string> progress, CancellationToken ct, bool quick = false)
        {
            double dur = Math.Max(0.1, o.To - o.From);
            string input = $"-ss {Inv(o.From)} -t {Inv(dur)} -i {MediaProc.Quote(src)}";
            string vf = $"fps={fps},{Scale(width, o.Format)}";
            string loop = o.Loop ? "0" : "-1";
            if (o.Format == ClipFormat.Gif)
            {
                if (quick)
                    return await MediaProc.RunFfmpegAsync($"{input} -filter_complex \"[0:v]{vf},split[a][b];[a]palettegen=max_colors=64[p];[b][p]paletteuse=dither=none\" -loop {loop} -f gif {MediaProc.Quote(outFile)}", dur, null, ct);
                var pal = outFile + ".palette.png";
                try
                {
                    var r1 = await MediaProc.RunFfmpegAsync($"{input} -vf \"{vf},palettegen=stats_mode=diff\" -frames:v 1 -update 1 {MediaProc.Quote(pal)}", dur, v => progress?.Invoke(v * 0.45, "Pass 1 of 2: choosing the best 256 colours for this clip"), ct);
                    if (!r1.Ok) return r1;
                    return await MediaProc.RunFfmpegAsync($"{input} -i {MediaProc.Quote(pal)} -lavfi \"{vf}[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=5\" -loop {loop} -f gif {MediaProc.Quote(outFile)}", dur, v => progress?.Invoke(0.45 + v * 0.55, "Pass 2 of 2: encoding with a custom palette"), ct);
                }
                finally { try { File.Delete(pal); } catch { } }
            }
            if (o.Format == ClipFormat.WebP)
                return await MediaProc.RunFfmpegAsync($"{input} -vf \"{vf}\" -c:v libwebp -lossless 0 -q:v {(quick ? 40 : 60)} -loop {loop} -an -f webp {MediaProc.Quote(outFile)}", dur, v => progress?.Invoke(v, "Encoding the animated WebP"), ct);
            return await MediaProc.RunFfmpegAsync($"{input} -vf \"{vf}\" -c:v libx264 -crf {(quick ? 30 : 24)} -preset veryfast -pix_fmt yuv420p -an -movflags +faststart -f mp4 {MediaProc.Quote(outFile)}", dur, v => progress?.Invoke(v, "Encoding the short MP4"), ct);
        }

        /// <summary>A quick, low-effort encode of the clip at the current settings, for the size estimate shown before saving.</summary>
        public static async Task<long> EstimateBytesAsync(string src, GifOptions o, CancellationToken ct)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "mt-est-" + Guid.NewGuid().ToString("N") + ExtensionFor(o.Format));
            try
            {
                var r = await EncodeAsync(src, o, o.Width, o.Fps, tmp, null, ct, quick: true);
                if (!r.Ok || !File.Exists(tmp)) return -1;
                long b = new FileInfo(tmp).Length;
                return o.Format == ClipFormat.Gif ? (long)(b * 1.35) : b;   // the quick GIF uses fewer colours and no dither, so it runs smaller than the real one
            }
            catch { return -1; }
            finally { try { File.Delete(tmp); } catch { } }
        }

        public static async Task<GifResult> RunAsync(string src, string dest, GifOptions o, Action<double, string> progress, CancellationToken ct)
        {
            var res = new GifResult { Output = dest, Seconds = o.To - o.From };
            var dir = Path.Combine(Path.GetTempPath(), "mt-gif-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var steps = o.TargetBytes > 0 ? Ladder(o.Width, o.Fps) : new List<(int, int)> { (o.Width, o.Fps) };
                string best = null;
                for (int i = 0; i < steps.Count; i++)
                {
                    var (w, f) = steps[i];
                    var file = Path.Combine(dir, "try" + i + ExtensionFor(o.Format));
                    int idx = i; int n = steps.Count;
                    var r = await EncodeAsync(src, o, w, f, file, (v, msg) => progress?.Invoke((idx + v) / n, (n > 1 ? $"Try {idx + 1} of {n} ({w} px, {f} fps). " : "") + msg), ct);
                    if (r.Cancelled) { res.Cancelled = true; res.Error = "Cancelled"; return res; }
                    if (!r.Ok || !File.Exists(file)) { res.Error = MediaProc.Plain(r.Error); res.Tried.Add(new GifAttempt { Width = w, Fps = f, Ok = false }); return res; }
                    long size = new FileInfo(file).Length;
                    res.Tried.Add(new GifAttempt { Width = w, Fps = f, Bytes = size, Ok = true });
                    if (o.TargetBytes <= 0 || size <= o.TargetBytes) { best = file; res.Width = w; res.Fps = f; res.Bytes = size; break; }
                }
                if (best == null)
                {
                    res.TargetTooSmall = true;
                    res.Error = "target";
                    return res;
                }
                var part = dest + ".part";
                File.Copy(best, part, true);
                File.Move(part, dest);
                res.Ok = true;
                return res;
            }
            catch (Exception ex) { res.Error = ex.Message; return res; }
            finally
            {
                try { if (File.Exists(dest + ".part")) File.Delete(dest + ".part"); } catch { }
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
