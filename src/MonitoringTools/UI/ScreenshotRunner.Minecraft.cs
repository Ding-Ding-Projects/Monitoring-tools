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
                    if (id != "overview" && mc.SectionBody is System.Windows.Controls.Panel body && body.Children.Count > 0)
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
                await CatalogueAndDiscoveryScenesAsync(cc, mc);
            }
            finally
            {
                McDiscovery.FakeProcesses = null; McDiscovery.FakeContainers = null;
                McBedrock.SetLatestForTest(null);
                cc.Close();
                McDemo.Remove();
            }
        }

        /// <summary>Hangar results, the compatibility badges, the Bedrock create steps, the Found list and an adopted server.</summary>
        private static async Task CatalogueAndDiscoveryScenesAsync(ControlCentreWindow cc, McManagerView mc)
        {
            void ScrollTo(System.Windows.FrameworkElement target)
            {
                cc.UpdateLayout();
                var top = target.TranslatePoint(new System.Windows.Point(0, 0), cc.Scroller);
                cc.Scroller.ScrollToVerticalOffset(cc.Scroller.VerticalOffset + top.Y - 24);
            }

            // ---- plugins: the badges, then Hangar results (sample hits; nothing is downloaded)
            mc.Select("sample-friends");
            McManagerView.CatalogueSource = "Modrinth";
            mc.ShowSection("content");
            await Task.Delay(700);
            ScrollTo(mc.SectionBody);
            await Shot(cc, "mc-plugins-badges", "Minecraft servers: installed plugins with a compatibility badge each, Update all and Undo (sample data)", 700);
            cc.Scroller.ScrollToTop();
            McManagerView.LastFinds["sample-friends|Hangar"] = new System.Collections.Generic.List<McCatalogHit>
            {
                new McCatalogHit { Source = "Hangar", ProjectId = "31", Title = "ViaVersion", Author = "ViaVersion", Downloads = 525367, Description = "Allow newer clients to join older server versions." },
                new McCatalogHit { Source = "Hangar", ProjectId = "12", Title = "ViaBackwards", Author = "ViaVersion", Downloads = 301442, Description = "Allow older clients to join newer server versions." },
                new McCatalogHit { Source = "Hangar", ProjectId = "1822", Title = "Chunky", Author = "pop4959", Downloads = 98210, Description = "Pre-generates chunks, quickly and efficiently." },
            };
            McManagerView.CatalogueSource = "Hangar";
            mc.ShowSection("content");
            await Task.Delay(700);
            if (mc.SectionBody is System.Windows.Controls.Panel body && body.Children.Count > 1) ScrollTo((System.Windows.FrameworkElement)body.Children[body.Children.Count - 1]);
            await Shot(cc, "mc-plugins-hangar", "Minecraft servers: Hangar results beside Modrinth for a Paper server (sample results)", 700);
            cc.Scroller.ScrollToTop();
            McManagerView.CatalogueSource = "Modrinth";
            McManagerView.LastFinds.Remove("sample-friends|Hangar");

            // ---- Bedrock in the create flow
            McBedrock.SetLatestForTest(new McBedrock.Release { Version = "1.26.61.1", Url = "https://www.minecraft.net/bedrockdedicatedserver/bin-win/bedrock-server-1.26.61.1.zip" });
            McManagerView.Draft = new McCreateDraft { Name = "Family Bedrock", Port = 25570 };
            mc.OpenCreate();
            await Task.Delay(500);
            SelfTest.Press(mc.CreateForm, "Bedrock edition");
            await Shot(cc, "mc-create-bedrock", "New server: Bedrock edition, with Mojang's newest Windows server", 1200);
            for (int i = 0; i < 5; i++) { SelfTest.Press(mc.CreateForm, "Next"); await Task.Delay(500); }
            await Shot(cc, "mc-create-bedrock-review", "New server: the review step for a Bedrock server", 1000);
            McManagerView.Draft = null;
            mc.CloseCreate();

            // ---- servers found on this computer, and one adopted (sample processes; nothing runs)
            var (procs, containers) = McDemo.FoundSample();
            McDiscovery.FakeProcesses = () => procs; McDiscovery.FakeContainers = () => containers;
            await mc.Discover();
            mc.Select("sample-friends");
            mc.ShowSection("overview");
            await Task.Delay(600);
            var found = SelfTest.Descend<System.Windows.Controls.StackPanel>(mc).FirstOrDefault(p => System.Windows.Automation.AutomationProperties.GetName(p) == "Found on this computer");
            if (found != null) ScrollTo(found);
            await Shot(cc, "mc-found", "Minecraft servers: servers found on this computer that the app did not make, each with Adopt (sample processes)", 700);
            cc.Scroller.ScrollToTop();
            var adopted = mc.AdoptNow(McManagerView.Found[0]);
            await McState.ProbeAsync(adopted);
            mc.ShowSection("overview");
            await Shot(cc, "mc-adopted", "Minecraft servers: an adopted Paper server that was started outside the app (sample process)", 1200);
            ServerManager.Servers.Remove(adopted); ServerManager.Save();
            McManagerView.Found.Clear();
            mc.Select("sample-friends");
        }
    }
}
