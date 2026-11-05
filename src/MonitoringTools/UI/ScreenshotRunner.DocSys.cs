using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core;
using MonitoringTools.Core.DocTools;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    // Captures for the archive, subtitle, font and disc image dialogs, on files made in the throwaway data folder. No font is
    // installed and no disc image is mounted for these captures: the install and mount steps are replaced by stand-ins.
    public static partial class ScreenshotRunner
    {
        private static async Task DocSysScenes()
        {
            var tmp = Path.Combine(Settings.DataDir, "docsys-capture");
            Directory.CreateDirectory(tmp);

            // ---- archive
            var zip = Path.Combine(tmp, "project-v2.zip");
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                void Add(string name, int size) { var e = z.CreateEntry(name); using var s = e.Open(); s.Write(new byte[size]); }
                Add("project/src/main.cs", 14000); Add("project/src/util.cs", 6000); Add("project/assets/logo.svg", 9000); Add("project/assets/banner.psd", 480000);
                Add("project/README.md", 2200); Add("notes.txt", 800);
            }
            var d = ExtractArchiveDialog.Open(zip);
            await Task.Delay(500);
            d.Tree.ToggleForTest("project/assets");
            await Shot(d, "docsys-extract-ready", "Extract an archive: the contents with check marks, a destination and a rule for names that exist", 400);
            d.PreviewWorking(6, 3, 500000, 235000, "project/assets/banner.psd");
            await Shot(d, "docsys-extract-working", "Extract an archive: working, with the file and bytes so far", 300);
            d.Close();
            var pw = Path.Combine(tmp, "taxes-2024.zip");
            ArchiveTools.WritePasswordZip(pw, "hunter2", ("a.txt", Encoding.UTF8.GetBytes("A")), ("b.txt", Encoding.UTF8.GetBytes("B")));
            d = ExtractArchiveDialog.Open(pw);
            await Task.Delay(400);
            d.StartForTest("not-it");
            await Shot(d, "docsys-extract-password", "Extract an archive: a password is needed, and a wrong one says nothing was extracted", 400);
            d.SetDestinationForTest(Path.Combine(tmp, "taxes-out"));
            d.StartForTest("hunter2");
            await Task.Delay(1200);
            await Shot(d, "docsys-extract-done", "Extract an archive: done, with what was renamed, not selected and refused", 300);
            d.Close();
            var slip = Path.Combine(tmp, "slip.zip");
            using (var z = ZipFile.Open(slip, ZipArchiveMode.Create))
            {
                void Add(string name, string t) { var e = z.CreateEntry(name); using var w = new StreamWriter(e.Open()); w.Write(t); }
                Add("docs/guide.txt", "guide"); Add("../evil.txt", "evil"); Add("..\\evil2.txt", "evil2");
            }
            d = ExtractArchiveDialog.Open(slip);
            await Task.Delay(400);
            d.SetDestinationForTest(Path.Combine(tmp, "slip-out"));
            d.StartForTest();
            await Task.Delay(1200);
            await Shot(d, "docsys-extract-refused", "Extract an archive: unsafe paths are refused and counted", 300);
            d.Close();
            // 7z through the bundled 7-Zip: the listing, encrypted names, and a RAR that cannot be read
            var sevenSrc = Path.Combine(tmp, "seven-src"); Directory.CreateDirectory(Path.Combine(sevenSrc, "photos", "2024"));
            File.WriteAllBytes(Path.Combine(sevenSrc, "photos", "2024", "harbour.jpg"), new byte[420000]); File.WriteAllBytes(Path.Combine(sevenSrc, "photos", "2024", "peak.jpg"), new byte[380000]);
            File.WriteAllText(Path.Combine(sevenSrc, "trip-notes.txt"), "Ferry at nine."); File.WriteAllBytes(Path.Combine(sevenSrc, "budget.xlsx"), new byte[24000]);
            var seven = Path.Combine(tmp, "holiday-photos.7z");
            var hidden = Path.Combine(tmp, "tax-papers.7z");
            if (CaptureSeven("a", "-t7z", seven, Path.Combine(sevenSrc, "*")) && CaptureSeven("a", "-t7z", "-psecret", "-mhe=on", hidden, Path.Combine(sevenSrc, "*")))
            {
                d = ExtractArchiveDialog.Open(seven);
                await Task.Delay(700);
                await Shot(d, "docsys-extract-7z", "Extract an archive: a 7z archive listed and extracted through the bundled 7-Zip", 400);
                d.Close();
                d = ExtractArchiveDialog.Open(hidden);
                await Task.Delay(700);
                await Shot(d, "docsys-extract-7z-names-locked", "Extract an archive: a 7z whose file names are encrypted asks for the password before listing", 400);
                d.Close();
            }
            var rar = Path.Combine(tmp, "old-backup.rar"); File.WriteAllBytes(rar, new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 });
            d = ExtractArchiveDialog.Open(rar);
            await Task.Delay(400);
            await Shot(d, "docsys-extract-rar", "Extract an archive: a RAR says the bundled 7-Zip has no RAR decoder and changes nothing", 300);
            d.Close();

            // Ask: the question for a name that exists
            var askZip = Path.Combine(tmp, "report-final.zip");
            using (var z = ZipFile.Open(askZip, ZipArchiveMode.Create))
                foreach (var n in new[] { "report.docx", "figures.xlsx" }) { var e = z.CreateEntry(n); using var s = e.Open(); s.Write(new byte[18000]); }
            var askOut = Path.Combine(tmp, "ask-out"); Directory.CreateDirectory(askOut);
            File.WriteAllBytes(Path.Combine(askOut, "report.docx"), new byte[16500]);
            d = ExtractArchiveDialog.Open(askZip);
            await Task.Delay(400);
            d.SetRuleForTest(OverwriteRule.Ask); d.SetDestinationForTest(askOut);
            d.StartForTest();
            for (int i = 0; i < 50 && d.State != ArchiveState.Conflict; i++) await Task.Delay(100);
            await Shot(d, "docsys-extract-ask", "Extract an archive: Ask stops at a name that exists and offers Replace, Keep both or Skip, with one answer for the rest", 300);
            d.Close();

            // ---- subtitles
            var srt = Path.Combine(tmp, "episode-04.srt");
            File.WriteAllText(srt, "1\r\n00:00:04,200 --> 00:00:06,800\r\nWelcome back to the show.\r\n\r\n2\r\n00:00:07,000 --> 00:00:10,400\r\nToday we are fixing the garden gate.\r\n\r\n3\r\n00:00:10,900 --> 00:00:13,300\r\nFirst, the hinge.\r\n\r\n4\r\n00:40:58,300 --> 00:41:00,500\r\nThat is all for today.\r\n");
            var sd = SubtitleTimingDialog.Open(srt);
            await Task.Delay(500);
            sd.SetShift("+2.400");
            await Shot(sd, "docsys-subtitle-shift", "Subtitle timing: shift every cue, with the result for the first and last cue", 300);
            sd.SetAnchors("00:00:05,100", "00:42:31,000");
            await Shot(sd, "docsys-subtitle-stretch", "Subtitle timing: stretch between two anchor cues, with frame rate presets", 300);
            sd.SetConvertTo(SubtitleFormat.Vtt);
            await Shot(sd, "docsys-subtitle-convert", "Subtitle timing: convert between SubRip and WebVTT", 300);
            sd.Close();
            var bad = Path.Combine(tmp, "episode-09.srt");
            File.WriteAllText(bad, "1\r\n00:11:02,100 --> 00:11:05,900\r\nLine that runs long\r\n\r\n2\r\n00:11:04,300 --> 00:11:06,000\r\nStarts before the last one ends\r\n\r\n3\r\n00:16:xx,xxx --> 00:16:41,200\r\nTime cannot be read\r\n");
            sd = SubtitleTimingDialog.Open(bad);
            await Task.Delay(500);
            await Shot(sd, "docsys-subtitle-problems", "Subtitle timing: overlapping and unreadable cues listed, with Fix overlaps and Skip broken cue", 300);
            sd.Close();

            // ---- fonts (the install step is a stand-in)
            var fontSrc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
            if (!File.Exists(fontSrc)) fontSrc = Directory.EnumerateFiles(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "*.ttf").FirstOrDefault();
            if (fontSrc != null)
            {
                var font = Path.Combine(tmp, Path.GetFileName(fontSrc)); File.Copy(fontSrc, font, true);
                bool installed = false;
                FontTools.InstalledHook = _ => installed; FontTools.InstallHook = p => { installed = true; return null; }; FontTools.UninstallHook = p => { installed = false; return null; };
                try
                {
                    var fd = FontInstallDialog.Open(font);
                    await Task.Delay(600);
                    await Shot(fd, "docsys-font-ready", "Font preview and install: the font from the file at four sizes, what it says about itself, nothing installed yet", 300);
                    Drive.Press(fd, "Install"); await Task.Delay(300);
                    await Shot(fd, "docsys-font-installed", "Font preview and install: installed for this user only (stand-in install step)", 300);
                    fd.Close();
                    fd = FontInstallDialog.Open(font);
                    await Task.Delay(600);
                    await Shot(fd, "docsys-font-already", "Font preview and install: an already installed font offers Uninstall", 300);
                    fd.Close();
                }
                finally { FontTools.InstalledHook = null; FontTools.InstallHook = null; FontTools.UninstallHook = null; }
                // a font collection: every face, with the face picker
                var ttcSrc = new[] { "cambria.ttc", "msgothic.ttc", "simsun.ttc" }.Select(n => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), n)).FirstOrDefault(p => File.Exists(p) && FontTools.FaceCount(p) > 1);
                if (ttcSrc != null)
                {
                    var ttc = Path.Combine(tmp, Path.GetFileName(ttcSrc)); File.Copy(ttcSrc, ttc, true);
                    FontTools.InstalledHook = _ => false;
                    try
                    {
                        var cd = FontInstallDialog.Open(ttc);
                        await Task.Delay(600);
                        cd.ShowFace(cd.Facts.Faces.Count - 1); await Task.Delay(300);
                        await Shot(cd, "docsys-font-collection", "Font preview and install: a font collection lists every face, previews the one picked and installs them all", 300);
                        cd.Close();
                    }
                    finally { FontTools.InstalledHook = null; }
                }
                var broken = Path.Combine(tmp, "broken-font.otf"); File.WriteAllBytes(broken, Enumerable.Range(0, 400).Select(i => (byte)(i * 7)).ToArray());
                var bd = FontInstallDialog.Open(broken);
                await Task.Delay(400);
                await Shot(bd, "docsys-font-unreadable", "Font preview and install: a damaged font says so and changes nothing", 300);
                bd.Close();
            }

            // ---- disc image (mount steps are stand-ins)
            var iso = Path.Combine(tmp, "Win-install.iso");
            SelfTest.BuildIso(iso, "WIN_INSTALL", ("SETUP.EXE", new byte[88000]), ("AUTORUN.INF", new byte[1000]), ("SOURCES/INSTALL.WIM", new byte[4_000_000]), ("BOOT/BOOT.SDI", new byte[3000]));
            var release = new ManualResetEventSlim(false); bool fail = false;
            DiscImageTools.StatusHook = _ => null;
            DiscImageTools.MountHook = p => { release.Wait(20000); return fail ? new MountResult { Error = "The file is on a network share that does not allow mounting." } : new MountResult { Ok = true, Drive = "E:" }; };
            DiscImageTools.DismountHook = p => null;
            try
            {
                var dd = DiscImageDialog.Open(iso);
                await Task.Delay(700);
                dd.Tree.SetAllExpanded(false); dd.Tree.ToggleForTest("SETUP.EXE");
                await Shot(dd, "docsys-disc-ready", "Disc image: not mounted, with the contents tree and a file chosen", 300);
                dd.MountForTest(); await Task.Delay(300);
                await Shot(dd, "docsys-disc-mounting", "Disc image: mounting", 300);
                release.Set(); await Task.Delay(900);
                await Shot(dd, "docsys-disc-mounted", "Disc image: mounted as a drive (stand-in mount step)", 300);
                dd.Close();
                release.Reset(); fail = true;
                dd = DiscImageDialog.Open(iso);
                await Task.Delay(700);
                dd.MountForTest(); release.Set(); await Task.Delay(900);
                await Shot(dd, "docsys-disc-failed", "Disc image: Windows refused to mount, with the reason and Browse without mounting", 300);
                dd.Close();
            }
            finally { DiscImageTools.MountHook = null; DiscImageTools.DismountHook = null; DiscImageTools.StatusHook = null; }

            // the tray menu with an image this app mounted (recorded as a stand-in; nothing is attached)
            DiscImageTools.RememberMount(iso, "E:");
            try
            {
                var menu = new TrayMenuWindow();
                menu.ShowAtCursor();
                await Shot(menu, "docsys-tray-mounted", "Tray menu: a disc image this app mounted, with its drive letter and Eject", 1200);
                menu.Close();
            }
            finally { DiscImageTools.ForgetMount(iso); }
        }

        private static bool CaptureSeven(params string[] args)
        {
            var exe = Core.Archives.ArchiveEngine.SevenZip;
            if (exe == null) return false;
            var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = System.Diagnostics.Process.Start(psi);
            p.StandardInput.Close();
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            return p.WaitForExit(60000) && p.ExitCode == 0;
        }
    }
}
