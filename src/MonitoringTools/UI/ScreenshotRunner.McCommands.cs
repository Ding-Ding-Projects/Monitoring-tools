using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // The command builder: a give task, a teleport with relative axes, the every-command walker and the favourites.
    public static partial class ScreenshotRunner
    {
        /// <summary>Captures the command builder scenes; true when only these were asked for (MT_MC_COMMANDS_ONLY=1).</summary>
        private static async Task<bool> McCommandScenesAsync(ControlCentreWindow cc, McManagerView mc)
        {
            var s = ServerManager.Servers.First(x => x.Id == "sample-friends");
            // The design's artboard is 1440 wide: the gallery beside the form needs the wider window.
            double w0 = cc.Width, h0 = cc.Height;
            cc.Width = 1700; cc.Height = 1500;
            await System.Threading.Tasks.Task.Delay(400);
            void Top()
            {
                cc.UpdateLayout();
                if (mc.SectionBody == null) return;
                var top = mc.SectionBody.TranslatePoint(new System.Windows.Point(0, 0), cc.Scroller);
                cc.Scroller.ScrollToVerticalOffset(cc.Scroller.VerticalOffset + top.Y - 16);
            }
            async Task Task(string id, Dictionary<string, string> values, string name, string caption)
            {
                var v = McManagerView.ValuesFor(s, id);
                v.Clear();
                foreach (var kv in values) v[kv.Key] = kv.Value;
                mc.OpenCommandTask(id);
                await System.Threading.Tasks.Task.Delay(900);
                Top();
                await Shot(cc, name, caption, 700);
                cc.Scroller.ScrollToTop();
            }
            var itemSearch = Search.SearchState.For("minecraft:pick:minecraft:item");
            itemSearch.SetQuery("diamond");   // as the design shows it: the item list searched for "diamond"
            await Task("give", new Dictionary<string, string> { ["targets"] = "Ava", ["item"] = "minecraft:diamond", ["count"] = "5" }, "mc-commands-give",
                "Minecraft servers: the command builder giving an item, with the online players, the version's item list searched for diamond and the coloured command line (sample data)");
            itemSearch.SetQuery("");
            await Task("tp", new Dictionary<string, string> { ["targets"] = "@a[distance=..10]", ["location"] = "~ ~5 ~" }, "mc-commands-tp",
                "Minecraft servers: teleport with a selector filter and relative axes (sample data)");

            // every command: effect give, walked node by node
            McManagerView.CommandMode = "tree";
            mc.ShowSection("commands");
            await System.Threading.Tasks.Task.Delay(700);
            mc.OpenLine(s, "effect give @a minecraft:speed 30 1");
            await System.Threading.Tasks.Task.Delay(700);
            Top();
            await Shot(cc, "mc-commands-tree", "Minecraft servers: Every command, walking effect give through the server's own command tree with a control per argument (sample data)", 700);
            cc.Scroller.ScrollToTop();

            // favourites at the top of the gallery
            var saved = s.FavouriteCommands.ToList();
            s.FavouriteCommands.Clear();
            s.FavouriteCommands.AddRange(new[] { "/time set day", "/give @a minecraft:bread 16", "/weather clear" });
            await Task("weather", new Dictionary<string, string> { ["weather"] = "clear" }, "mc-commands-favourites",
                "Minecraft servers: favourite commands at the top of the task gallery, and the weather task (sample data)");
            s.FavouriteCommands.Clear();
            s.FavouriteCommands.AddRange(saved);
            async Task ShotAt(System.Windows.FrameworkElement target, string name, string caption, double above = 120)
            {
                cc.UpdateLayout();
                if (target != null)
                {
                    var top = target.TranslatePoint(new System.Windows.Point(0, 0), cc.Scroller);
                    cc.Scroller.ScrollToVerticalOffset(Math.Max(0, cc.Scroller.VerticalOffset + top.Y - above));
                }
                await Shot(cc, name, caption, 700);
                cc.Scroller.ScrollToTop();
            }

            // item components built with controls
            McManagerView.CommandMode = "tasks";
            var gv = McManagerView.ValuesFor(s, "give"); gv.Clear();
            gv["targets"] = "Ava";
            gv["item"] = "minecraft:diamond_sword[minecraft:custom_name='\"Big sword\"',minecraft:lore=['\"Won at the arena\"'],minecraft:enchantments={levels:{\"minecraft:sharpness\":4,\"minecraft:unbreaking\":3}},minecraft:damage=3,minecraft:unbreakable={},minecraft:max_stack_size=1]";
            mc.OpenCommandTask("give");
            await System.Threading.Tasks.Task.Delay(1200);
            await ShotAt(SelfTest.Descend<McItemEditor>(mc).FirstOrDefault(), "mc-commands-item-components", "Minecraft servers: a give command with the item component editor: custom name, lore, enchantments with levels, damage, unbreakable and a generic component from the version's component list (sample data)", 260);
            gv.Clear();

            // NBT built with a tree
            McManagerView.CommandMode = "tree";
            mc.ShowSection("commands");
            await System.Threading.Tasks.Task.Delay(700);
            mc.OpenLine(s, "summon minecraft:cow ~ ~ ~ {Invulnerable:1b,CustomName:\"Bessie\",Tags:[\"pet\",\"lane\"],Brain:{angry:0b}}");
            await System.Threading.Tasks.Task.Delay(900);
            await ShotAt(SelfTest.Descend<McNbtEditor>(mc).FirstOrDefault(), "mc-commands-nbt", "Minecraft servers: Every command, summon with its NBT built in a tree: typed keys, a list and a nested compound, with the SNBT it writes (sample data)", 200);

            // plugin and mod commands read from the live server (sample text)
            McManagerView.CommandMode = "tasks";
            var pv = McManagerView.ValuesFor(s, "live:mute"); pv.Clear(); pv["player"] = "Ava"; pv["minutes"] = "10"; pv["reason"] = "spamming chat";
            mc.OpenCommandTask("give");
            await System.Threading.Tasks.Task.Delay(1200);
            mc.OpenCommandTask("live:mute");
            await System.Threading.Tasks.Task.Delay(900);
            Top();
            await Shot(cc, "mc-commands-plugins", "Minecraft servers: the Plugins and mods group in the task gallery, with a form made from a command's usage text and labelled as coming from the live server (sample commands)", 700);
            cc.Scroller.ScrollToTop();
            pv.Clear();
            McManagerView.CommandMode = "tasks";
            McManagerView.CommandTaskId = "give";
            cc.Width = w0; cc.Height = h0;
            await System.Threading.Tasks.Task.Delay(300);
            return Environment.GetEnvironmentVariable("MT_MC_COMMANDS_ONLY") == "1";
        }
    }
}
