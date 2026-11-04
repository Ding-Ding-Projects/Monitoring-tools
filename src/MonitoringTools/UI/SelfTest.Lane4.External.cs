using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // Following the external settings file, driven through the Modes page.
    public static partial class SelfTest
    {
        private static async Task<bool> WaitFor(Func<bool> done, int ms)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < until) { if (done()) return true; await Task.Delay(100); }
            return done();
        }

        private static async Task ExternalLane4ChecksAsync(string dir)
        {
            await CheckAsync("external settings file: following it through the Modes page applies it, follows an edit, keeps the last good values on a bad edit and stops", async () =>
            {
                var file = Path.Combine(dir, "external-l4.json");
                var s = Settings.Current;
                bool sounds0 = s.SoundsEnabled, clip0 = s.ClipboardEnabled, game0 = s.GameModeEnabled;
                s.GameModeEnabled = false;
                var rules0 = s.ScheduleRules; s.ScheduleRules = new System.Collections.Generic.List<ScheduleRule>(); Modes.Evaluate();   // a schedule rule left by another check would win over the file
                s.SoundsEnabled = true; s.ClipboardEnabled = true;
                var cc = new ControlCentreWindow { Width = 1180, Height = 900 };
                try
                {
                    File.WriteAllText(file, "{\"version\":1,\"settings\":{\"sounds\":false,\"toasts\":false,\"mystery\":1}}", new UTF8Encoding(false));
                    cc.Show(); await Task.Delay(700);
                    cc.Navigate("modes", animate: false); await Task.Delay(400);
                    var box = Descend<TextBox>(cc).FirstOrDefault(t => t.ToolTip as string == "Full path to a .json file");
                    if (box == null) return "no file path box on the Modes page";
                    box.Text = file;
                    if (!Press(cc, "Follow this file")) return "no Follow this file button";
                    if (Modes.SoundsOn || Modes.ClipboardToastsOn) return $"the file was not applied: sounds={Modes.SoundsOn} toasts={Modes.ClipboardToastsOn}";
                    if (!Modes.ExternalStatus.StartsWith("Applied 2 settings") || !Modes.ExternalStatus.Contains("ignored 1 unknown key")) return "status: " + Modes.ExternalStatus;
                    var shown = string.Join(" | ", Descend<TextBlock>(cc).Where(t => t.IsVisible).Select(t => t.Text));
                    if (!shown.Contains("Applied 2 settings")) return "the page does not show the applied status";

                    // an edit made elsewhere is followed by itself (the watcher plus its half second pause)
                    File.WriteAllText(file, "{\"version\":1,\"settings\":{\"sounds\":true,\"toasts\":false,\"gamemode\":true}}", new UTF8Encoding(false));
                    if (!await WaitFor(() => Modes.SoundsOn, 6000)) return "an edit to the file was not followed within 6 seconds: " + Modes.ExternalStatus;
                    if (!Modes.GameModeOn) return "game mode from the edited file was not in effect";

                    // a bad edit keeps the last good values and says why
                    File.WriteAllText(file, "{ this is not json", new UTF8Encoding(false));
                    if (!await WaitFor(() => !Modes.ExternalOk, 6000)) return "a broken file was not noticed";
                    if (!Modes.ExternalStatus.StartsWith("Not applied") || !Modes.ExternalStatus.Contains("last valid values stay")) return "status on a broken file: " + Modes.ExternalStatus;
                    if (!Modes.SoundsOn || !Modes.GameModeOn) return "a broken file changed the values in effect";
                    // and a value outside the allowed list is refused the same way
                    File.WriteAllText(file, "{\"version\":1,\"settings\":{\"gamemode\":\"maybe\"}}", new UTF8Encoding(false));
                    if (!await WaitFor(() => Modes.ExternalStatus.Contains("must be true or false"), 6000)) return "an unsupported value was not refused: " + Modes.ExternalStatus;

                    // saved settings were never written by the file
                    if (!s.SoundsEnabled || !s.ClipboardEnabled) return "the file overwrote the saved settings";

                    if (!Press(cc, "Stop following")) return "no Stop following button";
                    await Task.Delay(200);
                    if (!Modes.SoundsOn || !Modes.ClipboardToastsOn || Modes.GameModeOn != s.GameModeEnabled) return "stopping did not hand control back to the saved settings";
                    return s.ExternalFollowing ? "still marked as following" : null;
                }
                finally
                {
                    Modes.StopFollowing(); cc.Close();
                    s.SoundsEnabled = sounds0; s.ClipboardEnabled = clip0; s.GameModeEnabled = game0; s.ScheduleRules = rules0; Modes.Evaluate(); s.ExternalSettingsPath = "";
                    try { File.Delete(file); } catch { }
                }
            });
        }
    }
}
