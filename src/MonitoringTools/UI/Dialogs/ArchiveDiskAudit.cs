using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>The language audit's scenes for the archive and virtual disk windows: every view, in the states that change the wording.</summary>
    internal static class ArchiveDiskAudit
    {
        public static async Task ScenesAsync(string where = "archive and disk")
        {
            var dir = Path.Combine(Settings.DataDir, "audit-archive-disk"); Directory.CreateDirectory(dir);
            var a = Path.Combine(dir, "Report.txt"); File.WriteAllText(a, new string('x', 3000));
            var b = Path.Combine(dir, "IMG_1"); Directory.CreateDirectory(b); File.WriteAllBytes(Path.Combine(b, "IMG_1.jpg"), new byte[5000]);
            var items = new[] { a, b };

            ArchiveDialog.Open(items);
            var d = ArchiveDialog.Current;
            if (d != null)
            {
                await d.WaitForScanAsync(6000);
                LanguageAudit.Scan(where + ": archive zip", d);
                d.AddExclude("*.tmp", false); d.AddExclude(@"\.bak$", true);
                d.ExcludeMode.Select("regex"); await Task.Delay(300);
                LanguageAudit.Scan(where + ": archive exclude chips and regex", d);
                if (ArchiveEngine7z)
                {
                    d.FormatBox.Select("7z"); d.SetPassword("abc"); await Task.Delay(200);
                    Drive.Click(d.SplitSwitch); await Task.Delay(400);
                    LanguageAudit.Scan(where + ": archive 7z with password and volumes", d);
                }
                d.FormatBox.Select("tgz"); await Task.Delay(300);
                LanguageAudit.Scan(where + ": archive tar.gz", d);
                d.FormatBox.Select("zip"); d.NameBox.Text = "a:b"; await Task.Delay(400);
                LanguageAudit.Scan(where + ": archive refused name", d);
                d.NameBox.Text = "audit"; await Task.Delay(300);
                d.PreviewProgress(); await Task.Delay(250);
                LanguageAudit.Scan(where + ": archive progress", d);
                d.PreviewDone(); await Task.Delay(250);
                LanguageAudit.Scan(where + ": archive done", d);
                d.PreviewError(); await Task.Delay(250);
                LanguageAudit.Scan(where + ": archive error", d);
                d.Close();
            }

            DiskDialog.Open(items);
            var k = DiskDialog.Current;
            if (k != null)
            {
                await k.WaitForMeasureAsync(6000);
                k.NameBox.Text = "VOL1"; k.LabelBox.Text = "VOL1";
                LanguageAudit.Scan(where + ": disk form", k);
                k.SetSizeForTest(64, "GB"); await Task.Delay(300);
                LanguageAudit.Scan(where + ": disk FAT32 off at 64 GB", k);
                k.SetSizeForTest(8, "GB"); k.FileSystemBox.Select("FAT32"); k.MountBox.Select("folder"); k.TypeBox.Select("fixed"); k.ContentsBox.Select("move"); await Task.Delay(300);
                LanguageAudit.Scan(where + ": disk FAT32, folder, fixed, move", k);
                k.NameBox.Text = ""; await Task.Delay(300);
                LanguageAudit.Scan(where + ": disk refused", k);
                k.NameBox.Text = "VOL1"; k.MountBox.Select("letter"); await Task.Delay(200);
                k.PreviewProgress(); await Task.Delay(250);
                LanguageAudit.Scan(where + ": disk progress", k);
                k.PreviewDone(); await Task.Delay(250);
                LanguageAudit.Scan(where + ": disk done", k);
                k.PreviewError(); await Task.Delay(250);
                LanguageAudit.Scan(where + ": disk error", k);
                k.Close();
            }
        }

        private static bool ArchiveEngine7z => Core.Archives.ArchiveEngine.SevenZip != null;
    }
}
