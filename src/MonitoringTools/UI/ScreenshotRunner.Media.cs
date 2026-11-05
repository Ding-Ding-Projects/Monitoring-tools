using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        /// <summary>The six media file tool dialogs in the states the design shows, built from generated media in the throwaway folder.</summary>
        private static async Task MediaDialogScenes()
        {
            var f = await SelfTest.MakeMediaFixturesAsync(Path.Combine(Settings.DataDir, "media-dialogs"));
            if (f.Ffmpeg == null) return;
            const string p = "media-";

            // 1 trim
            var t = VideoTrimDialog.OpenFor(f.Video);
            await Task.Delay(700); await t.KeysReady;
            t.SetStartText("0:04.000"); t.SetEndText("0:10.000"); t.Timeline.SetPlayhead(5, false);
            await Shot(t, p + "trim-ready", "Trim and cut video: both points on key frames, so the cut is lossless", 1800);
            t.SetStartText("0:04.500");
            await Shot(t, p + "trim-exact", "Trim and cut video: a start between key frames needs an exact cut", 300);
            t.Close();
            var big = VideoTrimDialog.OpenFor(f.BigVideo);
            await Task.Delay(700); await big.KeysReady;
            big.SetStartText("0:00.500"); big.SetEndText("0:29.000"); big.SetExact(true); big.SetOutputName("capture-working.mp4");
            big.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Task.Delay(1500);
            await Shot(big, p + "trim-working", "Trim and cut video while it works: percentage, what is happening, bytes and the .part file", 100);
            big.Close();
            t = VideoTrimDialog.OpenFor(f.Video);
            await Task.Delay(500); await t.KeysReady;
            t.SetStartText("0:04.000"); t.SetEndText("0:10.000"); t.SetOutputName("capture-trim.mp4");
            t.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await t.Running;
            await Shot(t, p + "trim-done", "Trim and cut video: saved, with length, size against the original and the method", 300);
            t.Close();
            t = VideoTrimDialog.OpenFor(f.BadMp4);
            await Shot(t, p + "trim-error", "Trim and cut video: a damaged file, what was not changed", 500);
            t.Close();

            // 2 GIF
            var g = VideoGifDialog.OpenFor(f.Video);
            await Task.Delay(1200);
            g.SetRange(2, 8.5); g.SetWidth(480); g.SetFps(15); g.SetTarget(true, 8);
            await Shot(g, p + "gif-ready", "Video to GIF or short clip: range, size, target size and the estimate", 2500);
            g.SetOutputName("capture.gif");
            g.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await g.Running;
            await Shot(g, p + "gif-done", "Video to GIF or short clip: saved under the size limit", 300);
            g.Close();
            g = VideoGifDialog.OpenFor(f.NoiseVideo); await Task.Delay(900);
            g.SetRange(0, 16); g.SetWidth(480); g.SetFps(15); g.SetTarget(true, 1); g.SetOutputName("capture-small.gif");
            g.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Task.Delay(1500);
            await Shot(g, p + "gif-working", "Video to GIF or short clip while it works: which try and which pass", 100);
            await g.Running;
            await Shot(g, p + "gif-too-small", "Video to GIF or short clip: the target is too small, nothing saved, the tried sizes listed", 300);
            g.Close();

            // 3 audio
            var a = AudioEditDialog.OpenFor(f.Tone);
            await Task.Delay(800); await a.MeasuredReady;
            a.SetTimes(1, 11); a.SetFades(0.5, 2.0); a.SetTarget(1);
            await Shot(a, p + "audio-ready", "Trim, fade and loudness: waveform, fades, measured loudness and a target", 1500);
            a.SetOutputName("capture-edit.wav");
            a.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await a.Running;
            await Shot(a, p + "audio-done", "Trim, fade and loudness: before and after loudness and true peak", 300);
            a.Close();
            var badA = Path.Combine(f.Dir, "voice-note.m4a"); File.WriteAllBytes(badA, new byte[4000]);
            a = AudioEditDialog.OpenFor(badA);
            await Shot(a, p + "audio-error", "Trim, fade and loudness: audio that cannot be decoded", 500);
            a.Close();

            // 4 images
            var r = ImagesResizeDialog.OpenFor(f.Pictures.Take(6));
            await r.LoadedTask; await Task.Delay(900);
            r.SetCrop(2); r.SetWidthText("1600");
            await Shot(r, p + "images-ready", "Resize, crop and compress: batch list, before and after preview, size, crop and quality", 1800);
            r.Close();
            r = ImagesResizeDialog.OpenFor(f.Pictures.Take(3).Concat(new[] { f.BadJpeg }));
            await r.LoadedTask; await Task.Delay(500);
            r.SetWidthText("800");
            r.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await r.Running;
            await Shot(r, p + "images-done-failure", "Resize, crop and compress: three saved, one failed with its reason", 400);
            r.Close();
            r = ImagesResizeDialog.OpenFor(new string[0]); await r.LoadedTask;
            await Shot(r, p + "images-empty", "Resize, crop and compress: no pictures yet", 500);
            r.Close();

            // 5 privacy
            var pv = ImagePrivacyDialog.OpenFor(new[] { f.JpegGps, f.PngGps, f.JpegClean });
            await pv.LoadedTask; await Task.Delay(1000);
            await Shot(pv, p + "privacy-ready", "Image privacy clean-up: what each picture holds, the location warning and a drawn map", 1500);
            pv.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await pv.Running;
            await Shot(pv, p + "privacy-done", "Image privacy clean-up: clean copies saved, tags removed per picture and what was kept", 300);
            pv.Close();
            pv = ImagePrivacyDialog.OpenFor(new[] { f.JpegClean }); await pv.LoadedTask;
            await Shot(pv, p + "privacy-nothing", "Image privacy clean-up: nothing hidden, nothing to clean", 500);
            pv.Close();

            // 6 icon
            var m = MakeIconDialog.OpenFor(f.IconSource);
            await Shot(m, p + "icon-ready", "Make an icon: source on a checkerboard, sizes, shape and the preview on light and dark", 1500);
            m.SetOutputName("capture.ico");
            m.PrimaryButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await m.Running;
            await Shot(m, p + "icon-done", "Make an icon: seven sizes saved in one file", 300);
            m.Close();
            m = MakeIconDialog.OpenFor(f.IconSmall);
            await Shot(m, p + "icon-small-source", "Make an icon: a small source flags the sizes it cannot fill", 1200);
            m.Close();

            // Cantonese
            var saved = Settings.Current.LanguageMode;
            Settings.Current.LanguageMode = LanguageMode.Cantonese; Language.Refresh();
            var c = VideoTrimDialog.OpenFor(f.Video);
            await Task.Delay(700); await c.KeysReady; c.SetStartText("0:04.000"); c.SetEndText("0:10.000");
            await Task.Delay(300); Language.Refresh(c);
            await Shot(c, p + "trim-ready-cantonese", "Trim and cut video in Cantonese", 900);
            c.Close();
            Settings.Current.LanguageMode = saved; Language.Refresh();
        }
    }
}
