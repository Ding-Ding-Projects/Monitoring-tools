namespace MonitoringTools.Minecraft
{
    public static partial class McSampleReports
    {
        /// <summary>A short sample of what a server's help prints for plugin commands: Bukkit style lines and vanilla style usage lines. Sample text, not read from a server.</summary>
        public const string LiveHelp = "[14:20:01 INFO]: --------- Help: Index (1/2) ---------------------\n" +
            "[14:20:01 INFO]: /home: Teleports to one of your homes\n[14:20:01 INFO]: /mute: Stops a player from chatting\n[14:20:01 INFO]: /warp: Teleports to a named warp\n" +
            "[14:20:01 INFO]: /mute <player> [minutes] [reason]\n[14:20:01 INFO]: /home [name]\n[14:20:01 INFO]: /warp <name>\n[14:20:01 INFO]: /spark (start|stop|report)\n" +
            "[14:20:01 INFO]: /time set (day|night)\n[14:20:01 INFO]: /essentials:warp <name>";

        /// <summary>The sample data-driven registries together with the sample data component list, as one file.</summary>
        public static string MergedData()
        {
            var a = System.Text.Json.Nodes.JsonNode.Parse(DataRegistries).AsObject();
            var extra = System.Text.Json.Nodes.JsonNode.Parse(DataComponents).AsObject();
            foreach (var kv in extra) a[kv.Key] = kv.Value.DeepClone();
            return a.ToJsonString();
        }

        /// <summary>Sample data component ids for 1.21.4, as the data component registry lists them (a trimmed copy), for tests without a download.</summary>
        public const string DataComponents = @"{""minecraft:data_component_type"":[""minecraft:custom_name"",""minecraft:lore"",""minecraft:enchantments"",""minecraft:damage"",""minecraft:unbreakable"",""minecraft:custom_model_data"",""minecraft:max_stack_size"",""minecraft:rarity"",""minecraft:repair_cost"",""minecraft:dyed_color"",""minecraft:item_name""]}";
    }
}
