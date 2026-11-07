using System.Threading.Tasks;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        /// <summary>
        /// Every look in both themes: the overview, the Appearance page with the look picker, a clipboard toast and the tray
        /// menu, each drawn by the real windows after the look is applied. The person's own look, colour and theme are put back.
        /// </summary>
        private static async Task LookStyleScenes(ControlCentreWindow cc)
        {
            var s = Settings.Current;
            var saved = (s.Look, s.SeedColour, s.ThemeMode);
            try
            {
                foreach (var look in Looks.All)
                {
                    foreach (var theme in new[] { "light", "dark" })
                    {
                        s.Look = look.Id; s.SeedColour = look.DefaultSeed; s.ThemeMode = theme;
                        Appearance.Apply();
                        cc.Navigate("overview", animate: false);
                        await Shot(cc, $"look-{look.Id}-{theme}", $"The {look.Name} look in the {theme} theme: the overview with every page open", 1300);
                    }
                    s.ThemeMode = look == Looks.Pop ? "light" : "dark";
                    Appearance.Apply();
                    cc.Navigate("appearance", animate: false);
                    await Shot(cc, $"look-{look.Id}-appearance", $"The {look.Name} look: the Appearance page and its look picker", 1300);

                    var link = ClipItem.FromText(Demo.SampleUrl);
                    link.Gesture = global::MonitoringTools.Clip.Smart.CopyGesture.Copy;
                    await Toast($"look-{look.Id}-toast", $"The {look.Name} look: a copied link", App.Toasts.ShowClip(link));

                    var menu = new TrayMenuWindow();
                    menu.ShowAtCursor();
                    await Shot(menu, $"look-{look.Id}-tray", $"The {look.Name} look: the tray menu", 1100);
                    menu.Close();
                }
            }
            finally
            {
                (s.Look, s.SeedColour, s.ThemeMode) = saved;
                Appearance.Apply();
                await Task.Delay(300);
            }
        }
    }
}
