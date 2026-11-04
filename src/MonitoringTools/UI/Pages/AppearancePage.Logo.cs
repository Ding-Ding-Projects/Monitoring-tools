using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The last result line, kept because changing the logo redraws the whole page.
        private static string _logoNote; private static bool _logoProblem;

        // Logo glyph and custom image, for the drawer header and the tray icon.
        private FrameworkElement AppearanceLogoCard()
        {
            var sp = new StackPanel { Tag = "logo-custom" };
            sp.Children.Add(CardTitle("Logo glyph and image", "Choose a glyph from the app's icon set, or your own PNG or ICO picture, for the drawer logo and the tray icon. The tint above still colours a glyph. Starts as the Pulse glyph."));

            var preview = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var status = ModesNote(_logoNote ?? "", _logoProblem ? "B.Amber" : "B.TextMute");
            void Say(string text, bool problem = false) { _logoNote = text; _logoProblem = problem; status.Text = text; status.Foreground = Ui.Res(problem ? "B.Amber" : "B.TextMute"); }
            AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
            var host = new ContentControl { IsTabStop = false };

            void Paint()
            {
                preview.Children.Clear();
                var drawer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 24, 0) };
                var src = LogoKit.LoadImage();
                FrameworkElement big;
                if (src != null) big = new Image { Source = src, Width = 32, Height = 32, Stretch = Stretch.Uniform };
                else big = Ui.Icon(LogoKit.GlyphData(LogoKit.GlyphId), new SolidColorBrush(Appearance.LogoColor), 32, 2.2);
                drawer.Children.Add(big);
                drawer.Children.Add(new TextBlock { Text = "Drawer logo", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Res("B.TextSoft"), FontSize = 12 });
                AutomationProperties.SetName(drawer, "Drawer logo preview");
                preview.Children.Add(drawer);

                var tray = new StackPanel { Orientation = Orientation.Horizontal };
                using (var bmp = LogoKit.TrayBase(32))
                {
                    var ms = new MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); ms.Position = 0;
                    var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                    tray.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1B)), CornerRadius = new CornerRadius(8), Padding = new Thickness(2), Child = new Image { Source = bi, Width = 32, Height = 32 } });
                }
                tray.Children.Add(new TextBlock { Text = "Tray icon (the status dot is added on top)", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Res("B.TextSoft"), FontSize = 12 });
                AutomationProperties.SetName(tray, "Tray icon preview");
                preview.Children.Add(tray);

                host.Content = Choice("Glyph", LogoKit.Glyphs.Select(g => (g.Label, g.Id)).ToArray(), LogoKit.HasCustomImage ? "" : LogoKit.GlyphId, v => { var e = LogoKit.SetGlyph(v); Say(e ?? "Glyph changed. A custom image, if any, was removed.", e != null); Paint(); },
                    sub: "The symbol drawn in the drawer header and the tray icon. Choosing one replaces a custom image. Default: the pulse line.");
            }
            sp.Children.Add(preview);
            sp.Children.Add(host);

            var row = new WrapPanel();
            row.Children.Add(Ui.Button("Choose a PNG or ICO...", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "PNG or ICO images (*.png;*.ico)|*.png;*.ico", CheckFileExists = true, Title = "Choose a logo image" };
                if (dlg.ShowDialog() != true) return;
                var err = LogoKit.Import(dlg.FileName);
                Say(err ?? "Custom image set for the drawer and the tray.", err != null);
                Paint();
            }, "Btn.Accent", Icons.Image));
            row.Children.Add(Ui.Button("Remove the image", () => { LogoKit.RemoveImage(); Say("Custom image removed."); Paint(); }, "Btn.Ghost"));
            row.Children.Add(Ui.Button("Reset logo", () => { LogoKit.Reset(); Say("The logo is back to the default glyph."); Paint(); }, "Btn.Ghost", Icons.Restart));
            sp.Children.Add(row);
            sp.Children.Add(ModesNote($"PNG or ICO only, up to {LogoKit.MaxBytes / 1024} KB, {LogoKit.MinSide} to {LogoKit.MaxSide} pixels on each side. The picture is checked, then copied into the app's data folder; the original is never read again."));
            sp.Children.Add(status);
            Paint();
            return SearchScope.SetItem(Card(sp), "logo glyph custom image png ico tray drawer reset");
        }
    }
}
