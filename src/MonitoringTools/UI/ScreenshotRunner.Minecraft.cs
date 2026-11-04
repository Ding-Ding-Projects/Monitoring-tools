using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // The Minecraft server manager, every section and every create step, with sample servers in the throwaway data folder.
    public static partial class ScreenshotRunner
    {
        private static async Task MinecraftScenesAsync()
        {
            McDemo.Seed();
            var cc = new ControlCentreWindow { Width = 1280, Height = 900 };
            try
            {
                cc.Show();
                await Task.Delay(900);
                cc.Navigate("minecraft", animate: false);
                await Task.Delay(600);
                var mc = cc.MinecraftManager;
                mc.Select("sample-friends");
                foreach (var (id, name, caption) in new[]
                {
                    ("overview", "mc-overview", "Minecraft servers: overview of a running server (sample data)"),
                    ("console", "mc-console", "Minecraft servers: console with the typed command builder (sample data)"),
                    ("players", "mc-players", "Minecraft servers: players from ops.json, whitelist.json and banned-players.json (sample data)"),
                    ("settings", "mc-settings", "Minecraft servers: settings with keys, defaults and when each applies (sample data)"),
                    ("worlds", "mc-worlds", "Minecraft servers: worlds with size and seed (sample data)"),
                    ("content", "mc-plugins", "Minecraft servers: installed plugins and the catalogue (sample data)"),
                    ("backups", "mc-backups", "Minecraft servers: backups (sample data)"),
                    ("schedules", "mc-schedules", "Minecraft servers: schedules (sample data)"),
                })
                {
                    mc.ShowSection(id);
                    if (id == "console")
                    {
                        await Task.Delay(700);
                        foreach (var token in new[] { "time", "set", "day" }) { SelfTest.Press(mc, token); await Task.Delay(120); }
                    }
                    if (id == "settings")
                    {
                        await Task.Delay(700);
                        var pvp = SelfTest.Descend<System.Windows.Controls.CheckBox>(mc).FirstOrDefault(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Player versus player");
                        if (pvp != null) pvp.IsChecked = false;   // shows the unsaved bar; never saved
                    }
                    await Task.Delay(500);
                    // Scroll the section into view below the header, as a person would; the console shows its command builder.
                    if (mc.SectionBody is System.Windows.Controls.Panel body && body.Children.Count > 0)
                    {
                        var target = id == "console" ? (System.Windows.FrameworkElement)body.Children[body.Children.Count - 1] : mc.SectionBody;
                        var top = target.TranslatePoint(new System.Windows.Point(0, 0), cc.Scroller);
                        cc.Scroller.ScrollToVerticalOffset(cc.Scroller.VerticalOffset + top.Y - 24);
                    }
                    await Shot(cc, name, caption, 700);
                    cc.Scroller.ScrollToTop();
                }
                McManagerView.Pending.Clear();

                McManagerView.Draft = new McCreateDraft { Version = "1.21.4", Name = "Weekend server", Port = McManagerView.FreePort(25570) };
                mc.OpenCreate();
                for (int step = 0; step < 6; step++)
                {
                    await Shot(cc, "mc-create-" + (step + 1), "New server, step " + (step + 1) + " of 6: " + McManagerView.StepTitles[step], step == 0 || step == 1 ? 2500 : 1300);
                    if (step == 1) { McManagerView.Draft.Flavour = Flavour.Vanilla; McManagerView.Draft.Build = ""; }
                    if (step < 5) SelfTest.Press(mc.CreateForm, "Next");
                }
                McManagerView.Draft = null;
                mc.CloseCreate();
            }
            finally
            {
                cc.Close();
                McDemo.Remove();
            }
        }
    }
}
