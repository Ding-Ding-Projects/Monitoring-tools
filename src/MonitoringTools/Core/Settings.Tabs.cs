using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>Settings owned by the control centre tab strip: dock edge, open tabs, order, pins, groups and the active tab.</summary>
    public sealed partial class Settings
    {
        /// <summary>Edge the tab strip is docked to: left (default), top, right or bottom.</summary>
        public string TabDock { get; set; } = "left";
        /// <summary>Open tabs in order. Each entry is a control centre page id. Every page is open by default: the strip
        /// opens each page it has not offered before (see <see cref="KnownTabs"/>), so a new install shows them all.</summary>
        public List<string> OpenTabs { get; set; } = new List<string> { "overview" };
        /// <summary>Every page id the tab strip has already offered. A page missing from this list opens once, so pages
        /// added by an update appear without a trip to New tab, while tabs the person closed stay closed.</summary>
        public List<string> KnownTabs { get; set; } = new List<string>();
        /// <summary>Pinned tabs. Pinned tabs sit in their own first region and are skipped by bulk closes by default.</summary>
        public List<string> PinnedTabs { get; set; } = new List<string> { "overview" };
        public List<TabGroupState> TabGroups { get; set; } = new List<TabGroupState>();
        /// <summary>Tab id to the id of the group that holds it.</summary>
        public Dictionary<string, string> TabGroupOf { get; set; } = new Dictionary<string, string>();
        public string ActiveTab { get; set; } = "overview";
    }

    /// <summary>One tab group: its name, a theme colour role and whether it is collapsed in the strip.</summary>
    public sealed class TabGroupState
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Name { get; set; } = "Group";
        /// <summary>A theme brush key such as B.Mint.</summary>
        public string Colour { get; set; } = "B.Mint";
        public bool Collapsed { get; set; } = false;
    }
}
