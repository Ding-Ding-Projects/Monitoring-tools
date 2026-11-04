using System.Collections.Generic;

namespace MonitoringTools.Core
{
    // Per-element appearance overrides and the recent colours list.
    public sealed partial class Settings
    {
        /// <summary>Appearance overrides keyed by page id and element text. Empty by default, which means the theme is untouched.</summary>
        public Dictionary<string, ElementStyle> ElementStyles { get; set; } = new Dictionary<string, ElementStyle>();

        /// <summary>The colours most recently committed in the colour picker, newest first, as #AARRGGBB. At most 12.</summary>
        public List<string> RecentColours { get; set; } = new List<string>();
    }
}
