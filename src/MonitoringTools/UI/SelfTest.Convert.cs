using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        private static (int Code, string Out) RunTool(string exe, string args, int timeoutMs = 120000)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi);
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return (-1, "timed out"); }
            return (p.ExitCode, o.Result + e.Result);
        }

        private static async Task ConvertChecksAsync(string dir)
        {
            Check("converter: every listed format declares what it is for, what it keeps, and which files it takes", () =>
            {
                var bad = Formats.Visible.Where(f => string.IsNullOrWhiteSpace(f.Description) || string.IsNullOrWhiteSpace(f.Quality) || Formats.Sources(f).Length == 0).Select(f => f.Id).ToList();
                var dupes = Formats.All.GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (dupes.Count > 0) return "duplicate ids: " + string.Join(", ", dupes);
                return bad.Count == 0 ? null : "missing details: " + string.Join(", ", bad);
            });

            Check("converter: the dialog offers a video every video, audio and picture target, and an MP4 its H.265 and AV1 versions", () =>
            {
                var tmp = Path.Combine(Settings.DataDir, "convert-targets"); Directory.CreateDirectory(tmp);
                var mp4 = Path.Combine(tmp, "clip.mp4"); File.WriteAllBytes(mp4, new byte[16]);
                var ids = ConvertDialog.TargetsFor(new[] { mp4 }).Select(t => t.Spec.Id).ToList();
                foreach (var want in new[] { "mp4-h265", "mp4-av1", "webm", "mp3", "flac", "gif", "png", "apng" })
                    if (!ids.Contains(want)) return "missing " + want + " for an MP4";
                if (ids.Contains("mp4")) return "an MP4 was offered plain MP4, which would change nothing";
                var wav = Path.Combine(tmp, "tone.wav"); File.WriteAllBytes(wav, new byte[16]);
                var audioIds = ConvertDialog.TargetsFor(new[] { wav }).Select(t => t.Spec.Id).ToList();
                if (audioIds.Contains("png") || audioIds.Contains("webm")) return "audio was offered a picture or a silent video format";
                return audioIds.Contains("mp4") && audioIds.Contains("alac") ? null : "audio targets incomplete: " + string.Join(",", audioIds);
            });

            await CheckAsync("converter: the bundled FFmpeg really makes every format, and FFprobe reads each result", async () =>
            {
                var ffmpeg = Converter.FindFfmpeg();
                var given = Environment.GetEnvironmentVariable("MT_FFMPEG");   // a development build carries no FFmpeg; point at the pinned copy
                if (ffmpeg == null && !string.IsNullOrEmpty(given) && File.Exists(given)) { Settings.Current.FfmpegPath = given; ffmpeg = Converter.FindFfmpeg(); }
                if (ffmpeg == null) return "FFmpeg is not available in this build (set MT_FFMPEG to test a development build)";
                var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg), "ffprobe.exe");
                var tmp = Path.Combine(Settings.DataDir, "convert-real"); Directory.CreateDirectory(tmp);
                var video = Path.Combine(tmp, "source.mp4");
                var audio = Path.Combine(tmp, "source.wav");
                var picture = Path.Combine(tmp, "source.png");
                var make = new[]
                {
                    (video, "-f lavfi -i testsrc2=size=320x240:rate=15 -f lavfi -i sine=frequency=440:sample_rate=44100 -t 2 -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest"),
                    (audio, "-f lavfi -i sine=frequency=440:sample_rate=44100 -t 2 -c:a pcm_s16le"),
                    (picture, "-f lavfi -i testsrc2=size=320x240 -frames:v 1"),
                };
                foreach (var (path, args) in make)
                {
                    var r = await Task.Run(() => RunTool(ffmpeg, $"-hide_banner -loglevel error -y {args} \"{path}\""));
                    if (r.Code != 0 || !File.Exists(path)) return "could not make the test file " + Path.GetFileName(path) + ": " + r.Out;
                }
                var failures = new List<string>();
                int made = 0;
                foreach (var src in new[] { video, audio, picture })
                {
                    foreach (var (spec, _) in ConvertDialog.TargetsFor(new[] { src }))
                    {
                        if (!File.Exists(src)) break;
                        var job = new ConversionJob { Source = src, Format = spec, Output = Converter.OutputPathFor(src, spec, tmp), SourceBytes = new FileInfo(src).Length };
                        await Converter.RunAsync(job);
                        var name = Path.GetExtension(src).TrimStart('.') + " to " + spec.Id;
                        if (job.Failed || !File.Exists(job.Output) || new FileInfo(job.Output).Length == 0) { failures.Add(name + ": " + (job.Error ?? "no output")); continue; }
                        if (File.Exists(ffprobe))
                        {
                            var probe = await Task.Run(() => RunTool(ffprobe, $"-v error -show_entries stream=codec_type -of csv=p=0 \"{job.Output}\"", 30000));
                            if (probe.Code != 0 || string.IsNullOrWhiteSpace(probe.Out)) { failures.Add(name + ": FFprobe cannot read it (" + probe.Out.Trim() + ")"); continue; }
                        }
                        made++;
                    }
                }
                foreach (var src in new[] { video, audio, picture })
                    if (!File.Exists(src)) failures.Add("the original " + Path.GetFileName(src) + " was deleted, and it lives in the temp folder like a mail attachment would");
                File.WriteAllLines(Path.Combine(dir, "convert-results.txt"), new[] { $"made {made}" }.Concat(failures));
                return failures.Count == 0 ? null : $"{failures.Count} failed, {made} made: " + string.Join(" | ", failures.Take(20));
            });

            Check("files: the encryption routine takes a one-character password", () =>
            {
                var tmp = Path.Combine(Settings.DataDir, "crypt-test"); Directory.CreateDirectory(tmp);
                var plain = Path.Combine(tmp, "note.txt"); File.WriteAllText(plain, "hello");
                var r = FileOps.EncryptAsync(new[] { plain }, "a", false, _ => { }).GetAwaiter().GetResult();
                return r.Count == 1 && r[0].Ok ? null : "a one-character password was not accepted";
            });
        }
    }
}
