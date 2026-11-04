using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // The screen eyedropper read from a window of a known colour, and the appearance editor's Export and Import buttons with the file dialogs answered.
    public static partial class SelfTest
    {
        private static Color Hex(string h) => (Color)ColorConverter.ConvertFromString(h);
        private static bool Near(Color a, Color b, int tol) => Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol;

        private static async Task PickerLane4ChecksAsync(string dir)
        {
            await CheckAsync("colour picker: the eyedropper takes the colour of a known window's pixel through the real pixel read", async () =>
            {
                var known = Color.FromRgb(18, 200, 77);
                var swatch = new Window
                {
                    WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Left = 300, Top = 220, Width = 360, Height = 240,
                    Background = new SolidColorBrush(known), Topmost = false
                };
                var host = new Window { Width = 460, Height = 900, Left = 700, Top = 100, Title = "Eyedropper drive", Background = Ui.Res("B.Window") };
                var picker = new ColorPicker(Colors.Black) { Margin = new Thickness(16) };
                host.Content = new ScrollViewer { Content = picker };
                try
                {
                    swatch.Show(); host.Show();
                    await Task.Delay(700);
                    var origin = swatch.PointToScreen(new Point(180, 120));
                    // the same read the eyedropper does, straight from the screen
                    var direct = ColorPicker.ReadScreenPixel((int)origin.X, (int)origin.Y);
                    if (direct == null) return "the screen pixel could not be read at all";
                    if (!Near(direct.Value, known, 3)) return "the screen read " + ElementStyles.Hex(direct.Value) + " where the window is " + ElementStyles.Hex(known) + " (" + ColorPicker.Diag + ") at " + (int)origin.X + "," + (int)origin.Y;
                    picker.SetColor(Colors.Black, false);
                    Drive.Press(picker, "Pick from screen"); await Task.Delay(300);
                    if (picker.EyedropperForTest == null || !picker.EyedropperForTest.IsVisible) return "the eyedropper overlay did not open";
                    if (!picker.EyedropperForTest.Topmost) return "the overlay is not on top of the other windows";
                    if (!picker.ClickEyedropperForTest((int)origin.X, (int)origin.Y)) return "the overlay refused the click";
                    await Task.Delay(300);
                    if (picker.EyedropperForTest != null) return "the overlay stayed open after the click";
                    if (!Near(picker.Value, known, 3)) return "the picker took " + ElementStyles.Hex(picker.Value) + " instead of " + ElementStyles.Hex(known);
                    var hex = Drive.Need<TextBox>(picker, "HEX value").Text;
                    if (!Near(Hex(hex), known, 3)) return "the HEX field reads " + hex;
                    // Escape cancels and leaves the colour alone
                    Drive.Press(picker, "Pick from screen"); await Task.Delay(200);
                    var overlay = picker.EyedropperForTest;
                    if (overlay == null) return "the eyedropper did not open a second time";
                    overlay.Close(); await Task.Delay(100);
                    return picker.EyedropperForTest == null && Near(picker.Value, known, 3) ? null : "cancelling changed the colour or left the overlay";
                }
                finally { swatch.Close(); host.Close(); }
            });

            await CheckAsync("appearance editor: Export all looks and Import looks work through their real buttons with the file dialogs answered", async () =>
            {
                var host = new Window { Width = 560, Height = 320, Title = "Appearance files drive" };
                var first = new Button { Content = "Sample action", Style = Ui.Style("Btn.Ghost"), Margin = new Thickness(20) };
                host.Content = new StackPanel { Children = { first } };
                string k1 = null;
                var file = Path.Combine(dir, "looks-l4.json");
                try
                {
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

                    // cancelled dialogs change nothing
                    AppearanceEditorWindow.FilePickForTest = _ => null;
                    Drive.Press(ed, "Export all looks…"); Drive.Press(ed, "Import looks…");
                    if (File.Exists(file)) return "a cancelled export wrote a file";

                    // export
                    AppearanceEditorWindow.FilePickForTest = save => save ? file : null;
                    Drive.Press(ed, "Export all looks…"); await Wave6Flows.Settle(ed, 250);
                    if (!File.Exists(file)) return "Export all looks wrote no file";
                    var json = File.ReadAllText(file);
                    if (!json.Contains("FF3366", StringComparison.OrdinalIgnoreCase)) return "the exported file does not hold the look";
                    if (!Drive.TextOf(ed).Contains("Exported ")) return "the editor did not say it exported";

                    // remove the look, then import the file back
                    ElementStyles.Store(k1, null, false);
                    if (ElementStyles.Get(k1) != null) return "the look was not removed before import";
                    AppearanceEditorWindow.FilePickForTest = save => save ? null : file;
                    Drive.Press(ed, "Import looks…"); await Wave6Flows.Settle(ed, 300);
                    if (ElementStyles.Get(k1)?.Hover?.Foreground == null) return "Import looks did not bring the look back";
                    if (!Drive.TextOf(ed).Contains("Imported ")) return "the editor did not say it imported: " + Drive.TextOf(ed);

                    // a damaged file changes nothing and says so
                    File.WriteAllText(file, "{\"not\":\"a looks file\"}");
                    ElementStyles.Store(k1, null, false);
                    Drive.Press(ed, "Import looks…"); await Wave6Flows.Settle(ed, 300);
                    if (ElementStyles.Get(k1) != null) return "a damaged file changed the looks";
                    if (!Drive.TextOf(ed).Contains("Nothing was imported")) return "no refusal was shown for a damaged file: " + Drive.TextOf(ed);
                    // an oversized file is not read
                    File.WriteAllBytes(file, new byte[ElementStyles.MaxImportBytes + 10]);
                    Drive.Press(ed, "Import looks…"); await Wave6Flows.Settle(ed, 300);
                    return Drive.TextOf(ed).Contains("larger than") ? null : "an oversized file was not refused";
                }
                finally
                {
                    AppearanceEditorWindow.FilePickForTest = null;
                    foreach (var w in Application.Current.Windows.OfType<AppearanceEditorWindow>().ToList()) w.Close();
                    if (k1 != null) ElementStyles.Store(k1, null, false);
                    try { File.Delete(file); } catch { }
                    host.Close();
                }
            });
        }
    }
}
