using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI
{
    // "MT_ONLY=dialogs" opens the REAL common file dialogs (no test hook) and the real DatePicker calendar on a hidden desktop and waits for an
    // outside program (the lowlevel tools) to answer them: type a path into the file name box and press Save or Open, or click a day button.
    // Each step writes dialog-<name>.json beside the report (what to answer, the file to use, the window to click) and waits up to 150 seconds.
    // MT_DIALOGS_ONLY selects one step (appearance, bedrock or date).
    public static partial class SelfTest
    {
        private static void AnnounceDialog(string dir, string name, object info) =>
            File.WriteAllText(Path.Combine(dir, "dialog-" + name + ".json"), JsonSerializer.Serialize(info));

        /// <summary>Runs <paramref name="press"/> from the dispatcher queue so the check can keep waiting while the modal dialog is open.</summary>
        private static void PressLater(Action press) => Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, press);

        private static async Task DialogsLane4Async(string dir)
        {
            const int wait = 150_000;
            var only = Environment.GetEnvironmentVariable("MT_DIALOGS_ONLY");

            if (only is null or "appearance") await CheckAsync("real file dialogs: the appearance editor's Export all looks and Import looks open the native Save and Open dialogs, which are answered from outside", async () =>
            {
                var host = new Window { Width = 560, Height = 320, Title = "Appearance real dialogs" };
                var first = new Button { Content = "Sample action", Style = Ui.Style("Btn.Ghost"), Margin = new Thickness(20) };
                host.Content = new StackPanel { Children = { first } };
                string k1 = null;
                var file = Path.Combine(dir, "looks-real-dialog.json");
                AppearanceEditorWindow.FilePickForTest = null;
                try
                {
                    try { File.Delete(file); } catch { }
                    host.Show(); await Wave6Flows.Settle(host, 500);
                    k1 = ElementStyles.KeyFor(ElementStyles.PageIdFor(host), first);
                    AppearanceEditorWindow.Open(host, first); await Wave6Flows.Settle(host, 600);
                    var ed = Application.Current.Windows.OfType<AppearanceEditorWindow>().LastOrDefault();
                    if (ed == null) return "the appearance editor did not open";
                    Drive.Press(ed, "Colour settings"); await Wave6Flows.Settle(ed, 300);
                    Drive.Press(ed, "Hover state"); await Wave6Flows.Settle(ed, 250);
                    Drive.Type(Drive.Need<TextBox>(ed, "HEX value"), "#FF3366", enter: true); await Wave6Flows.Settle(ed, 250);
                    if (ElementStyles.Get(k1)?.Hover?.Foreground == null) return "no look was stored to export";
                    Drive.Press(ed, "Library settings"); await Wave6Flows.Settle(ed, 300);

                    // Export: the real SaveFileDialog
                    AnnounceDialog(dir, "appearance-export", new { dialog = "Save", title = "Export element looks", file });
                    PressLater(() => Drive.Press(ed, "Export all looks…"));
                    if (!await WaitFor(() => File.Exists(file), wait)) return "no file was saved through the native Save dialog within 150 seconds";
                    await Wave6Flows.Settle(ed, 500);
                    var json = File.ReadAllText(file);
                    if (!json.Contains("FF3366", StringComparison.OrdinalIgnoreCase)) return "the file saved through the dialog does not hold the look";
                    if (!Drive.TextOf(ed).Contains("Exported ")) return "the editor did not say it exported: " + Drive.TextOf(ed);

                    // Import: the real OpenFileDialog
                    ElementStyles.Store(k1, null, false);
                    if (ElementStyles.Get(k1) != null) return "the look was not removed before import";
                    AnnounceDialog(dir, "appearance-import", new { dialog = "Open", title = "Import element looks", file });
                    PressLater(() => Drive.Press(ed, "Import looks…"));
                    if (!await WaitFor(() => ElementStyles.Get(k1)?.Hover?.Foreground != null, wait)) return "the look did not come back through the native Open dialog within 150 seconds";
                    await Wave6Flows.Settle(ed, 400);
                    if (!Drive.TextOf(ed).Contains("Imported ")) return "the editor did not say it imported: " + Drive.TextOf(ed);
                    return null;
                }
                finally
                {
                    foreach (var w in Application.Current.Windows.OfType<AppearanceEditorWindow>().ToList()) w.Close();
                    if (k1 != null) ElementStyles.Store(k1, null, false);
                    host.Close();
                }
            });

            if (only is null or "bedrock") await CheckAsync("real file dialogs: Import a world on a Bedrock server opens the native Open dialog, which is answered from outside, and the world lands in the server's worlds folder", async () =>
            {
                McDemo.Seed();
                ControlCentreWindow cc = null; ServerDef s = null;
                var srv = McTemp("dlg-bedrock-server");
                var pack = Path.Combine(McTemp("dlg-bedrock-src"), "sample.mcworld");
                try
                {
                    Zip(pack, ("level.dat", "DLG1"), ("levelname.txt", "Dialog world"), ("db/000001.ldb", "d"));
                    s = new ServerDef { Id = "probe-bedrock-dialog", Name = "Bedrock dialog probe", Version = "1.26.61.1", Flavour = Flavour.Bedrock, Target = Target.LocalProcess, Port = 19191, FolderPath = srv, BackupDir = McTemp("dlg-bedrock-backups") };
                    var f = new McLocalFiles(srv);
                    f.WriteText("server.properties", "level-name=Dialog level\n");
                    f.MakeDir("worlds");
                    ServerManager.Servers.Add(s);
                    McState.Set(s, McRunState.Stopped, "probe");
                    cc = new ControlCentreWindow { Width = 1400, Height = 900 };
                    cc.Show(); await Task.Delay(700);
                    cc.Navigate("minecraft", animate: false); await Wave6Flows.Settle(cc, 600);
                    var mc = cc.MinecraftManager;
                    mc.Select(s.Id); mc.ShowSection("worlds"); await Wave6Flows.Settle(cc, 900);
                    var btn = Drive.Find<ButtonBase>(mc, "Import a world…");
                    if (btn == null) return "no Import a world button";
                    var target = Path.Combine(srv, "worlds", "Dialog level", "level.dat");
                    AnnounceDialog(dir, "bedrock-import", new { dialog = "Open", title = "Choose a .mcworld file, or cancel to choose a folder", file = pack });
                    PressLater(() => Drive.Click(btn));
                    if (!await WaitFor(() => File.Exists(target), wait)) return "no world was imported through the native Open dialog within 150 seconds";
                    await Wave6Flows.Settle(cc, 600);
                    if (File.ReadAllText(target) != "DLG1") return "the imported level.dat holds the wrong data";
                    if (!File.Exists(Path.Combine(srv, "worlds", "Dialog level", "db", "000001.ldb"))) return "the imported world lacks its db folder";
                    var text = string.Join(" ", Drive.All<TextBlock>(mc).Select(t => t.Text));
                    return text.Contains("Imported") ? null : "the page did not say it imported: " + text[..Math.Min(300, text.Length)];
                }
                finally { if (s != null) ServerManager.Servers.Remove(s); cc?.Close(); McDemo.Remove(); }
            });

            if (only is null or "date") await CheckAsync("real calendar: the Start date picker's popup is opened and a day button in it is clicked from outside, and the stored rule carries that date", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                var rules = Settings.Current.ScheduleRules;
                int baseline = rules.Count;
                try
                {
                    cc.Show(); await Wave6Flows.Settle(cc, 800);
                    cc.Navigate("modes", animate: false); await Wave6Flows.Settle(cc, 500);
                    Drive.Type(Drive.Need<TextBox>(cc, "Rule name"), "Calendar click rule");
                    Drive.Type(Drive.Need<TextBox>(cc, "Start time hour"), "9", enter: true); Drive.Type(Drive.Need<TextBox>(cc, "Start time minute"), "0", enter: true);
                    Drive.Type(Drive.Need<TextBox>(cc, "End time hour"), "10", enter: true); Drive.Type(Drive.Need<TextBox>(cc, "End time minute"), "0", enter: true);
                    Drive.Press(cc, "Monday");
                    Drive.Press(cc, "Theme"); await Wave6Flows.Settle(cc, 150);
                    Drive.Press(cc, "Dark"); await Wave6Flows.Settle(cc, 150);
                    var dp = Drive.Need<DatePicker>(cc, "Start date (optional)");
                    dp.DisplayDate = new DateTime(2027, 3, 1);
                    dp.BringIntoView(); await Wave6Flows.Settle(cc, 300);
                    dp.IsDropDownOpen = true; await Wave6Flows.Settle(cc, 700);
                    var popup = Drive.Descendants(dp).OfType<Popup>().FirstOrDefault();
                    var cal = Drive.Descendants(dp).OfType<System.Windows.Controls.Calendar>().FirstOrDefault() ?? popup?.Child as System.Windows.Controls.Calendar;
                    if (cal == null) return "the calendar popup did not open";
                    var day = Drive.All<CalendarDayButton>(cal).FirstOrDefault(b => b.IsVisible && b.Content?.ToString() == "17" && !b.IsInactive);
                    if (day == null) return "no day 17 button in the open calendar";
                    var src = (HwndSource)PresentationSource.FromVisual(day);
                    var screen = day.PointToScreen(new Point(day.ActualWidth / 2, day.ActualHeight / 2));
                    var pt = new PointPx { X = (int)Math.Round(screen.X), Y = (int)Math.Round(screen.Y) };
                    ScreenToClient(src.Handle, ref pt);
                    AnnounceDialog(dir, "datepicker", new { hwnd = src.Handle.ToInt64(), x = pt.X, y = pt.Y, wantsDate = "2027-03-17" });
                    if (!await WaitFor(() => dp.SelectedDate != null, wait)) return "no click arrived within 150 seconds (popup open " + dp.IsDropDownOpen + ")";
                    await Wave6Flows.Settle(cc, 400);
                    if (dp.SelectedDate != new DateTime(2027, 3, 17)) return "the picker holds " + dp.SelectedDate;
                    if (dp.IsDropDownOpen) return "the popup stayed open after the pick";
                    Drive.Press(cc, "Add rule"); await Wave6Flows.Settle(cc, 400);
                    if (rules.Count != baseline + 1) return "the rule was not stored: " + Drive.TextOf(cc);
                    return rules[^1].FromDate == "2027-03-17" ? null : "the rule's start date is " + rules[^1].FromDate;
                }
                finally
                {
                    while (rules.Count > baseline) rules.RemoveAt(rules.Count - 1);
                    Modes.Evaluate();
                    cc.Close();
                }
            });
        }
    }
}
