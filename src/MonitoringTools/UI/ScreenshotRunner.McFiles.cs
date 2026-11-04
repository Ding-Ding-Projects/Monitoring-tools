using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // Captures for the multi-select player list and for the SSH, Bedrock and add-on views. A sample SSH host is stood in for
    // by a folder in the throwaway data folder; the Bedrock server is a folder only, nothing is started.
    public static partial class ScreenshotRunner
    {
        private static async Task McFilesScenesAsync(ControlCentreWindow cc, McManagerView mc)
        {
            void ScrollTo(System.Windows.FrameworkElement target)
            {
                cc.UpdateLayout();
                var top = target.TranslatePoint(new System.Windows.Point(0, 0), cc.Scroller);
                cc.Scroller.ScrollToVerticalOffset(cc.Scroller.VerticalOffset + top.Y - 24);
            }

            // ---- the multi-select list with a regex search and three players chosen
            mc.Select("sample-friends");
            mc.ShowSection("players");
            await Task.Delay(800);
            var list = SelfTest.Descend<ListBox>(mc).First(l => System.Windows.Automation.AutomationProperties.GetName(l) == "Players");
            var field = SelfTest.Descend<Search.SearchField>(mc).First(f => f.Label == "Search players");
            field.State.SetRegex(true);
            field.State.SetQuery("^(Ava|Ben|Dev)$");
            await Task.Delay(400);
            list.SelectAll();
            SelfTest.Descend<TextBox>(mc).First(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Reason for kick or ban").Text = "Event over, thanks for coming";
            SelfTest.Descend<TextBox>(mc).First(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Teleport to").Text = "10 64 -20";
            SelfTest.Descend<TextBox>(mc).First(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Item to give").Text = "minecraft:diamond";
            SelfTest.Descend<TextBox>(mc).First(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Count").Text = "2";
            await Task.Delay(300);
            ScrollTo(field);   // from the search field down, so the whole bulk area, including the reason, teleport and give fields, is in the frame
            await Shot(cc, "mc-players-bulk", "Minecraft servers: the player list as a multi-select list, filtered by a regex, with three players chosen and the bulk actions (sample data)", 700);
            cc.Scroller.ScrollToTop();
            field.State.SetQuery(""); field.State.SetRegex(false);

            // ---- a sample SSH host: players, plugins and worlds are read over SSH (here, from a folder)
            var dir = Path.Combine(Settings.DataDir, "capture-ssh-host");
            Directory.CreateDirectory(Path.Combine(dir, "mods")); Directory.CreateDirectory(Path.Combine(dir, "world", "region")); Directory.CreateDirectory(Path.Combine(dir, "world_nether"));
            File.WriteAllText(Path.Combine(dir, "server.properties"), "level-name=world\n");
            File.WriteAllText(Path.Combine(dir, "ops.json"), "[ { \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000d001\", \"name\": \"Mara\", \"level\": 4 } ]");
            File.WriteAllText(Path.Combine(dir, "whitelist.json"), "[ { \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000d001\", \"name\": \"Mara\" }, { \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000d002\", \"name\": \"Noor\" } ]");
            File.WriteAllBytes(Path.Combine(dir, "world", "region", "r.0.0.mca"), new byte[3_400_000]); File.WriteAllBytes(Path.Combine(dir, "world", "level.dat"), new byte[1200]);
            File.WriteAllBytes(Path.Combine(dir, "world_nether", "level.dat"), new byte[900]);
            File.WriteAllText(Path.Combine(dir, "mods", "fabric-api-0.100.jar"), "x"); File.WriteAllText(Path.Combine(dir, "mods", "lithium-0.12.jar"), "x"); File.WriteAllText(Path.Combine(dir, "mods", "sodium-0.5.jar.disabled"), "x");
            var modded = ServerManager.Servers.First(x => x.Id == "sample-modded");
            McFiles.Override = x => x.Id == "sample-modded" ? new McLocalFiles(dir) : null;
            McState.Set(modded, McRunState.Stopped, "sample server");
            try
            {
                mc.Select("sample-modded");
                foreach (var (id, name, caption) in new[]
                {
                    ("players", "mc-ssh-players", "Minecraft servers: a stopped server on an SSH host; its player files are read and changed over SSH (sample host)"),
                    ("content", "mc-ssh-mods", "Minecraft servers: the mods folder of an SSH host, listed with switches and Remove (sample host)"),
                    ("worlds", "mc-ssh-worlds", "Minecraft servers: the world folders of an SSH host with their sizes and Download ZIP (sample host)"),
                })
                {
                    mc.ShowSection(id);
                    await Task.Delay(1300);
                    ScrollTo(mc.SectionBody);
                    await Shot(cc, name, caption, 500);
                    cc.Scroller.ScrollToTop();
                }
            }
            finally { McFiles.Override = null; McState.Set(modded, McRunState.Running, "sample server"); }

            // ---- a Bedrock server: players, add-ons and worlds
            var bdir = Path.Combine(Settings.DataDir, "capture-bedrock");
            var f = new McLocalFiles(bdir);
            f.WriteText("server.properties", "level-name=Bedrock level\nallow-list=true\nserver-port=19132\n");
            f.WriteText("allowlist.json", "[ { \"ignoresPlayerLimit\": false, \"name\": \"Steve\", \"xuid\": \"2535400000000001\" }, { \"ignoresPlayerLimit\": false, \"name\": \"Alex Smith\", \"xuid\": \"2535400000000002\" }, { \"ignoresPlayerLimit\": false, \"name\": \"Notch\" } ]");
            f.WriteText("permissions.json", "[ { \"permission\": \"operator\", \"xuid\": \"2535400000000001\" }, { \"permission\": \"visitor\", \"xuid\": \"2535400000000002\" } ]");
            f.MakeDir("worlds/Bedrock level/db"); f.WriteText("worlds/Bedrock level/level.dat", new string('x', 800)); f.WriteText("worlds/Bedrock level/levelname.txt", "Family world");
            f.MakeDir("worlds/Old creative/db"); f.WriteText("worlds/Old creative/level.dat", "x"); f.WriteText("worlds/Old creative/levelname.txt", "Old creative");
            f.MakeDir("behavior_packs/vanilla"); f.MakeDir("resource_packs/vanilla");
            var rp = Guid.NewGuid().ToString(); var bp = Guid.NewGuid().ToString();
            f.WriteText("behavior_packs/Better Mobs/manifest.json", SelfTest.Manifest("Better Mobs", bp, "data", rp));
            f.WriteText("resource_packs/Better Mobs Textures/manifest.json", SelfTest.Manifest("Better Mobs Textures", rp, "resources"));
            f.WriteText("resource_packs/Cute Pets/manifest.json", SelfTest.Manifest("Cute Pets", Guid.NewGuid().ToString(), "resources"));
            f.WriteText("worlds/Bedrock level/world_behavior_packs.json", "[ { \"pack_id\": \"" + bp + "\", \"version\": [1,2,3] } ]");
            f.WriteText("worlds/Bedrock level/world_resource_packs.json", "[ { \"pack_id\": \"" + rp + "\", \"version\": [1,2,3] } ]");
            var s = new ServerDef { Id = "sample-bedrock-capture", Name = "Family Bedrock", Version = "1.26.61.1", Flavour = Flavour.Bedrock, Target = Target.LocalProcess, Port = 19132, FolderPath = bdir };
            ServerManager.Servers.Add(s);
            McState.Set(s, McRunState.Stopped, "sample server");
            try
            {
                mc.Select(s.Id);
                foreach (var (id, name, caption) in new[]
                {
                    ("players", "mc-bedrock-players", "Minecraft servers: a Bedrock server's players from allowlist.json and permissions.json (sample data)"),
                    ("content", "mc-bedrock-addons", "Minecraft servers: Bedrock add-ons with a switch each, install, undo and remove (sample data)"),
                    ("worlds", "mc-bedrock-worlds", "Minecraft servers: Bedrock worlds with sizes, Download ZIP and Import a world (sample data)"),
                })
                {
                    mc.ShowSection(id);
                    await Task.Delay(1300);
                    ScrollTo(mc.SectionBody);
                    await Shot(cc, name, caption, 500);
                    cc.Scroller.ScrollToTop();
                }
            }
            finally { ServerManager.Servers.Remove(s); mc.Select("sample-friends"); }
        }
    }
}
