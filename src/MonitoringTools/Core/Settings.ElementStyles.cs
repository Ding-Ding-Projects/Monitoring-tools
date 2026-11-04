using System.Collections.Generic;

namespace MonitoringTools.Core
{
    // Per-element appearance overrides, named presets, the change history and the recent colours list.
    public sealed partial class Settings
    {
        /// <summary>Appearance overrides keyed by page id and element text. Empty by default, which means the theme is untouched.</summary>
        public Dictionary<string, ElementStyle> ElementStyles { get; set; } = new Dictionary<string, ElementStyle>();

        /// <summary>Named looks the person saved to reuse on other elements. Empty by default; at most 50.</summary>
        public Dictionary<string, ElementStyle> StylePresets { get; set; } = new Dictionary<string, ElementStyle>();

        /// <summary>The most recent appearance changes, oldest first, so one can be restored. At most 100.</summary>
        public List<StyleHistoryEntry> StyleHistory { get; set; } = new List<StyleHistoryEntry>();

        /// <summary>The colours most recently committed in the colour picker, newest first, as #AARRGGBB. At most 12.</summary>
        public List<string> RecentColours { get; set; } = new List<string>();
    }
}
