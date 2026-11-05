using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Core;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        // The archive and virtual disk windows. The form states are the real windows with real choices made. The progress, done and
        // error views are the real views filled with fixed numbers (named "preview" in the caption), because a real run is over too fast to freeze.
        private static async Task ArchiveDiskScenes()
        {
            var dir = Path.Combine(Settings.DataDir, "archive-disk-capture"); Directory.CreateDirectory(dir);
            var project = Path.Combine(dir, "Project"); Directory.CreateDirectory(Path.Combine(project, "photos"));
            File.WriteAllBytes(Path.Combine(project, "Report.docx"), new byte[2_100_000]);
            File.WriteAllBytes(Path.Combine(project, "photos", "IMG_2041.jpg"), new byte[3_400_000]);
            File.WriteAllText(Path.Combine(project, "notes.txt"), "notes");
            var items = new[] { Path.Combine(project, "Report.docx"), Path.Combine(project, "photos"), Path.Combine(project, "notes.txt") };

            ArchiveDialog.Open(items);
            var a = ArchiveDialog.Current;
            if (a != null)
            {
                await a.WaitForScanAsync(6000);
                await Shot(a, "archive-dialog", "Archive: the form with Zip chosen, the live estimate under it", 500);
                a.AddExclude("*.tmp", false); a.AddExclude(".git", false);
                a.FormatBox.Select("tgz");
                await Shot(a, "archive-dialog-targz", "Archive: tar.gz hides the password, volume and 7z rows", 500);
                if (Core.Archives.ArchiveEngine.SevenZip != null)
                {
                    a.FormatBox.Select("7z"); a.SetPassword("Correct-Horse-9!"); Drive.Click(a.EncryptNames); Drive.Click(a.SplitSwitch);
                    a.LevelSlider.Value = 4; a.ExcludeMode.Select("regex"); a.AddExclude(@"node_modules|\.cache", true);
                    await Shot(a, "archive-dialog-7z", "Archive: 7z with a password, hidden names, volumes, Maximum level and a regex exclude", 600);
                }
                a.FormatBox.Select("zip");
                a.PreviewProgress();
                await Shot(a, "archive-progress", "Archive progress (preview with fixed numbers): bar, speed, steps and Cancel", 400);
                a.PreviewDone();
                await Shot(a, "archive-done", "Archive done (preview with fixed numbers): sizes, test result, location", 400);
                a.PreviewError();
                await Shot(a, "archive-error", "Archive error (preview): nothing is left behind, Try again", 400);
                a.Close();
            }

            DiskDialog.Open(items);
            var d = DiskDialog.Current;
            if (d != null)
            {
                await d.WaitForMeasureAsync(6000);
                d.NameBox.Text = "Work"; d.LabelBox.Text = "Work"; d.ApplyForTest();
                await Shot(d, "disk-dialog", "Virtual disk: the permission note first, then the disk file, volume and what goes in", 600);
                d.SetSizeForTest(64, "GB");
                await Shot(d, "disk-dialog-fat32", "Virtual disk: FAT32 is off at 64 GB and the note says why", 400);
                d.SetSizeForTest(8, "GB"); d.FileSystemBox.Select("exFAT"); d.MountBox.Select("folder"); d.TypeBox.Select("fixed"); d.ContentsBox.Select("move"); d.AfterBox.Select("detach");
                await Shot(d, "disk-dialog-folder", "Virtual disk: exFAT, a fixed 8 GB VHDX mounted into a folder, move, then detach", 500);
                d.FileSystemBox.Select("NTFS"); d.MountBox.Select("letter"); d.TypeBox.Select("dynamic"); d.ContentsBox.Select("copy"); d.AfterBox.Select("keep"); d.SetSizeForTest(64, "GB");
                d.PreviewProgress();
                await Shot(d, "disk-progress", "Virtual disk progress (preview with fixed numbers): named steps, copy bytes, Cancel", 400);
                d.PreviewDone();
                await Shot(d, "disk-done", "Virtual disk done (preview with fixed numbers): Open, Show file, Detach", 400);
                d.PreviewError();
                await Shot(d, "disk-error", "Virtual disk error (preview): consent prompt declined, exit code 1223, nothing left behind", 400);
                d.Close();
            }
        }
    }
}
