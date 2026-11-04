using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // "MT_ONLY=lane4clicks" holds the app on a hidden desktop and waits for REAL pointer clicks posted to its windows from outside
    // (the lowlevel tools' mouse_click with a window handle). Each step writes click-<name>.json (the window handle and the client
    // point to click, in physical pixels) beside the report, waits up to 150 seconds for the outcome, and judges it.
    public static partial class SelfTest
    {
        [StructLayout(LayoutKind.Sequential)] private struct PointPx { public int X, Y; }
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd, ref PointPx p);

        private static void Announce(string dir, string name, Window w, int screenX, int screenY, string button)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            var p = new PointPx { X = screenX, Y = screenY };
            ScreenToClient(hwnd, ref p);
            File.WriteAllText(Path.Combine(dir, "click-" + name + ".json"), JsonSerializer.Serialize(new { hwnd = hwnd.ToInt64(), x = p.X, y = p.Y, button }));
        }

        private static (int X, int Y) CentreOnScreen(FrameworkElement e)
        {
            var c = e.PointToScreen(new Point(e.ActualWidth / 2, e.ActualHeight / 2));
            return ((int)Math.Round(c.X), (int)Math.Round(c.Y));
        }

        private static async Task ClicksLane4Async(string dir)
        {
            const int wait = 150_000;
            var only = Environment.GetEnvironmentVariable("MT_CLICKS_ONLY");   // eyedropper, palette or veil: run just that step

            if (only is null or "eyedropper") await CheckAsync("real click: a posted left click on the eyedropper overlay picks the colour of the window under it", async () =>
            {
                var known = Color.FromRgb(18, 200, 77);
                var swatch = new Window { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Left = 300, Top = 220, Width = 360, Height = 240, Background = new SolidColorBrush(known) };
                var host = new Window { Width = 460, Height = 900, Left = 700, Top = 100, Title = "Eyedropper click", Background = Ui.Res("B.Window") };
                var picker = new ColorPicker(Colors.Black) { Margin = new Thickness(16) };
                host.Content = new ScrollViewer { Content = picker };
                try
                {
                    swatch.Show(); host.Show(); await Task.Delay(700);
                    Drive.Press(picker, "Pick from screen"); await Task.Delay(400);
                    var overlay = picker.EyedropperForTest;
                    if (overlay == null) return "the overlay did not open";
                    var at = swatch.PointToScreen(new Point(180, 120));
                    Announce(dir, "eyedropper", overlay, (int)at.X, (int)at.Y, "left");
                    if (!await WaitFor(() => picker.EyedropperForTest == null, wait)) return "no click arrived within 150 seconds";
                    await Task.Delay(300);
                    return Near(picker.Value, known, 3) ? null : "the picker took " + ElementStyles.Hex(picker.Value) + " instead of " + ElementStyles.Hex(known) + " (" + ColorPicker.Diag + ")";
                }
                finally { picker.EyedropperForTest?.Close(); swatch.Close(); host.Close(); }
            });

            if (only is null or "palette") await CheckAsync("real click: a posted right click on the command palette opens its element menu", async () =>
            {
                ElementMenu.KeepOpenForTest = true;
                try
                {
                    CommandPalette.Open(); await Task.Delay(700);
                    var pal = CommandPalette.Current;
                    if (pal == null) return "the palette did not open";
                    var btn = Drive.All<Button>(pal).FirstOrDefault(b => b.IsVisible && Drive.LabelOf(b) is "Full" or "Card");
                    if (btn == null) return "no button to click on";
                    ElementMenu.CloseLast(); ElementMenu.ClearLabels();
                    var c = CentreOnScreen(btn);
                    Announce(dir, "palette", pal, c.X, c.Y, "right");
                    if (!await WaitFor(() => ElementMenu.LastCard != null, wait)) return "no menu opened within 150 seconds (handler saw " + ElementMenu.DiagSeen + " right clicks)";
                    var labels = ElementMenu.LastLabels.ToList();
                    return labels.Contains("Clear the search") && labels.Contains("Close the palette") ? null : "the menu offered: " + string.Join(", ", labels);
                }
                finally { ElementMenu.CloseLast(); CommandPalette.Current?.Close(); ElementMenu.KeepOpenForTest = false; }
            });

            if (only is null or "veil") await CheckAsync("real click: a posted left click on a locked button is stopped by the veil and opens the unlock prompt", async () =>
            {
                var btn = new Button { Content = "Veil sample", Margin = new Thickness(40), Style = Ui.Style("Btn.Accent") };
                int clicks = 0; btn.Click += (s, e) => clicks++;
                var host = new Window { Width = 420, Height = 260, Title = "Veil click", Background = Ui.Res("B.Window"), Content = btn };
                string page = null, key = null;
                try
                {
                    host.Show(); await Task.Delay(500);
                    ElementMenu.Attach(host);
                    page = ElementMenu.PageIdOf(host); key = ElementMenu.KeyOf(btn);
                    var err = Locks.Create(page, key, "Veil sample", LockPolicy.Pin, 5, "4826", null, null);
                    if (err != null) return "lock not made: " + err;
                    await Task.Delay(300);
                    var c = CentreOnScreen(btn);
                    Announce(dir, "veil", host, c.X, c.Y, "left");
                    int promptsBefore = ElementMenu.PromptsOpened;
                    bool prompt() => ElementMenu.PromptsOpened > promptsBefore;
                    if (!await WaitFor(prompt, wait)) return "no unlock prompt opened within 150 seconds (button clicks: " + clicks + ", hook: " + ElementMenu.HookDiag + ", locked: " + Locks.IsLocked(page, key) + ")";
                    return clicks == 0 ? null : "the locked button still received the click";
                }
                finally
                {
                    foreach (var w in Application.Current.Windows.OfType<Window>().Where(w => w.GetType().Name == "LockPromptWindow").ToList()) w.Close();
                    var e = page == null ? null : Locks.Find(page, key);
                    if (e != null) { try { new Locks.Session(e).Submit("4826"); } catch { } Locks.Remove(e.Id); }
                    host.Close();
                }
            });
        }
    }
}
