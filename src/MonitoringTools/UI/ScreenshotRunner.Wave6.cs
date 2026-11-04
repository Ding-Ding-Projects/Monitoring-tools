using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        // School mode with Windows Hello, the guided look flow, the custom logo and Export everything, captured from the real pages.
        // Scrolls the page's ScrollViewer so the element sits near the top of the view.
        private static void ScrollTo(FrameworkElement el)
        {
            if (el == null) return;
            DependencyObject p = el;
            while (p != null && p is not ScrollViewer) p = VisualTreeHelper.GetParent(p);
            if (p is ScrollViewer sv && sv.Content is UIElement content)
                sv.ScrollToVerticalOffset(Math.Max(0, el.TranslatePoint(new Point(0, 0), content).Y - 24));
        }

        private static async Task Wave6Scenes(ControlCentreWindow cc)
        {
            var s = Settings.Current;
            async Task Reveal(string text, int wait = 500)
            {
                await Task.Delay(300);
                var el = SelfTest.Descend<TextBlock>(cc).FirstOrDefault(t => t.Text == text && t.IsVisible);
                ScrollTo(el);
                await Task.Delay(wait);
            }

            // School mode: a throwaway shared record with a PIN set and the mode off, so the Windows Hello row shows its real state.
            Modes.RecordPathOverride = Path.Combine(Settings.DataDir, "school-capture", "school-mode.json");
            try
            {
                Modes.ReloadSchoolForTest();
                Modes.EnableSchool("4821", "4821", out _);
                Modes.DisableSchool("4821", out _);
                cc.Navigate("modes", animate: false);
                await Task.Delay(1200);
                await Reveal("Windows Hello (optional)", 700);
                await Shot(cc, "school-hello", "School mode: the optional Windows Hello way to turn it off, with its honest state", 600);
            }
            finally { Modes.RecordPathOverride = null; Modes.ReloadSchoolForTest(); }

            // Guided look flow, on its review step.
            cc.Navigate("appearance", animate: false);
            await Task.Delay(900);
            SelfTest.Press(cc, "Make a look step by step");
            await Task.Delay(300);
            var form = SelfTest.Descend<GuidedForm>(cc).FirstOrDefault();
            if (form != null)
            {
                SelfTest.Press(form, "Dark"); SelfTest.Press(form, "Next");
                SelfTest.Press(form, "Violet"); SelfTest.Press(form, "Next");
                SelfTest.Press(form, "Compact"); Drive.Type(SelfTest.Descend<NumberField>(form).First(f => f.Label == "Text size").Box, "120", true); SelfTest.Press(form, "Next");
                await Task.Delay(300);
                form.BringIntoView();
            }
            await Task.Delay(600);
            await Shot(cc, "guided-look", "Make a look: the review step lists exactly what would change before anything is applied", 600);

            // Custom logo: a generated PNG imported through the real validator, shown in the drawer and the preview.
            var png = Path.Combine(Settings.DataDir, "capture-logo.png");
            Demo.SavePng(Demo.SampleBitmap(128, 128, "MT", Color.FromRgb(0x8A, 0xB4, 0xFF)), png);
            LogoKit.Import(png);
            cc.Navigate("appearance", animate: false);
            await Task.Delay(900);
            await Reveal("Logo glyph and image", 700);
            await Shot(cc, "logo-custom", "Logo: a custom PNG in the drawer header and the tray preview, with the glyph choices and reset", 600);
            LogoKit.Reset();

            // Export everything.
            cc.Navigate("records", animate: false);
            await Task.Delay(900);
            await Reveal("Export everything", 700);
            await Shot(cc, "export-everything", "Export everything: one zip with every dataset in five formats, clipboard history left out unless chosen", 600);

            // Local history manager: two saved versions of a sample script, with the protection and retention controls.
            ScriptHistory.Save("script-sample", "echo one"); ScriptHistory.Save("script-sample", "echo two");
            cc.Navigate("records", animate: false);
            await Task.Delay(900);
            await Reveal("Local history", 700);
            await Shot(cc, "history-manager", "Local history: how saved versions are protected and how long they are kept, with each saved history", 600);
            ScriptHistory.Delete("script-sample");
            s.LanguageMode = LanguageMode.English;
        }
    }
}
