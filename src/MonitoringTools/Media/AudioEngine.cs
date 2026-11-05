using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Media
{
    /// <summary>A loudness reading from FFmpeg's loudnorm filter.</summary>
    public sealed class Loudness
    {
        public double I, TP, LRA, Thresh, Offset;
        public bool Ok;
        public override string ToString() => $"{I:0.0} LUFS · true peak {TP:0.0} dB";
    }

    public sealed class AudioPlan
    {
        public double Start, End;
        public double FadeIn, FadeOut;
        public bool Normalize;
        public double TargetLufs = -16;
    }

    public sealed class AudioResult
    {
        public bool Ok, Cancelled;
        public string Error, Output;
        public Loudness Before, After;
        public double Seconds;
    }

    /// <summary>Trim, fade and loudness for the Audio dialog. Loudness is two passes of loudnorm: the first measures the edited
    /// audio, the second applies exactly the measured values with the chosen target and a true-peak limit of -1.0 dB.</summary>
    public static class AudioEngine
    {
        public const double TruePeakLimit = -1.0;
        private static readonly ConcurrentDictionary<string, Loudness> Cache = new ConcurrentDictionary<string, Loudness>();
        private static string Inv(double v) => MediaProc.Inv(v, "0.###");

        public static string OutputExtension(string sourceExt)
        {
            switch ((sourceExt ?? "").ToLowerInvariant())
            {
                case ".wav": case ".flac": case ".mp3": case ".m4a": case ".aac": return sourceExt.ToLowerInvariant();
                case ".ogg": return ".ogg";
                default: return ".wav";
            }
        }

        /// <summary>Codec arguments that follow the source format (WAV and FLAC stay lossless; MP3 and AAC keep the source bit rate).</summary>
        public static string CodecArgs(string ext, MediaInfo info)
        {
            long kbps = info.BitRate > 0 ? Math.Clamp(info.BitRate / 1000, 96, 320) : 192;
            switch (ext.ToLowerInvariant())
            {
                case ".wav": return "-c:a " + (info.AudioCodec != null && info.AudioCodec.StartsWith("pcm_s24") ? "pcm_s24le" : "pcm_s16le");
                case ".flac": return "-c:a flac";
                case ".mp3": return $"-c:a libmp3lame -b:a {kbps}k";
                case ".m4a": return $"-c:a aac -b:a {kbps}k";
                case ".aac": return $"-c:a aac -b:a {kbps}k";
                case ".ogg": return "-c:a libvorbis -q:a 5";
                default: return "-c:a pcm_s16le";
            }
        }

        private static readonly Regex JsonNum = new Regex("\"(input_i|input_tp|input_lra|input_thresh|target_offset)\"\\s*:\\s*\"?(-?[0-9.]+|-inf|inf)\"?", RegexOptions.Compiled);

        private static Loudness ParseLoudnorm(string stderr)
        {
            var l = new Loudness();
            int found = 0;
            foreach (Match m in JsonNum.Matches(stderr ?? ""))
            {
                var v = m.Groups[2].Value;
                double d = v == "-inf" ? -70 : v == "inf" ? 0 : double.Parse(v, CultureInfo.InvariantCulture);
                switch (m.Groups[1].Value)
                {
                    case "input_i": l.I = d; found++; break;
                    case "input_tp": l.TP = d; found++; break;
                    case "input_lra": l.LRA = d; break;
                    case "input_thresh": l.Thresh = d; break;
                    case "target_offset": l.Offset = d; break;
                }
            }
            l.Ok = found >= 2;
            return l;
        }

        /// <summary>The loudness of the whole file or of one edited stretch. Cached per file, stamp and filter for the whole-file reading.</summary>
        public static async Task<Loudness> MeasureAsync(string path, double start, double end, double fadeIn, double fadeOut, CancellationToken ct, bool wholeFile = false)
        {
            string key = null;
            if (wholeFile)
            {
                try { var fi = new FileInfo(path); key = $"{path}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}"; if (Cache.TryGetValue(key, out var c)) return c; } catch { }
            }
            string chain = wholeFile ? "" : EditChain(start, end, fadeIn, fadeOut) + ",";
            var r = await MediaProc.RunFfmpegAsync($"-i {MediaProc.Quote(path)} -vn -af \"{chain}loudnorm=I=-16:TP={Inv(TruePeakLimit)}:LRA=11:print_format=json\" -f null -", 0, null, ct, wantInfoLog: true);
            var l = r.Ok ? ParseLoudnorm(r.Stderr) : new Loudness();
            if (key != null && l.Ok) Cache[key] = l;
            return l;
        }

        private static string EditChain(double start, double end, double fadeIn, double fadeOut)
        {
            double len = Math.Max(0.05, end - start);
            var f = $"atrim=start={Inv(start)}:end={Inv(end)},asetpts=PTS-STARTPTS";
            if (fadeIn > 0.001) f += $",afade=t=in:st=0:d={Inv(Math.Min(fadeIn, len))}";
            if (fadeOut > 0.001) f += $",afade=t=out:st={Inv(Math.Max(0, len - Math.Min(fadeOut, len)))}:d={Inv(Math.Min(fadeOut, len))}";
            return f;
        }

        /// <summary>The gain loudness normalisation would apply, and whether the limiter has to act to hold the peak limit.</summary>
        public static (double GainDb, bool Limited) GainFor(Loudness measured, double target)
        {
            if (measured == null || !measured.Ok) return (0, false);
            double gain = target - measured.I;
            return (gain, measured.TP + gain > TruePeakLimit + 0.01);
        }

        /// <summary>Peak heights (0 to 1) across the file, decoded in .NET from low-rate mono PCM, for the waveform.</summary>
        public static async Task<float[]> PeaksAsync(string path, double duration, int buckets, CancellationToken ct)
        {
            var r = await MediaProc.RunBytesAsync($"-i {MediaProc.Quote(path)} -vn -ac 1 -ar 4000 -f s16le -", ct, 180000);
            if (!r.Ok || r.Data.Length < 4) return null;
            int samples = r.Data.Length / 2;
            var peaks = new float[buckets];
            for (int b = 0; b < buckets; b++)
            {
                int from = (int)((long)samples * b / buckets), to = (int)((long)samples * (b + 1) / buckets);
                int max = 0;
                for (int i = from; i < to; i++)
                {
                    int v = Math.Abs((short)(r.Data[i * 2] | (r.Data[i * 2 + 1] << 8)));
                    if (v > max) max = v;
                }
                peaks[b] = max / 32768f;
            }
            return peaks;
        }

        public static async Task<AudioResult> RunAsync(string src, string dest, AudioPlan plan, MediaInfo info, Action<double, string> progress, CancellationToken ct)
        {
            var res = new AudioResult { Output = dest };
            var part = dest + ".part";
            var ext = Path.GetExtension(dest);
            var container = MediaProc.ContainerFor(ext);
            if (container == null) { res.Error = "This audio format cannot be written."; return res; }
            try
            {
                double start = Math.Clamp(plan.Start, 0, info.Duration), end = Math.Clamp(plan.End, 0, info.Duration);
                if (end - start < 0.05) { res.Error = "The selection is too short. Move the markers apart."; return res; }
                double len = end - start;
                res.Seconds = len;
                string chain = EditChain(start, end, plan.FadeIn, plan.FadeOut);
                int rate = info.SampleRate > 0 ? info.SampleRate : 44100;
                int steps = plan.Normalize ? 2 : 1;

                if (plan.Normalize)
                {
                    progress?.Invoke(0.02, "Step 1 of 2: measuring the loudness of the edited audio");
                    var m = await MeasureAsync(src, start, end, plan.FadeIn, plan.FadeOut, ct);
                    if (ct.IsCancellationRequested) { res.Cancelled = true; res.Error = "Cancelled"; return res; }
                    if (!m.Ok) { res.Error = "The loudness could not be measured. This audio may be silent or damaged."; return res; }
                    res.Before = m;
                    chain += $",loudnorm=I={Inv(plan.TargetLufs)}:TP={Inv(TruePeakLimit)}:LRA=11:measured_I={Inv(m.I)}:measured_TP={Inv(m.TP)}:measured_LRA={Inv(m.LRA)}:measured_thresh={Inv(m.Thresh)}:offset={Inv(m.Offset)}:linear=true,aresample={rate}";
                }
                var r = await MediaProc.RunFfmpegAsync($"-i {MediaProc.Quote(src)} -vn -af \"{chain}\" {CodecArgs(ext, info)} -f {container} {MediaProc.Quote(part)}", len,
                    v => progress?.Invoke(plan.Normalize ? 0.1 + v * 0.8 : v, plan.Normalize ? "Step 2 of 2: trimming, fading and applying the level" : "Trimming and fading"), ct);
                if (r.Cancelled) { res.Cancelled = true; res.Error = "Cancelled"; return res; }
                if (!r.Ok || !File.Exists(part) || new FileInfo(part).Length == 0) { res.Error = MediaProc.Plain(r.Error); return res; }
                File.Move(part, dest);
                progress?.Invoke(0.95, "Checking the result");
                res.After = await MeasureAsync(dest, 0, len, 0, 0, ct, wholeFile: true);
                if (res.Before == null) res.Before = await MeasureAsync(src, start, end, plan.FadeIn, plan.FadeOut, ct);
                res.Ok = true;
                return res;
            }
            catch (Exception ex) { res.Error = ex.Message; return res; }
            finally { try { if (File.Exists(part)) File.Delete(part); } catch { } }
        }
    }
}
