using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>The background updater against a fake release made from this very exe, and the game started and stopped notices.</summary>
        private static async Task UpdateChecksAsync(string dir)
        {
            await CheckAsync("updates: a newer release is downloaded, checked against its digest and version, waits while busy and installs when quiet", async () =>
            {
                var exe = ServiceHost.CurrentExe;
                var own = AutoUpdater.OwnVersion(exe);
                if (own == null) return "could not read this exe's own version";
                string sha;
                await using (var fs = File.OpenRead(exe)) sha = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();
                var folder = Path.Combine(dir, "updates-test");
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                string launched = null;
                var url = new Uri(exe).AbsoluteUri;
                AutoUpdater.Release rel = null;
                AutoUpdater.FolderForTest = folder;
                AutoUpdater.SourceForTest = () => Task.FromResult(rel);
                AutoUpdater.LaunchForTest = f => launched = f;
                try
                {
                    // 1. same version: up to date, nothing downloaded
                    AutoUpdater.ResetForTest(); AutoUpdater.CurrentForTest = own;
                    rel = new AutoUpdater.Release(own, url, sha);
                    await AutoUpdater.CheckAsync(true);
                    if (AutoUpdater.State != AutoUpdater.Phase.UpToDate) return "same version: " + AutoUpdater.Status;
                    if (Directory.Exists(folder) && Directory.GetFiles(folder).Length > 0) return "same version downloaded a file";

                    AutoUpdater.CurrentForTest = new Version(0, 9, 0);   // below the development build's own 1.0.0
                    // 2. no checksum: refused before downloading
                    AutoUpdater.ResetForTest(); rel = new AutoUpdater.Release(own, url, null);
                    await AutoUpdater.CheckAsync(true);
                    if (AutoUpdater.State != AutoUpdater.Phase.Failed || !AutoUpdater.Status.Contains("no published checksum")) return "no checksum: " + AutoUpdater.Status;
                    // 3. wrong checksum: thrown away
                    AutoUpdater.ResetForTest(); rel = new AutoUpdater.Release(own, url, new string('0', 64));
                    await AutoUpdater.CheckAsync(true);
                    if (!AutoUpdater.Status.Contains("did not match its published checksum")) return "bad checksum: " + AutoUpdater.Status;
                    if (Directory.GetFiles(folder).Length != 0) return "bad checksum left a file: " + string.Join(", ", Directory.GetFiles(folder).Select(Path.GetFileName));
                    // 4. right checksum but the file is not the release's version: thrown away
                    AutoUpdater.ResetForTest(); rel = new AutoUpdater.Release(new Version(own.Major + 50, 0, 0), url, sha);
                    await AutoUpdater.CheckAsync(true);
                    if (!AutoUpdater.Status.Contains("so it was thrown away") || !AutoUpdater.Status.Contains("says it is version")) return "wrong version: " + AutoUpdater.Status;
                    if (launched != null) return "a wrong file was launched";
                    // 5. good release while a conversion runs: kept and waiting, not launched
                    AutoUpdater.ResetForTest(); rel = new AutoUpdater.Release(own, url, sha);
                    AutoUpdater.BlockedForTest = () => "a conversion is running";
                    await AutoUpdater.CheckAsync(true);
                    if (AutoUpdater.State != AutoUpdater.Phase.Waiting || !AutoUpdater.Status.Contains("when the conversion finishes")) return "busy: " + AutoUpdater.Status;
                    if (launched != null) return "installed while busy";
                    var kept = Path.Combine(folder, $"MonitoringTools-{own.ToString(3)}.exe");
                    if (!File.Exists(kept)) return "the checked download was not kept at " + kept;
                    if (AutoUpdater.ReadyVersion != own) return "ready version " + AutoUpdater.ReadyVersion;
                    // 6. quiet now: the kept file is installed without downloading again
                    var stamp = File.GetLastWriteTimeUtc(kept);
                    AutoUpdater.BlockedForTest = () => null;
                    rel = null;   // a second download would fail: there is no release to read
                    await AutoUpdater.CheckAsync(true);
                    if (launched != kept) return "quiet: launched " + (launched ?? "nothing") + "; " + AutoUpdater.Status;
                    if (AutoUpdater.State != AutoUpdater.Phase.Installing) return "quiet state " + AutoUpdater.State;
                    if (File.GetLastWriteTimeUtc(kept) != stamp) return "downloaded again";
                    // 7. the notice the new version shows once
                    var flag = Path.Combine(Settings.DataDir, "updated.flag");
                    File.WriteAllText(flag, "1.0.28>1.0.31");
                    var note = AutoUpdater.TakeUpdatedNote();
                    if (note != "Updated from 1.0.28 to 1.0.31 in the background.") return "note: " + note;
                    if (File.Exists(flag) || AutoUpdater.TakeUpdatedNote() != null) return "the note shows more than once";
                    // 8. the real quiet test knows a development build never installs over itself
                    AutoUpdater.BlockedForTest = null;
                    var why = AutoUpdater.BlockedReason();
                    if (ServiceHost.IsPortableBuild && (why == null || !why.Contains("development build"))) return "development build not blocked: " + why;
                    return null;
                }
                finally
                {
                    AutoUpdater.SourceForTest = null; AutoUpdater.LaunchForTest = null; AutoUpdater.BlockedForTest = null;
                    AutoUpdater.FolderForTest = null; AutoUpdater.CurrentForTest = null; AutoUpdater.ResetForTest();
                }
            });

            await CheckAsync("game mode: a game starting shows \"detected\" without taking focus, closing shows \"closed\", and the switch turns both off", async () =>
            {
                var app = (App)System.Windows.Application.Current;
                var s = Settings.Current;
                var was = (s.GameDetectedNotice, s.GameModeEnabled);
                try
                {
                    s.GameModeEnabled = true; s.GameDetectedNotice = true;
                    // the self-test does not wire the watchers, so the notice the GamingChanged handler shows is called directly
                    App.Games.SimulateForTest("Roblox");
                    var on = app.ShowGameNotice(true);
                    await Task.Delay(400);
                    if (on == null) return "no \"Roblox detected\" toast (game mode on: " + Modes.GameModeOn + ")";
                    if (on.ShowActivated || on.IsActive) return "the notice took focus";
                    if (!Texts(on).Any(x => x.Contains("Game mode is on"))) return "detected toast does not say game mode is on";
                    App.Games.SimulateForTest(null);
                    app.ShowGameNotice(false);
                    await Task.Delay(400);
                    if (!App.Toasts.LiveToasts.Any(t => Texts(t).Any(x => x.Contains("Game mode is off")))) return "no \"Roblox closed\" toast";
                    if (!Texts(on).Any(x => x.Contains("Roblox"))) return "detected toast does not name Roblox";
                    foreach (var t in App.Toasts.LiveToasts.ToList()) { try { t.Close(); } catch { } }
                    await Task.Delay(300);
                    s.GameDetectedNotice = false;
                    App.Games.SimulateForTest("Minecraft");
                    if (app.ShowGameNotice(true) != null) return "the switch did not turn the notice off";
                    await Task.Delay(400);
                    App.Games.SimulateForTest(null);
                    return null;
                }
                finally
                {
                    App.Games.SimulateForTest(null);
                    (s.GameDetectedNotice, s.GameModeEnabled) = was;
                    foreach (var t in App.Toasts.LiveToasts.ToList()) { try { t.Close(); } catch { } }
                }
            });
        }

        private static System.Collections.Generic.IEnumerable<string> Texts(System.Windows.DependencyObject root)
        {
            var q = new System.Collections.Generic.Queue<System.Windows.DependencyObject>(); q.Enqueue(root);
            while (q.Count > 0)
            {
                var d = q.Dequeue();
                if (d is System.Windows.Controls.TextBlock tb) yield return tb.Text;
                int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(d);
                for (int i = 0; i < n; i++) q.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(d, i));
            }
        }
    }
}
