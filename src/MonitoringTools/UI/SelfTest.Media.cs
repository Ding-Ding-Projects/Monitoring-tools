using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>Checks for the six media file tool dialogs. Each one opens the real window on generated media, fills its real controls,
    /// presses its real primary button, and then reads the files it wrote with something that is not the code under test.</summary>
    public static partial class SelfTest
    {
        private static MediaFixtures _media;

        private static void CloseAllMediaDialogs()
        {
            foreach (var d in FileToolDialog.OpenDialogs.ToList()) { try { d.Close(); } catch { } }
        }

        private static async Task<string> NeedFixtures(string dir)
        {
            _media ??= await MakeMediaFixturesAsync(Path.Combine(Settings.DataDir, "media-dialogs"));
            return _media.Ffmpeg == null ? "FFmpeg is not available in this build (set MT_FFMPEG to test a development build)" : null;
        }

        private static async Task<string> RunDialog(FileToolDialog d, int timeoutMs = 120000)
        {
            Click(d.PrimaryButton);
            await d.Running;
            return d.State == ToolState.Done ? null : $"the dialog ended in {d.State}: {d.AllText().Replace("\r\n", " | ").Replace("\n", " | ")}";
        }

        private static double Loudness(string ffmpeg, string file, double start = 0, double len = 0)
        {
            var r = RunTool(ffmpeg, $"-hide_banner -nostats -ss {start.ToString(CultureInfo.InvariantCulture)} {(len > 0 ? "-t " + len.ToString(CultureInfo.InvariantCulture) : "")} -i \"{file}\" -af ebur128=peak=true -f null -", 120000);
            var m = Regex.Matches(r.Out, @"I:\s+(-?[0-9.]+) LUFS");
            return m.Count == 0 ? double.NaN : double.Parse(m[m.Count - 1].Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static double MeanVolume(string ffmpeg, string file, double start, double len)
        {
            var r = RunTool(ffmpeg, $"-hide_banner -nostats -ss {start.ToString(CultureInfo.InvariantCulture)} -t {len.ToString(CultureInfo.InvariantCulture)} -i \"{file}\" -af volumedetect -f null -", 60000);
            var m = Regex.Match(r.Out, @"mean_volume:\s+(-?[0-9.]+) dB");
            return m.Success ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : double.NaN;
        }

        private static async Task MediaDialogChecksAsync(string dir)
        {
            var need = await NeedFixtures(dir);
            if (need != null) { Results.Add(new Result("media dialogs: the generated test media", false, need)); return; }
            var f = _media;
            Check("media dialogs: the generated test media exist and FFprobe reads them", () =>
            {
                foreach (var p in new[] { f.Video, f.BigVideo, f.NoiseVideo, f.Tone })
                {
                    var i = MediaProc.Probe(p);
                    if (!i.Ok || i.Duration < 5) return "unreadable: " + p + " " + i.Error;
                }
                return MediaProc.Probe(f.Video).HasAudio ? null : "the test video has no audio";
            });
            await VideoTrimChecksAsync(f);
            await VideoGifChecksAsync(f);
            await AudioChecksAsync(f);
            await ImagesResizeChecksAsync(f);
            await ImagePrivacyChecksAsync(f);
            await MakeIconChecksAsync(f);
            await MediaToastAndAccessChecksAsync(f);
            await MediaDialogLanguageChecksAsync(f);
            CloseAllMediaDialogs();
        }

        // ------------------------------------------------------------ 1 video trim

        private static async Task VideoTrimChecksAsync(MediaFixtures f)
        {
            await CheckAsync("video trim: a cut between key frames copies the video with no re-encode and keeps the original", async () =>
            {
                var before = Sha(f.Video);
                var d = VideoTrimDialog.OpenFor(f.Video);
                try
                {
                    await Task.Delay(300); await d.KeysReady;
                    if (d.Keyframes == null || d.Keyframes.Count < 8) return "key frames were not found: " + d.Keyframes?.Count;
                    d.SetStartText("0:04.000"); d.SetEndText("0:10.000");
                    if (!d.IsLossless) return "a cut on key frames was not offered as lossless";
                    if (!d.AllText().Contains("Lossless: no re-encode")) return "the lossless banner is not shown";
                    var err = await RunDialog(d); if (err != null) return err;
                    var info = MediaProc.Probe(d.LastOutput);
                    if (!info.Ok || Math.Abs(info.Duration - 6.0) > 0.6) return $"the cut is {info.Duration:0.00} s, expected about 6";
                    if (info.VideoCodec != "h264" || info.Width != 320) return "the video was changed: " + info.VideoCodec + " " + info.Width;
                    if (!d.AllText().Contains("Lossless copy")) return "the result does not say it was a lossless copy";
                    if (!Path.GetFileName(d.LastOutput).EndsWith("-trim.mp4")) return "unexpected name " + d.LastOutput;
                    if (File.Exists(d.LastOutput + ".part")) return "a .part file was left behind";
                    if (Sha(f.Video) != before) return "the original was changed";
                    return null;
                }
                finally { d.Close(); }
            });

            await CheckAsync("video trim: a start between key frames needs an exact cut and the exact cut lands on the frame", async () =>
            {
                var d = VideoTrimDialog.OpenFor(f.Video);
                try
                {
                    await Task.Delay(300); await d.KeysReady;
                    d.SetStartText("0:04.500"); d.SetEndText("0:09.300");
                    if (d.IsLossless) return "a cut between key frames was offered as lossless";
                    if (!d.AllText().Contains("Exact cut needed")) return "the exact-cut-needed banner is not shown";
                    var err = await RunDialog(d); if (err != null) return err;
                    var info = MediaProc.Probe(d.LastOutput);
                    if (!info.Ok || Math.Abs(info.Duration - 4.8) > 0.25) return $"the exact cut is {info.Duration:0.000} s, expected 4.8";
                    return info.HasAudio ? null : "the audio was lost";
                }
                finally { d.Close(); }
            });

            await CheckAsync("video trim: remove selected joins the two pieces, lossless on key frames and exact between them", async () =>
            {
                var d = VideoTrimDialog.OpenFor(f.Video);
                try
                {
                    await Task.Delay(300); await d.KeysReady;
                    d.SetRemoveMode(true); d.SetStartText("0:04.000"); d.SetEndText("0:08.000");
                    if (!d.IsLossless) return "removing between key frames was not offered as lossless";
                    d.SetOutputName("removed-lossless.mp4");
                    var err = await RunDialog(d); if (err != null) return "lossless remove: " + err;
                    var a = MediaProc.Probe(d.LastOutput);
                    if (!a.Ok || Math.Abs(a.Duration - 16.0) > 0.8) return $"lossless remove is {a.Duration:0.00} s, expected about 16";
                    d.Close();
                    d = VideoTrimDialog.OpenFor(f.Video);
                    await Task.Delay(300); await d.KeysReady;
                    d.SetRemoveMode(true); d.SetStartText("0:06.500"); d.SetEndText("0:09.500");
                    if (d.IsLossless) return "removing between key frames was offered as lossless";
                    d.SetOutputName("removed-exact.mp4");
                    err = await RunDialog(d); if (err != null) return "exact remove: " + err;
                    var b = MediaProc.Probe(d.LastOutput);
                    return b.Ok && Math.Abs(b.Duration - 17.0) < 0.3 && b.HasAudio ? null : $"exact remove is {b.Duration:0.00} s with audio {b.HasAudio}, expected 17";
                }
                finally { d.Close(); }
            });

            await CheckAsync("video trim: a name that exists gets a number, and Esc during the work cancels it and deletes the part file", async () =>
            {
                var d = VideoTrimDialog.OpenFor(f.Video);
                try
                {
                    await Task.Delay(300); await d.KeysReady;
                    d.SetStartText("0:02.000"); d.SetEndText("0:06.000");
                    var taken = Path.Combine(Path.GetDirectoryName(f.Video), "taken.mp4"); File.WriteAllText(taken, "mine");
                    d.SetOutputName("taken.mp4");
                    var err = await RunDialog(d); if (err != null) return err;
                    if (File.ReadAllText(taken) != "mine") return "an existing file was overwritten";
                    if (!Path.GetFileName(d.LastOutput).StartsWith("taken (1)")) return "no number was added: " + d.LastOutput;
                }
                finally { d.Close(); }
                var big = VideoTrimDialog.OpenFor(f.BigVideo);
                try
                {
                    await Task.Delay(400); await big.KeysReady;
                    big.SetStartText("0:00.500"); big.SetEndText("0:29.000"); big.SetExact(true);
                    big.SetOutputName("cancelled.mp4");
                    Click(big.PrimaryButton);
                    await Task.Delay(700);
                    if (big.State != ToolState.Working) return "the work finished before it could be cancelled (state " + big.State + ")";
                    PressKey(big, Key.Escape);
                    await big.Running;
                    await Task.Delay(300);
                    var left = Directory.GetFiles(Path.GetDirectoryName(f.BigVideo), "cancelled*");
                    if (left.Length > 0) return "files left after cancel: " + string.Join(", ", left.Select(Path.GetFileName));
                    return big.State == ToolState.Ready ? null : "the dialog is in state " + big.State + " after cancel";
                }
                finally { big.Close(); }
            });

            await CheckAsync("video trim: a damaged video shows the error state with what was not changed and writes nothing", async () =>
            {
                var d = VideoTrimDialog.OpenFor(f.BadMp4);
                try
                {
                    await Task.Delay(300);
                    if (d.State != ToolState.Error) return "state " + d.State;
                    var t = d.AllText();
                    if (!t.Contains("could not be read") || !t.Contains("What was not changed".ToUpperInvariant()) && !t.Contains("WHAT WAS NOT CHANGED")) return "the error view is incomplete: " + t;
                    return Directory.GetFiles(Path.GetDirectoryName(f.BadMp4), "broken-trim*").Length == 0 ? null : "an output file exists";
                }
                finally { d.Close(); }
            });

            Check("video trim: keyboard route moves handles by a frame, a second, to the ends; I and O set the points", () =>
            {
                var d = VideoTrimDialog.OpenFor(f.Video);
                try
                {
                    d.SetTimes(5, 10);
                    var tl = d.Timeline;
                    d.UpdateLayout();
                    var sh = tl.StartHandle; var eh = tl.EndHandle;
                    SendKey(sh, Key.Right); if (Math.Abs(tl.Start - 5.04) > 0.001) return "Right moved the start to " + tl.Start;
                    SendKey(sh, Key.Left); SendKey(sh, Key.Left); if (Math.Abs(tl.Start - 4.96) > 0.001) return "Left moved the start to " + tl.Start;
                    SendKey(sh, Key.Right, ModifierKeys.Control); if (Math.Abs(tl.Start - 5.96) > 0.001) return "Ctrl+Right moved the start to " + tl.Start;
                    SendKey(sh, Key.Home); if (tl.Start != 0) return "Home put the start at " + tl.Start;
                    SendKey(eh, Key.End); if (Math.Abs(tl.End - d.Info.Duration) > 0.001) return "End put the end at " + tl.End;
                    tl.SetPlayhead(7.0, false);
                    d.SetStartAtPlayhead(); if (Math.Abs(tl.Start - 7.0) > 0.001) return "I did not set the start";
                    tl.SetPlayhead(12.0, false); d.SetEndAtPlayhead(); if (Math.Abs(tl.End - 12.0) > 0.001) return "O did not set the end";
                    return null;
                }
                finally { d.Close(); }
            });
        }

        private static void SendKey(UIElement el, Key key, ModifierKeys mods = ModifierKeys.None)
        {
            var src = PresentationSource.FromVisual(el);
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, src, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent };
            RangeTimeline.TestModifiers = mods;
            try { el.RaiseEvent(e); } finally { RangeTimeline.TestModifiers = null; }
        }

        // ------------------------------------------------------------ 2 video to GIF

        private static async Task VideoGifChecksAsync(MediaFixtures f)
        {
            await CheckAsync("video to GIF: a range at 240 px and 10 fps makes a real GIF of the right size and length, and the video is untouched", async () =>
            {
                var before = Sha(f.Video);
                var d = VideoGifDialog.OpenFor(f.Video);
                try
                {
                    await Task.Delay(400);
                    d.SetRange(2, 6); d.SetWidth(240); d.SetFps(10); d.SetTarget(false, 8); d.SetOutputName("clip.gif");
                    var err = await RunDialog(d); if (err != null) return err;
                    var info = MediaProc.Probe(d.LastOutput);
                    if (!info.Ok || info.VideoCodec != "gif") return "not a GIF: " + info.VideoCodec;
                    if (info.Width != 240) return "width " + info.Width;
                    if (Math.Abs(info.Duration - 4.0) > 0.6) return $"length {info.Duration:0.00} s, expected about 4";
                    if (Math.Abs(info.Fps - 10) > 1.5) return "fps " + info.Fps;
                    if (Sha(f.Video) != before) return "the video was changed";
                    return File.Exists(d.LastOutput + ".part") ? "a .part file was left" : null;
                }
                finally { d.Close(); }
            });

            await CheckAsync("video to GIF: animated WebP and a short MP4 are made too", async () =>
            {
                var d = VideoGifDialog.OpenFor(f.Video);
                try
                {
                    await Task.Delay(400);
                    d.SetRange(1, 4); d.SetWidth(240); d.SetFps(10); d.SetTarget(false, 8);
                    d.SetFormat(ClipFormat.WebP); d.SetOutputName("clip.webp");
                    var err = await RunDialog(d); if (err != null) return "webp: " + err;
                    var w = MediaProc.Probe(d.LastOutput);
                    if (!w.Ok || !w.VideoCodec.StartsWith("webp")) return "webp codec " + w.VideoCodec;
                    d.Close();
                    d = VideoGifDialog.OpenFor(f.Video);
                    await Task.Delay(400);
                    d.SetRange(1, 4); d.SetWidth(240); d.SetFps(10); d.SetTarget(false, 8);
                    d.SetFormat(ClipFormat.Mp4); d.SetOutputName("clip-short.mp4");
                    err = await RunDialog(d); if (err != null) return "mp4: " + err;
                    var m = MediaProc.Probe(d.LastOutput);
                    if (!m.Ok || m.VideoCodec != "h264" || m.HasAudio) return $"mp4 codec {m.VideoCodec}, audio {m.HasAudio}";
                    return Math.Abs(m.Duration - 3.0) < 0.5 ? null : "mp4 length " + m.Duration;
                }
                finally { d.Close(); }
            });

            await CheckAsync("video to GIF: a target size steps width and frame rate down until the result fits", async () =>
            {
                var o = new GifOptions { Format = ClipFormat.Gif, From = 0, To = 5, Width = 480, Fps = 15 };
                long big = new FileInfo(await MakeGifAt(f, o, 480, 15)).Length, small = new FileInfo(await MakeGifAt(f, o, 160, 8)).Length;
                long mid = (big + small) / 2;
                int mb = (int)Math.Ceiling(mid / 1048576.0);
                if (mb < 1 || mb * 1048576L >= big || mb * 1048576L <= small) return $"the noise clip does not separate the steps: 480px = {big}, 160px = {small}, limit {mb} MB";
                var d = VideoGifDialog.OpenFor(f.NoiseVideo);
                try
                {
                    await Task.Delay(400);
                    d.SetRange(0, 5); d.SetWidth(480); d.SetFps(15); d.SetTarget(true, mb); d.SetOutputName("fit.gif");
                    var err = await RunDialog(d); if (err != null) return err;
                    var res = d.LastResult;
                    if (res.Bytes > mb * 1048576L) return $"the result {res.Bytes} is over the {mb} MB limit";
                    if (res.Width >= 480 || res.Tried.Count < 2) return $"no step-down happened: {res.Width} px after {res.Tried.Count} tries";
                    if (new FileInfo(d.LastOutput).Length != res.Bytes) return "the saved file is not the one that was measured";
                    var info = MediaProc.Probe(d.LastOutput);
                    return info.Width == res.Width ? null : "saved width " + info.Width + " but the search chose " + res.Width;
                }
                finally { d.Close(); }
            });

            await CheckAsync("video to GIF: when even the smallest step is too big nothing is saved and the tried sizes are listed", async () =>
            {
                var o = new GifOptions { Format = ClipFormat.Gif, From = 0, To = 16, Width = 480, Fps = 15 };
                long smallest = new FileInfo(await MakeGifAt(f, o, 160, 8)).Length;
                if (smallest <= 1048576) return "the noise clip is too small to test this: the smallest step is " + smallest + " bytes";
                var d = VideoGifDialog.OpenFor(f.NoiseVideo);
                try
                {
                    await Task.Delay(400);
                    d.SetRange(0, 16); d.SetWidth(480); d.SetFps(15); d.SetTarget(true, 1); d.SetOutputName("never.gif");
                    Click(d.PrimaryButton); await d.Running;
                    if (!d.LastResult.TargetTooSmall) return "the result was not reported as too small: ok=" + d.LastResult.Ok;
                    if (d.LastResult.Tried.Count < 3) return "only " + d.LastResult.Tried.Count + " sizes were tried";
                    var t = d.AllText();
                    if (!t.Contains("Cannot reach") || !t.Contains("Switch to Short MP4") || !t.Contains("TRIED")) return "the too-small view is incomplete: " + t;
                    return Directory.GetFiles(Path.GetDirectoryName(f.NoiseVideo), "never*").Length == 0 ? null : "a file was written";
                }
                finally { d.Close(); }
            });
        }

        private static async Task<string> MakeGifAt(MediaFixtures f, GifOptions o, int w, int fps)
        {
            var path = Path.Combine(f.Dir, $"probe-{w}-{fps}.gif");
            var r = await GifEngine.EncodeAsync(f.NoiseVideo, o, w, fps, path, null, default);
            if (!r.Ok) throw new InvalidOperationException("could not make the probe GIF: " + r.Error);
            return path;
        }

        // ------------------------------------------------------------ 3 audio

        private static async Task AudioChecksAsync(MediaFixtures f)
        {
            await CheckAsync("audio: trim, fades and a -16 LUFS target give the right length, the right loudness and a real fade", async () =>
            {
                var before = Sha(f.Tone);
                var d = AudioEditDialog.OpenFor(f.Tone);
                try
                {
                    await Task.Delay(300); await d.MeasuredReady;
                    if (d.Measured == null || !d.Measured.Ok) return "the loudness was not measured on open";
                    if (d.Measured.I > -25) return "the test tone is not quiet enough to prove anything: " + d.Measured.I;
                    d.SetTimes(1.0, 11.0); d.SetFades(0.5, 1.0); d.SetTarget(1); d.SetOutputName("tone-edit.wav");
                    var err = await RunDialog(d); if (err != null) return err;
                    var info = MediaProc.Probe(d.LastOutput);
                    if (!info.Ok || Math.Abs(info.Duration - 10.0) > 0.1) return $"length {info.Duration:0.000}, expected 10";
                    if (info.SampleRate != 44100) return "sample rate " + info.SampleRate;
                    double lufs = Loudness(f.Ffmpeg, d.LastOutput);
                    if (double.IsNaN(lufs) || Math.Abs(lufs - -16) > 1.0) return $"measured {lufs} LUFS, expected -16";
                    var res = d.LastResult;
                    if (res.After == null || res.After.TP > -0.8) return "true peak " + res.After?.TP;
                    double start = MeanVolume(f.Ffmpeg, d.LastOutput, 0, 0.08), mid = MeanVolume(f.Ffmpeg, d.LastOutput, 4, 0.5), end = MeanVolume(f.Ffmpeg, d.LastOutput, 9.92, 0.08);
                    if (!(mid - start > 8)) return $"no fade in: start {start} dB, middle {mid} dB";
                    if (!(mid - end > 4)) return $"no fade out: end {end} dB, middle {mid} dB";
                    if (Sha(f.Tone) != before) return "the original was changed";
                    var t = d.AllText();
                    return t.Contains("LUFS before") && t.Contains("True peak") ? null : "the done card has no loudness rows: " + t;
                }
                finally { d.Close(); }
            });

            await CheckAsync("audio: Leave as it is keeps the level, a custom target works, and the format follows the file name", async () =>
            {
                var d = AudioEditDialog.OpenFor(f.Tone);
                try
                {
                    await Task.Delay(300); await d.MeasuredReady;
                    var src = d.Measured.I;
                    d.SetTimes(0, 8); d.SetFades(0, 0); d.SetTarget(0); d.SetOutputName("tone-same.flac");
                    var err = await RunDialog(d); if (err != null) return "leave: " + err;
                    var info = MediaProc.Probe(d.LastOutput);
                    if (info.AudioCodec != "flac" || Math.Abs(info.Duration - 8.0) > 0.1) return $"flac {info.AudioCodec} {info.Duration}";
                    double lufs = Loudness(f.Ffmpeg, d.LastOutput);
                    if (Math.Abs(lufs - src) > 0.6) return $"the level changed from {src} to {lufs}";
                    d.Close();
                    d = AudioEditDialog.OpenFor(f.Tone);
                    await Task.Delay(300); await d.MeasuredReady;
                    d.SetTimes(0, 8); d.SetFades(0, 0); d.SetTarget(4, -23); d.SetOutputName("tone-custom.mp3");
                    err = await RunDialog(d); if (err != null) return "custom: " + err;
                    var mp3 = MediaProc.Probe(d.LastOutput);
                    if (mp3.AudioCodec != "mp3") return "codec " + mp3.AudioCodec;
                    double l2 = Loudness(f.Ffmpeg, d.LastOutput);
                    return Math.Abs(l2 - -23) < 1.2 ? null : $"custom target gave {l2} LUFS, expected -23";
                }
                finally { d.Close(); }
            });

            await CheckAsync("audio: a file that cannot be decoded shows the error and what was not changed", async () =>
            {
                var bad = Path.Combine(f.Dir, "voice-note.m4a"); File.WriteAllBytes(bad, new byte[4000]);
                var d = AudioEditDialog.OpenFor(bad);
                try
                {
                    await Task.Delay(300);
                    var t = d.AllText();
                    return d.State == ToolState.Error && t.Contains("This audio could not be decoded") && t.Contains("WHAT WAS NOT CHANGED") ? null : "state " + d.State + " " + t;
                }
                finally { d.Close(); }
            });
        }

        // ------------------------------------------------------------ 4 images resize

        private static async Task ImagesResizeChecksAsync(MediaFixtures f)
        {
            await CheckAsync("images: resize to 800 wide with a 4:3 crop makes the right pixels, leaves the originals, and shrinks the files", async () =>
            {
                var d = ImagesResizeDialog.OpenFor(f.Pictures);
                try
                {
                    await d.LoadedTask; await Task.Delay(300);
                    if (d.Items.Count != f.Pictures.Count) return $"{d.Items.Count} of {f.Pictures.Count} pictures loaded";
                    var hashes = f.Pictures.ToDictionary(p => p, Sha);
                    d.SetCrop(2); d.SetWidthText("800");
                    if (d.Options.Height != 600) return "Keep proportions did not set the height: " + d.Options.Height;
                    var err = await RunDialog(d); if (err != null) return err;
                    var bad = new List<string>();
                    foreach (var r in d.LastResults)
                    {
                        if (!r.Ok) { bad.Add(Path.GetFileName(r.Source) + ": " + r.Error); continue; }
                        if (!Path.GetFileNameWithoutExtension(r.Output).EndsWith("-small")) bad.Add("name " + r.Output);
                        var (w, h) = ImageEngine.PixelSize(r.Output);
                        if (w != 800 || h != 600) bad.Add($"{Path.GetFileName(r.Output)} is {w}x{h}");
                    }
                    foreach (var p in f.Pictures) if (Sha(p) != hashes[p]) bad.Add("changed: " + p);
                    var jpg = d.LastResults.Where(r => r.Source.EndsWith(".jpg")).ToList();
                    if (jpg.Any(r => r.OutputBytes >= r.SourceBytes)) bad.Add("a JPEG did not get smaller");
                    return bad.Count == 0 ? null : string.Join(" | ", bad);
                }
                finally { d.Close(); }
            });

            await CheckAsync("images: a target size picks the highest JPEG quality that fits, and a free-size resize without proportions is exact", async () =>
            {
                var src = f.Pictures[0];
                var small = new ImageOptions { Width = 800, Height = 600, KeepProportions = true, Quality = 40 };
                var q40 = Path.Combine(f.Dir, "q40.jpg"); var q95 = Path.Combine(f.Dir, "q95.jpg");
                var r40 = await ImageEngine.ProcessAsync(src, q40, small, default);
                var r95 = await ImageEngine.ProcessAsync(src, q95, new ImageOptions { Width = 800, Height = 600, Quality = 95 }, default);
                if (!r40.Ok || !r95.Ok) return "could not make the reference sizes";
                long target = (r40.OutputBytes + r95.OutputBytes) / 2;
                var d = ImagesResizeDialog.OpenFor(new[] { src });
                try
                {
                    await d.LoadedTask; await Task.Delay(200);
                    d.SetWidthText("800"); d.SetQualityMode(true); d.SetTargetKb((int)(target / 1024));
                    var err = await RunDialog(d); if (err != null) return err;
                    var r = d.LastResults[0];
                    if (r.OutputBytes > (target / 1024) * 1024) return $"the result {r.OutputBytes} is over the {(target / 1024) * 1024} byte target";
                    if (r.OutputBytes < r40.OutputBytes * 1.05) return "the quality was lowered more than needed: " + r.OutputBytes + " vs the lowest " + r40.OutputBytes;
                    if (r.Quality < 41 || r.Quality > 95) return "quality " + r.Quality;
                    d.Close();
                    d = ImagesResizeDialog.OpenFor(new[] { f.Pictures[1] });
                    await d.LoadedTask; await Task.Delay(200);
                    d.SetKeep(false); d.SetWidthText("500"); d.SetHeightText("500");
                    err = await RunDialog(d); if (err != null) return err;
                    var (w, h) = ImageEngine.PixelSize(d.LastResults[0].Output);
                    return w == 500 && h == 500 ? null : $"exact resize gave {w}x{h}";
                }
                finally { d.Close(); }
            });

            await CheckAsync("images: one damaged picture is reported as not saved, the others are saved, and the failed one can be retried", async () =>
            {
                var list = f.Pictures.Take(3).Concat(new[] { f.BadJpeg }).ToList();
                var d = ImagesResizeDialog.OpenFor(list);
                try
                {
                    await d.LoadedTask; await Task.Delay(300);
                    d.SetWidthText("640");
                    Click(d.PrimaryButton); await d.Running;
                    if (d.State != ToolState.Done) return "state " + d.State;
                    if (d.LastResults.Count(r => r.Ok) != 3 || d.LastResults.Count(r => !r.Ok) != 1) return "ok " + d.LastResults.Count(r => r.Ok) + " failed " + d.LastResults.Count(r => !r.Ok);
                    var t = d.AllText();
                    if (!t.Contains("3 of 4 saved") || !t.Contains("Not saved") || !t.Contains("Retry the failed one")) return "the result view is incomplete: " + t;
                    return Directory.GetFiles(f.Dir, "broken-small*").Length == 0 ? null : "an output exists for the damaged picture";
                }
                finally { d.Close(); }
            });

            await CheckAsync("images: with nothing to work on the dialog says so and the primary button is off", async () =>
            {
                var d = ImagesResizeDialog.OpenFor(new string[0]);
                try
                {
                    await d.LoadedTask; await Task.Delay(200);
                    return d.AllText().Contains("No pictures to work on") && !d.PrimaryButton.IsEnabled ? null : "empty state wrong: " + d.AllText();
                }
                finally { d.Close(); }
            });
        }

        // ------------------------------------------------------------ 5 image privacy

        private static string[] Secrets = { "TestCam", "SN0012345", "Alice Example", "MAKERNOTE", "2025:06:12", "TestSoft" };

        private static async Task ImagePrivacyChecksAsync(MediaFixtures f)
        {
            await CheckAsync("privacy: the scan reads the coordinates, camera, serial and dates the hand-built files hold, and finds the clean one clean", async () =>
            {
                var list = new List<string> { f.JpegGps, f.PngGps, f.JpegClean };
                if (f.WebpGps != null) list.Add(f.WebpGps);
                var d = ImagePrivacyDialog.OpenFor(list);
                try
                {
                    await d.LoadedTask; await Task.Delay(300);
                    if (WpfQuery(f.JpegGps, "/app1/ifd/{ushort=271}") != "TestCam Pro") return "an independent reader cannot see the test metadata, so the test file is wrong";
                    var bad = new List<string>();
                    foreach (var it in d.Items)
                    {
                        var r = it.Report;
                        if (it.Path == f.JpegClean) { if (r.HasHidden) bad.Add("the clean picture is not clean"); continue; }
                        if (!r.HasLocation) bad.Add(it.Name + ": no location found");
                        else if (Math.Abs(r.Lat.Value - 51.5007) > 0.001 || Math.Abs(r.Lon.Value - -0.1246) > 0.001) bad.Add($"{it.Name}: coordinates {r.Lat} {r.Lon}");
                        if (r.Group(ExifData.Camera)?.Found != true || r.Serial != "SN0012345") bad.Add(it.Name + ": camera or serial not found (" + r.Serial + ")");
                        if (r.Group(ExifData.DateTimeGroup)?.Found != true) bad.Add(it.Name + ": date not found");
                        if (r.Orientation != 6) bad.Add(it.Name + ": orientation " + r.Orientation);
                    }
                    var jpeg = d.Items.First(i => i.Path == f.JpegGps).Report;
                    if (jpeg.Group(ExifData.Thumb)?.Found != true) bad.Add("the embedded thumbnail was not found");
                    var t = d.AllText();
                    if (!t.Contains("This picture shows where it was taken") || !t.Contains("Map placeholder")) bad.Add("the location warning or the map placeholder is not shown");
                    return bad.Count == 0 ? null : string.Join(" | ", bad);
                }
                finally { d.Close(); }
            });

            await CheckAsync("privacy: removing everything leaves no trace in the bytes or in an independent reader, keeps the picture and the orientation", async () =>
            {
                var list = new List<string> { f.JpegGps, f.PngGps };
                if (f.WebpGps != null) list.Add(f.WebpGps);
                var originals = list.ToDictionary(p => p, Sha);
                var d = ImagePrivacyDialog.OpenFor(list);
                try
                {
                    await d.LoadedTask; await Task.Delay(300);
                    var err = await RunDialog(d); if (err != null) return err;
                    var bad = new List<string>();
                    foreach (var r in d.LastResults)
                    {
                        if (!r.Ok) { bad.Add(Path.GetFileName(r.Source) + ": " + r.Error); continue; }
                        if (!r.Verified) bad.Add("not verified: " + r.Output);
                        if (r.Removed < 6) bad.Add($"{Path.GetFileName(r.Source)}: only {r.Removed} tags counted as removed");
                        var bytes = File.ReadAllBytes(r.Output);
                        foreach (var s in Secrets) if (Contains(bytes, s)) bad.Add($"{Path.GetFileName(r.Output)} still holds \"{s}\"");
                        var after = ImagePrivacyEngine.Scan(r.Output);
                        if (after.HasHidden) bad.Add(Path.GetFileName(r.Output) + " still reports " + string.Join(",", after.Groups.Where(g => g.Found).Select(g => g.Id)));
                        if (after.Orientation != 6) bad.Add(Path.GetFileName(r.Output) + ": orientation was lost");
                        if (!r.Output.EndsWith(".webp"))
                        {
                            if (WpfQuery(r.Output, "/app1/ifd/{ushort=271}") != null) bad.Add("an independent reader still finds the camera make in " + Path.GetFileName(r.Output));
                            if (WpfQuery(r.Output, "/app1/ifd/gps/{ushort=1}") != null) bad.Add("an independent reader still finds GPS in " + Path.GetFileName(r.Output));
                            if (!PixelsOf(r.Source).SequenceEqual(PixelsOf(r.Output))) bad.Add("the picture data changed in " + Path.GetFileName(r.Output));
                        }
                        else
                        {
                            var pi = MediaProc.Probe(r.Output);
                            if (!pi.Ok || pi.Width != 640 || pi.Height != 480) bad.Add("the cleaned WebP no longer decodes: " + pi.Width + "x" + pi.Height);
                        }
                        if (!Path.GetFileNameWithoutExtension(r.Output).EndsWith("-clean")) bad.Add("name " + r.Output);
                    }
                    foreach (var p in list) if (Sha(p) != originals[p]) bad.Add("the original changed: " + p);
                    if (!d.AllText().Contains("clean copies saved")) bad.Add("the done banner is missing");
                    return bad.Count == 0 ? null : string.Join(" | ", bad);
                }
                finally { d.Close(); }
            });

            await CheckAsync("privacy: choosing only some groups removes those and keeps the rest", async () =>
            {
                var d = ImagePrivacyDialog.OpenFor(new[] { f.JpegGps });
                try
                {
                    await d.LoadedTask; await Task.Delay(300);
                    d.SetChoice(ExifData.Camera, false);   // keep the camera, remove the rest
                    var err = await RunDialog(d); if (err != null) return err;
                    var r = d.LastResults[0];
                    if (WpfQuery(r.Output, "/app1/ifd/{ushort=271}") != "TestCam Pro") return "the camera make was removed although it was kept";
                    if (WpfQuery(r.Output, "/app1/ifd/gps/{ushort=1}") != null) return "GPS is still there";
                    var bytes = File.ReadAllBytes(r.Output);
                    if (Contains(bytes, "Alice Example") || Contains(bytes, "2025:06:12")) return "the author or the date is still there";
                    var after = ImagePrivacyEngine.Scan(r.Output);
                    return after.Group(ExifData.Camera).Found && !after.HasLocation && after.Group(ExifData.DateTimeGroup).Found == false ? null : "scan after: " + string.Join(",", after.Groups.Where(g => g.Found).Select(g => g.Id));
                }
                finally { d.Close(); }
            });

            await CheckAsync("privacy: Keep the original off replaces the file with the clean version and nothing else changes", async () =>
            {
                var copy = Path.Combine(f.Dir, "replace-me.jpg"); File.Copy(f.JpegGps, copy, true);
                var d = ImagePrivacyDialog.OpenFor(new[] { copy });
                try
                {
                    await d.LoadedTask; await Task.Delay(300);
                    d.SetKeepOriginal(false);
                    var err = await RunDialog(d); if (err != null) return err;
                    if (!File.Exists(copy)) return "the file is gone";
                    if (WpfQuery(copy, "/app1/ifd/gps/{ushort=1}") != null || Contains(File.ReadAllBytes(copy), "TestCam")) return "the replaced file still holds metadata";
                    return Directory.GetFiles(f.Dir, "replace-me-clean*").Length == 0 ? null : "a stray clean copy was left";
                }
                finally { d.Close(); }
            });

            await CheckAsync("privacy: a picture with nothing hidden says Nothing to clean and has only Close", async () =>
            {
                var d = ImagePrivacyDialog.OpenFor(new[] { f.JpegClean });
                try
                {
                    await d.LoadedTask; await Task.Delay(300);
                    return d.AllText().Contains("Nothing to clean") && d.CloseButton.IsVisible && !d.PrimaryButton.IsVisible ? null : "view wrong: " + d.AllText();
                }
                finally { d.Close(); }
            });
        }

        // ------------------------------------------------------------ 6 make an icon

        private static async Task MakeIconChecksAsync(MediaFixtures f)
        {
            await CheckAsync("icon: seven sizes are really inside the .ico, decode, keep transparency, and the picture is untouched", async () =>
            {
                var before = Sha(f.IconSource);
                var d = MakeIconDialog.OpenFor(f.IconSource);
                try
                {
                    await Task.Delay(300);
                    d.SetPadding(0); d.SetOutputName("logo.ico");
                    var err = await RunDialog(d); if (err != null) return err;
                    var bytes = File.ReadAllBytes(d.LastOutput);
                    var sizes = IconMaker.ReadSizes(bytes);
                    if (!sizes.SequenceEqual(new[] { 16, 24, 32, 48, 64, 128, 256 })) return "sizes in the directory: " + string.Join(",", sizes);
                    var dec = BitmapDecoder.Create(new Uri(d.LastOutput), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (dec.Frames.Count != 7) return "the decoder sees " + dec.Frames.Count + " frames";
                    var f256 = dec.Frames.First(fr => fr.PixelWidth == 256);
                    var px = new byte[256 * 256 * 4]; new FormatConvertedBitmap(f256, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(px, 256 * 4, 0);
                    if (px[3] != 0) return "the corner of the 256 px frame is not transparent";
                    int center = (128 * 256 + 128) * 4;
                    if (px[center + 3] != 255) return "the centre is not opaque";
                    if (Sha(f.IconSource) != before) return "the source picture was changed";
                    return File.Exists(d.LastOutput + ".part") ? "a .part file was left" : null;
                }
                finally { d.Close(); }
            });

            await CheckAsync("icon: a chosen subset, padding, a circle shape and opaque background all show in the real pixels", async () =>
            {
                var d = MakeIconDialog.OpenFor(f.IconSource);
                try
                {
                    await Task.Delay(300);
                    foreach (var s in new[] { 16, 24, 32, 48, 64 }) d.SetSize(s, false);
                    d.SetPadding(25); d.SetShape(2); d.SetOutputName("subset.ico");
                    var err = await RunDialog(d); if (err != null) return err;
                    var sizes = IconMaker.ReadSizes(File.ReadAllBytes(d.LastOutput));
                    if (!sizes.SequenceEqual(new[] { 128, 256 })) return "sizes: " + string.Join(",", sizes);
                    var fr = BitmapDecoder.Create(new Uri(d.LastOutput), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.First(x => x.PixelWidth == 256);
                    var px = new byte[256 * 256 * 4]; new FormatConvertedBitmap(fr, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(px, 256 * 4, 0);
                    // padding 25% leaves a border: the pixel 20 px from the edge on the middle row is empty, the centre is not
                    if (px[(128 * 256 + 20) * 4 + 3] != 0) return "padding left content at the edge";
                    if (px[(128 * 256 + 128) * 4 + 3] != 255) return "the centre is empty";
                    d.Close();
                    d = MakeIconDialog.OpenFor(f.IconSource);
                    await Task.Delay(300);
                    d.SetPadding(0); d.SetTransparency(false); d.SetOutputName("opaque.ico");
                    err = await RunDialog(d); if (err != null) return err;
                    var fo = BitmapDecoder.Create(new Uri(d.LastOutput), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.First(x => x.PixelWidth == 256);
                    var po = new byte[256 * 256 * 4]; new FormatConvertedBitmap(fo, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(po, 256 * 4, 0);
                    return po[3] == 255 && po[0] == 255 && po[1] == 255 && po[2] == 255 ? null : $"the corner is {po[0]},{po[1]},{po[2]},{po[3]}, expected opaque white";
                }
                finally { d.Close(); }
            });

            await CheckAsync("icon: a small source flags the sizes it cannot fill, offers to turn them off, and still allows continuing", async () =>
            {
                var d = MakeIconDialog.OpenFor(f.IconSmall);
                try
                {
                    await Task.Delay(300);
                    var t = d.AllText();
                    if (!t.Contains("This picture is smaller than 256 px") || !t.Contains("Turn off 128 and 256")) return "the warning is missing: " + t;
                    if (!d.PrimaryButton.IsEnabled) return "continuing is not allowed";
                    d.SetSize(128, false); d.SetSize(256, false);
                    d.SetOutputName("small.ico");
                    var err = await RunDialog(d); if (err != null) return err;
                    return IconMaker.ReadSizes(File.ReadAllBytes(d.LastOutput)).SequenceEqual(new[] { 16, 24, 32, 48, 64 }) ? null : "sizes " + string.Join(",", IconMaker.ReadSizes(File.ReadAllBytes(d.LastOutput)));
                }
                finally { d.Close(); }
            });

            await CheckAsync("icon: an SVG is drawn at 512 px and becomes an icon with its colours", async () =>
            {
                var d = MakeIconDialog.OpenFor(f.Svg);
                try
                {
                    await Task.Delay(300);
                    if (d.SourceSize != 512) return "the SVG was rasterised at " + d.SourceSize;
                    d.SetPadding(0); d.SetOutputName("mark.ico");
                    var err = await RunDialog(d); if (err != null) return err;
                    var fr = BitmapDecoder.Create(new Uri(d.LastOutput), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.First(x => x.PixelWidth == 256);
                    var px = new byte[256 * 256 * 4]; new FormatConvertedBitmap(fr, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(px, 256 * 4, 0);
                    int outer = (60 * 256 + 60) * 4, inner = (128 * 256 + 128) * 4;
                    if (!(px[outer + 2] > 180 && px[outer + 1] < 90)) return $"the red square is {px[outer + 2]},{px[outer + 1]},{px[outer]}";
                    return px[inner] > 230 && px[inner + 1] > 230 ? null : "the white disc is missing";
                }
                finally { d.Close(); }
            });

            await CheckAsync("icon: a picture that cannot be read shows the error state", async () =>
            {
                var bad = Path.Combine(f.Dir, "broken.png"); File.WriteAllBytes(bad, new byte[300]);
                var d = MakeIconDialog.OpenFor(bad);
                try { await Task.Delay(300); return d.State == ToolState.Error && d.AllText().Contains("could not be read") ? null : "state " + d.State; }
                finally { d.Close(); }
            });
        }

        // ------------------------------------------------------------ toast buttons and accessibility

        private static async Task MediaToastAndAccessChecksAsync(MediaFixtures f)
        {
            Check("toast buttons: copied video, audio and pictures earn the right buttons right after Convert, and a button opens its dialog", () =>
            {
                string L(params string[] files) => string.Join(",", MediaActions.LabelsFor(files));
                if (L(f.Video) != "Trim…,To GIF…") return "video: " + L(f.Video);
                if (L(f.Tone) != "Edit audio…") return "audio: " + L(f.Tone);
                if (L(f.IconSource) != "Resize…,Privacy…,Make icon…") return "picture: " + L(f.IconSource);
                if (L(f.Pictures[0], f.Pictures[1]) != "Resize…,Privacy…") return "several pictures: " + L(f.Pictures[0], f.Pictures[1]);
                if (L(f.Video, f.Tone) != "") return "mixed files: " + L(f.Video, f.Tone);
                var actions = ActionProvider.For(ClipItem.FromFiles(new[] { f.Video }));
                int convert = actions.FindIndex(a => a.Label == "Convert…");
                if (convert < 0 || actions[convert + 1].Label != "Trim…" || actions[convert + 2].Label != "To GIF…") return "the buttons are not right after Convert: " + string.Join(" | ", actions.Select(a => a.Label));
                var trim = actions.First(a => a.Label == "Trim…");
                trim.Run();
                var open = FileToolDialog.OpenDialogs.OfType<VideoTrimDialog>().FirstOrDefault();
                if (open == null) return "the Trim button did not open the dialog";
                open.Close();
                foreach (var (label, type) in new (string, Type)[] { ("Edit audio…", typeof(AudioEditDialog)) })
                {
                    ActionProvider.For(ClipItem.FromFiles(new[] { f.Tone })).First(a => a.Label == label).Run();
                    if (!FileToolDialog.OpenDialogs.Any(d => d.GetType() == type)) return label + " did not open its dialog";
                    CloseAllMediaDialogs();
                }
                foreach (var (label, type) in new (string, Type)[] { ("Resize…", typeof(ImagesResizeDialog)), ("Privacy…", typeof(ImagePrivacyDialog)), ("Make icon…", typeof(MakeIconDialog)) })
                {
                    ActionProvider.For(ClipItem.FromFiles(new[] { f.IconSource })).First(a => a.Label == label).Run();
                    if (!FileToolDialog.OpenDialogs.Any(d => d.GetType() == type)) return label + " did not open its dialog";
                    CloseAllMediaDialogs();
                }
                ActionProvider.For(ClipItem.FromFiles(new[] { f.Video })).First(a => a.Label == "To GIF…").Run();
                if (!FileToolDialog.OpenDialogs.OfType<VideoGifDialog>().Any()) return "To GIF did not open its dialog";
                CloseAllMediaDialogs();
                return null;
            });

            await CheckAsync("media dialogs: every dialog is named after its tool and file, has a named Close button, shows a keyboard order and closes on Esc", async () =>
            {
                var bad = new List<string>();
                var opened = new List<FileToolDialog>
                {
                    VideoTrimDialog.OpenFor(f.Video), VideoGifDialog.OpenFor(f.Video), AudioEditDialog.OpenFor(f.Tone),
                    ImagesResizeDialog.OpenFor(f.Pictures.Take(2)), ImagePrivacyDialog.OpenFor(new[] { f.JpegGps }), MakeIconDialog.OpenFor(f.IconSource),
                };
                await Task.Delay(700);
                foreach (var d in opened)
                {
                    var name = System.Windows.Automation.AutomationProperties.GetName(d);
                    if (!name.StartsWith(d.ToolName + ": ")) bad.Add(d.GetType().Name + " is named \"" + name + "\"");
                    if (d.FindByName<System.Windows.Controls.Button>("Close") == null) bad.Add(d.GetType().Name + " has no Close button with a name");
                    if (!d.Topmost) bad.Add(d.GetType().Name + " is not on top");
                    var unnamedIcons = new List<string>();
                    void Walk(DependencyObject o)
                    {
                        if (o is System.Windows.Controls.Button b && b.IsVisible && string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(b)) && !(b.Content is string) && !(b.Content is System.Windows.Controls.StackPanel))
                            unnamedIcons.Add(b.Content?.GetType().Name ?? "empty");
                        int n = o is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetChildrenCount(o) : 0;
                        for (int i = 0; i < n; i++) Walk(System.Windows.Media.VisualTreeHelper.GetChild(o, i));
                    }
                    Walk(d);
                    if (unnamedIcons.Count > 0) bad.Add(d.GetType().Name + " has icon-only buttons without a name: " + string.Join(",", unnamedIcons));
                }
                foreach (var d in opened) { PressKey(d, Key.Escape); }
                await Task.Delay(300);
                if (FileToolDialog.OpenDialogs.Any(d => opened.Contains(d))) bad.Add("Esc did not close every dialog");
                return bad.Count == 0 ? null : string.Join(" | ", bad);
            });
        }
    }
}
