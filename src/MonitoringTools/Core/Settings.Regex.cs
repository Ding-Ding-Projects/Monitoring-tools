using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>One expected-outcome line for a pattern: a piece of text and whether the pattern should match it.</summary>
    public sealed class RegexTestCase
    {
        public string Text { get; set; } = "";
        public bool ShouldMatch { get; set; } = true;
    }

    /// <summary>Settings owned by the regex builder: remembered patterns and test cases stored beside the library.</summary>
    public sealed partial class Settings
    {
        /// <summary>
        /// Remember the recent patterns of every search box across restarts. Off by default because patterns can be private.
        /// Turning it off also clears what was stored.
        /// </summary>
        public bool RegexKeepHistory { get; set; } = false;

        /// <summary>Recent patterns per search field id, newest first, at most 50 each. Only filled while <see cref="RegexKeepHistory"/> is on.</summary>
        public Dictionary<string, List<string>> RegexHistory { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>Expected-outcome test cases per library entry name. Kept apart from the entries so older library files still load.</summary>
        public Dictionary<string, List<RegexTestCase>> RegexPatternTests { get; set; } = new Dictionary<string, List<RegexTestCase>>();
    }
}
