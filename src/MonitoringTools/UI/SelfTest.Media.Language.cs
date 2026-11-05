using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>Opens each media dialog in each of its states with Cantonese on and reports any visible text, tooltip or accessible
        /// name that still holds English words that are not names, units or codes.</summary>
        private static async Task MediaDialogLanguageChecksAsync(MediaFixtures f)
        {
            await CheckAsync("media dialogs: every visible string of every dialog state has a Cantonese entry", async () =>
            {
                var saved = Settings.Current.LanguageMode;
                Settings.Current.LanguageMode = LanguageMode.Cantonese;
                Language.Refresh();
                LanguageAudit.Reset();
                int scanned = 0;
                async Task S(string where, MediaToolDialog d, int wait = 600)
                {
                    await Task.Delay(wait);
                    d.UpdateLayout();
                    Language.Refresh(d);
                    await Task.Delay(120);
                    LanguageAudit.Scan(where, d);
                    scanned++;
                }
                try
                {
                    // 1 trim
                    var d = VideoTrimDialog.OpenFor(f.Video);
                    await Task.Delay(400); await d.KeysReady;
                    d.SetStartText("0:04.000"); d.SetEndText("0:10.000");
                    await S("trim ready, lossless", d);
                    d.SetStartText("0:04.500"); await S("trim ready, exact needed", d, 200);
                    d.SetRemoveMode(true); await S("trim ready, remove", d, 200);
                    d.SetRemoveMode(false); d.SetStartText("0:04.000");
                    d.SetOutputName("lang-trim.mp4");
                    await RunDialog(d); await S("trim done", d, 300); d.Close();
                    var big = VideoTrimDialog.OpenFor(f.BigVideo);
                    await Task.Delay(400); await big.KeysReady;
                    big.SetStartText("0:00.500"); big.SetEndText("0:29.000"); big.SetExact(true); big.SetOutputName("lang-cancel.mp4");
                    Click(big.PrimaryButton); await Task.Delay(500);
                    await S("trim working", big, 100);
                    PressKey(big, Key.Escape); await big.Running; big.Close();
                    d = VideoTrimDialog.OpenFor(f.BadMp4); await S("trim error", d, 300); d.Close();

                    // 2 GIF
                    var g = VideoGifDialog.OpenFor(f.Video);
                    await Task.Delay(500);
                    g.SetRange(2, 6); g.SetWidth(240); g.SetFps(10); g.SetTarget(true, 8);
                    await S("gif ready", g, 1500);
                    g.SetOutputName("lang.gif"); await RunDialog(g); await S("gif done", g, 300); g.Close();
                    g = VideoGifDialog.OpenFor(f.NoiseVideo); await Task.Delay(500);
                    g.SetRange(0, 16); g.SetWidth(480); g.SetFps(15); g.SetTarget(true, 1); g.SetOutputName("lang-small.gif");
                    Click(g.PrimaryButton); await Task.Delay(400); await S("gif working", g, 100); await g.Running; await S("gif too small", g, 300); g.Close();

                    // 3 audio
                    var a = AudioEditDialog.OpenFor(f.Tone); await Task.Delay(400); await a.MeasuredReady;
                    a.SetTarget(1); a.SetTimes(1, 11); await S("audio ready", a);
                    a.SetTarget(4, -20); await S("audio ready, custom", a, 200); a.SetTarget(1);
                    a.SetOutputName("lang-audio.wav"); await RunDialog(a); await S("audio done", a, 300); a.Close();
                    var bad = Path.Combine(f.Dir, "lang-voice.m4a"); File.WriteAllBytes(bad, new byte[4000]);
                    a = AudioEditDialog.OpenFor(bad); await S("audio error", a, 300); a.Close();

                    // 4 images
                    var r = ImagesResizeDialog.OpenFor(f.Pictures.Take(3).Concat(new[] { f.BadJpeg }));
                    await r.LoadedTask; await S("images ready", r, 900);
                    r.SetQualityMode(true); await S("images ready, target size", r, 400); r.SetQualityMode(false);
                    r.SetWidthText("640"); Click(r.PrimaryButton); await Task.Delay(150); await S("images working", r, 50); await r.Running; await S("images done with a failure", r, 300); r.Close();
                    r = ImagesResizeDialog.OpenFor(new string[0]); await r.LoadedTask; await S("images empty", r, 300); r.Close();

                    // 5 privacy
                    var p = ImagePrivacyDialog.OpenFor(new[] { f.JpegGps, f.PngGps, f.JpegClean });
                    await p.LoadedTask; await S("privacy ready", p, 900);
                    await RunDialog(p); await S("privacy done", p, 300); p.Close();
                    p = ImagePrivacyDialog.OpenFor(new[] { f.JpegClean }); await p.LoadedTask; await S("privacy nothing to clean", p, 300); p.Close();

                    // 6 icon
                    var m = MakeIconDialog.OpenFor(f.IconSource); await S("icon ready", m, 700);
                    m.SetOutputName("lang.ico"); await RunDialog(m); await S("icon done", m, 300); m.Close();
                    m = MakeIconDialog.OpenFor(f.IconSmall); await S("icon small source", m, 600); m.Close();
                    var badPng = Path.Combine(f.Dir, "broken.png");
                    m = MakeIconDialog.OpenFor(badPng); await S("icon error", m, 300); m.Close();
                }
                finally
                {
                    CloseAllMediaDialogs();
                    Settings.Current.LanguageMode = saved;
                    Language.Refresh();
                }
                var left = LanguageAudit.Findings;
                var shown = left.Take(60).Select(x => "\"" + x.Text.Replace("\n", " / ") + "\" (" + x.Where.Split("  [")[0] + ", " + x.Kind + ")");
                try { File.WriteAllLines(Path.Combine(f.Dir, "language-media.txt"), left.Select(x => x.Where.Split("  [")[0] + " | " + x.Kind + " | " + x.Text.Replace("\n", " / "))); } catch { }
                var result = left.Count == 0 ? null : left.Count + " untranslated of " + LanguageAudit.Examined + " examined in " + scanned + " scenes: " + string.Join(" | ", shown);
                LanguageAudit.Reset();
                return result;
            });
        }
    }
}
